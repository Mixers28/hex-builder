using System;
using System.IO;
using System.Text;
using AlphaHex.Core;
using UnityEngine;

namespace AlphaHex.Runtime
{
    /// <summary>Validated local saves. Import reads the source; replacement is a separate explicit action.</summary>
    public static class SaveService
    {
        public static string DirectoryPath => RuntimeOptions.IsQa ? RuntimeOptions.QaDirectory : Application.persistentDataPath;
        public static string SavePath => Path.Combine(DirectoryPath, "alpha-hex-save.json");
        public static string PreviousPath => Path.Combine(DirectoryPath, "alpha-hex-previous-island.json");
        public static string GodotPath
        {
            get
            {
                string root = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
                    : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, "AlphaHexFrontier", "frontier.json");
            }
        }
        private const long MaxSaveBytes = 8 * 1024 * 1024;
        [Serializable] private sealed class SaveHeader { public int SchemaVersion = 0, schema = 0; }

        public static bool TryArchive(WorldState state, out string message) => TrySaveAt(state, PreviousPath, out message);
        public static bool TrySave(WorldState state, out string message) => TrySaveAt(state, SavePath, out message);
        public static bool TryLoad(out Simulation simulation, out string message) => TryLoadAt(SavePath, out simulation, out message);
        public static bool TryLoadPrevious(out Simulation simulation, out string message) => TryLoadAt(PreviousPath, out simulation, out message);

        public static bool TryLoadAt(string path, out Simulation simulation, out string message)
        {
            if (TryRead(path, out simulation, out string primaryError)) { message = "Settlement loaded."; return true; }
            if (TryRead(path + ".bak", out simulation, out _)) { message = "Recovered the previous valid save."; return true; }
            message = "Could not load settlement: " + primaryError; return false;
        }
        public static bool TrySaveAt(WorldState state, string path, out string message)
        {
            string temporary = path + ".pending";
            try
            {
                new Simulation(state);
                string json = JsonUtility.ToJson(state, true);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] data = new UTF8Encoding(false).GetBytes(json); stream.Write(data, 0, data.Length); stream.Flush(true);
                }
                if (!TryRead(temporary, out _, out string error)) throw new IOException("Temporary save verification failed: " + error);
                if (File.Exists(path))
                {
                    string backup = TryRead(path, out _, out _) ? path + ".bak" : null;
                    // Never replace a valid backup with an unreadable primary.
                    File.Replace(temporary, path, backup);
                }
                else File.Move(temporary, path);
                message = "Settlement saved."; return true;
            }
            catch (Exception error) when (Expected(error)) { message = "Save failed: " + error.Message; return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        public static Simulation Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("The save is empty.");
            var fields = SaveShape.RootFields(json);
            var header = new SaveHeader(); JsonUtility.FromJsonOverwrite(json, header);
            if (header == null) throw new ArgumentException("The save contains no world.");
            if (header.schema == 3 && header.SchemaVersion == 0)
            {
                SaveShape.Require(fields, "schema", "seed", "clock", "remainder", "stock", "population", "migration", "meal", "forage", "hungry",
                    "research", "research_job", "research_progress", "shipments", "expansions", "legacy_research_points", "tiles");
                var dto = new GodotSaveRecord(); JsonUtility.FromJsonOverwrite(json, dto); return Simulation.ImportGodot(dto);
            }
            if (header.schema != 0 || header.SchemaVersion != 1 && header.SchemaVersion != 2 && header.SchemaVersion != Simulation.CurrentSchemaVersion)
                throw new ArgumentException("Unsupported save version.");
            SaveShape.Require(fields, "SchemaVersion", "Seed", "ElapsedSeconds", "SimulationSteps", "PendingSeconds", "Stock", "Tiles", "Shipments");
            if (header.SchemaVersion >= 2) SaveShape.Require(fields, "Town");
            return Simulation.Restore(JsonUtility.FromJson<WorldState>(json));
        }
        public static bool TryRead(string path, out Simulation simulation, out string error)
        {
            simulation = null;
            try
            {
                if (!File.Exists(path)) throw new IOException("File not found: " + path);
                long length = new FileInfo(path).Length;
                if (length == 0 || length > MaxSaveBytes) throw new ArgumentException("The save size is invalid.");
                simulation = Decode(File.ReadAllText(path, Encoding.UTF8)); error = null; return true;
            }
            catch (Exception exception) when (Expected(exception)) { error = exception.Message; return false; }
        }
        private static bool Expected(Exception error) => error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException || error is InvalidOperationException || error is NotSupportedException;
    }
}
