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
            List<TileState> tiles = sim.State.Tiles;
            var producers = tiles.Where(t => t.Building == BuildingKind.Smelter
                || t.Building == BuildingKind.Forestry && t.TreesRemaining > 0
                || t.Building == BuildingKind.Mine && t.OreRemaining > 0).ToList();
            TileState isolated = Nearest(producers.Where(t => sim.GetPathToDepot(t.Coord) == null));
            if (isolated != null)
                return new SettlementObjective("Reconnect production", "Connect the " + Name(isolated.Building)
                    + " at " + isolated.Coord + " to the depot. Roads cost 2 timber per tile; adjoining buildings also connect.", isolated.Coord);

            bool forestry = producers.Any(t => t.Building == BuildingKind.Forestry);
            bool mine = producers.Any(t => t.Building == BuildingKind.Mine);
            bool smelter = producers.Any(t => t.Building == BuildingKind.Smelter);
            // Depletion, expansion and refining survive automatic building removal and save/load.
            // No separate tutorial flags or save-schema changes are needed.
            bool developed = smelter || tiles.Any(t => t.Claimed && HexCoord.Distance(t.Coord, Depot) > 1)
                || sim.GetTile(new HexCoord(-1, 0)).TreesRemaining == 0
                || sim.GetTile(new HexCoord(1, 0)).OreRemaining == 0;

            if (!mine && smelter && sim.State.Stock.Ore < Simulation.OrePerMetal && sim.State.Stock.Timber >= 20)
                return Supply(sim, BuildingKind.Mine, "Restore ore supply");
            if (!forestry)
                return Supply(sim, BuildingKind.Forestry, developed ? "Restore timber supply" : "Start timber production");
            if (!mine)
                return Supply(sim, BuildingKind.Mine, developed ? "Restore ore supply" : "Start iron mining");
            if (!smelter)
            {
                TileState site = Nearest(tiles.Where(t => t.Claimed && t.Building == BuildingKind.None
                    && t.TreesRemaining == 0 && t.OreRemaining == 0));
                if (site == null)
                    return new SettlementObjective("Make room for refining", "Clear a claimed resource tile by gathering, or claim clear land. A smelter needs an empty site.");
                return new SettlementObjective("Refine your first metal", "Build a smelter at " + site.Coord
                    + " for 20 timber + 4 metal. Connect it to the depot; each metal uses 2 ore.", site.Coord);
            }

            TileState runningLow = Nearest(producers.Where(t => t.Building == BuildingKind.Forestry && t.TreesRemaining <= 2
                || t.Building == BuildingKind.Mine && t.OreRemaining <= 4));
            if (runningLow != null)
                return Supply(sim, runningLow.Building, runningLow.Building == BuildingKind.Forestry ? "Plan the next forest" : "Plan the next mine");

            TileState frontier = Nearest(tiles.Where(t => !t.Claimed && t.Terrain != TerrainKind.Water
                && (t.TreesRemaining > 0 || t.OreRemaining > 0) && TouchesClaim(sim, t.Coord)));
            if (frontier != null)
                return new SettlementObjective("Secure the next deposit", "Claim " + frontier.Coord + " for 6 timber + 1 metal: "
                    + Deposit(frontier) + ". Connect new production with roads; shorter routes deliver more often.", frontier.Coord);
            return new SettlementObjective("Keep metal flowing", "Your production loop is connected. Inspect sites for remaining deposits and delivery times, then extend roads toward fresh resources.");
        }

        private static SettlementObjective Supply(Simulation sim, BuildingKind kind, string heading)
        {
            bool timber = kind == BuildingKind.Forestry;
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
                        return new SettlementObjective(heading, direction + " Each claim costs 6 timber + 1 metal. Then build a "
                            + Name(kind) + " and connect it to the depot.", step);
                    }
                    queue.Enqueue(next);
                }
            }
            bool remaining = sim.State.Tiles.Any(t => timber ? t.TreesRemaining > 0 : t.OreRemaining > 0);
            return new SettlementObjective(remaining ? "No new deposit in reach" : timber ? "Forests exhausted" : "Ore deposits exhausted",
                "No unused " + (timber ? "forest" : "ore deposit") + " remains reachable. Use current production and stock; Menu > New island starts a fresh island.");
        }

        private static TileState Nearest(IEnumerable<TileState> tiles) => tiles
            .OrderBy(t => HexCoord.Distance(t.Coord, Depot)).ThenBy(t => t.Coord.Q).ThenBy(t => t.Coord.R).FirstOrDefault();

        private static bool TouchesClaim(Simulation sim, HexCoord coord) => coord.Neighbors().Any(c => sim.GetTile(c)?.Claimed == true);
        private static string Name(BuildingKind kind) => kind == BuildingKind.Forestry ? "forestry camp" : kind == BuildingKind.Mine ? "mine" : "smelter";
        private static string Cost(BuildingKind kind) => kind == BuildingKind.Forestry ? "10 timber" : "14 timber + 2 metal";
        private static string Deposit(TileState tile) => tile.TreesRemaining > 0
            ? tile.TreesRemaining + " trees / " + (tile.TreesRemaining * Simulation.TimberPerTree) + " timber"
            : tile.OreRemaining + " ore";
    }
}
