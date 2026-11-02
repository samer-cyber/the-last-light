using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// API keys, read when first needed: from an environment variable, or from a secrets.env file
    /// (NAME=value lines) found beside the game or in a folder above it. In the editor that is the
    /// hackathon folder's own file, outside the game's repository. Keys are never logged or saved.
    /// </summary>
    public static class Secrets
    {
        static Dictionary<string, string> file;

        /// <summary>The key's value, or null when it is not set anywhere.</summary>
        public static string Get(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            file ??= Load();
            return file.TryGetValue(name, out value) && value.Length > 0 ? value : null;
        }

        static Dictionary<string, string> Load()
        {
            var values = new Dictionary<string, string>();
            // Assets/ in the editor, <Game>_Data/ in a build: look there and in the two folders above.
            var folder = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 3 && folder != null; i++, folder = folder.Parent)
            {
                string path = Path.Combine(folder.FullName, "secrets.env");
                if (!File.Exists(path)) continue;
                foreach (string line in File.ReadAllLines(path))
                {
                    int equals = line.IndexOf('=');
                    if (equals <= 0 || line.TrimStart().StartsWith("#")) continue;
                    string key = line.Substring(0, equals).Trim();
                    if (!values.ContainsKey(key)) values[key] = line.Substring(equals + 1).Trim().Trim('"', '\'');
                }
                break;
            }
            return values;
        }
    }
}
