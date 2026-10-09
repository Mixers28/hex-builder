using System;
using System.Collections.Generic;
using System.Globalization;

namespace AlphaHex.Core
{
    public sealed partial class Simulation
    {
        public const int PioneerCitizens = 4;
        public const int StartingFood = 24;
        public const int HouseCapacity = 4;
        public const int ImmigrationWave = 2;
        public const int ForestCapacity = 12;
        public const float ImmigrationSeconds = 20f;
        public const float MealSeconds = 30f;
        public const float ForageSeconds = 8f;
        public const float ForagerSeconds = 8f;
        public const float FarmSeconds = 20f;
        public const float ForesterSeconds = 8f;
        public const float TreeGrowthSeconds = 45f;
        public const float DeepMineSeconds = 7f;

        private readonly HashSet<HexCoord> _staffed = new HashSet<HexCoord>();
        private bool _staffingDirty = true;

        public int MealDemand => (State.Town.Population + 1) / 2;
        public int ResearchPoints => State.Town.EarnedMilestones - ResearchCount(State.Town.Research);
        public string TownName => State.Town.Population >= 14 ? "Town" : State.Town.Population >= 10 ? "Village"
            : State.Town.Population >= 6 ? "Hamlet" : "Pioneer camp";
        public int AssignedWorkers { get { EnsureStaffing(); return _staffed.Count; } }
        public int IdleCitizens => State.Town.Population - AssignedWorkers;

        public int HousingCapacity
        {
            get
            {
                int capacity = PioneerCitizens;
                foreach (TileState tile in _orderedTiles)
                    if (tile.Building == BuildingKind.House) capacity += HouseCapacity;
                return capacity;
            }
        }

        public int ConnectedVacancies
        {
            get
            {
                EnsureNetwork();
                int vacancies = 0;
                foreach (TileState tile in _orderedTiles)
                    if (tile.Building == BuildingKind.House && _towardDepot.ContainsKey(tile.Coord))
                        vacancies += HouseCapacity - tile.Residents;
                return vacancies;
            }
        }

        public bool HasResearch(ResearchKind research) => research == ResearchKind.None
            || (State.Town.Research & research) == research;

        public bool TryResearch(ResearchKind research, out string message)
        {
            if (research != ResearchKind.Farming && research != ResearchKind.Reforestation && research != ResearchKind.DeepMining)
                return Fail("Choose Farming, Reforestation or Deep mining.", out message);
            if (HasResearch(research)) return Fail("This research is already complete.", out message);
            if (ResearchPoints < 1) return Fail("Earn a research point by growing to 6, 10 or 14 citizens.", out message);
            State.Town.Research |= research;
            message = ResearchName(research) + " researched. Its building is now available in Town.";
            return true;
        }

        public bool TryToggleWork(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (!NeedsWorker(tile.Building)) return Fail("Select a workplace to pause or resume its work.", out message);
            tile.WorkPaused = !tile.WorkPaused;
            _staffingDirty = true;
            message = tile.WorkPaused ? "Work paused; the citizen is available elsewhere. Cargo already travelling still arrives."
                : "Work resumed. Connected food workplaces receive citizens first.";
            return true;
        }

        public bool TryForage(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest)
                return Fail("Forage on claimed grassland or forest.", out message);
            if (State.Town.ForageCooldown > 0)
                return Fail("Foraging recovers in " + Math.Ceiling(State.Town.ForageCooldown).ToString(CultureInfo.InvariantCulture) + " s.", out message);
            if (!CanReceive(ResourceKind.Food, 2)) return Fail("The depot food counter is full.", out message);
            AddToStock(ResourceKind.Food, 2);
            State.Town.ForageCooldown = ForageSeconds;
            message = "Gathered 2 food. Hand foraging is available again in 8 s.";
            return true;
        }

