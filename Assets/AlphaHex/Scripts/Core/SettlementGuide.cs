using System;
using System.Collections.Generic;
using System.Linq;

namespace AlphaHex.Core
{
    /// <summary>A read-only next step derived from the saved world, including older saves.</summary>
    public sealed class SettlementObjective
    {
        public string Heading { get; }
        public string Detail { get; }
        public HexCoord? Target { get; }

        public SettlementObjective(string heading, string detail, HexCoord? target = null)
        {
            Heading = heading;
            Detail = detail;
            Target = target;
        }
    }

    public static class SettlementGuide
    {
        private static readonly HexCoord Depot = new HexCoord(0, 0);

        public static SettlementObjective Next(Simulation sim)
        {
            var tiles = sim.State.Tiles;
            TileState isolated = Nearest(tiles.Where(t => t.Building != BuildingKind.None && sim.GetPathToDepot(t.Coord) == null));
            if (isolated != null) return new SettlementObjective("Connect your settlement", "Connect this " + Simulation.BuildingName(isolated.Building)
                + " with roads or adjoining buildings. Roads cost 2 timber.", isolated.Coord);
            TileState food = Nearest(tiles.Where(t => t.Building == BuildingKind.Forager || t.Building == BuildingKind.Farm));
            if (food == null || sim.State.Town.LastMealMissed)
                return new SettlementObjective("Feed your citizens", "Build a forager lodge for " + Simulation.CostText(Simulation.GetBuildCost(BuildingKind.Forager))
                    + " and assign a worker. Hand foraging [F] supplies 2 food every 8 seconds.", food?.Coord ?? new HexCoord(-1, 1));
            if (food.Workers == 0) return new SettlementObjective("Assign your food crew", "Select the lodge and assign a worker. New citizens remain available until you choose their workplace.", food.Coord);
            if (sim.ConnectedVacancies == 0 && sim.State.Town.Population < 10)
                return new SettlementObjective("Welcome more citizens", "Build a connected cottage for " + Simulation.CostText(Simulation.GetBuildCost(BuildingKind.House))
                    + ". Its four places fill one at a time, every 90 supported seconds.", Nearest(tiles.Where(ClearOwned))?.Coord);
            if (sim.State.Town.Population < 6) return new SettlementObjective("Your first newcomers", sim.GetImmigrationStatus());
            if (sim.State.Town.ResearchJob != ResearchKind.None) return new SettlementObjective("Research in progress",
                Simulation.ResearchName(sim.State.Town.ResearchJob) + ": " + Math.Ceiling(Simulation.ResearchSeconds(sim.State.Town.ResearchJob)
                - sim.State.Town.ResearchProgress) + " seconds remain. Keep food supplied and prepare materials.");
            return NextIndustry(sim);
        }
        public static SettlementObjective NextIndustry(Simulation sim)
        {
            var tiles = sim.State.Tiles;
            TileState unstaffed = Nearest(tiles.Where(t => Simulation.NeedsWorker(t.Building) && !t.WorkPaused && t.Workers == 0));
            if (unstaffed != null && sim.IdleCitizens > 0) return new SettlementObjective("Put citizens to work", "Assign a crew to this "
                + Simulation.BuildingName(unstaffed.Building) + ". More workers speed the job; each workplace still waits for its own cart.", unstaffed.Coord);
            if (!tiles.Any(t => t.Building == BuildingKind.Forestry && t.TreesRemaining > 0)) return Supply(sim, BuildingKind.Forestry, "Restore timber supply");
            if (!sim.HasResearch(ResearchKind.Brickmaking)) return new SettlementObjective("Learn brick firing",
                "Claim the clay bed for 6 timber; gather clay by hand or staff a clay pit. Research Brick firing at six citizens: "
                + Simulation.CostText(Simulation.ResearchCost(ResearchKind.Brickmaking)) + ".", Nearest(tiles.Where(t => t.ClayRemaining > 0))?.Coord);
            if (!tiles.Any(t => t.Building == BuildingKind.Kiln)) return new SettlementObjective("Fire the first bricks", "Build a brick kiln for "
                + Simulation.CostText(Simulation.GetBuildCost(BuildingKind.Kiln)) + ". Assign a crew: 4 clay + 2 timber becomes 2 bricks.", Nearest(tiles.Where(ClearOwned))?.Coord);
            if (!sim.HasResearch(ResearchKind.PrimitiveSmelting)) return new SettlementObjective("Learn primitive smelting", "Welcome eight citizens and research Primitive smelting for "
                + Simulation.CostText(Simulation.ResearchCost(ResearchKind.PrimitiveSmelting)) + ". Keep timber for kiln fuel and consider Woodland stewardship.");
            TileState bloomery = Nearest(tiles.Where(t => t.Building == BuildingKind.Bloomery));
            if (!sim.HasResearch(ResearchKind.Refining))
            {
                if (bloomery == null) return new SettlementObjective("Make metal without metal", "Build a primitive bloomery for "
                    + Simulation.CostText(Simulation.GetBuildCost(BuildingKind.Bloomery)) + ". Hand mining provides the ore.", Nearest(tiles.Where(ClearOwned))?.Coord);
                return new SettlementObjective("Smelt a manual batch", "Assign one worker and press Start batch. Each batch reserves 3 ore + 3 timber for 1 metal, then waits for delivery. At ten citizens, research Efficient refining.", bloomery.Coord);
            }
            if (!tiles.Any(t => t.Building == BuildingKind.Smelter)) return new SettlementObjective("Build an efficient foundry", "Save "
                + Simulation.CostText(Simulation.GetBuildCost(BuildingKind.Smelter)) + ". A staffed foundry automatically turns 2 ore + 1 timber into metal, faster than a bloomery.", Nearest(tiles.Where(ClearOwned))?.Coord);
            if (!sim.HasResearch(ResearchKind.Surveying)) return new SettlementObjective("Survey the frontier", "Research Frontier surveying, then build a connected watchtower. It reveals new land without claiming it.");
            if (!tiles.Any(t => t.Building == BuildingKind.Watchtower)) return new SettlementObjective("Build a watchtower", "Claim a clear frontier plot for 6 timber and build a watchtower for "
                + Simulation.CostText(Simulation.GetBuildCost(BuildingKind.Watchtower)) + ". Connect it to reveal more resources.", new HexCoord(0, 2));
            if (!sim.HasResearch(ResearchKind.QuarryTools)) return new SettlementObjective("Work discovered stone", "Research Stoneworking, then Quarrying tools. Claim and connect a stone outcrop before staffing its quarry.");
            return new SettlementObjective("Grow the frontier", "Use delivered stone to research Frontier masonry and reinforce a watchtower. Replant timber, reach deeper iron and keep the village supplied.");
        }
        private static bool ClearOwned(TileState tile) => tile.Claimed && tile.Building == BuildingKind.None && !tile.SaplingPlanted
            && tile.Terrain != TerrainKind.Water && tile.TreesRemaining == 0 && tile.OreRemaining == 0 && tile.StoneRemaining == 0 && tile.ClayRemaining == 0;

