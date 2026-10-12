using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    public sealed partial class Simulation
    {
        // Frozen schema-2 rules validate legacy saves before any migration is applied.
        private static int LegacyResearchCount(ResearchKind kind) => ((kind & ResearchKind.Farming) != 0 ? 1 : 0)
            + ((kind & ResearchKind.Reforestation) != 0 ? 1 : 0) + ((kind & ResearchKind.DeepMining) != 0 ? 1 : 0);
        private static ResearchKind LegacyRequiredResearch(BuildingKind kind) => kind == BuildingKind.Farm ? ResearchKind.Farming
            : kind == BuildingKind.Forester ? ResearchKind.Reforestation : kind == BuildingKind.DeepMine ? ResearchKind.DeepMining : ResearchKind.None;
        private static float LegacyJobSeconds(BuildingKind kind) => kind == BuildingKind.Forestry ? 4 : kind == BuildingKind.Mine ? 5
            : kind == BuildingKind.Smelter ? 6 : kind == BuildingKind.Forager || kind == BuildingKind.Forester ? 8
            : kind == BuildingKind.Farm ? 20 : kind == BuildingKind.DeepMine ? 7 : 0;
        private static void ValidateLegacyWorld(WorldState state)
        {
            if (state == null) throw InvalidSave("payload is null");
            if (state.SchemaVersion != 2) throw InvalidSave("unsupported schema version");
            if (state.Stock == null || state.Stock.Timber < 0 || state.Stock.Ore < 0 || state.Stock.Metal < 0 || state.Stock.Food < 0)
                throw InvalidSave("resource stock is missing or negative");
            if (state.Tiles == null || state.Tiles.Count != 1 + 3 * 6 * (6 + 1))
                throw InvalidSave("the island must contain every radius-6 hex exactly once");
            if (state.Shipments == null || state.Shipments.Count > state.Tiles.Count - 1)
                throw InvalidSave("shipment list is missing or too large");
            if (state.SimulationSteps < 0 || !IsFinite(state.ElapsedSeconds) || state.ElapsedSeconds < 0
                || !IsFinite(state.PendingSeconds) || state.PendingSeconds < 0 || state.PendingSeconds >= FixedStepSeconds)
                throw InvalidSave("simulation clock is invalid");
            float expectedElapsed = (float)(state.SimulationSteps * FixedStepSeconds);
            if (state.ElapsedSeconds != expectedElapsed)
                throw InvalidSave("elapsed time disagrees with the saved simulation steps");

            ValidateLegacyTown(state);
            var tiles = new Dictionary<HexCoord, TileState>();
            int depots = 0;
            int residents = PioneerCitizens;
            long reservedOre = 0;
            foreach (TileState tile in state.Tiles)
            {
                if (tile == null || tile.Building > BuildingKind.DeepMine || tile.Terrain > TerrainKind.Water) throw InvalidSave("tile entry is null");
                if (HexCoord.Distance(tile.Coord, DepotCoord) > 6 || tiles.ContainsKey(tile.Coord))
                    throw InvalidSave("duplicate or out-of-bounds tile coordinates");
                tiles.Add(tile.Coord, tile);
                if (!Enum.IsDefined(typeof(TerrainKind), tile.Terrain) || !Enum.IsDefined(typeof(BuildingKind), tile.Building))
                    throw InvalidSave("unknown terrain or building type");
                if (tile.Elevation < 0 || tile.Elevation > 5) throw InvalidSave("terrain elevation is outside the supported range");
                if (tile.Terrain == TerrainKind.Water && (tile.Claimed || tile.HasRoad || tile.Building != BuildingKind.None || tile.Elevation != 0))
                    throw InvalidSave("water cannot be owned or developed");
                if ((tile.HasRoad || tile.Building != BuildingKind.None) && !tile.Claimed)
                    throw InvalidSave("infrastructure is on unclaimed land");
                if (tile.TreesRemaining < 0 || tile.TreesRemaining > 64
                    || (tile.Terrain == TerrainKind.Forest) != (tile.TreesRemaining > 0))
                    throw InvalidSave("forest resource state is inconsistent");
                if (tile.InitialOre < 0 || tile.InitialOre > 100000 || tile.OreRemaining < 0 || tile.OreRemaining > tile.InitialOre
                    || (tile.Terrain == TerrainKind.Ore) != (tile.OreRemaining > 0))
                    throw InvalidSave("ore resource state is inconsistent");
                if (tile.InitialOre > 0 && tile.Terrain != TerrainKind.Ore && tile.Terrain != TerrainKind.Rock)
                    throw InvalidSave("ore history is attached to unrelated terrain");
                if (tile.DeepOreInitial != tile.InitialOre * 2 || tile.DeepOreRemaining < 0 || tile.DeepOreRemaining > tile.DeepOreInitial
                    || (tile.DeepOreRemaining < tile.DeepOreInitial && (tile.OreRemaining > 0 || (state.Town.Research & ResearchKind.DeepMining) == 0)))
                    throw InvalidSave("deep ore reserves are inconsistent");
                if (tile.ExcavationDepth != (CalculateDepth(tile.InitialOre, tile.OreRemaining) + CalculateDepth(tile.DeepOreInitial, tile.DeepOreRemaining)))
                    throw InvalidSave("excavation depth disagrees with resource depletion");
                if (tile.Residents < 0 || tile.Residents > HouseCapacity || tile.Residents % 2 != 0
                    || tile.Building != BuildingKind.House && tile.Residents != 0)
                    throw InvalidSave("house residents are inconsistent");
                residents += tile.Residents;
                if (tile.WorkPaused && !NeedsWorker(tile.Building)) throw InvalidSave("only workplaces can pause work");
                if (!IsFinite(tile.SaplingAge) || tile.SaplingAge < 0 || tile.SaplingAge >= 45f
                    || !tile.SaplingPlanted && tile.SaplingAge != 0
                    || tile.SaplingPlanted && (!tile.Claimed || tile.InitialOre > 0 || tile.TreesRemaining >= 12
                        || (state.Town.Research & ResearchKind.Reforestation) == 0
                        || (tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest)
                        || (tile.Building != BuildingKind.None && tile.Building != BuildingKind.Forestry)))
                    throw InvalidSave("sapling growth is inconsistent");
                if (!IsFinite(tile.WorkProgress) || tile.WorkProgress < 0)
                    throw InvalidSave("job progress is invalid");

                if (tile.Building == BuildingKind.Depot)
                {
                    depots++;
                    if (tile.Coord != DepotCoord || tile.Terrain != TerrainKind.Grass)
                        throw InvalidSave("the depot must occupy the central grass tile");
                }
                if (tile.Building == BuildingKind.Forestry && tile.Terrain != TerrainKind.Forest && tile.Terrain != TerrainKind.Grass)
                    throw InvalidSave("forestry is on unsuitable terrain");
                if (tile.Building == BuildingKind.Mine && (tile.InitialOre == 0 || (tile.Terrain != TerrainKind.Ore && tile.Terrain != TerrainKind.Rock)))
                    throw InvalidSave("a mine lacks a valid ore deposit");
                if ((tile.Building == BuildingKind.Smelter || tile.Building == BuildingKind.House)
                    && (tile.TreesRemaining > 0 || tile.OreRemaining > 0 || tile.SaplingPlanted))
                    throw InvalidSave("building terrain is not clear");
                if ((tile.Building == BuildingKind.Farm || tile.Building == BuildingKind.Forester)
                    && (tile.Terrain != TerrainKind.Grass || tile.InitialOre > 0 || tile.SaplingPlanted))
                    throw InvalidSave("farm or forester is not on clear grassland");
                if (tile.Building == BuildingKind.Forager && ((tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest) || tile.SaplingPlanted))
                    throw InvalidSave("forager is on unsuitable terrain");
                if (tile.Building == BuildingKind.DeepMine && (tile.InitialOre == 0 || tile.OreRemaining > 0))
                    throw InvalidSave("deep mine needs an exhausted surface deposit");
                ResearchKind required = LegacyRequiredResearch(tile.Building);
                if ((state.Town.Research & required) != required) throw InvalidSave("building research is missing");

                float jobSeconds = LegacyJobSeconds(tile.Building);
                if ((jobSeconds == 0 && tile.WorkProgress != 0) || (jobSeconds > 0 && tile.WorkProgress >= jobSeconds))
                    throw InvalidSave("job progress exceeds its duration");
                if (IsDepletedExtractor(tile) && tile.WorkProgress != 0)
                    throw InvalidSave("a depleted extractor still has an active job");
                if (tile.Building == BuildingKind.Smelter)
                {
                    if ((tile.InputOreReserved != 0 && tile.InputOreReserved != OrePerMetal)
                        || (tile.InputOreReserved == 0 && tile.WorkProgress != 0))
                        throw InvalidSave("smelter input reservation is inconsistent");
                }
                else if (tile.InputOreReserved != 0)
                    throw InvalidSave("only a smelter can reserve ore");
                reservedOre += tile.InputOreReserved;
            }
            if (depots != 1) throw InvalidSave("exactly one central depot is required");
            if (residents != state.Town.Population) throw InvalidSave("population disagrees with occupied homes");

            // Every claim must be reachable through other claimed land: this is the same
            // frontier rule enforced by TryClaim, independent of the road network.
            var claimed = new HashSet<HexCoord> { DepotCoord };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(DepotCoord);
            while (frontier.Count > 0)
            {
                foreach (HexCoord neighbor in frontier.Dequeue().Neighbors())
                    if (tiles.TryGetValue(neighbor, out TileState adjacent) && adjacent.Claimed && claimed.Add(neighbor))
                        frontier.Enqueue(neighbor);
            }
            foreach (TileState tile in state.Tiles)
                if (tile.Claimed && !claimed.Contains(tile.Coord))
                    throw InvalidSave("claimed land is disconnected from the depot territory");

            var sources = new HashSet<HexCoord>();
            long timber = state.Stock.Timber;
            long ore = state.Stock.Ore + reservedOre;
            long metal = state.Stock.Metal;
            long food = state.Stock.Food;
            foreach (Shipment shipment in state.Shipments)
            {
                if (shipment == null || shipment.Resource < ResourceKind.Timber || shipment.Resource > ResourceKind.Food || shipment.Amount <= 0)
                    throw InvalidSave("shipment resource or amount is invalid");
                if (shipment.Path == null || shipment.Path.Count < 2 || shipment.Path.Count > state.Tiles.Count
                    || shipment.Path[shipment.Path.Count - 1] != DepotCoord)
                    throw InvalidSave("shipment path must lead from a source to the depot");
                if (!IsFinite(shipment.Duration) || !IsFinite(shipment.Elapsed) || shipment.Elapsed < 0
                    || shipment.Elapsed >= shipment.Duration
                    || shipment.Duration != (shipment.Path.Count - 1) * 0.75f)
                    throw InvalidSave("shipment timing is inconsistent with its route");

                var visited = new HashSet<HexCoord>();
                for (int i = 0; i < shipment.Path.Count; i++)
                {
                    HexCoord coord = shipment.Path[i];
                    if (!visited.Add(coord) || !tiles.TryGetValue(coord, out TileState pathTile) || !IsNetworkTile(pathTile)
                        || (i > 0 && HexCoord.Distance(shipment.Path[i - 1], coord) != 1))
                        throw InvalidSave("shipment path is disconnected, repeated, or blocked");
                }
                TileState source = tiles[shipment.Path[0]];
                if (!sources.Add(source.Coord) || source.WorkProgress != 0 || source.InputOreReserved != 0)
                    throw InvalidSave("a source has duplicate output or another job during delivery");
                if (shipment.Resource == ResourceKind.Timber && (source.Building != BuildingKind.Forestry || shipment.Amount != TimberPerTree)
                    || shipment.Resource == ResourceKind.Ore && ((source.Building != BuildingKind.Mine && source.Building != BuildingKind.DeepMine) || shipment.Amount > 2)
                    || shipment.Resource == ResourceKind.Metal && (source.Building != BuildingKind.Smelter || shipment.Amount != 1)
                    || shipment.Resource == ResourceKind.Food && !((source.Building == BuildingKind.Forager && shipment.Amount == 2)
                        || (source.Building == BuildingKind.Farm && shipment.Amount == 6)))
                    throw InvalidSave("shipment contents disagree with its source building");
                if (shipment.Resource == ResourceKind.Timber) timber += shipment.Amount;
                else if (shipment.Resource == ResourceKind.Ore) ore += shipment.Amount;
                else if (shipment.Resource == ResourceKind.Metal) metal += shipment.Amount;
                else food += shipment.Amount;
            }
            if (timber > int.MaxValue || ore > int.MaxValue || metal > int.MaxValue || food > int.MaxValue)
                throw InvalidSave("stock and owned goods would overflow a resource counter");
        }

        private static void ValidateLegacyTown(WorldState state)
        {
            TownState town = state.Town;
            if (town == null || town.Population < PioneerCitizens || town.Population > PioneerCitizens + 126 * HouseCapacity
                || town.Population % 2 != 0) throw InvalidSave("town population is invalid");
            int milestones = town.Population >= 14 ? 3 : town.Population >= 10 ? 2 : town.Population >= 6 ? 1 : 0;
            if (town.EarnedMilestones != milestones || (town.Research & ~(ResearchKind.Farming | ResearchKind.Reforestation | ResearchKind.DeepMining)) != 0
                || LegacyResearchCount(town.Research) > town.EarnedMilestones)
                throw InvalidSave("research milestones are inconsistent");
            if (!IsFinite(town.ImmigrationProgress) || town.ImmigrationProgress < 0 || town.ImmigrationProgress >= 20f
                || !IsFinite(town.MealProgress) || town.MealProgress < 0 || town.MealProgress >= MealSeconds
                || !IsFinite(town.ForageCooldown) || town.ForageCooldown < 0 || town.ForageCooldown > ForageSeconds)
                throw InvalidSave("town progress is invalid");
        }
    }
}
