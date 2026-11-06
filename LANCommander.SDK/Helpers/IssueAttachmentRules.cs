using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Helpers
{
    /// <summary>
    /// What may be attached to an issue. The file types are fixed; the count and size limits are the
    /// server's (<see cref="IssueAttachmentLimits"/>). The server enforces these; clients check them
    /// first so the user hears about a problem before anything is uploaded.
    /// </summary>
    public static class IssueAttachmentRules
    {
        /// <summary>Shown inline by browsers and the launcher.</summary>
        private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".bmp"] = "image/bmp",
        };

        /// <summary>Logs and config files. Always served as plain text, so markup in them is never rendered.</summary>
        private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".json", ".xml", ".yml", ".yaml", ".ini", ".cfg", ".conf", ".csv",
        };

        /// <summary>Archives of several logs, and crash dumps. Only ever downloaded.</summary>
        private static readonly HashSet<string> OtherExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".7z", ".dmp",
        };

        public static IEnumerable<string> AllowedExtensions =>
            ImageTypes.Keys.Concat(TextExtensions).Concat(OtherExtensions);

        public static bool IsAllowed(string fileName) =>
            AllowedExtensions.Contains(Path.GetExtension(fileName ?? string.Empty), StringComparer.OrdinalIgnoreCase);

        public static bool IsImage(string fileName) =>
            ImageTypes.ContainsKey(Path.GetExtension(fileName ?? string.Empty));

        /// <summary>
        /// The content type to serve a file with, decided by its extension rather than by whatever the
        /// uploader claimed.
        /// </summary>
        public static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty);

            if (ImageTypes.TryGetValue(extension, out var imageType))
                return imageType;

            if (TextExtensions.Contains(extension))
                return "text/plain; charset=utf-8";

            return "application/octet-stream";
        }

        /// <summary>
        /// Checks a set of files before upload. Returns null if they can all be attached, otherwise a
        /// message for the user.
        /// </summary>
        public static string Validate(IReadOnlyCollection<(string FileName, long Size)> files, IssueAttachmentLimits limits)
        {
            limits ??= IssueAttachmentLimits.Default;

            if (files.Count > 0 && !limits.Enabled)
                return "This server doesn't accept attachments.";

            if (files.Count > limits.MaxFiles)
                return limits.MaxFiles == 1 ? "You can attach 1 file." : $"You can attach up to {limits.MaxFiles} files.";

            foreach (var (fileName, size) in files)
            {
                if (!IsAllowed(fileName))
                    return $"{fileName} can't be attached. Attach screenshots, logs, text files, .zip or .7z archives, or crash dumps.";

                if (size > limits.MaxFileSize)
                    return $"{fileName} is larger than {FormatSize(limits.MaxFileSize)}.";

                if (size == 0)
                    return $"{fileName} is empty.";
            }

            return null;
        }

        /// <summary>A file size limit in the units the settings page uses: whole megabytes where it can.</summary>
        public static string FormatSize(long bytes) =>
            bytes % (1024 * 1024) == 0 ? $"{bytes / (1024 * 1024)} MB" : $"{bytes / 1024.0 / 1024.0:0.#} MB";
    }
}
