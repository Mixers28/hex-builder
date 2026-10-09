using System;
using System.Linq;
using AlphaHex.Core;

namespace AlphaHex.SimulationChecks
{
    internal static partial class Program
    {
        private static readonly HexCoord Home = new HexCoord(0, -1);

        private static Simulation GrowTown()
        {
            Simulation sim = Simulation.Create();
            Check(sim.TryBuild(Smelter, BuildingKind.Forager, out _), "Build food supply.");
            foreach (HexCoord coord in new[] { Home, new HexCoord(-1, 1), new HexCoord(1, -1) })
                Check(sim.TryBuild(coord, BuildingKind.House, out _), "Build housing.");
            sim.Tick(140);
            Equal(16, sim.State.Town.Population, "All three homes fill from supported immigration.");
            return sim;
        }

        private static void TownGrowth()
        {
            Simulation sim = Simulation.Create();
            Equal(4, sim.State.Town.Population, "Pioneers.");
            sim.Tick(20);
            Equal(4, sim.State.Town.Population, "Immigration without housing.");
            Check(sim.TryBuild(Home, BuildingKind.House, out _), "House placement.");
            sim.Tick(19.875f);
            Equal(4, sim.State.Town.Population, "Early immigration.");
            sim.Tick(0.125f);
            Equal(6, sim.State.Town.Population, "First wave.");
            Equal(2, sim.GetTile(Home).Residents, "House census.");
            Equal(1, sim.ResearchPoints, "First milestone.");
            Check(!sim.TryDemolish(Home, out _), "Occupied house was removed.");
            sim = GrowTown();
            Equal(3, sim.ResearchPoints, "All three milestones.");
            Equal(16, sim.HousingCapacity, "Housing count.");
            new Simulation(Clone(sim.State));

            sim = Simulation.Create();
            HexCoord remote = new HexCoord(0, 2);
            Check(sim.TryClaim(remote, out _) && sim.TryBuild(remote, BuildingKind.House, out _), "Remote home.");
            sim.Tick(25);
            Equal(4, sim.State.Town.Population, "Disconnected home welcomed citizens.");
            Check(sim.TryBuildRoad(Smelter, out _), "Home access.");
            sim.Tick(20);
            Equal(6, sim.State.Town.Population, "Connected home did not welcome citizens.");
        }

        private static void FoodRecovery()
        {
            WorldState state = Simulation.Create().State;
            state.Stock.Food = 0;
            Simulation sim = new Simulation(state);
            Check(sim.TryBuild(Home, BuildingKind.House, out _), "Recovery house.");
            sim.Tick(30);
            Check(sim.State.Town.LastMealMissed, "Meal shortage missing.");
            Equal(4, sim.State.Town.Population, "Hungry town grew.");
            Check(!sim.TryForage(Mine, out _), "Foraged a quarry.");
            Check(sim.TryForage(Home, out _), "Manual food recovery.");
            string before = Save(sim.State);
            Check(!sim.TryForage(Home, out _), "Cooldown bypassed.");
            Equal(before, Save(sim.State), "Rejected forage altered state.");
            Check(sim.TryBuild(Smelter, BuildingKind.Forager, out _), "Recovery workplace.");
            sim.Tick(55);
            Check(!sim.State.Town.LastMealMissed && sim.State.Town.Population >= 6, "Food failed to restore immigration.");
        }

        private static void Staffing()
        {
            Simulation sim = Simulation.Create();
            Check(sim.TryBuild(Forest, BuildingKind.Forestry, out _) && sim.TryBuild(Mine, BuildingKind.Mine, out _), "Industry staffing setup.");
            foreach (HexCoord coord in new[] { Home, Smelter, new HexCoord(-1, 1), new HexCoord(1, -1) })
                Check(sim.TryBuild(coord, BuildingKind.Forager, out _), "Food staffing setup.");
            Equal(4, sim.AssignedWorkers, "Overassigned pioneers.");
            Equal(0, sim.GetWorkersAssigned(Forest), "Industry took food worker.");
            sim.Tick(5);
            Equal(0f, sim.GetTile(Forest).WorkProgress, "Unstaffed logging progressed.");
            Check(sim.TryToggleWork(Home, out _), "Pause food job.");
            Equal(1, sim.GetWorkersAssigned(Forest), "Freed citizen was not reassigned.");
            sim.Tick(4);
            Check(sim.GetInTransit(ResourceKind.Timber) > 0, "Staffed logging failed.");
            Check(sim.TryToggleWork(Forest, out _), "Pause shipment source.");
            sim.Tick(0.75f);
            Equal(0, sim.GetInTransit(ResourceKind.Timber), "Paused workplace held up cargo.");
            new Simulation(Clone(sim.State));
        }

