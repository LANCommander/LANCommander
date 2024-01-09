using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LANCommander.SDK.Helpers
{
    public static class ScriptHelper
    {
        public static readonly ILogger Logger;

        public static string SaveTempScript(Script script)
        {
            var tempPath = SaveTempScript(script.Contents);

            Logger?.LogTrace("Wrote script {Script} to {Destination}", script.Name, tempPath);

            return tempPath;
        }

        public static string SaveTempScript(string contents)
        {
            var tempPath = Path.GetTempFileName();

            // PowerShell will only run scripts with the .ps1 file extension
            File.Move(tempPath, tempPath + ".ps1");

            tempPath = tempPath + ".ps1";

            File.WriteAllText(tempPath, contents);

            return tempPath;
        }

        public static void SaveScript(Game game, ScriptType type)
        {
            var scriptContents = GetScriptContents(game, type);

            if (!String.IsNullOrWhiteSpace(scriptContents))
            {
                var filename = GetScriptFilePath(game.InstallDirectory, game.Id, type);

                if (!Directory.Exists(Path.GetDirectoryName(filename)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filename));

                if (File.Exists(filename))
                    File.Delete(filename);

                Logger?.LogTrace("Writing {ScriptType} script to {Destination}", type, filename);

                File.WriteAllText(filename, scriptContents);
            }
        }

        public static string GetScriptContents(Game game, ScriptType type)
        {
            var script = game.Scripts.FirstOrDefault(s => s.Type == type);

            if (script == null)
                return String.Empty;

            if (script.RequiresAdmin)
                script.Contents = "# Requires Admin" + "\r\n\r\n" + script.Contents;

            return script.Contents;
        }

        public static string GetScriptFilePath(string installDirectory, Guid gameId, ScriptType type)
        {
            return GetScriptFilePath(installDirectory, gameId.ToString(), type);
        }

        public static string GetScriptFilePath(string installDirectory, string gameId, ScriptType type)
        {
            var filename = GetScriptFileName(type);

            return Path.Combine(installDirectory, ".lancommander", gameId, filename);
        }

        public static string GetScriptFileName(ScriptType type)
        {
            Dictionary<ScriptType, string> filenames = new Dictionary<ScriptType, string>() {
                { ScriptType.Install, "Install.ps1" },
                { ScriptType.Uninstall, "Uninstall.ps1" },
                { ScriptType.NameChange, "ChangeName.ps1" },
                { ScriptType.KeyChange, "ChangeKey.ps1" }
            };

            return filenames[type];
        }
    }
}
