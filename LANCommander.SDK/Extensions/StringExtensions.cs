using System.Collections;
using System;
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Core.Tokens;
using static System.Environment;
using System.Collections.Generic;

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
            
            if (filename.EndsWith('.'))
                filename = filename.Substring(0, filename.Length - 1);

            return filename;
        }

        static readonly Regex LeadingArticleExpression = new Regex(@"^(?:a|an|the)\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// Returns the key a title should be sorted by: leading whitespace and a leading English
        /// article ("The", "A", "An") are dropped, so "The Witcher" sorts under W.
        /// </summary>
        public static string ToSortableTitle(this string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            title = title.TrimStart();

            var stripped = LeadingArticleExpression.Replace(title, "");

            // A title that is only an article ("The ", "A  ") keeps its original text
            return stripped.Length > 0 ? stripped : title.TrimEnd();
        }

        /// <summary>
        /// Picks the explicit sort title when set, otherwise the title, and normalizes it with
        /// <see cref="ToSortableTitle(string?)"/>.
        /// </summary>
        public static string ToSortableTitle(string? sortTitle, string? title)
            => (string.IsNullOrWhiteSpace(sortTitle) ? title : sortTitle).ToSortableTitle();

        internal static bool ContainsIgnoreCase(this string input, string search)
        {
            return input.IndexOf(search, StringComparison.OrdinalIgnoreCase) != -1;
        }

        public static string ExpandEnvironmentVariables(this string path, string installDirectory, bool skipSlashes = false)
        {
            path = path.Replace("{InstallDir}", installDirectory);

            if (!skipSlashes)
                path = path.Replace('/', Path.DirectorySeparatorChar);

            SpecialFolder[] supportedSpecialFolders = new SpecialFolder[]
            {
                SpecialFolder.CommonApplicationData,
                SpecialFolder.CommonDesktopDirectory,
                SpecialFolder.CommonDocuments,
                SpecialFolder.CommonMusic,
                SpecialFolder.CommonPictures,
                SpecialFolder.CommonPrograms,
                SpecialFolder.CommonStartMenu,
                SpecialFolder.CommonStartup,
                SpecialFolder.CommonVideos,
                SpecialFolder.Desktop,
                SpecialFolder.Fonts,
                SpecialFolder.MyDocuments,
                SpecialFolder.MyMusic,
                SpecialFolder.MyPictures,
                SpecialFolder.MyVideos,
                SpecialFolder.StartMenu
            };

            foreach (SpecialFolder folder in supportedSpecialFolders)
            {
                path = Regex.Replace(path, $"%{folder}%", Environment.GetFolderPath(folder));
            }

            path = Environment.ExpandEnvironmentVariables(path);

            return path.TrimEnd(Path.DirectorySeparatorChar);
        }

        public static string DeflateEnvironmentVariables(this string path, string installDirectory)
        {
            path = path.Replace(installDirectory.TrimEnd(Path.DirectorySeparatorChar), "{InstallDir}");

            SpecialFolder[] supportedSpecialFolders = new SpecialFolder[]
            {
                SpecialFolder.CommonApplicationData,
                SpecialFolder.CommonDesktopDirectory,
                SpecialFolder.CommonDocuments,
                SpecialFolder.CommonMusic,
                SpecialFolder.CommonPictures,
                SpecialFolder.CommonPrograms,
                SpecialFolder.CommonStartMenu,
                SpecialFolder.CommonStartup,
                SpecialFolder.CommonVideos,
                SpecialFolder.Desktop,
                SpecialFolder.Fonts,
                SpecialFolder.MyDocuments,
                SpecialFolder.MyMusic,
                SpecialFolder.MyPictures,
                SpecialFolder.MyVideos,
                SpecialFolder.StartMenu
            };

            foreach (SpecialFolder folder in supportedSpecialFolders)
            {
                var value = (string)(Environment.GetFolderPath(folder));

                // Folders that don't exist on this platform (most of them on Linux) resolve to an empty string
                if (string.IsNullOrEmpty(value))
                    continue;

                path = Regex.Replace(path, Regex.Escape(value), $"%{folder}%", RegexOptions.IgnoreCase);
            }

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

                // Unset outside of Windows, and Regex.Escape throws on null
                if (string.IsNullOrEmpty(value))
                    continue;

                path = Regex.Replace(path, Regex.Escape(value), $"%{variable}%", RegexOptions.IgnoreCase);
            }

            return path;
        }

        public static string FastReverse(this string input)
        {
            return string.Create(input.Length, input, (chars, state) => {
                var pos = 0;
                for (int i = state.Length -1 ; i >=0 ; i--)
                {
                    chars[pos++] = state[i];
                }
            });
        }
    }
}
