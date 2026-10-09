using System;
using System.Linq;
using AlphaHex.Core;
using NUnit.Framework;
using UnityEngine;

namespace AlphaHex.Tests
{
    public sealed class CoreTests
    {
        private static readonly HexCoord Mine = new HexCoord(1, 0);
        private static readonly HexCoord Smelter = new HexCoord(0, 1);

        [Test]
        public void JsonUtilityPreservesAnActiveDeliveryAndThePartialClock()
        {
            Simulation original = Simulation.Create();
            Assert.That(original.TryBuild(Mine, BuildingKind.Mine, out _), Is.True);
            original.Tick(5.31f);
            Assert.That(original.State.Shipments.Count, Is.EqualTo(1));
            Assert.That(original.State.PendingSeconds, Is.GreaterThan(0));

            WorldState payload = JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(original.State));
            Simulation loaded = new Simulation(payload);
            original.Tick(10.17f);
            loaded.Tick(10.17f);

            Assert.That(JsonUtility.ToJson(loaded.State), Is.EqualTo(JsonUtility.ToJson(original.State)));
            Assert.That(loaded.State.Stock.Ore, Is.GreaterThan(0));
        }

        [Test]
        public void JsonUtilityPreservesReservedSmelterInput()
        {
            Simulation original = Simulation.Create();
            Assert.That(original.TryExcavate(Mine, out _) && original.TryExcavate(Mine, out _), Is.True);
            Assert.That(original.TryBuild(Smelter, BuildingKind.Smelter, out _), Is.True);
            original.Tick(2.375f);
            Assert.That(original.GetTile(Smelter).InputOreReserved, Is.EqualTo(2));

            Simulation loaded = new Simulation(JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(original.State)));
            loaded.Tick(4.375f);
            Assert.That(loaded.State.Stock.Ore, Is.Zero);
            Assert.That(loaded.State.Stock.Metal, Is.EqualTo(7));
            Assert.That(loaded.GetTile(Smelter).InputOreReserved, Is.Zero);
        }

        [Test]
        public void ADisconnectedMineWaitsForAnActualRoad()
        {
            Simulation sim = Simulation.Create();
            HexCoord source = new HexCoord(2, 0);
            Assert.That(sim.TryClaim(source, out _), Is.True);
            Assert.That(sim.TryBuild(source, BuildingKind.Mine, out _), Is.True);
            sim.Tick(30);
            Assert.That(sim.GetTile(source).OreRemaining, Is.EqualTo(48));
            Assert.That(sim.State.Stock.Ore, Is.Zero);

            Assert.That(sim.TryBuildRoad(Mine, out _), Is.True);
            sim.Tick(5);
            Assert.That(sim.State.Stock.Ore, Is.Zero);
            Assert.That(sim.State.Shipments.Single().Amount, Is.EqualTo(2));
            sim.Tick(1.5f);
            Assert.That(sim.State.Stock.Ore, Is.EqualTo(2));
        }

        [Test]
        public void FinalOreCannotBeExtractedTwice()
        {
            Simulation sim = Simulation.Create();
            Assert.That(sim.TryBuild(Mine, BuildingKind.Mine, out _), Is.True);
            for (int i = 0; i < 35; i++) Assert.That(sim.TryExcavate(Mine, out _), Is.True);
            sim.Tick(5);
            Assert.That(sim.State.Shipments.Single().Amount, Is.EqualTo(1));
            Assert.That(sim.TryExcavate(Mine, out _), Is.False);
            sim.Tick(100);
            Assert.That(sim.State.Stock.Ore, Is.EqualTo(36));
            Assert.That(sim.GetTile(Mine).ExcavationDepth, Is.EqualTo(3));
            Assert.That(sim.GetTile(Mine).Terrain, Is.EqualTo(TerrainKind.Rock));
        }

        [Test]
        public void DuplicateOrUnsupportedSaveDataIsRejected()
        {
            WorldState state = Simulation.Create().State;
            state.Tiles[1].Coord = state.Tiles[0].Coord;
            Assert.Throws<ArgumentException>(() => new Simulation(state));
            state = Simulation.Create().State;
            state.SchemaVersion++;
            Assert.Throws<ArgumentException>(() => new Simulation(state));
        }

        [TestCase(BuildingKind.Forestry)]
        [TestCase(BuildingKind.Mine)]
        public void JsonUtilityPreservesFinalDeliveryUntilExtractorClears(BuildingKind kind)
        {
            Simulation original = Simulation.Create();
            HexCoord coord = kind == BuildingKind.Forestry ? new HexCoord(-1, 0) : Mine;
            Assert.That(original.TryBuild(coord, kind, out _), Is.True);
            TileState tile = original.GetTile(coord);
            while (tile.TreesRemaining > 1) Assert.That(original.TryHarvest(coord, out _), Is.True);
            while (tile.OreRemaining > 1) Assert.That(original.TryExcavate(coord, out _), Is.True);
            original.Tick(kind == BuildingKind.Forestry ? Simulation.ForestrySeconds : Simulation.MineSeconds);
            int timber = original.State.Stock.Timber;
            int ore = original.State.Stock.Ore;

            Simulation loaded = new Simulation(JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(original.State)));
            loaded.Tick(0.625f);
            Assert.That(loaded.GetTile(coord).Building, Is.EqualTo(kind));
            Assert.That(loaded.State.Shipments.Count, Is.EqualTo(1));
            loaded.Tick(0.125f);
            original.Tick(0.75f);
            Assert.That(JsonUtility.ToJson(loaded.State), Is.EqualTo(JsonUtility.ToJson(original.State)));
            Assert.That(loaded.GetTile(coord).Building, Is.EqualTo(BuildingKind.None));
            Assert.That(loaded.GetTile(coord).HasRoad, Is.True);
            Assert.That(loaded.State.Shipments, Is.Empty);
            Assert.That(loaded.State.Stock.Timber, Is.EqualTo(timber + (kind == BuildingKind.Forestry ? 3 : 0)));
            Assert.That(loaded.State.Stock.Ore, Is.EqualTo(ore + (kind == BuildingKind.Mine ? 1 : 0)));
            Assert.DoesNotThrow(() => new Simulation(JsonUtility.FromJson<WorldState>(JsonUtility.ToJson(loaded.State))));
        }

        [Test]
        public void JsonUtilityKeepsExpansionGuidanceAfterStarterDepletion()
        {
            Simulation original = Simulation.Create();
            HexCoord forest = new HexCoord(-1, 0);
            Assert.That(original.TryBuild(forest, BuildingKind.Forestry, out _), Is.True);
            original.Tick(60);
            Assert.That(original.GetTile(forest).Building, Is.EqualTo(BuildingKind.None));
            string json = JsonUtility.ToJson(original.State);
            Simulation loaded = new Simulation(JsonUtility.FromJson<WorldState>(json));
            SettlementObjective goal = SettlementGuide.Next(loaded);
            Assert.That(goal.Heading, Is.EqualTo("Restore timber supply"));
            Assert.That(goal.Target, Is.EqualTo(SettlementGuide.Next(original).Target));
            Assert.That(goal.Target.HasValue, Is.True);
            Assert.That(goal.Target.Value, Is.Not.EqualTo(forest));
            Assert.That(JsonUtility.ToJson(loaded.State), Is.EqualTo(json), "Guidance must not mutate the save.");
        }
    }
}