        public string GetImmigrationStatus()
        {
            if (ConnectedVacancies < ImmigrationWave) return "Build connected houses: each holds 4 new citizens.";
            if (State.Town.LastMealMissed) return "Food ran short. Supply the next meal before welcoming citizens.";
            if (State.Stock.Food < ImmigrationFoodRequired()) return "Store " + ImmigrationFoodRequired() + " food to welcome 2 citizens.";
            return "2 citizens arrive in " + Math.Ceiling(ImmigrationSeconds - State.Town.ImmigrationProgress)
                .ToString(CultureInfo.InvariantCulture) + " s; they use 2 food on arrival.";
        }

        public int GetWorkersAssigned(HexCoord coord)
        {
            EnsureStaffing();
            return _staffed.Contains(coord) ? 1 : 0;
        }

        public List<HexCoord> GetStaffedWorkplaces()
        {
            EnsureStaffing();
            var result = new List<HexCoord>();
            foreach (TileState tile in _orderedTiles)
                if (_staffed.Contains(tile.Coord)) result.Add(tile.Coord);
            return result;
        }

        public static bool NeedsWorker(BuildingKind kind) => kind == BuildingKind.Forestry || kind == BuildingKind.Mine
            || kind == BuildingKind.Smelter || kind == BuildingKind.Forager || kind == BuildingKind.Farm
            || kind == BuildingKind.Forester || kind == BuildingKind.DeepMine;

        public static ResearchKind RequiredResearch(BuildingKind kind) => kind == BuildingKind.Farm ? ResearchKind.Farming
            : kind == BuildingKind.Forester ? ResearchKind.Reforestation : kind == BuildingKind.DeepMine ? ResearchKind.DeepMining : ResearchKind.None;

        public static string ResearchName(ResearchKind kind) => kind == ResearchKind.DeepMining ? "Deep mining" : kind.ToString();

        private static int ResearchCount(ResearchKind research) => ((research & ResearchKind.Farming) != 0 ? 1 : 0)
            + ((research & ResearchKind.Reforestation) != 0 ? 1 : 0) + ((research & ResearchKind.DeepMining) != 0 ? 1 : 0);

        private int ImmigrationFoodRequired() => State.Town.Population + ImmigrationWave;

        private void StepTown()
        {
            TownState town = State.Town;
            town.ForageCooldown = Math.Max(0, town.ForageCooldown - (float)FixedStepSeconds);
            town.MealProgress += (float)FixedStepSeconds;
            if (town.MealProgress >= MealSeconds)
            {
                town.MealProgress = 0;
                town.LastMealMissed = State.Stock.Food < MealDemand;
                State.Stock.Food = Math.Max(0, State.Stock.Food - MealDemand);
            }
            if (ConnectedVacancies >= ImmigrationWave && !town.LastMealMissed && State.Stock.Food >= ImmigrationFoodRequired())
            {
                town.ImmigrationProgress += (float)FixedStepSeconds;
                if (town.ImmigrationProgress >= ImmigrationSeconds)
                {
                    town.ImmigrationProgress = 0;
                    foreach (TileState tile in _orderedTiles)
                    {
                        if (tile.Building != BuildingKind.House || tile.Residents > HouseCapacity - ImmigrationWave
                            || !_towardDepot.ContainsKey(tile.Coord)) continue;
                        tile.Residents += ImmigrationWave;
                        town.Population += ImmigrationWave;
                        State.Stock.Food -= ImmigrationWave;
                        town.EarnedMilestones = Math.Max(town.EarnedMilestones,
                            town.Population >= 14 ? 3 : town.Population >= 10 ? 2 : town.Population >= 6 ? 1 : 0);
                        _staffingDirty = true;
                        break;
                    }
                }
            }
            // Growth pauses with shortages or disconnected housing; no progress is discarded.
            foreach (TileState tile in _orderedTiles)
            {
                if (!tile.SaplingPlanted) continue;
                tile.SaplingAge += (float)FixedStepSeconds;
                if (tile.SaplingAge < TreeGrowthSeconds) continue;
                tile.SaplingPlanted = false;
                tile.SaplingAge = 0;
                tile.TreesRemaining++;
                tile.Terrain = TerrainKind.Forest;
                _staffingDirty = true;
            }
        }