        private static void ResearchUnlocks()
        {
            Simulation sim = Simulation.Create();
            string before = Save(sim.State);
            Check(!sim.TryResearch(ResearchKind.Farming, out _), "Unearned research.");
            Check(!sim.TryBuild(Home, BuildingKind.Farm, out _), "Locked farm built.");
            Check(!sim.TryResearch(ResearchKind.Farming | ResearchKind.DeepMining, out _), "Combined research accepted.");
            Equal(before, Save(sim.State), "Rejected research spent resources.");
            sim = GrowTown();
            foreach (ResearchKind kind in new[] { ResearchKind.Farming, ResearchKind.Reforestation, ResearchKind.DeepMining })
                Check(sim.TryResearch(kind, out _), "Earned research rejected.");
            Equal(0, sim.ResearchPoints, "Research was free.");
            Check(!sim.TryResearch(ResearchKind.Farming, out _), "Duplicate research accepted.");
            Check(sim.TryClaim(new HexCoord(0, 2), out _) && sim.TryBuild(new HexCoord(0, 2), BuildingKind.Farm, out _), "Researched farm build.");
            sim.Tick(20);
            Equal(6, sim.State.Shipments.Single(s => s.Path[0] == new HexCoord(0, 2)).Amount, "Farm food batch.");
            new Simulation(Clone(sim.State));
        }

        private static void Replanting()
        {
            Simulation sim = GrowTown();
            Check(sim.TryResearch(ResearchKind.Reforestation, out _), "Reforestation research.");
            // Put a nursery beside the claimed frontier forest, then exhaust its logging camp.
            Check(sim.TryClaim(new HexCoord(-2, 0), out _), "Planting frontier.");
            while (sim.GetTile(Forest).TreesRemaining > 0) sim.TryHarvest(Forest, out _);
            Check(sim.TryBuild(Forest, BuildingKind.Forester, out _), "Forester build.");
            HexCoord managed = new HexCoord(-2, 0);
            Check(sim.TryBuild(managed, BuildingKind.Forestry, out _), "Managed logging camp.");
            while (sim.GetTile(managed).TreesRemaining > 0) sim.TryHarvest(managed, out _);
            Equal(BuildingKind.Forestry, sim.GetTile(managed).Building, "Managed logging cleared.");
            sim.Tick(8);
            Check(sim.GetTile(managed).SaplingPlanted, "Forester did not plant.");
            Simulation loaded = new Simulation(Clone(sim.State));
            sim.Tick(45);
            loaded.Tick(45);
            Equal(Save(sim.State), Save(loaded.State), "Sapling save continuity.");
            Equal(1, sim.GetTile(managed).TreesRemaining, "Sapling maturity.");
            int timber = sim.State.Stock.Timber;
            sim.Tick(6);
            Equal(timber + 3, sim.State.Stock.Timber, "Regrown timber not delivered.");
            Check(sim.TryDemolish(Forest, out _), "Remove forester.");
            sim.Tick(0.125f);
            Equal(BuildingKind.None, sim.GetTile(managed).Building, "Unmanaged exhausted camp remained.");
            new Simulation(Clone(sim.State));
        }

        private static void DeepExtraction()
        {
            Simulation sim = GrowTown();
            Check(sim.TryResearch(ResearchKind.DeepMining, out _), "Deep research.");
            Check(!sim.TryBuild(Mine, BuildingKind.DeepMine, out _), "Deep mine on an unexhausted surface.");
            while (sim.GetTile(Mine).OreRemaining > 0) sim.TryExcavate(Mine, out _);
            Check(sim.TryBuild(Mine, BuildingKind.DeepMine, out _), "Deep mine construction.");
            int total = sim.State.Stock.Ore + sim.GetTile(Mine).DeepOreRemaining;
            sim.Tick(2);
            Check(sim.TryDemolish(Mine, out _), "Partial deep job removal.");
            Equal(72, sim.GetTile(Mine).DeepOreRemaining, "Partial job spent reserve.");
            // Rebuilding is paid from a fixture stock; it must never refill a deposit.
            sim.State.Stock.Timber += 20;
            sim.State.Stock.Metal += 6;
            Check(sim.TryBuild(Mine, BuildingKind.DeepMine, out _), "Deep rebuild.");
            sim.Tick(7);
            Equal(70, sim.GetTile(Mine).DeepOreRemaining, "Deep batch extraction.");
            Simulation loaded = new Simulation(Clone(sim.State));
            sim.Tick(300);
            loaded.Tick(300);
            Equal(Save(sim.State), Save(loaded.State), "Deep save continuity.");
            Equal(total, sim.State.Stock.Ore, "Deep ore conservation.");
            Equal(6, sim.GetTile(Mine).ExcavationDepth, "Deep quarry stages.");
            Equal(BuildingKind.None, sim.GetTile(Mine).Building, "Exhausted deep mine remained.");
            Check(sim.GetTile(Mine).HasRoad && !sim.TryBuild(Mine, BuildingKind.DeepMine, out _), "Deep reserve reset.");
        }

