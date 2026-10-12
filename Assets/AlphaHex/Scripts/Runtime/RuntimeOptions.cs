using System;
using System.IO;
using UnityEngine;

namespace AlphaHex.Runtime
{
    public static class RuntimeOptions
    {
        private static readonly string[] Arguments = Environment.GetCommandLineArgs();
        // The native installer test inherits this through the updater's replacement/restart.
        public static bool IsUpdateQa => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALPHA_HEX_QA_UPDATE_DIRECTORY"));
        public static bool IsQa => Array.IndexOf(Arguments, "--alpha-qa") >= 0 || IsUpdateQa;
        public static string QaDirectory
        {
            get
            {
                int index = Array.IndexOf(Arguments, "--qa-directory");
                return index >= 0 && index + 1 < Arguments.Length ? Path.GetFullPath(Arguments[index + 1])
                    : IsUpdateQa ? Path.GetFullPath(Environment.GetEnvironmentVariable("ALPHA_HEX_QA_UPDATE_DIRECTORY"))
                    : Path.Combine(Application.temporaryCachePath, "AlphaHex-QA");
            }
        }
    }
}
