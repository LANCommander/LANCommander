using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    internal class PowerShellRuntime
    {
        public void RunScript(string script, Dictionary<string, object> parameters)
        {
            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript(script);
                ps.AddParameters(parameters);

                var output = ps.Invoke();
            }
        }

        public void RunInstallScript(Game game)
        {
            var defaultParameters = new Dictionary<string, object>()
            {
                { "InstallDir", game.InstallDirectory },
                { "Title", game.Name },
                { "Description", game.Description },
                { "GameId", game.GameId }
            };

            var scriptPath = Path.Combine(game.InstallDirectory, "_install.ps1");

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException(scriptPath);

            var scriptContents = File.ReadAllText(scriptPath);

            RunScript(scriptPath, defaultParameters);
        }
    }
}