        private static SettlementObjective Supply(Simulation sim, BuildingKind kind, string heading)
        {
            bool timber = kind == BuildingKind.Forestry;
            if (!timber && sim.HasResearch(ResearchKind.DeepMining))
            {
                TileState deep = Nearest(sim.State.Tiles.Where(t => t.Claimed && t.Building == BuildingKind.None
                    && t.InitialOre > 0 && t.OreRemaining == 0 && t.DeepOreRemaining > 0));
                if (deep != null) return new SettlementObjective("Mine below the quarry", "Build a deep mine at " + deep.Coord
                    + " for " + Simulation.CostText(Simulation.GetBuildCost(BuildingKind.DeepMine)) + ". " + deep.DeepOreRemaining + " finite deep ore remains.", deep.Coord);
            }
            Func<TileState, bool> available = t => t.Building == BuildingKind.None
                && (timber ? t.TreesRemaining > 0 : t.OreRemaining > 0);
            TileState site = Nearest(sim.State.Tiles.Where(t => t.Claimed && available(t)));
            if (site != null)
            {
                ResourceStock cost = Simulation.GetBuildCost(kind);
                string action = sim.State.Stock.Timber < cost.Timber || sim.State.Stock.Metal < cost.Metal
                    ? "Gather by hand while saving for a " + Name(kind) + " (" + Cost(kind) + ")."
                    : "Build a " + Name(kind) + " for " + Cost(kind) + "; connect it to the depot.";
                return new SettlementObjective(heading, site.Coord + " has " + Deposit(site) + ". " + action, site.Coord);
            }

            // Search outward from owned land over land only. The target is the first
            // claimable step, so Show site never suggests paying for a distant claim.
            var queue = new Queue<TileState>();
            var firstClaim = new Dictionary<HexCoord, HexCoord>();
            foreach (TileState owned in sim.State.Tiles.Where(t => t.Claimed).OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R))
            {
                firstClaim[owned.Coord] = owned.Coord;
                queue.Enqueue(owned);
            }
            while (queue.Count > 0)
            {
                TileState current = queue.Dequeue();
                foreach (HexCoord coord in current.Coord.Neighbors())
                {
                    TileState next = sim.GetTile(coord);
                    if (next == null || next.Terrain == TerrainKind.Water || firstClaim.ContainsKey(coord)) continue;
                    HexCoord step = current.Claimed ? coord : firstClaim[current.Coord];
                    firstClaim[coord] = step;
                    if (available(next))
                    {
                        string direction = step == coord ? "Claim " + coord + ": " + Deposit(next) + "."
                            : "Claim " + step + " toward " + (timber ? "forest " : "ore ") + coord + ".";
                        return new SettlementObjective(heading, direction + " Each claim costs 6 timber. Then build a "
                            + Name(kind) + " and connect it to the depot.", step);
                    }
                    queue.Enqueue(next);
                }
            }
            bool remaining = sim.State.Tiles.Any(t => timber ? t.TreesRemaining > 0 : t.OreRemaining > 0);
            return new SettlementObjective(remaining ? "No new deposit in reach" : timber ? "Forests exhausted" : "Ore deposits exhausted",
                "No unused " + (timber ? "forest" : "surface ore deposit") + " remains reachable. Grow your town to research "
                    + (timber ? "replanting foresters. They restore trees on adjacent owned grass and forest." : "deep mining. Old quarries have finite deeper reserves."));
        }

        private static TileState Nearest(IEnumerable<TileState> tiles) => tiles
            .OrderBy(t => HexCoord.Distance(t.Coord, Depot)).ThenBy(t => t.Coord.Q).ThenBy(t => t.Coord.R).FirstOrDefault();

        private static bool TouchesClaim(Simulation sim, HexCoord coord) => coord.Neighbors().Any(c => sim.GetTile(c)?.Claimed == true);
        private static string Name(BuildingKind kind) => Simulation.BuildingName(kind).ToLowerInvariant();
        private static string Cost(BuildingKind kind) => Simulation.CostText(Simulation.GetBuildCost(kind));
        private static string Deposit(TileState tile) => tile.TreesRemaining > 0
            ? tile.TreesRemaining + " trees / " + (tile.TreesRemaining * Simulation.TimberPerTree) + " timber"
            : tile.OreRemaining + " ore";
    }
}
