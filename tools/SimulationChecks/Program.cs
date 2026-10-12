using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AlphaHex.Core;

namespace AlphaHex.SimulationChecks
{
    internal static partial class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };
        private static readonly HexCoord Forest = new HexCoord(-1, 0), Mine = new HexCoord(1, 0), Smelter = new HexCoord(1, -1), Home = new HexCoord(0, 1), Clay = new HexCoord(0, -2);
        private static int Main()
        {
            var tests = new (string, Action)[] {
                ("Seeded discovery and starter resources", Generation), ("Rejected commands are atomic; claims need no metal", Claims),
                ("New construction prerequisites and costs", Construction), ("Manual recovery conserves finite deposits", ManualConservation),
                ("Disconnected production, jobs and delivery latency", Network), ("Final extraction survives saving and clears after delivery", FinalDelivery),
                ("Shared processor inputs are reserved exactly once", Reservations), ("Manual batches require crews and explicit requests", Batches),
                ("Demolition refunds inputs once and protects routes", Demolition), ("All resource counters include cargo and reservations", Capacity),
                ("Fixed timesteps and partial clocks survive saves", Continuity), ("Malformed states are rejected", InvalidStates),
                ("Manual crews, gradual migration and hunger recovery", TownGrowth), ("Research payments and prerequisites", Research),
                ("Renewable woodland and finite deep reserves", Renewal), ("Permanent exploration and tower reinforcement", Exploration),
                ("Unity schemas 1 and 2 migrate without editing their source", UnityMigration), ("Godot jobs, crews, discoveries and cargo migrate", GodotMigration),
                ("Corrupt legacy records do not become valid during migration", InvalidMigration), ("Guidance is current and read-only", Guidance),
                ("Zero-metal start reaches an efficient foundry using commands", Playthrough)
            };
            int failed = 0;
            foreach (var test in tests) try { test.Item2(); Console.WriteLine("PASS " + test.Item1); }
                catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + test.Item1 + "\n" + error); }
            Console.WriteLine($"{tests.Length - failed}/{tests.Length} checks passed against production Core."); return failed == 0 ? 0 : 1;
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Equal<T>(T expected, T actual, string message) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception(message + $" Expected {expected}, got {actual}."); }
        private static void Throws<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { return; } throw new Exception(message); }
        private static string Save(WorldState state) => JsonSerializer.Serialize(state, JsonOptions);
        private static WorldState Clone(WorldState state) => JsonSerializer.Deserialize<WorldState>(Save(state), JsonOptions);
        private static Simulation Reload(Simulation sim) => new Simulation(Clone(sim.State));
        // Deliberately provisioned fixtures isolate accounting from pacing. Playthrough uses no provisioning.
        private static Simulation Ready()
        {
            var state = Simulation.Create().State;
            state.Stock = new ResourceStock(10000, 100, 100, 10000, 100, 1000, 1000);
            state.Town.Research = (ResearchKind)1023;
            return new Simulation(state);
        }
        private static void Build(Simulation sim, HexCoord coord, BuildingKind kind, int crew = 1)
        {
            Check(sim.TryBuild(coord, kind, out string why), why);
            if (Simulation.NeedsWorker(kind)) Check(sim.TryAssignWorkers(coord, crew, out why), why);
        }
        private static void Claim(Simulation sim, HexCoord coord) { Check(sim.TryClaim(coord, out string why), why); }
        private static void Road(Simulation sim, HexCoord coord) { Check(sim.TryBuildRoad(coord, out string why), why); }
        private static void Generation()
        {
            Equal(Save(Simulation.Create(417).State), Save(Simulation.Create(417).State), "Seed drift.");
            Check(Save(Simulation.Create(417).State) != Save(Simulation.Create(418).State), "Seed ignored.");
            foreach (int seed in new[] { int.MinValue, -1, 0, 1, 417, int.MaxValue })
            {
                var sim = Simulation.Create(seed); Equal(37, sim.State.Tiles.Count, "Starting reveal."); Equal(7, sim.State.Tiles.Count(t => t.Claimed), "Claims.");
                Check(sim.GetTile(Forest).TreesRemaining > 0 && sim.GetTile(Mine).OreRemaining > 0 && sim.GetTile(Clay).ClayRemaining > 0, "Starter resources.");
                Equal(0, sim.State.Stock.Metal, "Metal-free start."); Reload(sim);
            }
        }
        private static void Claims()
        {
            var sim = Simulation.Create(); var before = Save(sim.State);
            Check(!sim.TryClaim(new HexCoord(4, 0), out _), "Unseen claim."); Check(!sim.TryClaim(Mine, out _), "Duplicate claim.");
            Check(!sim.TryClaim(new HexCoord(3, 0), out _), "Non-adjacent claim."); Equal(before, Save(sim.State), "Failed command mutated state.");
            Claim(sim, Clay); Equal(74, sim.State.Stock.Timber, "Claim cost."); Equal(0, sim.State.Stock.Metal, "Claim spent metal.");
            sim.State.Stock.Timber = 5; before = Save(sim.State); Check(!sim.TryClaim(new HexCoord(2, 0), out _), "Unaffordable claim."); Equal(before, Save(sim.State), "Partial spending.");
        }
        private static void Construction()
        {
            var sim = Simulation.Create();
            foreach (var kind in new[] { BuildingKind.Mine, BuildingKind.Smelter, BuildingKind.Kiln, BuildingKind.Bloomery, BuildingKind.Quarry, BuildingKind.Watchtower })
                Check(!sim.TryBuild(Smelter, kind, out _), "Missing research allowed: " + kind);
            var state = Save(sim.State); Check(!sim.TryBuild(Forest, BuildingKind.House, out _), "House cleared trees."); Equal(state, Save(sim.State), "Rejected build consumed goods.");
            sim = Ready(); var cost = Simulation.GetBuildCost(BuildingKind.Smelter); int wood = sim.State.Stock.Timber, brick = sim.State.Stock.Brick;
            Build(sim, Smelter, BuildingKind.Smelter); Equal(wood - cost.Timber, sim.State.Stock.Timber, "Foundry wood."); Equal(brick - cost.Brick, sim.State.Stock.Brick, "Foundry bricks.");
            Check(!sim.TryBuild(Smelter, BuildingKind.Smelter, out _), "Duplicate building."); Reload(sim);
        }
        private static void ManualConservation()
        {
            var sim = Simulation.Create(); Claim(sim, Clay);
            int timber = sim.State.Stock.Timber, trees = sim.GetTile(Forest).TreesRemaining, ore = sim.GetTile(Mine).InitialOre, clay = sim.GetTile(Clay).InitialClay;
            while (sim.TryHarvest(Forest, out _)) { } while (sim.TryExcavate(Mine, out _)) { } while (sim.TryGatherClay(Clay, out _)) { }
            Equal(timber + trees * 3, sim.State.Stock.Timber, "Timber duplicated."); Equal(ore, sim.State.Stock.Ore, "Ore duplicated."); Equal(clay, sim.State.Stock.Clay, "Clay duplicated.");
            Equal(3, sim.GetTile(Mine).ExcavationDepth, "Quarry stages."); Reload(sim);
        }
        private static void Network()
        {
            var sim = Ready(); var remote = new HexCoord(2, 0); Claim(sim, remote); Build(sim, remote, BuildingKind.Mine);
            int initial = sim.GetTile(remote).OreRemaining, stock = sim.State.Stock.Ore; sim.Tick(30); Equal(initial, sim.GetTile(remote).OreRemaining, "Disconnected work.");
            Road(sim, Mine); sim.Tick(15); Equal(initial - 2, sim.GetTile(remote).OreRemaining, "Extraction."); Equal(stock, sim.State.Stock.Ore, "Arrival was immediate.");
            sim.Tick(2.375f); Equal(0f, sim.GetTile(remote).WorkProgress, "Producer worked during delivery."); sim.Tick(.125f); Equal(stock + 2, sim.State.Stock.Ore, "Delivery.");
            sim.Tick(.125f); Check(sim.GetTile(remote).WorkProgress > 0, "Next job did not start.");
        }
        private static void FinalDelivery()
        {
            foreach (var kind in new[] { BuildingKind.Forestry, BuildingKind.Mine, BuildingKind.ClayPit })
            {
                var sim = Ready(); HexCoord coord = kind == BuildingKind.Forestry ? Forest : kind == BuildingKind.Mine ? Mine : Clay;
                if (coord == Clay) { Claim(sim, Clay); Road(sim, new HexCoord(0, -1)); }
                Build(sim, coord, kind);
                var tile = sim.GetTile(coord);
                while (tile.TreesRemaining > 1) Check(sim.TryHarvest(coord, out _), "Hand timber.");
                while (tile.OreRemaining > 1) Check(sim.TryExcavate(coord, out _), "Hand ore.");
                while (tile.ClayRemaining > 1) Check(sim.TryGatherClay(coord, out _), "Hand clay.");
                sim.Tick(Simulation.JobSeconds(kind)); Check(sim.State.Shipments.Count == 1, "Final output missing.");
                Check(!sim.TryDemolish(coord, out _), "Removed moving source."); var loaded = Reload(sim);
                float duration = loaded.State.Shipments[0].Duration; sim.Tick(duration); loaded.Tick(duration);
                Equal(Save(sim.State), Save(loaded.State), "Final cargo save continuity."); Equal(BuildingKind.None, loaded.GetTile(coord).Building, "Exhausted building remains.");
                Check(loaded.GetTile(coord).HasRoad && loaded.GetTile(coord).Workers == 0, "Cleanup road/crew."); Reload(loaded);
            }
        }
        private static void Reservations()
        {
            var sim = Ready(); Build(sim, Smelter, BuildingKind.Smelter); Build(sim, Home, BuildingKind.Smelter);
            sim.State.Stock.Ore = 2; sim.State.Stock.Timber = 1; sim.Tick(.125f);
            Equal(2, sim.GetReservedOre(), "Shared ore spent twice."); Equal(1, sim.State.Tiles.Sum(t => t.InputTimberReserved), "Fuel spent twice.");
            Equal(1, sim.State.Tiles.Count(t => t.WorkProgress > 0), "Two unfunded jobs.");
            var loaded = Reload(sim); loaded.Tick(20); Equal(101 - 8, loaded.State.Stock.Metal, "Metal output.");
            sim = Ready(); Build(sim, Smelter, BuildingKind.Kiln); sim.State.Stock.Timber = 2; sim.State.Stock.Clay = 4;
            sim.Tick(1); Equal(2, sim.GetTile(Smelter).InputTimberReserved, "Kiln fuel."); Equal(4, sim.GetTile(Smelter).InputClayReserved, "Kiln clay."); Reload(sim);
        }
        private static void Batches()
        {
            var sim = Ready(); Build(sim, Smelter, BuildingKind.Bloomery); int metal = sim.State.Stock.Metal;
            sim.Tick(100); Equal(metal, sim.State.Stock.Metal, "Bloomery ran without request.");
            Check(sim.TryStartBatch(Smelter, out _), "Batch rejected."); Check(!sim.TryStartBatch(Smelter, out _), "Duplicate batch.");
            sim.Tick(30); Check(sim.TryToggleWork(Smelter, out _), "Pause."); float work = sim.GetTile(Smelter).WorkProgress; sim.Tick(30); Equal(work, sim.GetTile(Smelter).WorkProgress, "Paused work.");
            sim = Reload(sim); Check(sim.TryAssignWorkers(Smelter, 1, out _), "Resume crew."); sim.Tick(61.25f); Equal(metal + 1, sim.State.Stock.Metal, "Batch output.");
            sim.Tick(200); Equal(metal + 1, sim.State.Stock.Metal, "Batch repeated automatically."); Check(!sim.GetTile(Smelter).BatchRequested, "Request not cleared.");
        }
        private static void Demolition()
        {
            var sim = Ready(); Build(sim, Smelter, BuildingKind.Bloomery); int ore = sim.State.Stock.Ore, timber = sim.State.Stock.Timber;
            Check(sim.TryStartBatch(Smelter, out _), "Batch."); sim.Tick(10); Check(sim.TryDemolish(Smelter, out _), "Demolition.");
            Equal(ore, sim.State.Stock.Ore, "Ore refund."); Equal(timber, sim.State.Stock.Timber, "Fuel refund."); Check(!sim.TryDemolish(Smelter, out _), "Double refund.");
            Check(!sim.TryDemolish(new HexCoord(0, 0), out _), "Hall removed.");
            Build(sim, Mine, BuildingKind.Mine); sim.Tick(15); Check(!sim.TryDemolish(Mine, out _), "Delivery source removed."); Reload(sim);
        }
        private static void Capacity()
        {
            foreach (var resource in Simulation.Resources)
            {
                var state = Clone(Ready().State); typeof(ResourceStock).GetField(resource.ToString()).SetValue(state.Stock, -1);
                Throws<ArgumentException>(() => new Simulation(state), "Negative stock accepted: " + resource);
            }
            var sim = Ready(); Build(sim, Smelter, BuildingKind.Kiln); sim.Tick(1);
            sim.State.Stock.Clay = int.MaxValue; Throws<ArgumentException>(() => Reload(sim), "Reserved clay overflow.");
            sim = Ready(); sim.State.Stock.Timber = int.MaxValue; Check(!sim.TryHarvest(Forest, out _), "Manual overflow.");
            sim = Ready(); Build(sim, Mine, BuildingKind.Mine); sim.Tick(15); sim.State.Stock.Ore = int.MaxValue;
            Throws<ArgumentException>(() => Reload(sim), "Cargo overflow.");
        }
        private static void Continuity()
        {
            var sim = Ready(); Build(sim, Mine, BuildingKind.Mine); Build(sim, Smelter, BuildingKind.Kiln);
            sim.Tick(15.3125f); var loaded = Reload(sim); sim.Tick(120.1875f); loaded.Tick(120.1875f); Equal(Save(sim.State), Save(loaded.State), "Cargo/job/remainder changed after load.");
            var whole = Ready(); Build(whole, Mine, BuildingKind.Mine); var small = Reload(whole);
            whole.Tick(120); for (int i = 0; i < 960; i++) small.Tick(.125f); Equal(Save(whole.State), Save(small.State), "Timestep drift.");
            var before = Save(whole.State); foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, 3601f }) Throws<ArgumentOutOfRangeException>(() => whole.Tick(invalid), "Bad tick.");
            Equal(before, Save(whole.State), "Bad time mutated world.");
        }
        private static void InvalidStates()
        {
            var corruptions = new Action<WorldState>[] { s => s.SchemaVersion = 100, s => s.Tiles[1].Coord = s.Tiles[0].Coord,
                s => s.Tiles.RemoveAt(0), s => s.Town.Population++, s => s.Town.Research = ResearchKind.Refining,
                s => s.Tiles[0].InputClayReserved = 4, s => s.PendingSeconds = .125, s => s.Town.ResearchProgress = 1,
                s => s.Tiles[0].Workers = 1, s => s.Tiles[0].TowerLevel = 1, s => s.SimulationSteps = 1,
                s => s.Tiles[0].WorkProgress = float.NaN, s => s.Tiles[0].BatchRequested = true,
                s => s.Tiles[0].Coord = new HexCoord(int.MaxValue, int.MinValue), s => s.Tiles[0].Claimed = true };
            foreach (var corrupt in corruptions) { var state = Clone(Simulation.Create().State); corrupt(state); Throws<ArgumentException>(() => new Simulation(state), "Corrupt state accepted."); }
        }
        private static void Guidance()
        {
            var sim = Simulation.Create(); string before = Save(sim.State); var goal = SettlementGuide.Next(sim);
            Check(goal.Detail.Contains("8 timber"), "Outdated food cost."); Equal(before, Save(sim.State), "Guide mutated state.");
        }
    }
}