        private static void TownContinuity()
        {
            Simulation sim = Simulation.Create();
            Check(sim.TryBuild(Home, BuildingKind.House, out _) && sim.TryBuild(Smelter, BuildingKind.Forager, out _), "Town continuity setup.");
            sim.Tick(8.3f);
            Simulation loaded = new Simulation(Clone(sim.State));
            sim.Tick(52.5f);
            for (int i = 0; i < 420; i++) loaded.Tick(0.125f);
            Equal(Save(sim.State), Save(loaded.State), "Town fixed-step continuity.");
        }

        private static void TownMigration()
        {
            Simulation old = Simulation.Create();
            Check(old.TryBuild(Mine, BuildingKind.Mine, out _), "Legacy mine.");
            old.Tick(5.25f);
            WorldState legacy = Clone(old.State);
            legacy.SchemaVersion = 1;
            legacy.Town = null;
            legacy.Stock.Food = 0;
            foreach (TileState tile in legacy.Tiles) tile.DeepOreInitial = tile.DeepOreRemaining = 0;
            string before = Save(legacy);
            Simulation migrated = Simulation.Restore(legacy);
            Equal(before, Save(legacy), "Migration mutated input.");
            Equal(24, migrated.State.Stock.Food, "Legacy provisions.");
            Equal(4, migrated.State.Town.Population, "Legacy citizens.");
            Equal(72, migrated.GetTile(Mine).DeepOreRemaining, "Legacy deep ore.");
            Equal(34, migrated.GetTile(Mine).OreRemaining, "Migration refilled surface ore.");
            migrated.Tick(0.5f);
            Equal(2, migrated.State.Stock.Ore, "Legacy cargo lost.");
            migrated.State.Stock.Food = 3;
            Equal(3, Simulation.Restore(Clone(migrated.State)).State.Stock.Food, "Repeated load granted provisions.");
            legacy.Tiles.First(t => t.Coord == Smelter).Building = BuildingKind.Farm;
            Throws<ArgumentException>(() => Simulation.Restore(legacy), "Legacy new building bypass.");
        }

        private static void InvalidTownSaves()
        {
            Reject(s => s.Town = null, "Missing town.");
            Reject(s => s.Town.Population = 6, "Homeless citizens.");
            Reject(s => s.Town.Research = ResearchKind.Farming, "Unearned research.");
            Reject(s => s.Town.MealProgress = float.NaN, "Invalid meal progress.");
            Reject(s => s.Town.ForageCooldown = 9, "Invalid forage cooldown.");
            Reject(s => s.Stock.Food = -1, "Negative food.");
            Reject(s => s.Tiles.First(t => t.Coord == Mine).DeepOreRemaining = 999, "Excess deep reserves.");
            Reject(s => s.Tiles.First(t => t.Coord == Forest).SaplingPlanted = true, "Unresearched sapling.");
            Reject(s => s.Tiles.First(t => t.Coord == Home).Residents = 2, "Residents outside a house.");
            Simulation sim = GrowTown();
            sim.State.Town.Research = (ResearchKind)8;
            Throws<ArgumentException>(() => new Simulation(Clone(sim.State)), "Unknown research bits.");
        }

        private static void TownGuidance()
        {
            Simulation sim = Simulation.Create();
            string initial = Save(sim.State);
            SettlementObjective goal = SettlementGuide.Next(sim);
            Equal("Feed your citizens", goal.Heading, "First town step.");
            Equal(initial, Save(sim.State), "Town guidance changed state.");
            Check(goal.Target.HasValue && sim.TryBuild(goal.Target.Value, BuildingKind.Forager, out _), "Food target is unusable.");
            goal = SettlementGuide.Next(sim);
            Equal("Welcome more citizens", goal.Heading, "Housing step.");
            Check(goal.Target.HasValue && sim.TryBuild(goal.Target.Value, BuildingKind.House, out _), "House target is unusable.");
            Equal("Your first newcomers", SettlementGuide.Next(sim).Heading, "Immigration waiting step.");
            sim.Tick(20);
            Equal("Choose your research", SettlementGuide.Next(sim).Heading, "Research step.");
        }
    }
}
