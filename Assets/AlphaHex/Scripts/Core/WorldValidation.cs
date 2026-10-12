using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    public sealed partial class Simulation
    {
        private static void ValidateState(WorldState state)
        {
            if (state == null) throw InvalidSave("payload is null");
            if (state.SchemaVersion != CurrentSchemaVersion) throw InvalidSave("unsupported schema version");
            if (state.Stock == null || state.Stock.Timber < 0 || state.Stock.Ore < 0 || state.Stock.Metal < 0 || state.Stock.Food < 0 || state.Stock.Stone < 0 || state.Stock.Clay < 0 || state.Stock.Brick < 0)
                throw InvalidSave("resource stock is missing or negative");
            if (state.Tiles == null || state.Tiles.Count < 37 || state.Tiles.Count > 1 + 3 * WorldRadius * (WorldRadius + 1))
                throw InvalidSave("the discovered region has an invalid size");
            if (state.Shipments == null || state.Shipments.Count > state.Tiles.Count - 1)
                throw InvalidSave("shipment list is missing or too large");
            if (state.ExpansionClaims < 0) throw InvalidSave("invalid expansion count");
            if (state.SimulationSteps < 0 || !IsFinite(state.ElapsedSeconds) || state.ElapsedSeconds < 0
                || !IsFinite(state.PendingSeconds) || state.PendingSeconds < 0 || state.PendingSeconds >= FixedStepSeconds)
                throw InvalidSave("simulation clock is invalid");
            float expectedElapsed = (float)(state.SimulationSteps * FixedStepSeconds);
            if (state.ElapsedSeconds != expectedElapsed)
                throw InvalidSave("elapsed time disagrees with the saved simulation steps");

            ValidateTown(state);
            var tiles = new Dictionary<HexCoord, TileState>();
            int depots = 0;
            int residents = PioneerCitizens;
            int workforce = 0;
            var committed = new long[Resources.Length];
            foreach (var resource in Resources) committed[(int)resource] = StockAmount(state.Stock, resource);
            foreach (TileState tile in state.Tiles)
            {
                if (tile == null) throw InvalidSave("tile entry is null");
                if (HexCoord.Distance(tile.Coord, DepotCoord) > WorldRadius || tiles.ContainsKey(tile.Coord))
                    throw InvalidSave("duplicate or out-of-bounds tile coordinates");
                tiles.Add(tile.Coord, tile);
                if (!Enum.IsDefined(typeof(TerrainKind), tile.Terrain) || !Enum.IsDefined(typeof(BuildingKind), tile.Building))
                    throw InvalidSave("unknown terrain or building type");
                if (tile.Elevation < 0 || tile.Elevation > 5) throw InvalidSave("terrain elevation is outside the supported range");
                if (tile.Terrain == TerrainKind.Water && (tile.Claimed || tile.HasRoad || tile.Building != BuildingKind.None))
                    throw InvalidSave("water cannot be owned or developed");
                if ((tile.HasRoad || tile.Building != BuildingKind.None) && !tile.Claimed)
                    throw InvalidSave("infrastructure is on unclaimed land");
                if (tile.TreesRemaining < 0 || tile.TreesRemaining > 64
                    || (tile.Terrain == TerrainKind.Forest) != (tile.TreesRemaining > 0))
                    throw InvalidSave("forest resource state is inconsistent");
                if (tile.InitialOre < 0 || tile.InitialOre > 1000000 || tile.OreRemaining < 0 || tile.OreRemaining > tile.InitialOre
                    || (tile.Terrain == TerrainKind.Ore) != (tile.OreRemaining > 0))
                    throw InvalidSave("ore resource state is inconsistent");
                if (tile.InitialOre > 0 && tile.Terrain != TerrainKind.Ore && tile.Terrain != TerrainKind.Rock)
                    throw InvalidSave("ore history is attached to unrelated terrain");
                if (tile.DeepOreInitial != tile.InitialOre * 2 || tile.DeepOreRemaining < 0 || tile.DeepOreRemaining > tile.DeepOreInitial
                    || (tile.DeepOreRemaining < tile.DeepOreInitial && (tile.OreRemaining > 0 || (state.Town.Research & ResearchKind.DeepMining) == 0)))
                    throw InvalidSave("deep ore reserves are inconsistent");
                if (tile.InitialStone < 0 || tile.InitialStone > 1000000 || tile.StoneRemaining < 0 || tile.StoneRemaining > tile.InitialStone
                    || tile.InitialStone > 0 && tile.Terrain != TerrainKind.Stone && tile.Terrain != TerrainKind.Rock
                    || tile.Terrain == TerrainKind.Stone && tile.InitialStone == 0
                    || tile.InitialOre > 0 && tile.InitialStone > 0)
                    throw InvalidSave("stone reserve is inconsistent");
                if (tile.InitialClay < 0 || tile.InitialClay > 1000000 || tile.ClayRemaining < 0 || tile.ClayRemaining > tile.InitialClay
                    || tile.InitialClay > 0 && (tile.InitialOre > 0 || tile.InitialStone > 0 || tile.TreesRemaining > 0)
                    || tile.ClayRemaining > 0 && tile.Terrain != TerrainKind.Clay && tile.Terrain != TerrainKind.Grass
                    || tile.ClayRemaining == 0 && tile.Terrain == TerrainKind.Clay)
                    throw InvalidSave("clay reserve is inconsistent");
                if (tile.Workers < 0 || tile.Workers > CrewCapacity(tile.Building) || tile.WorkPaused && tile.Workers != 0)
                    throw InvalidSave("crew allocation is invalid");
                workforce += tile.Workers;
                if (tile.Building == BuildingKind.Watchtower
                    ? tile.TowerLevel < 1 || tile.TowerLevel > 2 || tile.SurveyedLevel < 0 || tile.SurveyedLevel > tile.TowerLevel
                        || tile.TowerLevel == 2 && (state.Town.Research & ResearchKind.Masonry) == 0
                    : tile.TowerLevel != 0 || tile.SurveyedLevel != 0)
                    throw InvalidSave("tower survey state is invalid");
                if (tile.ExcavationDepth != TileDepth(tile))
                    throw InvalidSave("excavation depth disagrees with resource depletion");
                if (tile.Residents < 0 || tile.Residents > HouseCapacity || tile.Residents % ImmigrationWave != 0
                    || tile.Building != BuildingKind.House && tile.Residents != 0)
                    throw InvalidSave("house residents are inconsistent");
                residents += tile.Residents;
                if (tile.WorkPaused && !NeedsWorker(tile.Building)) throw InvalidSave("only workplaces can pause work");
                if (!IsFinite(tile.SaplingAge) || tile.SaplingAge < 0 || tile.SaplingAge >= TreeGrowthSeconds
                    || !tile.SaplingPlanted && tile.SaplingAge != 0
                    || tile.SaplingPlanted && (!tile.Claimed || tile.InitialOre > 0 || tile.InitialStone > 0 || tile.InitialClay > 0 || tile.TreesRemaining >= ForestCapacity
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
                if ((tile.Building == BuildingKind.Smelter || tile.Building == BuildingKind.House || tile.Building == BuildingKind.Watchtower
                    || tile.Building == BuildingKind.Kiln || tile.Building == BuildingKind.Bloomery || tile.Building == BuildingKind.Depot)
                    && (tile.TreesRemaining > 0 || tile.OreRemaining > 0 || tile.SaplingPlanted))
                    throw InvalidSave("building terrain is not clear");
                if ((tile.Building == BuildingKind.Farm || tile.Building == BuildingKind.Forester)
                    && (tile.Terrain != TerrainKind.Grass || tile.InitialOre > 0 || tile.SaplingPlanted))
                    throw InvalidSave("farm or forester is not on clear grassland");
                if (tile.Building == BuildingKind.Forager && ((tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest) || tile.SaplingPlanted))
                    throw InvalidSave("forager is on unsuitable terrain");
                if (tile.Building == BuildingKind.DeepMine && (tile.InitialOre == 0 || tile.OreRemaining > 0))
                    throw InvalidSave("deep mine needs an exhausted surface deposit");
                if (tile.Building == BuildingKind.Quarry && tile.InitialStone == 0 || tile.Building == BuildingKind.ClayPit && tile.InitialClay == 0)
                    throw InvalidSave("extractor has no matching deposit");
                if (tile.StoneRemaining > 0 && tile.Building != BuildingKind.None && tile.Building != BuildingKind.Quarry
                    || tile.ClayRemaining > 0 && tile.Building != BuildingKind.None && tile.Building != BuildingKind.ClayPit)
                    throw InvalidSave("building overlaps a deposit");
                ResearchKind required = RequiredResearch(tile.Building);
                if ((state.Town.Research & required) != required) throw InvalidSave("building research is missing");

                float jobSeconds = JobSeconds(tile.Building);
                if ((jobSeconds == 0 && tile.WorkProgress != 0) || (jobSeconds > 0 && tile.WorkProgress >= jobSeconds))
                    throw InvalidSave("job progress exceeds its duration");
                if (IsDepletedExtractor(tile) && tile.WorkProgress != 0)
                    throw InvalidSave("a depleted extractor still has an active job");
                if (tile.InputOreReserved < 0 || tile.InputTimberReserved < 0 || tile.InputClayReserved < 0)
                    throw InvalidSave("negative job reservation");
                if (tile.LegacyOreJob && (tile.Building != BuildingKind.Smelter || tile.InputOreReserved != 2
                    || tile.InputTimberReserved != 0 || tile.InputClayReserved != 0)) throw InvalidSave("invalid legacy foundry job");
                if (HasInputs(tile))
                {
                    var cost = tile.LegacyOreJob ? new ResourceStock(0, 2, 0) : InputCost(tile.Building);
                    if (!IsProcessor(tile.Building) || tile.InputOreReserved != cost.Ore
                        || tile.InputTimberReserved != cost.Timber || tile.InputClayReserved != cost.Clay)
                        throw InvalidSave("job inputs disagree with the recipe");
                }
                else if (IsProcessor(tile.Building) && tile.WorkProgress > 0) throw InvalidSave("processor work has no reserved inputs");
                if (tile.Building == BuildingKind.Bloomery ? tile.BatchRequested != HasInputs(tile) : tile.BatchRequested)
                    throw InvalidSave("manual batch request is inconsistent");
                foreach (var resource in Resources) committed[(int)resource] += Reserved(tile, resource);
            }
            if (depots != 1) throw InvalidSave("exactly one central depot is required");
            if (workforce > state.Town.Population) throw InvalidSave("crews exceed the population");
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
            foreach (Shipment shipment in state.Shipments)
            {
                if (shipment == null || !Enum.IsDefined(typeof(ResourceKind), shipment.Resource) || shipment.Amount <= 0)
                    throw InvalidSave("shipment resource or amount is invalid");
                if (shipment.Path == null || shipment.Path.Count < 2 || shipment.Path.Count > state.Tiles.Count
                    || shipment.Path[shipment.Path.Count - 1] != DepotCoord)
                    throw InvalidSave("shipment path must lead from a source to the depot");
                if (!IsFinite(shipment.Duration) || !IsFinite(shipment.Elapsed) || shipment.Elapsed < 0
                    || shipment.Elapsed >= shipment.Duration
                    || shipment.Duration != (shipment.Path.Count - 1) * DeliverySecondsPerEdge
                        && shipment.Duration != (shipment.Path.Count - 1) * 0.75f)
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
                if (!sources.Add(source.Coord) || source.WorkProgress != 0 || HasInputs(source) || source.BatchRequested)
                    throw InvalidSave("a source has duplicate output or another job during delivery");
                bool validAmount = source.Building == BuildingKind.Forestry ? shipment.Amount == 3
                    : source.Building == BuildingKind.Forager ? shipment.Amount <= 3
                    : source.Building == BuildingKind.Farm ? shipment.Amount <= 6
                    : source.Building == BuildingKind.ClayPit ? shipment.Amount <= 3
                    : source.Building == BuildingKind.Kiln ? shipment.Amount == 2
                    : source.Building == BuildingKind.Smelter || source.Building == BuildingKind.Bloomery ? shipment.Amount == 1
                    : source.Building == BuildingKind.Mine || source.Building == BuildingKind.DeepMine || source.Building == BuildingKind.Quarry ? shipment.Amount <= 2 : false;
                if (!validAmount || shipment.Resource != OutputResource(source.Building))
                    throw InvalidSave("shipment contents disagree with its source building");
                committed[(int)shipment.Resource] += shipment.Amount;
            }
            foreach (long total in committed) if (total > int.MaxValue)
                throw InvalidSave("stock and owned goods would overflow a resource counter");
        }

        private static ArgumentException InvalidSave(string reason) => new ArgumentException("Invalid world save: " + reason + ".", "state");
    }
}
