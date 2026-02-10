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
    public async Task<bool> Tool_RunDetectInstallScriptAsync(string installDirectory, Guid gameId, Tool tool)
    {
        bool result = default;
        
        var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
        var toolManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Tool>(installDirectory, tool.Id);
        
        var path = ScriptHelper.GetScriptFilePath(installDirectory, tool.Id, Enums.ScriptType.DetectInstall);

        try
        {
            if (File.Exists(path))
            {
                using (var op = logger.BeginOperation("Executing install detection script"))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.DetectInstall);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("ToolManifest", toolManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("ToolManifestPath", ManifestHelper.GetPath(installDirectory, tool.Id))
                            .Enrich("ScriptPath", path)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("ToolName", toolManifest.Name)
                            .Enrich("ToolId", toolManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }
                    
                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }

                    script.UseWorkingDirectory(Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, tool.Id)));
                    script.UseFile(path);
                    
                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                    {
                        using (var timeoutCancellationTokenSource = new CancellationTokenSource())
                        {
                            var task = script.ExecuteAsync<bool>();
                            
                            var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), timeoutCancellationTokenSource.Token));
                            
                            if (completedTask == task) 
                            {
                                await timeoutCancellationTokenSource.CancelAsync();
                                return await task;
                            } else {
                                throw new TimeoutException("The operation has timed out.");
                            }
                        }
                    }
                    
                    result = await script.ExecuteAsync<bool>();

                    op.Complete();
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Detect Install script");
        }

        return result;
    }

    public async Task<int> Tool_RunInstallScriptAsync(string installDirectory, Guid gameId, Tool tool)
    {
        int result = default;

        var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
        var toolManifest = await ManifestHelper.ReadAsync<Tool>(installDirectory, tool.Id);

        var path = ScriptHelper.GetScriptFilePath(installDirectory, tool.Id, Enums.ScriptType.Install);
        
        try
        {
            if (Path.Exists(path))
            {
                using (var op = logger.BeginOperation("Executing install detection script"))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.Install);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("ToolManifest", toolManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());

                    try
                    {
                        op
                            .Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("ToolManifestPath", ManifestHelper.GetPath(installDirectory, tool.Id))
                            .Enrich("ScriptPath", path)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("ToolName", toolManifest.Name)
                            .Enrich("ToolId", toolManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }
                    
                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    var extractionPath = Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, tool.Id), "Files");

                    script.UseWorkingDirectory(extractionPath);
                    script.UseFile(path);

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();

                    op.Complete();
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Detect Install script");
        }

        return result;
    }
    
    public async Task<int> Tool_RunBeforeStartScriptAsync(string installDirectory, Guid gameId, Tool tool)
    {
        int result = default;

        try
        {
            var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var toolManifest = await ManifestHelper.ReadAsync<Tool>(installDirectory, tool.Id);
            
            var path = ScriptHelper.GetScriptFilePath(installDirectory, tool.Id, Enums.ScriptType.BeforeStart);

            using (var op = logger.BeginOperation("Executing before start script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.BeforeStart);
                    var playerAlias = await GameClient.GetPlayerAliasAsync(installDirectory, gameId);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("ToolManifest", toolManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("PlayerAlias", playerAlias);

                    try
                    {
                        op
                            .Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("ToolManifestPath", ManifestHelper.GetPath(installDirectory, tool.Id))
                            .Enrich("ScriptPath", path)
                            .Enrich("PlayerAlias", playerAlias)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("ToolName", toolManifest.Name)
                            .Enrich("ToolId", toolManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }
                    
                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    var extractionPath = Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, tool.Id), "Files");

                    script.UseWorkingDirectory(extractionPath);
                    script.UseFile(path);

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();
                }
                else
                {
                    logger?.LogTrace("No before start script found");
                }

                op.Complete();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Before Start script");
        }

        return result;
    }

    public async Task<int> Tool_RunAfterStopScriptAsync(string installDirectory, Guid gameId, Tool tool)
    {
        int result = default;

        try
        {
            var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var toolManifest = await ManifestHelper.ReadAsync<Tool>(installDirectory, tool.Id);
            
            var path = ScriptHelper.GetScriptFilePath(installDirectory, tool.Id, Enums.ScriptType.AfterStop);

            using (var op = logger.BeginOperation("Executing after stop script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.AfterStop);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("ToolManifest", toolManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("PlayerAlias", await GameClient.GetPlayerAliasAsync(installDirectory, gameId));

                    try
                    {
                        op
                            .Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("ToolManifestPath", ManifestHelper.GetPath(installDirectory, tool.Id))
                            .Enrich("ScriptPath", path)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("ToolName", toolManifest.Name)
                            .Enrich("ToolId", toolManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }
                    
                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    var extractionPath = Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, tool.Id), "Files");

                    script.UseWorkingDirectory(extractionPath);
                    script.UseFile(path);

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();
                }
                else
                {
                    logger?.LogTrace("No after stop script found");
                }

                op.Complete();
            }

        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run an After Stop script");
        }

        return result;
    }
    
    public async Task<Package> Tool_RunPackageScriptAsync(Script packageScript, Tool tool)
    {
        try
        {
            using (var op = logger.BeginOperation("Executing tool package script"))
            {
                var script = powerShellScriptFactory.Create(Enums.ScriptType.Package);

                script.AddVariable("Tool", tool);

                script.UseInline(packageScript.Contents);
                
                try
                {
                    op
                        .Enrich("ToolId", tool.Id)
                        .Enrich("ToolName", tool.Name)
                        .Enrich("ScriptId", packageScript.Id)
                        .Enrich("ScriptName", packageScript.Name);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not enrich logs");
                }
                
                if (Debug)
                    script.EnableDebug();

                return await script.ExecuteAsync<Package>();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not execute tool package script");
        }

        return null;
    }
}