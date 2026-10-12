using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlphaHex.Runtime
{
    internal static class SaveShape
    {
        [Serializable] private sealed class Key { public string name = ""; }
        // JsonUtility supplies defaults for missing objects. Check actual root keys before deserializing.
        // This scanner only extracts keys; JsonUtility still parses and validates the complete JSON.
        internal static HashSet<string> RootFields(string json)
        {
            var fields = new HashSet<string>(StringComparer.Ordinal); int depth = 0;
            for (int index = 0; index < json.Length; index++)
            {
                char character = json[index];
                if (character == '{' || character == '[') depth++;
                else if (character == '}' || character == ']') depth--;
                else if (character == '"')
                {
                    int start = index;
                    for (index++; index < json.Length; index++)
                    {
                        if (json[index] == '\\') { index++; continue; }
                        if (json[index] == '"') break;
                    }
                    if (index >= json.Length) throw new ArgumentException("Unterminated JSON string.");
                    int next = index + 1; while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                    if (depth != 1 || next >= json.Length || json[next] != ':') continue;
                    string token = json.Substring(start, index - start + 1);
                    string key = JsonUtility.FromJson<Key>("{\"name\":" + token + "}").name;
                    if (!fields.Add(key)) throw new ArgumentException("Duplicate save field: " + key);
                }
            }
            return fields;
        }
        internal static void Require(HashSet<string> fields, params string[] names)
        {
            foreach (string name in names) if (!fields.Contains(name)) throw new ArgumentException("The save is missing " + name + ".");
        }
    }
}
