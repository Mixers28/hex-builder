using System;
using System.Text;

namespace AlphaHex.Updates
{
    public static class UpdateFeed
    {
        public const string Repository = "https://github.com/Mixers28/hex-builder";
        public const string Channel = "win-playtest";
        public const string PackageId = "AlphaHex";
    }
    public static class UpdateArguments
    {
        public static string Quote(string value)
        {
            var result = new StringBuilder(); result.Append('"'); int slashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { slashes++; continue; }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character); slashes = 0;
            }
            result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
        }
    }
    [Serializable] public sealed class UpdateRequest
    {
        public string command = "", version = "", statusPath = "", operationId = "";
        public int gamePid, launcherPid;
        public long gameStartedUtcTicks;
    }
    [Serializable] public sealed class UpdateResponse
    {
        public string phase = "", message = "", version = "", operationId = "";
        public int progress;
        public long availableBytes;
    }
}
