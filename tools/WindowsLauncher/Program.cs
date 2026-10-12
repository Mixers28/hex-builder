using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AlphaHex.Updates;
using Velopack;
using Velopack.Sources;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    private static int Main(string[] args)
    {
        VelopackApp.Build().SetArgs(args).SetAutoApplyOnStartup(false).Run();
        return Run(args).GetAwaiter().GetResult();
    }
    private static async Task<int> Run(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--self-test") return await LauncherChecks.Run();
            if (args.Length == 2 && args[0] == "--update") return await Update(args[1]);
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This launcher is for Windows.");
            using var instance = new Mutex(true, "Local\\AlphaHex.Game", out bool first);
            if (!first) { ShowError("Alpha Hex is already running."); return 0; }
            string gamePath = Path.Combine(AppContext.BaseDirectory, "AlphaHexGame.exe");
            if (!File.Exists(gamePath)) throw new FileNotFoundException("The game files are missing. Install Alpha Hex again.");
            var start = new ProcessStartInfo(gamePath) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
            foreach (string arg in args) start.ArgumentList.Add(arg);
            string? qaDirectory = Environment.GetEnvironmentVariable("ALPHA_HEX_QA_UPDATE_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(qaDirectory))
            {
                start.ArgumentList.Add("-batchmode"); start.ArgumentList.Add("-nographics");
                start.ArgumentList.Add("-logFile"); start.ArgumentList.Add(Path.Combine(qaDirectory, "player-" + Environment.ProcessId + ".log"));
            }
            start.Environment["ALPHA_HEX_LAUNCHER_PID"] = Environment.ProcessId.ToString();
            using var game = Process.Start(start) ?? throw new IOException("The game could not start.");
            await game.WaitForExitAsync(); return game.ExitCode;
        }
        catch (Exception error) { ShowError(error.Message); return 1; }
    }
    private static async Task<int> Update(string requestPath)
    {
        if (new FileInfo(requestPath).Length > 32768) throw new ArgumentException("Update request is too large.");
        var request = JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(requestPath), Json) ?? throw new ArgumentException("Missing update request.");
        ValidateRequest(request);
        var status = new StatusWriter(request);
        try
        {
            var manager = new UpdateManager(new GithubSource(UpdateFeed.Repository, accessToken: null, prerelease: true),
                new UpdateOptions { ExplicitChannel = UpdateFeed.Channel, AllowVersionDowngrade = false });
            if (!manager.IsInstalled || manager.AppId != UpdateFeed.PackageId)
            { status.Write("unsupported", "Install the Windows setup once to enable updates."); return 0; }
            if (request.command == "apply")
            {
                var pending = manager.UpdatePendingRestart;
                if (pending == null || pending.Version.ToString() != request.version) throw new InvalidOperationException("The selected update is not ready. Check and download it again.");
                using var game = Process.GetProcessById(request.gamePid);
                if (game.StartTime.ToUniversalTime().Ticks != request.gameStartedUtcTicks
                    || !string.Equals(game.MainModule?.FileName, Path.Combine(AppContext.BaseDirectory, "AlphaHexGame.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The running game could not be verified.");
                status.Write("ready_to_exit", "Save complete. Closing the game to install the update.", request.version);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                await game.WaitForExitAsync(timeout.Token);
                if (request.launcherPid > 0)
                {
                    try
                    {
                        using var launcher = Process.GetProcessById(request.launcherPid);
                        if (string.Equals(launcher.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                            await launcher.WaitForExitAsync(timeout.Token);
                    }
                    catch (ArgumentException) { /* The original launcher has already exited. */ }
                }
                manager.WaitExitThenApplyUpdates(pending, silent: false, restart: true);
                return 0; // Velopack waits for this helper too, then replaces the closed installation.
            }
            var staged = manager.UpdatePendingRestart;
            if (request.command == "check" && staged != null)
            { status.Write("downloaded", "Update downloaded. Save and restart when ready.", staged.Version.ToString()); return 0; }
            status.Write("checking", "Checking GitHub Releases…");
            var update = await manager.CheckForUpdatesAsync().WaitAsync(TimeSpan.FromSeconds(45));
            if (update == null) { status.Write("latest", "You have the latest published playtest.", manager.CurrentVersion?.ToString()); return 0; }
            string version = update.TargetFullRelease.Version.ToString();
            if (request.command == "check")
            { status.Write("available", "A new playtest is available. Download it while you play.", version, bytes: update.TargetFullRelease.Size); return 0; }
            if (version != request.version) throw new InvalidOperationException("The published release changed. Check for updates again before downloading.");
            using var downloadTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
            await manager.DownloadUpdatesAsync(update, p => status.Write("downloading", "Downloading and verifying update…", version, p), downloadTimeout.Token);
            status.Write("downloaded", "Update verified. Save and restart when ready.", version); return 0;
        }
        catch (Exception error) { status.Write("error", "Update failed: " + error.Message); return 1; }
    }
    internal static void ValidateRequest(UpdateRequest request)
    {
        if (request.command is not ("check" or "download" or "apply") || string.IsNullOrWhiteSpace(request.statusPath)
            || !Path.IsPathFullyQualified(request.statusPath) || !Guid.TryParseExact(request.operationId, "N", out _)) throw new ArgumentException("Invalid update request.");
        if (request.command != "check" && !SemanticVersion.TryParse(request.version, out _)) throw new ArgumentException("Invalid target version.");
        if (request.command == "apply" && (request.gamePid <= 0 || request.gameStartedUtcTicks <= 0)) throw new ArgumentException("Missing running game identity.");
    }
    private static void ShowError(string message)
    {
        Console.Error.WriteLine(message);
        if (OperatingSystem.IsWindows()) MessageBox(IntPtr.Zero, message, "Alpha Hex", 0x10);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
    internal sealed class StatusWriter(UpdateRequest request)
    {
        private readonly object gate = new();
        public void Write(string phase, string message, string? version = null, int progress = 0, long bytes = 0)
        {
            lock (gate)
            {
                string temporary = request.statusPath + ".pending";
                Directory.CreateDirectory(Path.GetDirectoryName(request.statusPath)!);
                var response = new UpdateResponse { phase = phase, message = message, version = version ?? "", progress = progress,
                    availableBytes = bytes, operationId = request.operationId };
                File.WriteAllText(temporary, JsonSerializer.Serialize(response, Json));
                for (int attempt = 0; ; attempt++)
                {
                    try { File.Move(temporary, request.statusPath, true); return; }
                    catch (IOException) when (attempt < 4) { Thread.Sleep(100); }
                }
            }
        }
    }
}
