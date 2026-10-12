using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    public sealed partial class Simulation
    {
        public static readonly BuildingKind[] Buildable = { BuildingKind.House, BuildingKind.Forager,
            BuildingKind.Forestry, BuildingKind.ClayPit, BuildingKind.Kiln, BuildingKind.Bloomery,
            BuildingKind.Mine, BuildingKind.Smelter, BuildingKind.Farm, BuildingKind.Forester,
            BuildingKind.Watchtower, BuildingKind.Quarry, BuildingKind.DeepMine };
        public static readonly ResourceKind[] Resources = (ResourceKind[])Enum.GetValues(typeof(ResourceKind));
        public bool CanAfford(ResourceStock cost)
        {
            foreach (var resource in Resources) if (StockAmount(State.Stock, resource) < StockAmount(cost, resource)) return false;
            return true;
        }
        private void Spend(ResourceStock cost) { foreach (var resource in Resources) AddToStock(resource, -(int)StockAmount(cost, resource)); }
        public static string CostText(ResourceStock cost)
        {
            var parts = new List<string>();
            foreach (var resource in Resources) if (StockAmount(cost, resource) > 0)
                parts.Add(StockAmount(cost, resource) + " " + resource.ToString().ToLowerInvariant());
            return parts.Count == 0 ? "no materials" : string.Join(" · ", parts);
        }
        public static ResourceStock InputCost(BuildingKind building) => building == BuildingKind.Kiln ? new ResourceStock(2, 0, 0, clay: 4)
            : building == BuildingKind.Bloomery ? new ResourceStock(3, 3, 0) : building == BuildingKind.Smelter ? new ResourceStock(1, 2, 0) : new ResourceStock();
        public static bool IsProcessor(BuildingKind kind) => kind == BuildingKind.Kiln || kind == BuildingKind.Bloomery || kind == BuildingKind.Smelter;
        public static int Reserved(TileState tile, ResourceKind resource) => resource == ResourceKind.Ore ? tile.InputOreReserved
            : resource == ResourceKind.Timber ? tile.InputTimberReserved : resource == ResourceKind.Clay ? tile.InputClayReserved : 0;
        private static bool HasInputs(TileState tile) => tile.InputOreReserved > 0 || tile.InputTimberReserved > 0 || tile.InputClayReserved > 0;
        private void ReserveInputs(TileState tile)
        {
            ResourceStock input = InputCost(tile.Building); Spend(input);
            tile.InputOreReserved = input.Ore; tile.InputTimberReserved = input.Timber; tile.InputClayReserved = input.Clay;
        }
        private static void ClearInputs(TileState tile) { tile.InputOreReserved = tile.InputTimberReserved = tile.InputClayReserved = 0; tile.LegacyOreJob = false; }
        private void RefundInputs(TileState tile)
        {
            foreach (var resource in Resources) AddToStock(resource, Reserved(tile, resource));
            ClearInputs(tile);
        }
        public bool TryGatherClay(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.ClayRemaining <= 0) return Fail("There is no surface clay here.", out message);
            if (!CanReceive(ResourceKind.Clay, 1)) return Fail("Clay stores are full.", out message);
            tile.ClayRemaining--; AddToStock(ResourceKind.Clay, 1);
            if (tile.ClayRemaining == 0) { tile.Terrain = TerrainKind.Grass; tile.WorkProgress = 0; }
            TryClearDepletedExtractor(tile); message = "Gathered 1 clay; " + tile.ClayRemaining + " remains."; return true;
        }
        public bool TryStartBatch(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.Building != BuildingKind.Bloomery) return Fail("Select a primitive bloomery.", out message);
            if (tile.BatchRequested || FindShipment(coord) != null) return Fail("Wait for this batch and its delivery to finish.", out message);
            if (tile.WorkPaused || tile.Workers != 1) return Fail("Assign one worker before starting a batch.", out message);
            if (GetPathToDepot(coord) == null) return Fail("Connect the bloomery to the village hall.", out message);
            if (!CanAfford(InputCost(tile.Building))) return Fail("A batch needs 3 ore and 3 timber.", out message);
            if (!CanReceive(ResourceKind.Metal, 1)) return Fail("Metal stores are full.", out message);
            ReserveInputs(tile); tile.BatchRequested = true;
            message = "Batch started. Its 3 ore and 3 timber are reserved until completion or removal."; return true;
        }
        public bool TryUpgradeTower(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.Building != BuildingKind.Watchtower || tile.TowerLevel != 1) return Fail("Select a wooden watchtower.", out message);
            if (!HasResearch(ResearchKind.Masonry)) return Fail("Research Frontier masonry first.", out message);
            var cost = new ResourceStock(12, 0, 4, stone: 16);
            if (!CanAfford(cost)) return Fail("Reinforcement needs " + CostText(cost) + ".", out message);
            Spend(cost); tile.TowerLevel = 2; RevealConnectedTowers();
            message = "Tower reinforced. Connect it to survey six hexes in every direction."; return true;
        }
        private void RevealConnectedTowers()
        {
            EnsureNetwork();
            var towers = _orderedTiles.FindAll(t => t.Building == BuildingKind.Watchtower
                && t.SurveyedLevel < t.TowerLevel && _towardDepot.ContainsKey(t.Coord));
            foreach (var tower in towers)
            {
                int radius = tower.TowerLevel == 2 ? 6 : 4;
                for (int q = tower.Coord.Q - radius; q <= tower.Coord.Q + radius; q++)
                    for (int r = tower.Coord.R - radius; r <= tower.Coord.R + radius; r++)
                    {
                        var coord = new HexCoord(q, r);
                        if (HexCoord.Distance(coord, tower.Coord) > radius || HexCoord.Distance(coord, DepotCoord) > WorldRadius || _tiles.ContainsKey(coord)) continue;
                        var tile = GenerateTile(State.Seed, coord); _tiles.Add(coord, tile); _orderedTiles.Add(tile); State.Tiles.Add(tile);
                    }
                tower.SurveyedLevel = tower.TowerLevel;
            }
        }
        private static TileState GenerateTile(int seed, HexCoord coord)
        {
            long value = ((long)coord.Q * 92837111 ^ (long)coord.R * 689287499 ^ (long)seed * 283923481) & 0x7fffffff;
            value = ((value ^ (value >> 13)) * 1274126177) & 0x7fffffff;
            int roll = (int)(value % 100), distance = HexCoord.Distance(coord, DepotCoord);
            TerrainKind terrain = roll < 34 ? TerrainKind.Forest : roll < 49 ? TerrainKind.Ore
                : roll < 62 && distance >= 4 ? TerrainKind.Stone : roll >= 62 && roll < 74 ? TerrainKind.Clay : TerrainKind.Grass;
            if (distance > 5 && roll > 96) terrain = TerrainKind.Water;
            if (distance <= 1 || coord == new HexCoord(0, 2) || coord == new HexCoord(0, 3) || coord == new HexCoord(0, 4)) terrain = TerrainKind.Grass;
            if (coord == new HexCoord(-1, 0) || coord == new HexCoord(-2, 0)) terrain = TerrainKind.Forest;
            if (coord == new HexCoord(1, 0) || coord == new HexCoord(2, 0)) terrain = TerrainKind.Ore;
            if (coord == new HexCoord(0, 5) || coord == new HexCoord(5, -2) || coord == new HexCoord(-5, 2)) terrain = TerrainKind.Stone;
            if (coord == new HexCoord(0, -2)) terrain = TerrainKind.Clay;
            int ore = terrain == TerrainKind.Ore ? 48 + (int)(value % 45) : 0;
            int stone = terrain == TerrainKind.Stone ? 80 + (int)(value % 61) : 0;
            int clay = terrain == TerrainKind.Clay ? 140 + (int)(value % 41) : 0;
            return new TileState { Coord = coord, Terrain = terrain, Elevation = distance <= 1 || terrain == TerrainKind.Water ? 0 : (int)(value % 3),
                TreesRemaining = terrain == TerrainKind.Forest ? 10 + (int)(value % 9) : 0,
                InitialOre = ore, OreRemaining = ore, DeepOreInitial = ore * 2, DeepOreRemaining = ore * 2,
                InitialStone = stone, StoneRemaining = stone, InitialClay = clay, ClayRemaining = clay };
        }
    }
}
