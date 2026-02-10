using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Services;

public partial class ScriptClient
{
    public async Task Authentication_RunUserLoginScript(Script loginScript, User user)
    {
        try
        {
            using (var op = logger.BeginOperation("Executing user login script"))
            {
                var script = powerShellScriptFactory.Create(Enums.ScriptType.UserLogin);

                script.AddVariable("User", user);

                script.UseInline(loginScript.Contents);

                try
                {
                    op
                        .Enrich("UserId", user.Id)
                        .Enrich("Username", user.UserName)
                        .Enrich("ScriptId", loginScript.Id)
                        .Enrich("ScriptName", loginScript.Name);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not enrich logs");
                }

                await script.ExecuteAsync<int>();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not execute user login script");
        }
    }
    
    public async Task Authentication_RunUserRegistrationScript(Script registrationScript, User user)
    {
        try
        {
            using (var op = logger.BeginOperation("Executing user registration script"))
            {
                var script = powerShellScriptFactory.Create(Enums.ScriptType.UserRegistration);

                script.AddVariable("User", user);

                script.UseInline(registrationScript.Contents);

                try
                {
                    op
                        .Enrich("UserId", user.Id)
                        .Enrich("Username", user.UserName)
                        .Enrich("ScriptId", registrationScript.Id)
                        .Enrich("ScriptName", registrationScript.Name);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not enrich logs");
                }

                await script.ExecuteAsync<int>();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not execute user registration script");
        }
    }
}