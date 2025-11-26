using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Serilog;

namespace LANCommander.SDK.PowerShell.Extensions;

public static class InitialSessionStateExtensions
{
    public static void RegisterCustomCmdlets(this InitialSessionState initialSessionState)
    {
        var assembly = Assembly.GetEntryAssembly()!;
        var cmdlets = assembly.ExportedTypes.Where(t => t.IsSubclassOf(typeof(Cmdlet)) && !t.IsAbstract);

        foreach (var cmdlet in cmdlets)
        {
            try
            {
                var cmdletAttribute = cmdlet.GetCustomAttribute<CmdletAttribute>()!;

                initialSessionState.Commands.Add(
                    new SessionStateCmdletEntry($"{cmdletAttribute.VerbName}-{cmdletAttribute.NounName}", cmdlet,
                        null));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not register cmdlet of type {CmdletType}", cmdlet.FullName);
            }
        }
    }
}