using System;
using System.IO;
using System.Linq;
using AlphaHex.Core;
using AlphaHex.Runtime;
using AlphaHex.Updates;
using NUnit.Framework;
using UnityEngine;

namespace AlphaHex.Tests
{
    public sealed class CoreTests
    {
        private static readonly HexCoord Mine = new HexCoord(1, 0), Processor = new HexCoord(1, -1);
        private static Simulation Ready()
        {
            var state = Simulation.Create().State; state.Stock = new ResourceStock(1000, 100, 100, 1000, 100, 100, 100); state.Town.Research = (ResearchKind)1023;
            return new Simulation(state);
        }
        private static Simulation Reload(Simulation sim) => SaveService.Decode(JsonUtility.ToJson(sim.State));
        private static void Build(Simulation sim, HexCoord coord, BuildingKind building)
        {
            Assert.That(sim.TryBuild(coord, building, out string why), Is.True, why);
            Assert.That(sim.TryAssignWorkers(coord, 1, out why), Is.True, why);
        }
        [Test] public void NativeJsonPreservesCargoJobFuelAndClock()
        {
            var original = Ready(); Build(original, Mine, BuildingKind.Mine); Build(original, Processor, BuildingKind.Kiln); original.Tick(15.3125f);
            Assert.That(original.State.Shipments.Count, Is.EqualTo(1)); Assert.That(original.GetTile(Processor).InputClayReserved, Is.EqualTo(4));
            var loaded = Reload(original); original.Tick(200.1875f); loaded.Tick(200.1875f);
            Assert.That(JsonUtility.ToJson(loaded.State), Is.EqualTo(JsonUtility.ToJson(original.State)));
        }
        [Test] public void ManualBatchRemainsExplicitAfterRoundTrip()
        {
            var sim = Ready(); Build(sim, Processor, BuildingKind.Bloomery); Assert.That(sim.TryStartBatch(Processor, out _), Is.True); sim.Tick(40.3125f);
            var loaded = Reload(sim); Assert.That(loaded.GetTile(Processor).BatchRequested, Is.True);
            loaded.Tick(160); Assert.That(loaded.State.Stock.Metal, Is.EqualTo(101)); Assert.That(loaded.GetTile(Processor).BatchRequested, Is.False);
            loaded.Tick(100); Assert.That(loaded.State.Stock.Metal, Is.EqualTo(101));
        }
        [TestCase("unity-v1")][TestCase("unity-v2")][TestCase("godot-v3")][TestCase("godot-frontier-v3")]
        public void ReleasedSaveFixturesMigrateAndResume(string fixture)
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "AlphaHex/Tests/Fixtures", fixture + ".json"));
            var imported = SaveService.Decode(source); var loaded = Reload(imported);
            Assert.That(imported.State.SchemaVersion, Is.EqualTo(Simulation.CurrentSchemaVersion)); Assert.That(imported.GetTile(Processor).InputOreReserved, Is.EqualTo(2));
            Assert.That(imported.GetTile(Processor).LegacyOreJob, Is.True); Assert.That(imported.GetTile(Processor).InputTimberReserved, Is.Zero);
            imported.Tick(200); loaded.Tick(200); Assert.That(JsonUtility.ToJson(loaded.State), Is.EqualTo(JsonUtility.ToJson(imported.State)));
            Assert.That(imported.GetTile(Processor).LegacyOreJob, Is.False);
        }
        [Test] public void JsonRejectsMissingHeadersAndPayloads()
        {
            foreach (string json in new[] { "", "{}", "null", "{\"SchemaVersion\":4}", "{\"schema\":3}", "{\"SchemaVersion\":55}" })
                Assert.Throws<ArgumentException>(() => SaveService.Decode(json), json);
            string save = JsonUtility.ToJson(Simulation.Create().State);
            Assert.Throws<ArgumentException>(() => SaveService.Decode(save.Replace("\"Stock\"", "\"MissingStock\"")));
        }
        [Test] public void GodotFractionalResourcesAreNotTruncated()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "AlphaHex/Tests/Fixtures/godot-v3.json"));
            var dto = JsonUtility.FromJson<GodotSaveRecord>(source); dto.stock.ore = .5;
            Assert.Throws<ArgumentException>(() => Simulation.ImportGodot(dto));
        }
        [Test] public void FinalDeliveryRetainsItsSourceUntilArrival()
        {
            var sim = Ready(); Build(sim, Mine, BuildingKind.Mine);
            while (sim.GetTile(Mine).OreRemaining > 1) Assert.That(sim.TryExcavate(Mine, out _), Is.True);
            sim.Tick(15); Assert.That(sim.TryDemolish(Mine, out _), Is.False); var loaded = Reload(sim);
            loaded.Tick(1.125f); Assert.That(loaded.GetTile(Mine).Building, Is.EqualTo(BuildingKind.Mine));
            loaded.Tick(.125f); Assert.That(loaded.GetTile(Mine).Building, Is.EqualTo(BuildingKind.None)); Assert.That(loaded.GetTile(Mine).HasRoad, Is.True);
            Assert.That(loaded.GetTile(Mine).Workers, Is.Zero); Reload(loaded);
        }
        [Test] public void SaveBackupRecoveryAndFailedWritePreserveFiles()
        {
            string folder = Path.Combine(Path.GetTempPath(), "AlphaHex-SaveTest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "save.json"); var sim = Simulation.Create();
                Assert.That(SaveService.TrySaveAt(sim.State, path, out string message), Is.True, message); string first = File.ReadAllText(path);
                Assert.That(sim.TryClaim(new HexCoord(0, -2), out _), Is.True);
                Assert.That(SaveService.TrySaveAt(sim.State, path, out message), Is.True, message); Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(first));
                File.WriteAllText(path, "broken"); Assert.That(SaveService.TryLoadAt(path, out var recovered, out message), Is.True, message);
                Assert.That(recovered.State.Stock.Timber, Is.EqualTo(80)); Assert.That(SaveService.TrySaveAt(sim.State, path, out message), Is.True, message);
                Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(first), "Corrupt primary replaced a good backup.");
                string blocked = Path.Combine(folder, "blocked.json"); Directory.CreateDirectory(blocked);
                Assert.That(SaveService.TrySaveAt(sim.State, blocked, out _), Is.False); Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(first));
                Assert.That(File.Exists(blocked + ".pending"), Is.False);
            }
            finally { Directory.Delete(folder, true); }
        }
        [Test] public void UpdateProtocolRoundTripsThroughUnityJson()
        {
            var request = new UpdateRequest { command = "apply", version = "0.2.0", operationId = Guid.NewGuid().ToString("N"),
                gameStartedUtcTicks = 639000000000000000, statusPath = @"C:\Users\A Person\AppData\status.json", gamePid = 1234, launcherPid = 1233 };
            var read = JsonUtility.FromJson<UpdateRequest>(JsonUtility.ToJson(request));
            Assert.That(read.gameStartedUtcTicks, Is.EqualTo(request.gameStartedUtcTicks)); Assert.That(read.statusPath, Is.EqualTo(request.statusPath));
            Assert.That(UpdateArguments.Quote(request.statusPath), Is.EqualTo("\"" + request.statusPath + "\""));
        }
        [Test] public void GodotImportDefaultsToThePlatformUserDataDirectory()
        {
            if (Application.platform == RuntimePlatform.OSXEditor)
                Assert.That(SaveService.GodotPath, Does.Contain(Path.Combine("Library", "Application Support", "AlphaHexFrontier")));
            else Assert.That(SaveService.GodotPath, Does.EndWith(Path.Combine("AlphaHexFrontier", "frontier.json")));
        }
        [Test] public void MalformedReservationsAndCensusAreRejected()
        {
            var state = Ready().State; state.Town.Population++; Assert.Throws<ArgumentException>(() => new Simulation(state));
            var sim = Ready(); Build(sim, Processor, BuildingKind.Bloomery); Assert.That(sim.TryStartBatch(Processor, out _), Is.True);
            sim.GetTile(Processor).InputTimberReserved--; Assert.Throws<ArgumentException>(() => Reload(sim));
        }
    }
}
