using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    public sealed partial class Simulation
    {
        public const int PioneerCitizens = 4, StartingFood = 24, HouseCapacity = 4, ImmigrationWave = 1, ForestCapacity = 18;
        public const float ImmigrationSeconds = 90, MealSeconds = 30, ForageSeconds = 8;
        public const float ForagerSeconds = 12, FarmSeconds = 24, ForesterSeconds = 16, TreeGrowthSeconds = 90, DeepMineSeconds = 18;
        public const float QuarrySeconds = 20, ClaySeconds = 24, KilnSeconds = 45, BloomerySeconds = 90;
        public static readonly ResearchKind[] ResearchTopics = { ResearchKind.Brickmaking, ResearchKind.PrimitiveSmelting,
            ResearchKind.Refining, ResearchKind.Farming, ResearchKind.Reforestation, ResearchKind.Surveying,
            ResearchKind.Stoneworking, ResearchKind.QuarryTools, ResearchKind.Masonry, ResearchKind.DeepMining };
        private readonly HashSet<HexCoord> _staffed = new HashSet<HexCoord>();
        private bool _staffingDirty = true;
        public int MealDemand => (State.Town.Population + 1) / 2;
        public int ResearchPoints => State.Town.LegacyResearchPoints;
        public string TownName => State.Town.Population >= 14 ? "Town" : State.Town.Population >= 10 ? "Village" : State.Town.Population >= 6 ? "Hamlet" : "Pioneer camp";
        public int AssignedWorkers { get { int count = 0; foreach (var tile in _orderedTiles) count += tile.Workers; return count; } }
        public int IdleCitizens => State.Town.Population - AssignedWorkers;
        public int HousingCapacity { get { int count = PioneerCitizens; foreach (var tile in _orderedTiles) if (tile.Building == BuildingKind.House) count += HouseCapacity; return count; } }
        public int ConnectedVacancies { get { EnsureNetwork(); int count = 0; foreach (var tile in _orderedTiles)
            if (tile.Building == BuildingKind.House && _towardDepot.ContainsKey(tile.Coord)) count += HouseCapacity - tile.Residents; return count; } }
        public bool HasResearch(ResearchKind kind) => kind == ResearchKind.None || (State.Town.Research & kind) == kind;
        public static bool IsLegacyResearch(ResearchKind kind) => kind == ResearchKind.Farming || kind == ResearchKind.Reforestation || kind == ResearchKind.DeepMining;
        public static int CrewCapacity(BuildingKind kind) => kind == BuildingKind.Forager || kind == BuildingKind.Forester ? 2
            : kind == BuildingKind.Bloomery ? 1 : JobSeconds(kind) > 0 ? 3 : 0;
        public static bool NeedsWorker(BuildingKind kind) => CrewCapacity(kind) > 0;
        public static float CrewSpeed(int crew) => crew == 0 ? 0 : 1 + 0.55f * (crew - 1);
        public static ResearchKind RequiredResearch(BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Farm: return ResearchKind.Farming;
                case BuildingKind.Forester: return ResearchKind.Reforestation;
                case BuildingKind.DeepMine: return ResearchKind.DeepMining;
                case BuildingKind.Watchtower: return ResearchKind.Surveying;
                case BuildingKind.Quarry: return ResearchKind.QuarryTools;
                case BuildingKind.Kiln: return ResearchKind.Brickmaking;
                case BuildingKind.Bloomery: case BuildingKind.Mine: return ResearchKind.PrimitiveSmelting;
                case BuildingKind.Smelter: return ResearchKind.Refining;
                default: return ResearchKind.None;
            }
        }
        public static string ResearchName(ResearchKind kind)
        {
            switch (kind)
            {
                case ResearchKind.Brickmaking: return "Brick firing";
                case ResearchKind.PrimitiveSmelting: return "Primitive smelting";
                case ResearchKind.Refining: return "Efficient refining";
                case ResearchKind.Farming: return "Cultivation";
                case ResearchKind.Reforestation: return "Woodland stewardship";
                case ResearchKind.DeepMining: return "Deep mining";
                case ResearchKind.Surveying: return "Frontier surveying";
                case ResearchKind.Stoneworking: return "Stoneworking";
                case ResearchKind.QuarryTools: return "Quarrying tools";
                case ResearchKind.Masonry: return "Frontier masonry";
                default: return "None";
            }
        }
        public static ResourceStock ResearchCost(ResearchKind kind)
        {
            switch (kind)
            {
                case ResearchKind.Brickmaking: return new ResourceStock(20, 0, 0, clay: 8);
                case ResearchKind.PrimitiveSmelting: return new ResourceStock(24, 0, 0, brick: 12);
                case ResearchKind.Refining: return new ResourceStock(24, 0, 2, brick: 10);
                case ResearchKind.Farming: return new ResourceStock(16, 0, 0, brick: 6);
                case ResearchKind.Reforestation: return new ResourceStock(18, 0, 0, clay: 4);
                case ResearchKind.Surveying: return new ResourceStock(16, 0, 3);
                case ResearchKind.Stoneworking: return new ResourceStock(12, 0, 4);
                case ResearchKind.QuarryTools: return new ResourceStock(24, 0, 8);
                case ResearchKind.Masonry: return new ResourceStock(20, 0, 6, stone: 10);
                case ResearchKind.DeepMining: return new ResourceStock(30, 0, 8);
                default: return new ResourceStock();
            }
        }
        public static int ResearchPopulation(ResearchKind kind) => kind == ResearchKind.Brickmaking ? 6
            : kind == ResearchKind.PrimitiveSmelting || kind == ResearchKind.QuarryTools ? 8
            : kind == ResearchKind.Refining || kind == ResearchKind.Masonry || kind == ResearchKind.DeepMining ? 10 : 6;
        public static float ResearchSeconds(ResearchKind kind) => kind == ResearchKind.PrimitiveSmelting ? 240
            : kind == ResearchKind.Refining ? 300 : kind == ResearchKind.Brickmaking || kind == ResearchKind.Masonry || kind == ResearchKind.DeepMining ? 180
            : kind == ResearchKind.QuarryTools ? 150 : kind == ResearchKind.Reforestation ? 120 : 90;
        public static ResearchKind ResearchPrerequisite(ResearchKind kind) => kind == ResearchKind.PrimitiveSmelting ? ResearchKind.Brickmaking
            : kind == ResearchKind.Refining ? ResearchKind.PrimitiveSmelting : kind == ResearchKind.Stoneworking ? ResearchKind.Surveying
            : kind == ResearchKind.QuarryTools ? ResearchKind.Stoneworking : kind == ResearchKind.Masonry ? ResearchKind.QuarryTools : ResearchKind.None;
        public static string ResearchEffect(ResearchKind kind) => kind == ResearchKind.Brickmaking ? "Fire clay and timber into construction bricks."
            : kind == ResearchKind.PrimitiveSmelting ? "Build a bloomery and start worker-operated batches; unlock mines."
            : kind == ResearchKind.Refining ? "Build an automatic foundry: less ore, less fuel and faster output."
            : kind == ResearchKind.Farming ? "Build farms for more food." : kind == ResearchKind.Reforestation ? "Plant renewable woodland."
            : kind == ResearchKind.Surveying ? "Connected watchtowers reveal more land."
            : kind == ResearchKind.Stoneworking ? "Understand a discovered stone deposit."
            : kind == ResearchKind.QuarryTools ? "Build staffed stone quarries."
            : kind == ResearchKind.Masonry ? "Reinforce towers to reveal a wider frontier." : "Reach finite deep iron below exhausted surface deposits.";
        public string ResearchError(ResearchKind kind)
        {
            if (Array.IndexOf(ResearchTopics, kind) < 0) return "Choose a research project.";
            if (HasResearch(kind)) return "Research is already complete.";
            if (State.Town.ResearchJob != ResearchKind.None) return "Finish the current research project first.";
            if (State.Town.LegacyResearchPoints > 0 && IsLegacyResearch(kind)) return "";
            if (!HasResearch(ResearchPrerequisite(kind))) return "First research " + ResearchName(ResearchPrerequisite(kind)) + ".";
            if (State.Town.Population < ResearchPopulation(kind)) return "Welcome " + ResearchPopulation(kind) + " citizens first.";
            if (kind == ResearchKind.Stoneworking && !_orderedTiles.Exists(t => t.InitialStone > 0)) return "Discover a stone outcrop first.";
            return CanAfford(ResearchCost(kind)) ? "" : "Need " + CostText(ResearchCost(kind)) + ".";
        }
        public bool TryResearch(ResearchKind kind, out string message)
        {
            string error = ResearchError(kind);
            if (error != "") return Fail(error, out message);
            if (State.Town.LegacyResearchPoints > 0 && IsLegacyResearch(kind))
            { State.Town.LegacyResearchPoints--; State.Town.Research |= kind; message = ResearchName(kind) + " unlocked using a legacy credit."; return true; }
            Spend(ResearchCost(kind)); State.Town.ResearchJob = kind; State.Town.ResearchProgress = 0;
            message = "Research begun: " + ResearchName(kind) + "."; return true;
        }
        public bool TryAssignWorkers(HexCoord coord, int count, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            int capacity = CrewCapacity(tile.Building);
            if (capacity == 0 || count < 0 || count > capacity) return Fail("This workplace has " + capacity + " crew places.", out message);
            if (count - tile.Workers > IdleCitizens) return Fail("No available citizens. Reassign a crew or welcome migrants.", out message);
            tile.Workers = count; tile.WorkPaused = false; _staffingDirty = true;
            message = BuildingName(tile.Building) + ": " + count + " / " + capacity + " workers."; return true;
        }
        public bool TryToggleWork(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (!NeedsWorker(tile.Building)) return Fail("Select a workplace.", out message);
            tile.WorkPaused = !tile.WorkPaused; if (tile.WorkPaused) tile.Workers = 0; _staffingDirty = true;
            message = tile.WorkPaused ? "Paused; crew released. Reserved inputs and cargo are retained." : "Resumed. Assign a crew to continue."; return true;
        }
        public bool TryForage(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest) return Fail("Forage on grass or woodland.", out message);
            if (State.Town.ForageCooldown > 0) return Fail("Foraging recovers in " + Math.Ceiling(State.Town.ForageCooldown) + " s.", out message);
            if (!CanReceive(ResourceKind.Food, 2)) return Fail("Food stores are full.", out message);
            AddToStock(ResourceKind.Food, 2); State.Town.ForageCooldown = ForageSeconds;
            message = "Gathered 2 food. Hand foraging recovers in 8 s."; return true;
        }
        public string GetImmigrationStatus() => ConnectedVacancies < 1 ? "Build connected cottages for newcomers."
            : State.Town.LastMealMissed ? "Supply the next meal before welcoming citizens."
            : State.Stock.Food < State.Town.Population + 1 ? "Store " + (State.Town.Population + 1) + " food for immigration."
            : "A newcomer arrives in " + Math.Ceiling(ImmigrationSeconds - State.Town.ImmigrationProgress) + " s and uses 2 food.";
        public int GetWorkersAssigned(HexCoord coord) => GetTile(coord)?.Workers ?? 0;
        public List<HexCoord> GetStaffedWorkplaces()
        {
            var result = new List<HexCoord>(); EnsureStaffing();
            foreach (var tile in _orderedTiles) if (_staffed.Contains(tile.Coord)) for (int i = 0; i < tile.Workers; i++) result.Add(tile.Coord);
            return result;
        }
        private void StepTown()
        {
            TownState town = State.Town;
            town.ForageCooldown = Math.Max(0, town.ForageCooldown - (float)FixedStepSeconds);
            town.MealProgress += (float)FixedStepSeconds;
            if (town.MealProgress >= MealSeconds) { town.MealProgress = 0; town.LastMealMissed = State.Stock.Food < MealDemand; State.Stock.Food = Math.Max(0, State.Stock.Food - MealDemand); }
            if (ConnectedVacancies > 0 && !town.LastMealMissed && State.Stock.Food >= town.Population + 1)
            {
                town.ImmigrationProgress += (float)FixedStepSeconds;
                if (town.ImmigrationProgress >= ImmigrationSeconds)
                    foreach (var tile in _orderedTiles)
                        if (tile.Building == BuildingKind.House && tile.Residents < HouseCapacity && _towardDepot.ContainsKey(tile.Coord))
                        { tile.Residents++; town.Population++; State.Stock.Food -= 2; town.ImmigrationProgress = 0; break; }
            }
            if (town.ResearchJob != ResearchKind.None)
            {
                town.ResearchProgress += (float)FixedStepSeconds;
                if (town.ResearchProgress >= ResearchSeconds(town.ResearchJob))
                { town.Research |= town.ResearchJob; town.ResearchJob = ResearchKind.None; town.ResearchProgress = 0; }
            }
            foreach (var tile in _orderedTiles)
            {
                if (!tile.SaplingPlanted) continue;
                tile.SaplingAge += (float)FixedStepSeconds;
                if (tile.SaplingAge < TreeGrowthSeconds) continue;
                tile.SaplingPlanted = false; tile.SaplingAge = 0; tile.TreesRemaining++; tile.Terrain = TerrainKind.Forest;
            }
        }
        private void EnsureStaffing()
        {
            EnsureNetwork(); if (!_staffingDirty) return;
            _staffed.Clear();
            foreach (var tile in _orderedTiles) if (tile.Workers > 0 && !tile.WorkPaused && _towardDepot.ContainsKey(tile.Coord)) _staffed.Add(tile.Coord);
            _staffingDirty = false;
        }
        private TileState FindPlantingSite(TileState forester)
        {
            TileState best = null;
            foreach (var coord in forester.Coord.Neighbors())
            {
                var tile = GetTile(coord);
                if (tile == null || !tile.Claimed || tile.InitialOre > 0 || tile.InitialStone > 0 || tile.InitialClay > 0 || tile.SaplingPlanted
                    || tile.TreesRemaining >= ForestCapacity || (tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest)
                    || (tile.Building != BuildingKind.None && tile.Building != BuildingKind.Forestry)) continue;
                if (best == null || tile.TreesRemaining < best.TreesRemaining || tile.TreesRemaining == best.TreesRemaining && tile.Building == BuildingKind.Forestry) best = tile;
            }
            return best;
        }
        private bool IsManagedForest(TileState tile) { foreach (var coord in tile.Coord.Neighbors()) if (GetTile(coord)?.Building == BuildingKind.Forester) return true; return false; }
        private static ResourceKind OutputResource(BuildingKind kind) => kind == BuildingKind.Forestry ? ResourceKind.Timber
            : kind == BuildingKind.Mine || kind == BuildingKind.DeepMine ? ResourceKind.Ore
            : kind == BuildingKind.Forager || kind == BuildingKind.Farm ? ResourceKind.Food
            : kind == BuildingKind.Quarry ? ResourceKind.Stone : kind == BuildingKind.ClayPit ? ResourceKind.Clay
            : kind == BuildingKind.Kiln ? ResourceKind.Brick : ResourceKind.Metal;
        private static int OutputAmount(TileState tile) => tile.Building == BuildingKind.Forestry ? TimberPerTree
            : tile.Building == BuildingKind.Mine ? Math.Min(2, tile.OreRemaining) : tile.Building == BuildingKind.DeepMine ? Math.Min(2, tile.DeepOreRemaining)
            : tile.Building == BuildingKind.Quarry ? Math.Min(2, tile.StoneRemaining) : tile.Building == BuildingKind.ClayPit ? Math.Min(3, tile.ClayRemaining)
            : tile.Building == BuildingKind.Forager ? 3 : tile.Building == BuildingKind.Farm ? 6 : tile.Building == BuildingKind.Kiln ? 2 : 1;
        private static int TileDepth(TileState tile) => CalculateDepth(tile.InitialOre, tile.OreRemaining) + CalculateDepth(tile.DeepOreInitial, tile.DeepOreRemaining) + CalculateDepth(tile.InitialStone, tile.StoneRemaining);
        private static void ValidateTown(WorldState state)
        {
            var town = state.Town;
            if (town == null || town.Population < 4 || town.Population > 4 + 816 * 4 || town.LegacyResearchPoints < 0 || town.LegacyResearchPoints > 3)
                throw InvalidSave("town population or legacy credits are invalid");
            if ((town.Research & ~(ResearchKind)1023) != 0 || town.ResearchJob != ResearchKind.None && Array.IndexOf(ResearchTopics, town.ResearchJob) < 0)
                throw InvalidSave("unknown research");
            foreach (var kind in ResearchTopics) if ((town.Research & kind) != 0 && (town.Research & ResearchPrerequisite(kind)) != ResearchPrerequisite(kind)) throw InvalidSave("missing research prerequisite");
            if (!IsFinite(town.ImmigrationProgress) || town.ImmigrationProgress < 0 || town.ImmigrationProgress >= ImmigrationSeconds
                || !IsFinite(town.MealProgress) || town.MealProgress < 0 || town.MealProgress >= MealSeconds
                || !IsFinite(town.ForageCooldown) || town.ForageCooldown < 0 || town.ForageCooldown > ForageSeconds
                || !IsFinite(town.ResearchProgress) || town.ResearchProgress < 0
                || town.ResearchJob == ResearchKind.None && town.ResearchProgress != 0
                || town.ResearchJob != ResearchKind.None && (town.ResearchProgress >= ResearchSeconds(town.ResearchJob)
                    || (town.Research & town.ResearchJob) != 0 || town.Population < ResearchPopulation(town.ResearchJob)
                    || (town.Research & ResearchPrerequisite(town.ResearchJob)) != ResearchPrerequisite(town.ResearchJob))) throw InvalidSave("town progress is invalid");
        }
    }
}
