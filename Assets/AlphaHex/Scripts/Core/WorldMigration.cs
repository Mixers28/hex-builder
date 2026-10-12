using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    public sealed partial class Simulation
    {
        /// <summary>Validate the original rules, then upgrade a copy. Never mutate the source save.</summary>
        public static Simulation Restore(WorldState state)
        {
            if (state == null || state.SchemaVersion == CurrentSchemaVersion) return new Simulation(state);
            if (state.SchemaVersion != 1 && state.SchemaVersion != 2) throw InvalidSave("unsupported Unity save version");
            if (state.Stock == null || state.Tiles == null || state.Shipments == null || state.SchemaVersion == 2 && state.Town == null)
                throw InvalidSave("legacy world is incomplete");
            bool first = state.SchemaVersion == 1;
            var copy = new WorldState { SchemaVersion = 2, Seed = state.Seed, ElapsedSeconds = state.ElapsedSeconds,
                SimulationSteps = state.SimulationSteps, PendingSeconds = state.PendingSeconds,
                Stock = new ResourceStock(state.Stock.Timber, state.Stock.Ore, state.Stock.Metal, first ? StartingFood : state.Stock.Food),
                Town = first ? new TownState() : state.Town.Copy() };
            foreach (var tile in state.Tiles)
            {
                if (tile == null || first && tile.Building > BuildingKind.Smelter) throw InvalidSave("invalid legacy tile");
                var next = tile.Copy();
                if (first) { next.DeepOreInitial = next.InitialOre * 2; next.DeepOreRemaining = next.DeepOreInitial; }
                copy.Tiles.Add(next);
            }
            foreach (var cargo in state.Shipments)
            {
                if (cargo == null || cargo.Path == null || first && cargo.Resource > ResourceKind.Metal) throw InvalidSave("invalid legacy shipment");
                copy.Shipments.Add(new Shipment { Resource = cargo.Resource, Amount = cargo.Amount, Elapsed = cargo.Elapsed,
                    Duration = cargo.Duration, Path = new List<HexCoord>(cargo.Path) });
            }
            ValidateLegacyWorld(copy);
            copy.SchemaVersion = CurrentSchemaVersion;
            copy.Town.LegacyResearchPoints = copy.Town.EarnedMilestones - LegacyResearchCount(copy.Town.Research);
            copy.Town.ImmigrationProgress *= ImmigrationSeconds / 20f;
            copy.Tiles.Sort((a, b) => a.Coord.Q != b.Coord.Q ? a.Coord.Q.CompareTo(b.Coord.Q) : a.Coord.R.CompareTo(b.Coord.R));
            foreach (var tile in copy.Tiles)
            {
                float duration = LegacyJobSeconds(tile.Building);
                if (duration > 0) tile.WorkProgress *= JobSeconds(tile.Building) / duration;
                tile.SaplingAge *= TreeGrowthSeconds / 45f;
                tile.LegacyOreJob = tile.InputOreReserved > 0;
                PreserveIndustryUnlock(copy, tile.Building);
            }
            AddLegacyClay(copy);
            var simulation = new Simulation(copy);
            int available = copy.Town.Population;
            for (int pass = 0; pass < 2; pass++) foreach (var tile in copy.Tiles)
            {
                bool food = tile.Building == BuildingKind.Forager || tile.Building == BuildingKind.Farm;
                if (available <= 0 || (pass == 0) != food || !NeedsWorker(tile.Building) || tile.WorkPaused
                    || simulation.GetPathToDepot(tile.Coord) == null) continue;
                tile.Workers = 1; available--;
            }
            return new Simulation(copy);
        }
        private static void PreserveIndustryUnlock(WorldState world, BuildingKind kind)
        {
            if (kind == BuildingKind.Mine || kind == BuildingKind.Smelter)
                world.Town.Research |= ResearchKind.Brickmaking | ResearchKind.PrimitiveSmelting;
            if (kind == BuildingKind.Smelter) world.Town.Research |= ResearchKind.Refining;
        }
        private static void AddLegacyClay(WorldState world)
        {
            if (world.Tiles.Exists(t => t.InitialClay > 0)) return;
            var candidates = world.Tiles.FindAll(t => t.Terrain == TerrainKind.Grass && t.Building == BuildingKind.None
                && t.InitialOre == 0 && t.InitialStone == 0 && !t.SaplingPlanted);
            candidates.Sort((a, b) => HexCoord.Distance(a.Coord, DepotCoord).CompareTo(HexCoord.Distance(b.Coord, DepotCoord)));
            var chosen = candidates.Find(t => t.Coord == new HexCoord(0, -2));
            if (chosen == null && candidates.Count > 0) chosen = candidates[0];
            if (chosen == null)
            {
                var known = new HashSet<HexCoord>(); foreach (var tile in world.Tiles) known.Add(tile.Coord);
                foreach (var tile in world.Tiles)
                {
                    foreach (var coord in tile.Coord.Neighbors())
                    {
                        if (known.Contains(coord) || HexCoord.Distance(coord, DepotCoord) > WorldRadius) continue;
                        chosen = new TileState { Coord = coord, Terrain = TerrainKind.Clay };
                        break;
                    }
                    if (chosen != null) break;
                }
                if (chosen != null) world.Tiles.Add(chosen);
            }
            // Never overwrite a developed or exhausted plot on a completely occupied legacy map.
            if (chosen != null) { chosen.InitialClay = 180; chosen.ClayRemaining = 180; }
        }
    }
}
