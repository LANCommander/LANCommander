using System.Collections;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace LANCommander.SDK.Extensions
{
    public static class StringExtensions
    {
        public static string SanitizeFilename(this string filename, string replacement = "")
        {
            var colonInTitle = new Regex(@"(\w)(: )(\w)");
            var removeInvalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

            filename = colonInTitle.Replace(filename, "$1 - $3");
            filename = removeInvalidChars.Replace(filename, replacement);

            return filename;
        }

        internal static bool ContainsIgnoreCase(this string input, string search)
        {
            return input.IndexOf(search, StringComparison.OrdinalIgnoreCase) != -1;
        }

        internal static string DeflateEnvironmentVariables(this string path, string installDirectory)
        {
            path = path.Replace(installDirectory.TrimEnd(Path.DirectorySeparatorChar), "{InstallDir}");

            // These are the ones we're going to support. They are ordered in likeliness they'll be used.
            // These paths get complicated, but we'll do our best.
            string[] supportedVariables = new string[]
            {
                "TEMP",
                "TMP",
                "LOCALAPPDATA",
                "APPDATA",
                "USERPROFILE",
                "PUBLIC",
                "ProgramData",
                "CommonProgramFiles(x86)",
                "CommonProgramFiles",
                "ProgramFiles(x86)",
                "ProgramFiles",
                "SystemRoot",
                "windir",
                "USERNAME",
                "SystemDrive"
            };

            foreach (var variable in supportedVariables)
            {
                var value = (string)(Environment.GetEnvironmentVariable(variable));

                path = Regex.Replace(path, Regex.Escape(value), $"%{variable}%", RegexOptions.IgnoreCase);
            }

            DictionaryEntry currentEntry = new DictionaryEntry("", "");

            foreach (object key in Environment.GetEnvironmentVariables().Keys)
            {
                string value = (string)Environment.GetEnvironmentVariables()[key];

                if (path.Contains(value) && value.Length > ((string)currentEntry.Value).Length)
                {
                    currentEntry.Key = (string)key;
                    currentEntry.Value = value;
                }
            }

            return path;
        }
    }
}
