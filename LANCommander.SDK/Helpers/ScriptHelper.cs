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

        public static Task SaveScriptAsync(Game game, ScriptType type)
        {
            return SaveScriptAsync(game, type, game.InstallDirectory);
        }
        
        public static async Task SaveScriptAsync(Game game, ScriptType type, string installDirectory)
        {
            var scriptContents = GetScriptContents(game, type);

            if (!String.IsNullOrWhiteSpace(scriptContents))
            {
                var filename = GetScriptFilePath(installDirectory, game.Id, type);

                if (!Directory.Exists(Path.GetDirectoryName(filename)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filename));

                if (File.Exists(filename))
                    File.Delete(filename);

                Logger?.LogTrace("Writing {ScriptType} script to {Destination}", type, filename);

                await File.WriteAllTextAsync(filename, scriptContents);
            }
        }

        public static async Task SaveScriptAsync(Game game, Redistributable redistributable, ScriptType type)
        {
            var scriptContents = GetScriptContents(redistributable, type);

            if (!String.IsNullOrWhiteSpace(scriptContents))
            {
                var fileName = GetScriptFilePath(game.InstallDirectory, redistributable.Id, type);

                if (!Directory.Exists(Path.GetDirectoryName(fileName)))
                    Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                
                if (File.Exists(fileName))
                    File.Delete(fileName);
                
                Logger?.LogTrace("Writing {ScriptType} script to {Destination}", type, fileName);

                await File.WriteAllTextAsync(fileName, scriptContents);
            }
        }
        
        public static async Task SaveScriptAsync(Tool tool, ScriptType type, string installDirectory)
        {
            var scriptContents = GetScriptContents(tool, type);

            if (!String.IsNullOrWhiteSpace(scriptContents))
            {
                var filename = GetScriptFilePath(installDirectory, tool.Id, type);

                if (!Directory.Exists(Path.GetDirectoryName(filename)))
                    Directory.CreateDirectory(Path.GetDirectoryName(filename));

                if (File.Exists(filename))
                    File.Delete(filename);

                Logger?.LogTrace("Writing {ScriptType} script to {Destination}", type, filename);

                await File.WriteAllTextAsync(filename, scriptContents);
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

        public static string GetScriptContents(Redistributable redistributable, ScriptType type)
        {
            var script = redistributable.Scripts.FirstOrDefault(s => s.Type == type);

            if (script == null)
                return String.Empty;
            
            if (script.RequiresAdmin)
                script.Contents = "# Requires Admin" + "\r\n\r\n" + script.Contents;

            return script.Contents;
        }
        
        public static string GetScriptContents(Tool tool, ScriptType type)
        {
            var script = tool.Scripts.FirstOrDefault(s => s.Type == type);

            if (script == null)
                return String.Empty;
            
            if (script.RequiresAdmin)
                script.Contents = "# Requires Admin" + "\r\n\r\n" + script.Contents;

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

        public static string GetScriptFileName(ScriptType type)
        {
            Dictionary<ScriptType, string> filenames = new Dictionary<ScriptType, string>() {
                { ScriptType.Install, "Install.ps1" },
                { ScriptType.Uninstall, "Uninstall.ps1" },
                { ScriptType.NameChange, "ChangeName.ps1" },
                { ScriptType.KeyChange, "ChangeKey.ps1" },
                { ScriptType.DetectInstall, "DetectInstall.ps1" },
                { ScriptType.BeforeStart, "BeforeStart.ps1" },
                { ScriptType.AfterStop, "AfterStop.ps1" }
            };

            return filenames[type];
        }
    }
}
