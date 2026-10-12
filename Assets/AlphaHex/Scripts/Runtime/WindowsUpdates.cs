using System;
using System.Diagnostics;
using System.IO;
using AlphaHex.Updates;
using UnityEngine;

namespace AlphaHex.Runtime
{
    /// <summary>The player only requests updates. The separate launcher replaces files after this process exits.</summary>
    public sealed class WindowsUpdates : MonoBehaviour
    {
        public string Status { get; private set; }
        public string ButtonLabel => phase == "available" ? "Download " + targetVersion : phase == "downloaded" ? "Save, update & restart"
            : phase == "unsupported" ? "Get Windows setup" : Busy ? "Please wait…" : "Check for updates";
        public bool Busy { get; private set; }
        public bool CanAct => !Busy && Application.platform == RuntimePlatform.WindowsPlayer;
        private GameBootstrap game;
        private Process helper;
        private string phase = "idle", targetVersion = "", operationId, statusPath;
        private float deadline, nextPoll;
        private bool wasPaused, applying;
        private string HelperPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "AlphaHex.exe");
        public void Initialize(GameBootstrap owner)
        {
            game = owner;
            Status = Application.platform == RuntimePlatform.WindowsPlayer ? "Version " + Application.version + " · updates from GitHub Releases."
                : "Version " + Application.version + " · in-game updates are available in the Windows installation.";
        }
        public void Act()
        {
            if (!CanAct) return;
            if (phase == "unsupported" || !File.Exists(HelperPath)) { Application.OpenURL(UpdateFeed.Repository + "/releases"); return; }
            string command = phase == "available" ? "download" : phase == "downloaded" ? "apply" : "check";
            applying = command == "apply";
            if (applying)
            {
                wasPaused = game.Paused; if (!wasPaused) game.TogglePause();
                if (!game.SaveGame()) { ResumeAfterError(); return; }
            }
            try
            {
                operationId = Guid.NewGuid().ToString("N");
                string folder = Path.Combine(SaveService.DirectoryPath, "updates"); Directory.CreateDirectory(folder);
                string requestPath = Path.Combine(folder, operationId + ".request.json"); statusPath = Path.Combine(folder, operationId + ".status.json");
                using (var current = Process.GetCurrentProcess())
                {
                    int.TryParse(Environment.GetEnvironmentVariable("ALPHA_HEX_LAUNCHER_PID"), out int launcherPid);
                    var request = new UpdateRequest { command = command, version = targetVersion, operationId = operationId, statusPath = statusPath,
                        gamePid = current.Id, gameStartedUtcTicks = current.StartTime.ToUniversalTime().Ticks, launcherPid = launcherPid };
                    File.WriteAllText(requestPath, JsonUtility.ToJson(request));
                }
                var start = new ProcessStartInfo(HelperPath, "--update " + UpdateArguments.Quote(requestPath))
                    { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(HelperPath) };
                helper = Process.Start(start) ?? throw new IOException("The update helper could not start.");
                Busy = true; phase = command == "download" ? "downloading" : "checking";
                Status = applying ? "Save complete. Preparing the update…" : "Contacting the update service…";
                deadline = Time.realtimeSinceStartup + (command == "download" ? 1260 : 65); nextPoll = 0;
            }
            catch (Exception error) { Fail(error.Message); }
        }
        private void Update()
        {
            if (!Busy || Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + .25f;
            try
            {
                if (File.Exists(statusPath))
                {
                    UpdateResponse response = JsonUtility.FromJson<UpdateResponse>(File.ReadAllText(statusPath));
                    if (response != null && response.operationId == operationId)
                    {
                        phase = response.phase; Status = response.message; targetVersion = response.version;
                        if (phase == "downloading") Status += " " + response.progress + "%";
                        if (phase == "available") Status += " v" + targetVersion + " · up to " + Math.Ceiling(response.availableBytes / 1048576d) + " MB.";
                        if (phase == "ready_to_exit")
                        {
                            if (!game.SaveAndExit()) { StopHelper(); Fail("The final save failed. The game remains open."); }
                            else Busy = false;
                            return;
                        }
                        if (phase == "available" || phase == "downloaded" || phase == "latest" || phase == "unsupported" || phase == "error")
                        { Busy = false; if (phase == "error") ResumeAfterError(); helper?.Dispose(); helper = null; return; }
                    }
                }
                if (helper != null && helper.HasExited) { Fail("The update helper stopped before completing its request."); return; }
                if (Time.realtimeSinceStartup > deadline) { StopHelper(); Fail("The update request timed out. Your current installation is unchanged."); }
            }
            catch (IOException) { /* An atomic status replacement may briefly contend with the reader. */ }
            catch (UnauthorizedAccessException error) { StopHelper(); Fail(error.Message); }
            catch (ArgumentException error) { StopHelper(); Fail(error.Message); }
        }
        private void Fail(string message)
        {
            Busy = false; phase = "error"; Status = "Update failed: " + message; ResumeAfterError(); helper?.Dispose(); helper = null;
            game.SetNotice(Status);
        }
        private void ResumeAfterError() { if (applying && !wasPaused && game.Paused) game.TogglePause(); applying = false; }
        private void StopHelper() { try { if (helper != null && !helper.HasExited) helper.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }
        private void OnDestroy() { helper?.Dispose(); }
    }
}
