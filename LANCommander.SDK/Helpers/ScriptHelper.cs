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
            var tempPath = Path.GetTempFileName();

            // PowerShell will only run scripts with the .ps1 file extension
            File.Move(tempPath, tempPath + ".ps1");

            Logger?.LogTrace("Writing script {Script} to {Destination}", script.Name, tempPath);

            File.WriteAllText(tempPath, script.Contents);

            return tempPath;
        }

        public static void SaveScript(Game game, ScriptType type)
        {
            var script = game.Scripts.FirstOrDefault(s => s.Type == type);

            if (script == null)
                return;

            if (script.RequiresAdmin)
                script.Contents = "# Requires Admin" + "\r\n\r\n" + script.Contents;

            var filename = PowerShellRuntime.GetScriptFilePath(game, type);

            if (File.Exists(filename))
                File.Delete(filename);

            Logger?.LogTrace("Writing {ScriptType} script to {Destination}", type, filename);

            File.WriteAllText(filename, script.Contents);
        }
    }
}
