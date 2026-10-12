using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlphaHex.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AlphaHex.Runtime
{
    /// <summary>Opt-in player smoke checks. Every file is isolated by --alpha-qa --qa-directory PATH.</summary>
    public sealed class FrontierQa : MonoBehaviour
    {
        [Serializable] private sealed class Measurement { public string label; public int width, height; public float scale, minimumButtonPixels; public bool fullscreen; }
        [Serializable] private sealed class Result { public bool passed, headless; public string version; public List<Measurement> screens = new List<Measurement>(); public string[] errors; }
        [Serializable] private sealed class PackageManifest { public string package_version; }
        private readonly List<string> errors = new List<string>();
        private readonly Result result = new Result();
        private GameBootstrap game;
        private bool headless;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void Activate()
        { if (RuntimeOptions.IsQa) new GameObject("Isolated player QA").AddComponent<FrontierQa>(); }
        private void Start() { Application.logMessageReceived += CaptureError; StartCoroutine(Guarded()); }
        private void CaptureError(string text, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
        private IEnumerator Guarded()
        {
            var work = Run();
            while (true)
            {
                object wait;
                try { if (!work.MoveNext()) break; wait = work.Current; }
                catch (Exception exception) { errors.Add(exception.ToString()); Finish(false); yield break; }
                yield return wait;
            }
            Finish(errors.Count == 0);
        }
        private void Finish(bool passed)
        {
            result.passed = passed; result.headless = headless; result.version = Application.version; result.errors = errors.ToArray();
            Directory.CreateDirectory(RuntimeOptions.QaDirectory); File.WriteAllText(Path.Combine(RuntimeOptions.QaDirectory, "player-result.json"), JsonUtility.ToJson(result, true));
            Debug.Log(passed ? "ALPHA_HEX_QA_PASS" : "ALPHA_HEX_QA_FAIL " + string.Join("; ", errors)); Application.Quit(passed ? 0 : 1);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Build(HexCoord coord, BuildingKind kind)
        {
            game.Select(coord); game.SetTool((WorldTool)Enum.Parse(typeof(WorldTool), kind.ToString())); game.ApplyTool();
            Require(game.Simulation.GetTile(coord).Building == kind, game.Notice);
        }
        private void Claim(HexCoord coord) { Require(game.Simulation.TryClaim(coord, out string why), why); }
        private IEnumerator Run()
        {
            yield return null; game = FindFirstObjectByType<GameBootstrap>(); Require(game != null, "Bootstrap missing.");
            headless = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
            if (!game.Paused) game.TogglePause();
            string updateExpected = Path.Combine(RuntimeOptions.QaDirectory, "update-expected.json");
            if (RuntimeOptions.IsUpdateQa && File.Exists(updateExpected))
            {
                game.LoadGame(); if (!game.Paused) game.TogglePause();
                Require(JsonUtility.ToJson(game.Simulation.State) == File.ReadAllText(updateExpected), "Updater restart changed the saved settlement.");
                var manifest = JsonUtility.FromJson<PackageManifest>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "alphahex-build.json")));
                Require(manifest.package_version == Environment.GetEnvironmentVariable("ALPHA_HEX_QA_UPDATE_VERSION"), "Restart used the old installation.");
                File.WriteAllText(Path.Combine(RuntimeOptions.QaDirectory, "update-result.json"), "{\"passed\":true,\"savePreserved\":true,\"restarted\":true}");
                yield break;
            }
            Require(game.Simulation.State.Stock.Metal == 0 && game.Simulation.State.Tiles.Count == 37, "Wrong fresh game.");
            if (!headless) { Screen.SetResolution(1600, 900, FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(1); yield return Capture("starting-village"); }
            // Provision only this isolated presentation fixture; command-only progression is checked in SimulationChecks.
            game.Simulation.State.Stock = new ResourceStock(10000, 200, 200, 10000, 200, 300, 300);
            game.Simulation.State.Town.Research = (ResearchKind)1023;
            Build(new HexCoord(0, 1), BuildingKind.House); Build(new HexCoord(-1, 1), BuildingKind.Forager);
            Build(new HexCoord(-1, 0), BuildingKind.Forestry); Build(new HexCoord(1, 0), BuildingKind.Mine);
            Build(new HexCoord(1, -1), BuildingKind.Smelter); Build(new HexCoord(0, -1), BuildingKind.Kiln);
            Claim(new HexCoord(0, -2)); Build(new HexCoord(0, -2), BuildingKind.ClayPit);
            Claim(new HexCoord(0, 2)); Build(new HexCoord(0, 2), BuildingKind.Bloomery);
            game.Select(new HexCoord(0, 2)); game.SetTool(WorldTool.Inspect); game.AssignSelected(1);
            game.StartSelectedBatch(); Require(game.Simulation.GetTile(new HexCoord(0, 2)).BatchRequested, "Manual batch UI failed.");
            game.Simulation.Tick(20.3125f); game.Select(new HexCoord(0, 2));
            if (!headless) { yield return null; yield return Capture("manual-bloomery"); }
            game.Select(new HexCoord(0, -1)); game.AssignSelected(1); game.Simulation.Tick(1); game.SetTool(WorldTool.Inspect);
            if (!headless) { yield return null; yield return Capture("clay-and-kiln"); }
            Claim(new HexCoord(0, 3)); Build(new HexCoord(0, 3), BuildingKind.Watchtower); game.UpgradeSelectedTower();
            Require(game.Simulation.State.Tiles.Count > 37, "Tower UI did not reveal new land.");
            Require(game.SaveGame(), game.Notice); string expected = JsonUtility.ToJson(game.Simulation.State); game.LoadGame();
            if (!game.Paused) game.TogglePause(); Require(JsonUtility.ToJson(game.Simulation.State) == expected, "Native player save changed state.");
            var hud = FindFirstObjectByType<GameHud>(); game.Select(new HexCoord(0, 3)); game.SetTool(WorldTool.Inspect);
            if (!headless)
            {
                yield return Capture("frontier");
                hud.ShowResearch(); yield return null; yield return Capture("research"); hud.CloseTown(); hud.ToggleMenu();
                foreach (var size in new[] { new Vector2Int(1280, 800), new Vector2Int(1920, 1080), new Vector2Int(2560, 1600) })
                { Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(.8f); yield return Capture("menu-" + size.x + "x" + size.y); }
                Screen.fullScreen = true; yield return new WaitForSecondsRealtime(1.5f); yield return Capture("fullscreen"); Screen.fullScreen = false;
                hud.CloseMenu(); hud.ToggleTown(); yield return null; yield return Capture("workers"); hud.CloseTown();
                var world = FindFirstObjectByType<HexWorldView>();
                Vector3 screen = Camera.main.WorldToScreenPoint(world.WorldPosition(new HexCoord(0, 0)));
                Require(world.TryPick(Camera.main.ScreenPointToRay(screen), out var picked) && picked == new HexCoord(0, 0), "Scaled hex picking disagrees with terrain.");
            }
            string sourceFile = Path.Combine(RuntimeOptions.QaDirectory, "import-source.json");
            string[] args = Environment.GetCommandLineArgs(); int fixtureIndex = Array.IndexOf(args, "--qa-fixtures");
            if (fixtureIndex >= 0 && fixtureIndex + 1 < args.Length)
            {
                File.Copy(Path.Combine(args[fixtureIndex + 1], "godot-frontier-v3.json"), sourceFile, true); string source = File.ReadAllText(sourceFile);
                game.ImportSave(sourceFile); if (!game.Paused) game.TogglePause(); Require(File.ReadAllText(sourceFile) == source, "Import altered source file.");
                Require(game.Simulation.GetTile(new HexCoord(0, 2)).TowerLevel == 2, "Godot save import lost reinforced tower.");
            }
            // A failed write must leave the live game running; use a directory to make the target unwritable.
            string held = SaveService.SavePath + ".qa-held"; File.Move(SaveService.SavePath, held); Directory.CreateDirectory(SaveService.SavePath);
            Require(!game.SaveAndExit() && !game.ExitRequested, "Failed save exited the game.");
            Directory.Delete(SaveService.SavePath); File.Move(held, SaveService.SavePath);
            if (RuntimeOptions.IsUpdateQa)
            {
                game.Updates.Act(); float deadline = Time.realtimeSinceStartup + 60;
                while (game.Updates.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                Require(!game.Updates.Busy && game.Updates.ButtonLabel == "Save, update & restart", "Staged update was not recognized: " + game.Updates.Status);
                File.Move(SaveService.SavePath, held); Directory.CreateDirectory(SaveService.SavePath);
                game.Updates.Act();
                Require(!game.Updates.Busy && !game.ExitRequested, "Updater continued after a failed save.");
                Directory.Delete(SaveService.SavePath); File.Move(held, SaveService.SavePath);
                Require(game.SaveGame(), game.Notice); File.WriteAllText(updateExpected, JsonUtility.ToJson(game.Simulation.State));
                game.Updates.Act(); deadline = Time.realtimeSinceStartup + 70;
                while (!game.ExitRequested && game.Updates.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                Require(game.ExitRequested, "Updater did not complete the save/exit handshake: " + game.Updates.Status);
                yield break; // Finish exits this isolated player; the real helper performs replacement and restart.
            }
            Require(game.SaveAndExit() && game.ExitRequested, "Successful Save & Exit did not request exit.");
            Require(SaveService.TryLoad(out var saved, out string message), message); new Simulation(saved.State);
        }
        private IEnumerator Capture(string label)
        {
            yield return new WaitForEndOfFrame(); Canvas.ForceUpdateCanvases();
            var canvas = FindFirstObjectByType<GameHud>().GetComponent<Canvas>(); float minimum = float.MaxValue;
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                var rect = button.GetComponent<RectTransform>(); minimum = Mathf.Min(minimum, rect.rect.height * canvas.scaleFactor);
                // Scrolling content may extend beyond its viewport. Fixed navigation must remain on screen.
                if (button.GetComponentInParent<ScrollRect>() != null) continue;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Require(corners.All(p => p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1), "Navigation outside screen: " + button.name);
            }
            Require(minimum >= 35, "Buttons became too small: " + minimum);
            result.screens.Add(new Measurement { label = label, width = Screen.width, height = Screen.height, scale = canvas.scaleFactor, minimumButtonPixels = minimum, fullscreen = Screen.fullScreen });
            string path = Path.Combine(RuntimeOptions.QaDirectory, label + ".png"); Directory.CreateDirectory(RuntimeOptions.QaDirectory);
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 8; while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(File.Exists(path), "Screenshot was not written: " + label);
        }
        private void OnDestroy() { Application.logMessageReceived -= CaptureError; }
    }
}
