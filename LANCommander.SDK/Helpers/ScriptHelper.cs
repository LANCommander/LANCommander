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
            var script = game.Scripts.FirstOrDefault(s => s.Type == type);

            if (script == null)
                return;

            if (script.RequiresAdmin)
                script.Contents = "# Requires Admin" + "\r\n\r\n" + script.Contents;

            var filename = GetScriptFilePath(game, type);

            if (File.Exists(filename))
                File.Delete(filename);

            Logger?.LogTrace("Writing {ScriptType} script to {Destination}", type, filename);

            File.WriteAllText(filename, script.Contents);
        }

        public static string GetScriptFilePath(Game game, ScriptType type)
        {
            return GetScriptFilePath(game.InstallDirectory, type);
        }

        public static string GetScriptFilePath(string installDirectory, ScriptType type)
        {
            Dictionary<ScriptType, string> filenames = new Dictionary<ScriptType, string>() {
                { ScriptType.Install, "_install.ps1" },
                { ScriptType.Uninstall, "_uninstall.ps1" },
                { ScriptType.NameChange, "_changename.ps1" },
                { ScriptType.KeyChange, "_changekey.ps1" }
            };

            var filename = filenames[type];

            return Path.Combine(installDirectory, filename);
        }
    }
}
