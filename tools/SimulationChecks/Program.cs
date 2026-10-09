using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AlphaHex.Core;

namespace AlphaHex.SimulationChecks
{
    internal static class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };
        private static readonly HexCoord Forest = new HexCoord(-1, 0);
        private static readonly HexCoord Mine = new HexCoord(1, 0);
        private static readonly HexCoord Smelter = new HexCoord(0, 1);
        private static int _passed;

        private static int Main()
        {
            var checks = new (string Name, Action Run)[]
            {
                ("Deterministic island and a usable starter area", DeterministicIsland),
                ("Claims enforce adjacency, ownership and payment", Claims),
                ("Building placement preserves resources and construction costs", Construction),
                ("Manual gathering depletes finite resources exactly once", ManualConservation),
                ("Disconnected production pauses and connected output travels", NetworkAndDeliveries),
                ("The final ore batch cannot be duplicated", LastOreBatch),
                ("Full automated extraction conserves finite timber and ore", AutomatedDepletion),
                ("Exhausted extractors clear only after their saved final delivery arrives", FinalDeliveryCleanup),
                ("Manual depletion clears idle extractors and preserves outgoing cargo", ManualExtractorCleanup),
                ("Cleared extractors preserve other producers' delivery routes", CleanupPreservesRoutes),
                ("Older saves clean up exhausted extractors when simulation resumes", LegacyExtractorCleanup),
                ("Smelters reserve shared input exactly once", SharedSmeltingInputs),
                ("Demolition returns reserved input and preserves active delivery routes", Demolition),
                ("Mine-to-smelter chain conserves ore across stock, jobs and shipments", ProductionConservation),
                ("Save and load resume an active delivery", DeliverySaveContinuity),
                ("Save and load resume reserved input and partial jobs", JobSaveContinuity),
                ("Small and large timesteps produce the same world", TimestepConsistency),
                ("Invalid save payloads fail before simulation starts", InvalidSaves),
                ("Invalid tick durations cannot alter the world", InvalidTicks)
            };

            foreach (var check in checks)
            {
                try
                {
                    check.Run();
                    _passed++;
                    Console.WriteLine("PASS  " + check.Name);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("FAIL  " + check.Name + "\n" + exception);
                    return 1;
                }
            }
            Console.WriteLine("\n" + _passed + " simulation checks passed against the Unity project's actual Core source.");
            return 0;
        }

        private static void DeterministicIsland()
        {
            Simulation first = Simulation.Create(417);
            Simulation same = Simulation.Create(417);
            Simulation different = Simulation.Create(418);
            Equal(Save(first.State), Save(same.State), "Same seed produced a different payload.");
            Check(!first.State.Tiles.Select(t => (t.Terrain, t.Elevation, t.TreesRemaining, t.OreRemaining))
                .SequenceEqual(different.State.Tiles.Select(t => (t.Terrain, t.Elevation, t.TreesRemaining, t.OreRemaining))),
                "Different seeds only changed the seed field.");
            Equal(127, first.State.Tiles.Count, "Radius-6 tile count.");
            Equal(7, first.State.Tiles.Count(t => t.Claimed), "Starter claims.");
            foreach (int seed in new[] { int.MinValue, -1, 0, 1, 417, 1000000, int.MaxValue })
            {
                Simulation sim = Simulation.Create(seed);
                Check(sim.GetTile(Forest).Claimed && sim.GetTile(Forest).TreesRemaining > 0, "Missing starter forest.");
                Check(sim.GetTile(Mine).Claimed && sim.GetTile(Mine).OreRemaining > 0, "Missing starter mine.");
                Equal(TerrainKind.Grass, sim.GetTile(Smelter).Terrain, "Starter smelter ground.");
                Equal(6, new HexCoord(0, 0).Neighbors().Distinct().Count(), "Hex neighbors.");
                Check(new HexCoord(0, 0).Neighbors().All(c => HexCoord.Distance(c, new HexCoord(0, 0)) == 1), "Hex distance.");
            }
        }

        private static void Claims()
        {
            Simulation sim = Simulation.Create();
            string before = Save(sim.State);
            Check(!sim.TryClaim(new HexCoord(4, 0), out _), "Distant claim succeeded.");
            Check(!sim.TryClaim(new HexCoord(999, 999), out _), "Off-map claim succeeded.");
            Check(!sim.TryClaim(Mine, out _), "Duplicate claim succeeded.");
            TileState water = sim.State.Tiles.First(t => t.Terrain == TerrainKind.Water);
            Check(!sim.TryClaim(water.Coord, out _), "Water claim succeeded.");
            Equal(before, Save(sim.State), "Rejected claims changed the world.");
            Check(sim.TryClaim(new HexCoord(2, 0), out _), "Adjacent ore claim failed.");
            Equal(74, sim.State.Stock.Timber, "Claim timber cost.");
            Equal(9, sim.State.Stock.Metal, "Claim metal cost.");
            Check(!sim.TryExcavate(new HexCoord(-2, 0), out _), "An unclaimed resource was mined.");

            WorldState poor = Clone(Simulation.Create().State);
            poor.Stock.Metal = 0;
            Simulation unaffordable = new Simulation(poor);
            Check(!unaffordable.TryClaim(new HexCoord(2, 0), out _), "Unfunded claim succeeded.");
            Equal(80, poor.Stock.Timber, "An unfunded claim spent part of its price.");
        }

        private static void Construction()
        {
            Simulation sim = Simulation.Create();
            string before = Save(sim.State);
            Check(!sim.TryBuild(Forest, BuildingKind.Smelter, out _), "Smelter destroyed a standing forest.");
            Check(!sim.TryBuild(Smelter, BuildingKind.Mine, out _), "Mine was built without ore.");
            Check(!sim.TryBuild(Smelter, BuildingKind.Depot, out _), "Second depot was built.");
            Check(!sim.TryBuild(Smelter, (BuildingKind)99, out _), "Unknown building was accepted.");
            Equal(before, Save(sim.State), "Rejected building changed the world.");
            Check(sim.TryBuild(Forest, BuildingKind.Forestry, out _), "Forestry build failed.");
            Check(sim.TryBuild(Mine, BuildingKind.Mine, out _), "Mine build failed.");
            Check(sim.TryBuild(Smelter, BuildingKind.Smelter, out _), "Smelter build failed.");
            Equal(36, sim.State.Stock.Timber, "First production chain timber cost.");
            Equal(4, sim.State.Stock.Metal, "First production chain metal cost.");
            Check(!sim.TryBuild(Forest, BuildingKind.Forestry, out _), "Duplicate building succeeded.");
            Check(!sim.TryBuildRoad(Forest, out _), "A redundant road was charged under a building.");
            Equal(12, sim.GetTile(Forest).TreesRemaining, "Constructing forestry consumed trees.");
        }

        private static void ManualConservation()
        {
            Simulation sim = Simulation.Create();
            int trees = sim.GetTile(Forest).TreesRemaining;
            for (int i = 0; i < trees; i++) Check(sim.TryHarvest(Forest, out _), "Manual forest depletion stopped early.");
            Equal(80 + trees * 3, sim.State.Stock.Timber, "Manual timber total.");
            Equal(TerrainKind.Grass, sim.GetTile(Forest).Terrain, "Cleared forest terrain.");
            Check(!sim.GetTile(Forest).HasRoad, "Hand gathering on bare land created a road.");
            string cleared = Save(sim.State);
            Check(!sim.TryHarvest(Forest, out _), "Depleted forest yielded timber.");
            Equal(cleared, Save(sim.State), "Rejected harvest changed state.");

            int ore = sim.GetTile(Mine).InitialOre;
            for (int i = 0; i < ore; i++) Check(sim.TryExcavate(Mine, out _), "Hand mining stopped early.");
            Equal(ore, sim.State.Stock.Ore, "Manual ore total.");
            Equal(3, sim.GetTile(Mine).ExcavationDepth, "Quarry did not reach its final stage.");
            Equal(TerrainKind.Rock, sim.GetTile(Mine).Terrain, "Depleted ore terrain.");
            Check(!sim.GetTile(Mine).HasRoad, "Hand mining on bare land created a road.");
            Check(!sim.TryExcavate(Mine, out _), "Depleted ore was duplicated.");
            new Simulation(Clone(sim.State));
        }

        private static void NetworkAndDeliveries()
        {
            Simulation sim = Simulation.Create();
            HexCoord source = new HexCoord(2, 0);
            Check(sim.TryClaim(source, out _), "Source claim failed.");
            Check(sim.TryBuild(source, BuildingKind.Mine, out _), "Disconnected mine build failed.");
            Check(sim.GetPathToDepot(source) == null, "Empty owned land counted as a road.");
            sim.Tick(60);
            Equal(48, sim.GetTile(source).OreRemaining, "Disconnected mine extracted ore.");
            Equal(0, sim.State.Shipments.Count, "Disconnected mine sent output.");
            Check(sim.GetStatus(source).Contains("connect"), "Disconnected status has no actionable reason.");

            Check(sim.TryBuildRoad(Mine, out _), "Connecting road failed.");
            Equal(3, sim.GetPathToDepot(source).Count, "Connected path length.");
            sim.Tick(5);
            Equal(0, sim.State.Stock.Ore, "Output entered stock before delivery.");
            Equal(1, sim.State.Shipments.Count, "Expected one mine output shipment.");
            Equal(46, sim.GetTile(source).OreRemaining, "Output did not consume the deposit exactly once.");
            sim.Tick(1.375f);
            Equal(0, sim.State.Stock.Ore, "Delivery arrived before route travel finished.");
            Equal(0f, sim.GetTile(source).WorkProgress, "Building produced during its own delivery.");
            Check(!sim.TryDemolish(Mine, out _), "An active delivery road was removed.");
            sim.Tick(0.125f);
            Equal(2, sim.State.Stock.Ore, "Delivery did not arrive.");
            Check(sim.TryDemolish(Mine, out _), "Idle road removal failed.");
            sim.Tick(30);
            Equal(46, sim.GetTile(source).OreRemaining, "Mine kept producing after its road was removed.");
        }

        private static void LastOreBatch()
        {
            Simulation sim = Simulation.Create();
            Check(sim.TryBuild(Mine, BuildingKind.Mine, out _), "Mine build failed.");
            for (int i = 0; i < 35; i++) Check(sim.TryExcavate(Mine, out _), "Hand mining failed.");
            sim.Tick(5);
            Equal(1, sim.State.Shipments.Single().Amount, "The last one ore became a full two-ore batch.");
            Equal(35, sim.State.Stock.Ore, "Output arrived early.");
            Check(!sim.TryExcavate(Mine, out _), "Manual action duplicated the in-flight final ore.");
            sim.Tick(100);
            Equal(36, sim.State.Stock.Ore, "Final ore conservation.");
            Equal(0, sim.State.Shipments.Count, "A depleted mine kept shipping.");
        }

        private static void AutomatedDepletion()
        {
            Simulation sim = Simulation.Create();
            Check(sim.TryBuild(Forest, BuildingKind.Forestry, out _), "Forestry build failed.");
            Check(sim.TryBuild(Mine, BuildingKind.Mine, out _), "Mine build failed.");
            int startTimber = sim.State.Stock.Timber;
            sim.Tick(200);
            Equal(startTimber + 36, sim.State.Stock.Timber, "Automated timber yield.");
            Equal(36, sim.State.Stock.Ore, "Automated ore yield.");
            Equal(0, sim.State.Shipments.Count, "Depleted extraction retained shipments.");
            Equal(0, sim.GetTile(Forest).TreesRemaining, "Forest not depleted.");
            Equal(0, sim.GetTile(Mine).OreRemaining, "Mine not depleted.");
            Equal(3, sim.GetTile(Mine).ExcavationDepth, "Automatic quarry stages.");
            foreach (HexCoord coord in new[] { Forest, Mine })
            {
                Equal(BuildingKind.None, sim.GetTile(coord).Building, "Exhausted building remained.");
                Check(sim.GetTile(coord).HasRoad, "The extractor's access road was lost.");
            }
            new Simulation(Clone(sim.State));
        }

        private static void FinalDeliveryCleanup()
        {
            foreach (BuildingKind kind in new[] { BuildingKind.Forestry, BuildingKind.Mine })
            {
                Simulation sim = Simulation.Create();
                HexCoord coord = kind == BuildingKind.Forestry ? Forest : Mine;
                Check(sim.TryBuild(coord, kind, out _), "Extractor build failed.");
                GatherToRemaining(sim, coord, 1);
                sim.Tick(kind == BuildingKind.Forestry ? Simulation.ForestrySeconds : Simulation.MineSeconds);
                Equal(kind, sim.GetTile(coord).Building, "Final cargo lost its source building.");
                Check(sim.GetStatus(coord).Contains("Final delivery"), "Final delivery status was missing.");
                int timber = sim.State.Stock.Timber;
                int ore = sim.State.Stock.Ore;
                int metal = sim.State.Stock.Metal;
                Simulation loaded = new Simulation(Clone(sim.State));
                sim.Tick(0.625f);
                Equal(kind, sim.GetTile(coord).Building, "Extractor cleared before cargo arrived.");
                sim.Tick(0.125f);
                loaded.Tick(0.75f);
                Equal(Save(sim.State), Save(loaded.State), "Final delivery cleanup diverged after loading.");
                Equal(BuildingKind.None, sim.GetTile(coord).Building, "Delivered extractor remained.");
                Check(sim.GetTile(coord).HasRoad, "Cleanup lost the access road.");
                Equal(0, sim.State.Shipments.Count, "Final shipment remained after arrival.");
                sim.Tick(100);
                Equal(timber + (kind == BuildingKind.Forestry ? 3 : 0), sim.State.Stock.Timber, "Final timber yield or construction refund.");
                Equal(ore + (kind == BuildingKind.Mine ? 1 : 0), sim.State.Stock.Ore, "Final ore yield.");
                Equal(metal, sim.State.Stock.Metal, "Cleanup refunded construction metal.");
                Check(sim.TryBuild(coord, BuildingKind.Smelter, out _), "Cleared site could not be reused.");
                new Simulation(Clone(sim.State));
            }
        }

        private static void ManualExtractorCleanup()
        {
            foreach (BuildingKind kind in new[] { BuildingKind.Forestry, BuildingKind.Mine })
            foreach (bool delivering in new[] { false, true })
            foreach (bool connected in new[] { false, true })
            {
                if (delivering && !connected) continue;
                Simulation sim = Simulation.Create();
                HexCoord coord = new HexCoord((kind == BuildingKind.Forestry ? -1 : 1) * (connected ? 1 : 2), 0);
                if (!connected) Check(sim.TryClaim(coord, out _), "Frontier claim failed.");
                Check(sim.TryBuild(coord, kind, out _), "Extractor build failed.");
                int timber = sim.State.Stock.Timber;
                int ore = sim.State.Stock.Ore;
                int trees = sim.GetTile(coord).TreesRemaining;
                int deposit = sim.GetTile(coord).OreRemaining;
                sim.Tick(delivering ? (kind == BuildingKind.Forestry ? Simulation.ForestrySeconds : Simulation.MineSeconds) : 0.5f);
                GatherToRemaining(sim, coord, 0);
                Equal(delivering ? kind : BuildingKind.None, sim.GetTile(coord).Building, "Manual depletion cleanup timing.");
                Equal(0f, sim.GetTile(coord).WorkProgress, "Depleted extractor kept partial work.");
                new Simulation(Clone(sim.State));
                sim.Tick(1);
                Equal(BuildingKind.None, sim.GetTile(coord).Building, "Manually exhausted extractor remained.");
                Check(sim.GetTile(coord).HasRoad, "Manual cleanup lost the access road.");
                Equal(timber + trees * 3, sim.State.Stock.Timber, "Manual and automatic timber were lost or duplicated.");
                Equal(ore + deposit, sim.State.Stock.Ore, "Manual and automatic ore were lost or duplicated.");
                new Simulation(Clone(sim.State));
            }
        }

        private static void CleanupPreservesRoutes()
        {
            Simulation sim = Simulation.Create();
            HexCoord frontier = new HexCoord(2, 0);
            Check(sim.TryBuild(Mine, BuildingKind.Mine, out _), "Near mine build failed.");
            GatherToRemaining(sim, Mine, 1);
            int initialOre = sim.State.Stock.Ore;
            Check(sim.TryClaim(frontier, out _) && sim.TryBuild(frontier, BuildingKind.Mine, out _), "Frontier mine failed.");
            sim.Tick(5.75f);
            Equal(BuildingKind.None, sim.GetTile(Mine).Building, "Exhausted transit building remained.");
            Check(sim.State.Shipments.Single().Path.Contains(Mine), "Expected delivery through the cleared site.");
            Equal(3, sim.GetPathToDepot(frontier).Count, "Cleanup disconnected the frontier.");
            Simulation loaded = new Simulation(Clone(sim.State));
            sim.Tick(7.25f);
            loaded.Tick(7.25f);
            Equal(Save(sim.State), Save(loaded.State), "A shipment across the cleared site failed after loading.");
            Equal(initialOre + 5, sim.State.Stock.Ore, "Frontier deliveries stopped or duplicated after cleanup.");
        }

        private static void LegacyExtractorCleanup()
        {
            Simulation sim = Simulation.Create();
            GatherToRemaining(sim, Forest, 0);
            GatherToRemaining(sim, Mine, 0);
            // Version-1 saves allowed depleted, idle buildings without explicit roads.
            sim.GetTile(Forest).Building = BuildingKind.Forestry;
            sim.GetTile(Mine).Building = BuildingKind.Mine;
            Simulation loaded = new Simulation(Clone(sim.State));
            int timber = loaded.State.Stock.Timber;
            int ore = loaded.State.Stock.Ore;
            loaded.Tick(0.125f);
            foreach (HexCoord coord in new[] { Forest, Mine })
            {
                Equal(BuildingKind.None, loaded.GetTile(coord).Building, "Legacy exhausted building remained.");
                Check(loaded.GetTile(coord).HasRoad, "Legacy cleanup lost its network tile.");
            }
            Equal(timber, loaded.State.Stock.Timber, "Legacy cleanup awarded timber.");
            Equal(ore, loaded.State.Stock.Ore, "Legacy cleanup awarded ore.");
            Equal(BuildingKind.Depot, loaded.GetTile(new HexCoord(0, 0)).Building, "Cleanup removed the depot.");
            new Simulation(Clone(loaded.State));
        }

        private static void GatherToRemaining(Simulation sim, HexCoord coord, int remaining)
        {
            TileState tile = sim.GetTile(coord);
            while (tile.TreesRemaining > remaining) Check(sim.TryHarvest(coord, out _), "Hand gathering failed.");
            while (tile.OreRemaining > remaining) Check(sim.TryExcavate(coord, out _), "Hand mining failed.");
        }

        private static void SharedSmeltingInputs()
        {
            Simulation sim = Simulation.Create();
            Check(sim.TryExcavate(Mine, out _) && sim.TryExcavate(Mine, out _), "Starter ore failed.");
            Check(sim.TryBuild(Smelter, BuildingKind.Smelter, out _), "First smelter build failed.");
            Check(sim.TryBuild(new HexCoord(0, -1), BuildingKind.Smelter, out _), "Second smelter build failed.");
            int startMetal = sim.State.Stock.Metal;
            sim.Tick(0.125f);
            Equal(0, sim.State.Stock.Ore, "Smelter did not reserve its input.");
            Equal(2, sim.State.Tiles.Sum(t => t.InputOreReserved), "Two smelters reserved the same ore.");
            sim.Tick(20);
            Equal(startMetal + 1, sim.State.Stock.Metal, "Two ore did not produce exactly one metal.");
            Equal(0, sim.State.Tiles.Sum(t => t.InputOreReserved), "Consumed input remained reserved.");
            Check(sim.State.Tiles.Where(t => t.Building == BuildingKind.Smelter).All(t => t.WorkProgress == 0), "Unfunded smelting continued.");
        }

        private static void Demolition()
        {
            Simulation sim = Simulation.Create();
            Check(!sim.TryDemolish(new HexCoord(0, 0), out _), "The depot was removable.");
            Check(sim.TryExcavate(Mine, out _) && sim.TryExcavate(Mine, out _), "Starter ore failed.");
            Check(sim.TryBuild(Smelter, BuildingKind.Smelter, out _), "Smelter build failed.");
            int timber = sim.State.Stock.Timber;
            int metal = sim.State.Stock.Metal;
            sim.Tick(2);
            Equal(0, sim.State.Stock.Ore, "Input was not reserved.");
            Check(sim.TryDemolish(Smelter, out _), "Partial job could not be demolished.");
            Equal(2, sim.State.Stock.Ore, "Unprocessed ore was not returned.");
            Equal(timber, sim.State.Stock.Timber, "Demolition refunded construction timber.");
            Equal(metal, sim.State.Stock.Metal, "Demolition refunded construction metal.");
            Check(!sim.TryDemolish(Smelter, out _), "Repeated demolition produced resources.");

            Check(sim.TryBuild(Mine, BuildingKind.Mine, out _), "Mine build failed.");
            sim.Tick(5);
            Check(!sim.TryDemolish(Mine, out _), "A source with active output was demolished.");
            sim.Tick(0.75f);
            Check(sim.TryDemolish(Mine, out _), "Idle source could not be demolished.");
            new Simulation(Clone(sim.State));
        }

        private static void ProductionConservation()
        {
            Simulation sim = Simulation.Create();
            Check(sim.TryBuild(Mine, BuildingKind.Mine, out _), "Mine build failed.");
            Check(sim.TryBuild(Smelter, BuildingKind.Smelter, out _), "Smelter build failed.");
            int initialMetal = sim.State.Stock.Metal;
            int originalOre = sim.GetTile(Mine).InitialOre;
            for (int i = 0; i < 500; i++)
            {
                sim.Tick(0.375f);
                int oreInFlight = sim.State.Shipments.Where(s => s.Resource == ResourceKind.Ore).Sum(s => s.Amount);
                int metalInFlight = sim.State.Shipments.Where(s => s.Resource == ResourceKind.Metal).Sum(s => s.Amount);
                int reserved = sim.State.Tiles.Sum(t => t.InputOreReserved);
                int equivalentOre = sim.GetTile(Mine).OreRemaining + sim.State.Stock.Ore + oreInFlight + reserved
                    + 2 * (sim.State.Stock.Metal - initialMetal + metalInFlight);
                Equal(originalOre, equivalentOre, "Ore was lost or duplicated between world, inputs and output.");
            }
            Equal(initialMetal + 18, sim.State.Stock.Metal, "36 ore should eventually yield 18 metal.");
            Equal(BuildingKind.Smelter, sim.GetTile(Smelter).Building, "An idle smelter was removed.");
        }

        private static void DeliverySaveContinuity()
        {
            Simulation original = Simulation.Create();
            Check(original.TryBuild(Mine, BuildingKind.Mine, out _), "Mine build failed.");
            original.Tick(5.31f);
            Check(original.State.Shipments.Count == 1 && original.State.Shipments[0].Elapsed > 0, "Save scenario has no active delivery.");
            Simulation loaded = new Simulation(Clone(original.State));
            original.Tick(0.5f);
            loaded.Tick(0.5f);
            Equal(Save(original.State), Save(loaded.State), "Delivery changed across serialization.");
            Equal(2, loaded.State.Stock.Ore, "Loaded output did not arrive exactly once.");
            original.Tick(19.31f);
            loaded.Tick(19.31f);
            Equal(Save(original.State), Save(loaded.State), "Future output diverged after load.");
        }

        private static void JobSaveContinuity()
        {
            Simulation original = Simulation.Create();
            Check(original.TryExcavate(Mine, out _) && original.TryExcavate(Mine, out _), "Starter ore failed.");
            Check(original.TryBuild(Smelter, BuildingKind.Smelter, out _), "Smelter build failed.");
            original.Tick(2.375f);
            Equal(2, original.GetTile(Smelter).InputOreReserved, "Scenario lacks reserved input.");
            Simulation loaded = new Simulation(Clone(original.State));
            original.Tick(4.375f);
            loaded.Tick(4.375f);
            Equal(Save(original.State), Save(loaded.State), "Partial job diverged on load.");
            Equal(7, loaded.State.Stock.Metal, "Loaded job output was lost or duplicated.");
            Equal(0, loaded.State.Stock.Ore, "Reserved input was refunded on load.");
        }

        private static void TimestepConsistency()
        {
            Simulation whole = Simulation.Create();
            Check(whole.TryBuild(Forest, BuildingKind.Forestry, out _), "Forestry build failed.");
            Check(whole.TryBuild(Mine, BuildingKind.Mine, out _), "Mine build failed.");
            Check(whole.TryBuild(Smelter, BuildingKind.Smelter, out _), "Smelter build failed.");
            Simulation tiny = new Simulation(Clone(whole.State));
            Simulation uneven = new Simulation(Clone(whole.State));
            whole.Tick(60);
            for (int i = 0; i < 6000; i++) tiny.Tick(0.01f);
            for (int i = 0; i < 120; i++)
            {
                uneven.Tick(0.125f);
                uneven.Tick(0.375f);
            }
            // The integral simulation, jobs and arrivals must match. Floating input
            // partitioning may leave a sub-microsecond remainder, which is not gameplay.
            Check(Math.Abs(tiny.State.PendingSeconds - whole.State.PendingSeconds) < 0.00001, "Clock remainder drift is excessive.");
            tiny.State.PendingSeconds = whole.State.PendingSeconds;
            Equal(Save(whole.State), Save(tiny.State), "Tiny frame updates produced different gameplay.");
            Equal(Save(whole.State), Save(uneven.State), "Uneven frame updates produced different gameplay.");
        }

        private static void InvalidSaves()
        {
            Throws<ArgumentException>(() => new Simulation(null), "Null save.");
            Reject(s => s.SchemaVersion = 99, "Future schema.");
            Reject(s => s.Stock = null, "Missing stock.");
            Reject(s => s.Stock.Ore = -1, "Negative stock.");
            Reject(s => s.Tiles.RemoveAt(0), "Missing tile.");
            Reject(s => s.Tiles[1].Coord = s.Tiles[0].Coord, "Duplicate coordinate.");
            Reject(s => s.Tiles[0].Coord = new HexCoord(int.MaxValue, int.MinValue), "Overflow coordinate.");
            Reject(s => s.Tiles.First(t => t.Terrain == TerrainKind.Water).Claimed = true, "Claimed water.");
            Reject(s => s.Tiles.First(t => HexCoord.Distance(t.Coord, new HexCoord(0, 0)) == 4 && t.Terrain != TerrainKind.Water).Claimed = true, "Isolated claim.");
            Reject(s => s.Tiles.First(t => t.Coord == Forest).TreesRemaining = -2, "Negative trees.");
            Reject(s => s.Tiles.First(t => t.Coord == Mine).ExcavationDepth = 3, "Depth does not match ore.");
            Reject(s => s.Tiles.First(t => t.Coord == Smelter).Building = (BuildingKind)123, "Unknown building.");
            Reject(s => s.Tiles.First(t => t.Coord == Smelter).WorkProgress = float.NaN, "NaN progress.");
            Reject(s => s.Tiles.First(t => t.Coord == Smelter).InputOreReserved = 2, "Reservation without a smelter.");
            Reject(s => s.ElapsedSeconds = 100, "Inconsistent clock.");
            Reject(s => s.PendingSeconds = double.PositiveInfinity, "Infinite clock.");

            Simulation active = Simulation.Create();
            Check(active.TryBuild(Mine, BuildingKind.Mine, out _), "Mine build failed.");
            active.Tick(5.25f);
            WorldState invalid = Clone(active.State);
            invalid.Shipments.Add(Clone(active.State).Shipments[0]);
            Throws<ArgumentException>(() => new Simulation(invalid), "Duplicate source shipment.");
            invalid = Clone(active.State);
            invalid.Shipments[0].Path[1] = new HexCoord(5, 0);
            Throws<ArgumentException>(() => new Simulation(invalid), "Broken delivery path.");
            invalid = Clone(active.State);
            invalid.Shipments[0].Resource = ResourceKind.Metal;
            Throws<ArgumentException>(() => new Simulation(invalid), "Mine shipping metal.");
            invalid = Clone(active.State);
            invalid.Shipments[0].Elapsed = invalid.Shipments[0].Duration;
            Throws<ArgumentException>(() => new Simulation(invalid), "Already delivered shipment.");
            invalid = Clone(active.State);
            invalid.Stock.Ore = int.MaxValue;
            Throws<ArgumentException>(() => new Simulation(invalid), "Incoming goods overflow stock.");
        }

        private static void InvalidTicks()
        {
            Simulation sim = Simulation.Create();
            string before = Save(sim.State);
            foreach (float seconds in new[] { -1f, float.NaN, float.PositiveInfinity, 3601f })
                Throws<ArgumentOutOfRangeException>(() => sim.Tick(seconds), "Invalid tick duration.");
            sim.Tick(0);
            Equal(before, Save(sim.State), "Invalid or zero tick changed the world.");
        }

        private static void Reject(Action<WorldState> corrupt, string reason)
        {
            WorldState state = Clone(Simulation.Create().State);
            corrupt(state);
            Throws<ArgumentException>(() => new Simulation(state), reason);
        }

        private static string Save(WorldState state) => JsonSerializer.Serialize(state, JsonOptions);
        private static WorldState Clone(WorldState state) => JsonSerializer.Deserialize<WorldState>(Save(state), JsonOptions);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + " Expected " + expected + ", actual " + actual + ".");
        }
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(message + " Expected " + typeof(T).Name + ".");
        }
    }
}
