using System;
using System.IO;
using System.Text;
using AlphaHex.Core;
using UnityEngine;

namespace AlphaHex.Runtime
{
    /// <summary>Local versioned save data. Never replace a running world until validation succeeds.</summary>
    public static class SaveService
    {
        public static string SavePath => Path.Combine(Application.persistentDataPath, "alpha-hex-save.json");
        public static string PreviousPath => Path.Combine(Application.persistentDataPath, "alpha-hex-previous-island.json");
        private const long MaxSaveBytes = 4 * 1024 * 1024;

        public static bool TryArchive(WorldState state, out string message)
        {
            try
            {
                new Simulation(state);
                Directory.CreateDirectory(Application.persistentDataPath);
                string temporary = PreviousPath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(state, true), new UTF8Encoding(false));
                if (File.Exists(PreviousPath))
                {
                    try { File.Replace(temporary, PreviousPath, null); }
                    catch (PlatformNotSupportedException) { File.Copy(temporary, PreviousPath, true); File.Delete(temporary); }
                    catch (IOException) { File.Copy(temporary, PreviousPath, true); File.Delete(temporary); }
                }
                else File.Move(temporary, PreviousPath);
                message = "Previous island archived.";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                               exception is ArgumentException || exception is InvalidOperationException)
            {
                message = "Archive failed: " + exception.Message;
                return false;
            }
        }

        public static bool TryLoadPrevious(out Simulation simulation, out string message)
        {
            bool success = TryRead(PreviousPath, out simulation, out string error);
            message = success ? "Previous island restored." : "No readable previous island: " + error;
            return success;
        }

        public static bool TrySave(WorldState state, out string message)
        {
            string temporary = SavePath + ".tmp";
            try
            {
                // Validate the same plain data that will be written; no engine objects are serialized.
                new Simulation(state);
                string json = JsonUtility.ToJson(state, true);
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (!File.Exists(SavePath))
                {
                    File.Move(temporary, SavePath);
                }
                else
                {
                    bool primaryIsValid = TryRead(SavePath, out _, out _);
                    string backup = primaryIsValid ? SavePath + ".bak" : null;
                    try
                    {
                        File.Replace(temporary, SavePath, backup);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithBackup(temporary, backup);
                    }
                    catch (IOException)
                    {
                        // Some target filesystems don't support File.Replace; retain a valid backup.
                        ReplaceWithBackup(temporary, backup);
                    }
                }
                message = "Settlement saved.";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                               exception is ArgumentException || exception is InvalidOperationException)
            {
                message = "Save failed: " + exception.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void ReplaceWithBackup(string temporary, string backup)
        {
            if (backup != null) File.Copy(SavePath, backup, true);
            File.Copy(temporary, SavePath, true);
            File.Delete(temporary);
        }

        public static bool TryLoad(out Simulation simulation, out string message)
        {
            if (TryRead(SavePath, out simulation, out string primaryError))
            {
                message = "Settlement loaded.";
                return true;
            }
            if (TryRead(SavePath + ".bak", out simulation, out _))
            {
                message = "Recovered the previous saved settlement.";
                return true;
            }
            message = File.Exists(SavePath) ? "Save could not be loaded: " + primaryError : "No saved settlement yet.";
            return false;
        }

        private static bool TryRead(string path, out Simulation simulation, out string error)
        {
            simulation = null;
            error = "File not found.";
            if (!File.Exists(path)) return false;
            try
            {
                var info = new FileInfo(path);
                if (info.Length == 0 || info.Length > MaxSaveBytes)
                    throw new ArgumentException("The save size is invalid.");
                string json = File.ReadAllText(path, Encoding.UTF8);
                WorldState state = JsonUtility.FromJson<WorldState>(json);
                if (state == null) throw new ArgumentException("The save contains no world.");
                simulation = new Simulation(state);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                               exception is ArgumentException || exception is InvalidOperationException)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