        private void EnsureStaffing()
        {
            EnsureNetwork();
            if (!_staffingDirty) return;
            _staffed.Clear();
            // Food is the recovery path; remaining workplaces are stable in coordinate order.
            for (int priority = 0; priority < 2; priority++)
                foreach (TileState tile in _orderedTiles)
                {
                    if (_staffed.Count >= State.Town.Population) break;
                    bool food = tile.Building == BuildingKind.Forager || tile.Building == BuildingKind.Farm;
                    if ((priority == 0) != food || !NeedsWorker(tile.Building) || tile.WorkPaused
                        || !_towardDepot.ContainsKey(tile.Coord)) continue;
                    if (IsDepletedExtractor(tile) && FindShipment(tile.Coord) == null
                        && !(tile.Building == BuildingKind.Forestry && IsManagedForest(tile))) continue;
                    _staffed.Add(tile.Coord);
                }
            _staffingDirty = false;
        }

        private TileState FindPlantingSite(TileState forester)
        {
            TileState best = null;
            foreach (HexCoord coord in forester.Coord.Neighbors())
            {
                TileState tile = GetTile(coord);
                if (tile == null || !tile.Claimed || tile.InitialOre > 0 || tile.SaplingPlanted
                    || tile.TreesRemaining >= ForestCapacity || (tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest)
                    || (tile.Building != BuildingKind.None && tile.Building != BuildingKind.Forestry)) continue;
                if (best == null || tile.TreesRemaining < best.TreesRemaining
                    || tile.TreesRemaining == best.TreesRemaining && tile.Building == BuildingKind.Forestry && best.Building != BuildingKind.Forestry)
                    best = tile;
            }
            return best;
        }

        private bool IsManagedForest(TileState tile)
        {
            foreach (HexCoord coord in tile.Coord.Neighbors())
                if (GetTile(coord)?.Building == BuildingKind.Forester) return true;
            return false;
        }

        private static ResourceKind OutputResource(BuildingKind kind) => kind == BuildingKind.Forestry ? ResourceKind.Timber
            : kind == BuildingKind.Mine || kind == BuildingKind.DeepMine ? ResourceKind.Ore
            : kind == BuildingKind.Forager || kind == BuildingKind.Farm ? ResourceKind.Food : ResourceKind.Metal;

        private static int OutputAmount(TileState tile) => tile.Building == BuildingKind.Forestry ? TimberPerTree
            : tile.Building == BuildingKind.Mine ? Math.Min(2, tile.OreRemaining)
            : tile.Building == BuildingKind.DeepMine ? Math.Min(2, tile.DeepOreRemaining)
            : tile.Building == BuildingKind.Forager ? 2 : tile.Building == BuildingKind.Farm ? 6 : 1;

        private static int TileDepth(TileState tile) => CalculateDepth(tile.InitialOre, tile.OreRemaining)
            + CalculateDepth(tile.DeepOreInitial, tile.DeepOreRemaining);

        private static void ValidateTown(WorldState state)
        {
            TownState town = state.Town;
            if (town == null || town.Population < PioneerCitizens || town.Population > PioneerCitizens + 126 * HouseCapacity
                || town.Population % ImmigrationWave != 0) throw InvalidSave("town population is invalid");
            int milestones = town.Population >= 14 ? 3 : town.Population >= 10 ? 2 : town.Population >= 6 ? 1 : 0;
            if (town.EarnedMilestones != milestones || (town.Research & ~(ResearchKind.Farming | ResearchKind.Reforestation | ResearchKind.DeepMining)) != 0
                || ResearchCount(town.Research) > town.EarnedMilestones)
                throw InvalidSave("research milestones are inconsistent");
            if (!IsFinite(town.ImmigrationProgress) || town.ImmigrationProgress < 0 || town.ImmigrationProgress >= ImmigrationSeconds
                || !IsFinite(town.MealProgress) || town.MealProgress < 0 || town.MealProgress >= MealSeconds
                || !IsFinite(town.ForageCooldown) || town.ForageCooldown < 0 || town.ForageCooldown > ForageSeconds)
                throw InvalidSave("town progress is invalid");
        }
    }
}
