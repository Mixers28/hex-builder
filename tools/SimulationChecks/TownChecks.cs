using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AlphaHex.Core;

namespace AlphaHex.SimulationChecks
{
    internal static partial class Program
    {
        private static void TownGrowth()
        {
            var sim = Simulation.Create(); Build(sim, Home, BuildingKind.House); Build(sim, new HexCoord(-1, 1), BuildingKind.Forager, 2);
            Equal(0, sim.GetTile(Home).Residents, "House filled immediately."); sim.Tick(89.875f); Equal(4, sim.State.Town.Population, "Early migrant.");
            sim.Tick(.125f); Equal(5, sim.State.Town.Population, "One migrant expected."); Equal(2, sim.AssignedWorkers, "Migrant automatically assigned.");
            Check(!sim.TryAssignWorkers(new HexCoord(-1, 1), 3, out _), "Crew cap ignored."); Check(!sim.TryDemolish(Home, out _), "Occupied house removed.");
            sim = Reload(sim); Check(sim.TryAssignWorkers(new HexCoord(-1, 1), 0, out _), "Crew release."); sim.State.Stock.Food = 0; sim.Tick(90);
            Equal(5, sim.State.Town.Population, "Hungry migrants."); Check(sim.State.Town.LastMealMissed, "Missing shortage.");
            Check(sim.TryForage(Home, out _), "Manual food."); Check(!sim.TryForage(Home, out _), "Forage cooldown.");
            Check(sim.TryAssignWorkers(new HexCoord(-1, 1), 2, out _), "Food crew."); sim.Tick(180); Check(sim.State.Town.Population > 5 && !sim.State.Town.LastMealMissed, "Food recovery."); Reload(sim);
        }
        private static Simulation TenCitizens()
        {
            var sim = Ready(); Build(sim, Home, BuildingKind.House); Build(sim, new HexCoord(0, -1), BuildingKind.House); sim.Tick(540);
            Equal(10, sim.State.Town.Population, "Fixture migration."); return sim;
        }
        private static void Research()
        {
            var sim = TenCitizens(); sim.State.Town.Research = ResearchKind.None; sim = Reload(sim);
            Check(!sim.TryResearch(ResearchKind.PrimitiveSmelting, out _), "Missing brick prerequisite.");
            int timber = sim.State.Stock.Timber, clay = sim.State.Stock.Clay;
            Check(sim.TryResearch(ResearchKind.Brickmaking, out _), "Brick research rejected."); Equal(timber - 20, sim.State.Stock.Timber, "Research timber."); Equal(clay - 8, sim.State.Stock.Clay, "Research clay.");
            Check(!sim.TryResearch(ResearchKind.Farming, out _), "Parallel research."); sim.Tick(70.3125f); var loaded = Reload(sim);
            sim.Tick(110); loaded.Tick(110); Equal(Save(sim.State), Save(loaded.State), "Research save continuity."); Check(sim.HasResearch(ResearchKind.Brickmaking), "Research incomplete.");
            Check(!sim.TryResearch(ResearchKind.Brickmaking, out _), "Repeated research."); Check(sim.TryResearch(ResearchKind.PrimitiveSmelting, out _), "Primitive research.");
            sim.Tick(240); Check(sim.HasResearch(ResearchKind.PrimitiveSmelting), "Primitive incomplete.");
        }
        private static void Renewal()
        {
            var sim = Ready(); Build(sim, Forest, BuildingKind.Forestry); Build(sim, new HexCoord(0, -1), BuildingKind.Forester);
            while (sim.TryHarvest(Forest, out _)) { } sim.Tick(16); Check(sim.GetTile(Forest).SaplingPlanted, "Sapling not planted.");
            var loaded = Reload(sim); sim.Tick(90); loaded.Tick(90); Equal(Save(sim.State), Save(loaded.State), "Sapling load continuity."); Check(sim.GetTile(Forest).TreesRemaining > 0, "No renewal.");
            sim = Ready(); while (sim.TryExcavate(Mine, out _)) { } int deep = sim.GetTile(Mine).DeepOreInitial;
            Build(sim, Mine, BuildingKind.DeepMine); int ore = sim.State.Stock.Ore; sim.Tick(2000);
            Equal(deep, sim.State.Stock.Ore - ore + sim.GetInTransit(ResourceKind.Ore), "Deep finite accounting."); Equal(0, sim.GetTile(Mine).DeepOreRemaining, "Deep reserve not exhausted.");
            Equal(6, sim.GetTile(Mine).ExcavationDepth, "Deep depth."); Reload(sim);
        }
        private static void Exploration()
        {
            var sim = Ready(); var coord = new HexCoord(0, 2); Claim(sim, coord); Build(sim, coord, BuildingKind.Watchtower);
            Equal(37, sim.State.Tiles.Count, "Disconnected tower revealed."); Road(sim, Home); Check(sim.GetTile(new HexCoord(0, 5))?.InitialStone > 0, "Guaranteed stone not revealed.");
            int known = sim.State.Tiles.Count; Check(sim.TryUpgradeTower(coord, out _), "Upgrade failed."); Check(sim.State.Tiles.Count > known, "No upgraded reveal."); known = sim.State.Tiles.Count;
            Check(sim.TryDemolish(coord, out _), "Tower removal."); Equal(known, Reload(sim).State.Tiles.Count, "Discovery lost.");
            Check(sim.State.Tiles.All(t => HexCoord.Distance(t.Coord, new HexCoord()) <= 16), "World limit.");
        }
        private static string Fixture(string name) => File.ReadAllText(Path.Combine("Assets", "AlphaHex", "Tests", "Fixtures", name + ".json"));
        private static void UnityMigration()
        {
            foreach (int version in new[] { 1, 2 })
            {
                var source = JsonSerializer.Deserialize<WorldState>(Fixture("unity-v" + version), JsonOptions); string before = Save(source);
                var sim = Simulation.Restore(source); Equal(before, Save(source), "Source save mutated."); Equal(127, sim.State.Tiles.Count, "Known world lost.");
                Equal(version == 1 ? 4 : 6, sim.State.Town.Population, "Population migration."); Equal(version == 1 ? 24 : 29, sim.State.Stock.Food, "Food migration.");
                Equal(.75f, sim.State.Shipments.Single().Duration, "Old delivery speed changed."); Equal(9f, sim.GetTile(Smelter).WorkProgress, "Foundry completion fraction.");
                Check(sim.GetTile(Smelter).LegacyOreJob && sim.GetTile(Smelter).InputTimberReserved == 0, "Old job charged fuel.");
                Check(sim.HasResearch(ResearchKind.Refining) && sim.State.Tiles.Any(t => t.ClayRemaining > 0), "New chain compatibility.");
                var loaded = Reload(sim); sim.Tick(25); loaded.Tick(25); Equal(Save(sim.State), Save(loaded.State), "Converted save drift.");
                Check(!sim.GetTile(Smelter).LegacyOreJob, "Legacy flag survived completed job.");
            }
        }
        private static void GodotMigration()
        {
            foreach (string name in new[] { "godot-v3", "godot-frontier-v3" })
            {
                string json = Fixture(name); var dto = JsonSerializer.Deserialize<GodotSaveRecord>(json, JsonOptions);
                var sim = Simulation.ImportGodot(dto); Equal((int)dto.stock.timber, sim.State.Stock.Timber, "Godot stock.");
                Equal(dto.tiles.Length, sim.State.Tiles.Count, "Godot discoveries."); Equal(dto.tiles.Sum(t => (int)t.workers), sim.AssignedWorkers, "Godot crews.");
                Equal(dto.shipments.Length, sim.State.Shipments.Count, "Godot cargo."); Equal(dto.clock, sim.State.SimulationSteps * Simulation.FixedStepSeconds, "Godot clock.");
                Equal(dto.remainder, sim.State.PendingSeconds, "Godot remainder."); Equal(dto.tiles.Select(t => ((int)t.q, (int)t.r)).ToArray().Length, sim.State.Tiles.Count, "Order size.");
                for (int i = 0; i < dto.tiles.Length; i++) Equal(new HexCoord((int)dto.tiles[i].q, (int)dto.tiles[i].r), sim.State.Tiles[i].Coord, "Discovery order.");
                var loaded = Reload(sim); sim.Tick(200); loaded.Tick(200); Equal(Save(sim.State), Save(loaded.State), "Godot import save continuity.");
                Check(!sim.GetTile(Smelter).LegacyOreJob, "Imported legacy job never cleared.");
            }
        }
        private static void InvalidMigration()
        {
            var source = JsonSerializer.Deserialize<WorldState>(Fixture("unity-v2"), JsonOptions); source.Town.Population++;
            string before = Save(source); Throws<ArgumentException>(() => Simulation.Restore(source), "Invalid legacy census."); Equal(before, Save(source), "Rejected source mutated.");
            var dto = JsonSerializer.Deserialize<GodotSaveRecord>(Fixture("godot-v3"), JsonOptions); dto.stock.ore = 1.5;
            Throws<ArgumentException>(() => Simulation.ImportGodot(dto), "Fractional resource truncated.");
            dto = JsonSerializer.Deserialize<GodotSaveRecord>(Fixture("godot-v3"), JsonOptions); dto.tiles[0].work = double.NaN;
            Throws<ArgumentException>(() => Simulation.ImportGodot(dto), "Nonfinite legacy job accepted.");
            dto = JsonSerializer.Deserialize<GodotSaveRecord>(Fixture("godot-v3"), JsonOptions); dto.shipments[0].path[0] = "99,99";
            Throws<ArgumentException>(() => Simulation.ImportGodot(dto), "Invalid cargo route accepted.");
        }
        private static void EnsureTimber(Simulation sim, int amount)
        {
            int safety = 0;
            while (sim.State.Stock.Timber < amount)
            {
                if (++safety > 3000) throw new Exception("Timber recovery did not converge.");
                var forest = sim.State.Tiles.FirstOrDefault(t => t.Claimed && t.TreesRemaining > 0);
                if (forest != null) { Check(sim.TryHarvest(forest.Coord, out string why), why); continue; }
                var frontier = sim.State.Tiles.FirstOrDefault(t => !t.Claimed && t.TreesRemaining > 0 && t.Coord.Neighbors().Any(c => sim.GetTile(c)?.Claimed == true));
                if (frontier != null && sim.State.Stock.Timber >= 6) { Claim(sim, frontier.Coord); continue; }
                sim.Tick(10);
            }
        }
        private static void WaitFor(Simulation sim, Func<bool> condition, float limit = 1200)
        {
            float start = sim.State.ElapsedSeconds;
            while (!condition()) { Check(sim.State.ElapsedSeconds - start < limit, "Playthrough stalled."); EnsureTimber(sim, 12); sim.Tick(1); }
        }
        private static void ResearchNow(Simulation sim, ResearchKind kind)
        {
            EnsureTimber(sim, Simulation.ResearchCost(kind).Timber + 12);
            Check(sim.TryResearch(kind, out string why), why); WaitFor(sim, () => sim.HasResearch(kind));
        }
        private static void Playthrough()
        {
            foreach (int seed in new[] { 417, 418 }) Playthrough(seed);
        }
        private static void Playthrough(int seed)
        {
            var sim = Simulation.Create(seed); Build(sim, Home, BuildingKind.House); Build(sim, new HexCoord(-1, 1), BuildingKind.Forager, 2);
            Build(sim, Forest, BuildingKind.Forestry); Claim(sim, Clay); Road(sim, new HexCoord(0, -1)); Build(sim, Clay, BuildingKind.ClayPit);
            WaitFor(sim, () => sim.State.Town.Population >= 6); ResearchNow(sim, ResearchKind.Brickmaking);
            EnsureTimber(sim, 30); Build(sim, Smelter, BuildingKind.Kiln);
            ResearchNow(sim, ResearchKind.Reforestation); EnsureTimber(sim, 30); Build(sim, new HexCoord(0, -1), BuildingKind.Forester);
            EnsureTimber(sim, 28); Claim(sim, new HexCoord(0, 2)); Build(sim, new HexCoord(0, 2), BuildingKind.House);
            WaitFor(sim, () => sim.State.Stock.Brick >= 12 && sim.State.Town.Population >= 8); ResearchNow(sim, ResearchKind.PrimitiveSmelting);
            WaitFor(sim, () => sim.State.Stock.Brick >= 14); EnsureTimber(sim, 50); Claim(sim, new HexCoord(0, 3));
            var bloomery = new HexCoord(0, 3); Build(sim, bloomery, BuildingKind.Bloomery); Build(sim, Mine, BuildingKind.Mine);
            float firstMetal = 0;
            while (sim.State.Stock.Metal < 6)
            {
                EnsureTimber(sim, 30); WaitFor(sim, () => sim.State.Stock.Ore >= 3);
                Check(sim.TryStartBatch(bloomery, out string why), why); int metal = sim.State.Stock.Metal;
                // Persist in the middle of a manual batch to exercise the actual continuation path.
                sim.Tick(30.3125f); sim = Reload(sim); WaitFor(sim, () => sim.State.Stock.Metal > metal);
                if (firstMetal == 0) firstMetal = sim.State.ElapsedSeconds;
            }
            WaitFor(sim, () => sim.State.Town.Population >= 10 && sim.State.Stock.Brick >= 26);
            ResearchNow(sim, ResearchKind.Refining); EnsureTimber(sim, 42);
            Check(sim.TryDemolish(bloomery, out string remove), remove); Build(sim, bloomery, BuildingKind.Smelter);
            int before = sim.State.Stock.Metal; WaitFor(sim, () => sim.State.Stock.Metal > before); Reload(sim);
            Console.WriteLine($"  Seed {seed}: first metal {firstMetal / 60:0.0} min; working foundry {sim.State.ElapsedSeconds / 60:0.0} min (simulation time, not human playtime).");
        }
    }
}
