using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AlphaHex.Updates;
using Velopack;
using Velopack.Locators;

internal static class LauncherChecks
{
    public static async Task<int> Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "alphahex-updater-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var request = new UpdateRequest { command = "check", operationId = Guid.NewGuid().ToString("N"), statusPath = Path.Combine(root, "status.json") };
            Program.ValidateRequest(request); var writer = new Program.StatusWriter(request); writer.Write("available", "Ready", "0.2.0", bytes: 1024);
            var status = JsonSerializer.Deserialize<UpdateResponse>(File.ReadAllText(request.statusPath), Program.Json)!;
            Require(status.operationId == request.operationId && status.availableBytes == 1024, "Status protocol lost data.");
            request.command = "apply"; request.version = "0.2.0";
            bool rejected = false; try { Program.ValidateRequest(request); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "Apply accepted without a live game identity.");
            Require(UpdateArguments.Quote(@"C:\Games With Spaces\request.json") == "\"C:\\Games With Spaces\\request.json\"", "Windows path quoting changed separators.");
            string feed = Path.Combine(root, "feed"), cache = Path.Combine(root, "packages"), app = Path.Combine(root, "current");
            Directory.CreateDirectory(feed); Directory.CreateDirectory(cache); Directory.CreateDirectory(app);
            var locator = new TestVelopackLocator(UpdateFeed.PackageId, "0.1.0", cache, app, root, Path.Combine(root, "Update.exe"), UpdateFeed.Channel, null, null, Path.Combine(app, "AlphaHex.exe"));
            var manager = new UpdateManager(feed, new UpdateOptions { ExplicitChannel = UpdateFeed.Channel, AllowVersionDowngrade = false }, locator);
            Require(manager.IsInstalled, "Test install was not recognized.");
            string package = CreatePackage(feed, "0.2.0"); WriteFeed(feed, package, "0.2.0");
            var update = await manager.CheckForUpdatesAsync(); Require(update != null && update.TargetFullRelease.Version.ToString() == "0.2.0", "Release selection failed.");
            await manager.DownloadUpdatesAsync(update!);
            string downloaded = Path.Combine(cache, Path.GetFileName(package)); Require(File.Exists(downloaded), "No staged package.");
            Require(SHA256.HashData(File.ReadAllBytes(downloaded)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(package))), "Downloaded bytes differ.");
            Require(manager.UpdatePendingRestart?.Version.ToString() == "0.2.0", "Downloaded update is not restartable.");
            package = CreatePackage(feed, "0.3.0"); WriteFeed(feed, package, "0.3.0", corruptHash: true);
            update = await manager.CheckForUpdatesAsync(); rejected = false;
            try { await manager.DownloadUpdatesAsync(update!); } catch { rejected = true; }
            Require(rejected, "Corrupt package passed checksum verification.");
            package = CreatePackage(feed, "0.0.1"); WriteFeed(feed, package, "0.0.1");
            Require(await manager.CheckForUpdatesAsync() == null, "Updater allowed an unintended downgrade.");
            Console.WriteLine("PASS update protocol, Windows path quoting, release selection, real package download, checksum rejection, pending restart and downgrade prevention.");
            Console.WriteLine("Installer replacement/relaunch requires the separate native Windows integration check.");
            return 0;
        }
        finally { Directory.Delete(root, true); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string CreatePackage(string directory, string version)
    {
        string path = Path.Combine(directory, $"AlphaHex-{version}-full.nupkg");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(zip.CreateEntry("AlphaHex.nuspec").Open()))
            writer.Write($"<?xml version=\"1.0\"?><package><metadata><id>AlphaHex</id><version>{version}</version><authors>Alpha Hex</authors><description>Updater test fixture</description><channel>{UpdateFeed.Channel}</channel></metadata></package>");
        using (var writer = new StreamWriter(zip.CreateEntry("lib/net45/fixture.txt").Open())) writer.Write("Synthetic updater fixture " + version);
        return path;
    }
    private static void WriteFeed(string directory, string package, string version, bool corruptHash = false)
    {
        byte[] bytes = File.ReadAllBytes(package);
        var feed = new { Assets = new[] { new { PackageId = UpdateFeed.PackageId, Version = version, Type = "Full", FileName = Path.GetFileName(package),
            SHA1 = Convert.ToHexString(SHA1.HashData(bytes)), SHA256 = corruptHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes)), Size = bytes.LongLength } } };
        File.WriteAllText(Path.Combine(directory, "releases." + UpdateFeed.Channel + ".json"), JsonSerializer.Serialize(feed));
    }
}
