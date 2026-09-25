using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Helpers
{
    public static class ScriptHelper
    {
        public static readonly ILogger Logger;

        public static async Task<string> SaveTempScriptAsync(Script script)
        {
            var tempPath = await SaveTempScriptAsync(script.Contents);

            Logger?.LogTrace("Wrote script {Script} to {Destination}", script.Name, tempPath);

            return tempPath;
        }

        public static async Task<string> SaveTempScriptAsync(string contents)
        {
            var tempPath = Path.GetTempFileName();

            // PowerShell will only run scripts with the .ps1 file extension
            File.Move(tempPath, tempPath + ".ps1");

            tempPath = tempPath + ".ps1";

            await File.WriteAllTextAsync(tempPath, contents);

            return tempPath;
        }
        
        public static async Task SaveScriptAsync(Game game, Script script, string installDirectory)
        {
            var scriptContents = GetScriptContents(script);

            if (!String.IsNullOrWhiteSpace(scriptContents))
            {
                var filename = GetScriptFilePath(installDirectory, game.Id, script.Type);

                if (!Directory.Exists(Path.GetDirectoryName(filename)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filename));

                if (File.Exists(filename))
                    File.Delete(filename);

                Logger?.LogTrace("Writing {ScriptType} script to {Destination}", script.Type, filename);

                await File.WriteAllTextAsync(filename, scriptContents);
            }
        }

        public static async Task SaveScriptAsync(Game game, Redistributable redistributable, Script script)
        {
            var scriptContents = GetScriptContents(script);

            if (!String.IsNullOrWhiteSpace(scriptContents))
            {
                var fileName = GetScriptFilePath(game.InstallDirectory, redistributable.Id, script.Type);

                if (!Directory.Exists(Path.GetDirectoryName(fileName)))
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                
                if (File.Exists(fileName))
                    File.Delete(fileName);
                
                Logger?.LogTrace("Writing {ScriptType} script to {Destination}", script.Type, fileName);

                await File.WriteAllTextAsync(fileName, scriptContents);
            }
        }
        
        public static async Task SaveScriptAsync(Tool tool, Script script, string installDirectory)
        {
            var scriptContents = GetScriptContents(script);

            if (!String.IsNullOrWhiteSpace(scriptContents))
            {
                var filename = GetScriptFilePath(installDirectory, tool.Id, script.Type);

                if (!Directory.Exists(Path.GetDirectoryName(filename)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filename));

                if (File.Exists(filename))
                    File.Delete(filename);

                Logger?.LogTrace("Writing {ScriptType} script to {Destination}", script.Type, filename);

                await File.WriteAllTextAsync(filename, scriptContents);
            }
        }

        public static string GetScriptContents(Script script)
        {
            if (script == null)
                return String.Empty;

            if (script.RequiresAdmin)
                script.Contents = "#Requires -RunAsAdministrator" + "\r\n\r\n" + script.Contents;

            return script.Contents;
        }

        public static string GetScriptFilePath(string installDirectory, Guid id, ScriptType type)
        {
            return GetScriptFilePath(installDirectory, id.ToString(), type);
        }

        public static string GetScriptFilePath(string installDirectory, string id, ScriptType type)
        {
            var filename = GetScriptFileName(type);

            return Path.Combine(installDirectory, ".lancommander", id, filename);
        }

        private static readonly Dictionary<ScriptType, string> ScriptFileNames = new Dictionary<ScriptType, string>() {
            { ScriptType.Install, "Install.ps1" },
            { ScriptType.Uninstall, "Uninstall.ps1" },
            { ScriptType.NameChange, "ChangeName.ps1" },
            { ScriptType.KeyChange, "ChangeKey.ps1" },
            { ScriptType.DetectInstall, "DetectInstall.ps1" },
            { ScriptType.BeforeStart, "BeforeStart.ps1" },
            { ScriptType.AfterStop, "AfterStop.ps1" },
            { ScriptType.Package, "Package.ps1" },
            { ScriptType.RunWrapper, "RunWrapper.ps1" }
        };

        private const string RequiresAdminHeader = "#Requires -RunAsAdministrator" + "\r\n\r\n";

        public static string GetScriptFileName(ScriptType type)
        {
            return ScriptFileNames[type];
        }

        /// <summary>
        /// The inverse of <see cref="GetScriptFilePath(string, Guid, ScriptType)"/>: recognises
        /// <c>&lt;installDirectory&gt;/.lancommander/&lt;id&gt;/&lt;Type&gt;.ps1</c> and extracts its parts.
        /// </summary>
        public static bool TryParseScriptFilePath(string path, out string installDirectory, out Guid ownerId, out ScriptType type)
        {
            installDirectory = null;
            ownerId = Guid.Empty;
            type = default;

            if (String.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var fileName = Path.GetFileName(path);
                var match = ScriptFileNames.FirstOrDefault(f => String.Equals(f.Value, fileName, StringComparison.OrdinalIgnoreCase));

                if (match.Value == null)
                    return false;

                var ownerDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
                var metadataDirectory = Path.GetDirectoryName(ownerDirectory);

                if (!Guid.TryParse(Path.GetFileName(ownerDirectory), out ownerId))
                    return false;

                if (!String.Equals(Path.GetFileName(metadataDirectory), ".lancommander", StringComparison.OrdinalIgnoreCase))
                    return false;

                installDirectory = Path.GetDirectoryName(metadataDirectory);
                type = match.Key;

                return installDirectory != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Removes the <c>#Requires -RunAsAdministrator</c> header that <see cref="GetScriptContents"/>
        /// prepends when a script is written to disk, so contents read back from an install can be sent to
        /// the server (where the flag is stored separately) without the header piling up.
        /// </summary>
        public static string StripRequiresAdminHeader(string contents)
        {
            if (String.IsNullOrEmpty(contents))
                return contents ?? String.Empty;

            while (contents.StartsWith(RequiresAdminHeader, StringComparison.Ordinal))
                contents = contents.Substring(RequiresAdminHeader.Length);

            return contents;
        }

        /// <summary>A stable hash of script contents, used to detect concurrent edits of a server script.</summary>
        public static string HashContents(string contents)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(contents ?? String.Empty));

            return Convert.ToHexString(bytes);
        }
    }
}
