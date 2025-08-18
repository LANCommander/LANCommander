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

namespace LANCommander.SDK.Services
{
    public class ScriptService
    {
        private readonly ILogger _logger;

        private readonly Client _client;

        public delegate Task<bool> ExternalScriptRunnerHandler(PowerShellScript script);
        public event ExternalScriptRunnerHandler ExternalScriptRunner;

        public bool Debug { get; set; } = false;

        public Func<System.Management.Automation.PowerShell, Task> OnDebugStart;
        public Func<System.Management.Automation.PowerShell, Task> OnDebugBreak;
        public Func<LogLevel, string, Task> OnOutput;

        public ScriptService(Client client)
        {
            _client = client;
        }

        public ScriptService(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }
        
        #region Authentication Scripts
        public async Task RunUserLoginScript(Script loginScript, User user)
        {
            try
            {
                using (var op = _logger.BeginOperation("Executing user login script"))
                {
                    var script = new PowerShellScript(Enums.ScriptType.UserLogin);

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
                        _logger?.LogError(ex, "Could not enrich logs");
                    }

                    await script.ExecuteAsync<int>();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not execute user login script");
            }
        }
        
        public async Task RunUserRegistrationScript(Script registrationScript, User user)
        {
            try
            {
                using (var op = _logger.BeginOperation("Executing user registration script"))
                {
                    var script = new PowerShellScript(Enums.ScriptType.UserRegistration);

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
                        _logger?.LogError(ex, "Could not enrich logs");
                    }

                    await script.ExecuteAsync<int>();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not execute user registration script");
            }
        }
        #endregion

        #region Redistributables
        public async Task<bool> RunDetectInstallScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
        {
            bool result = default;
            
            var gameManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
            var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);
            
            var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.DetectInstall);

            try
            {
                if (File.Exists(path))
                {
                    using (var op = _logger.BeginOperation("Executing install detection script"))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.DetectInstall);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", gameManifest);
                        script.AddVariable("RedistributableManifest", redistributableManifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());

                        try
                        {
                            op.Enrich("InstallDirectory", installDirectory)
                                .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                                .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                                .Enrich("ScriptPath", path)
                                .Enrich("GameTitle", gameManifest.Title)
                                .Enrich("GameId", gameManifest.Id)
                                .Enrich("RedistributableName", redistributableManifest.Name)
                                .Enrich("RedistributableId", redistributableManifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }
                        
                        if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                        {
                            foreach (var customField in gameManifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }

                        script.UseWorkingDirectory(Path.Combine(GameService.GetMetadataDirectoryPath(installDirectory, redistributableId)));
                        script.UseFile(path);

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                        {
                            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
                            {
                                var task = script.ExecuteAsync<bool>();
                                var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), timeoutCancellationTokenSource.Token));
                                if (completedTask == task) {
                                    timeoutCancellationTokenSource.Cancel();
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
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Detect Install script");
            }

            return result;
        }

        public async Task<int> RunInstallScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
        {
            int result = default;

            var gameManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
            var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);

            var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.Install);
            
            try
            {
                if (Path.Exists(path))
                {
                    using (var op = _logger.BeginOperation("Executing install detection script"))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.Install);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", gameManifest);
                        script.AddVariable("RedistributableManifest", redistributableManifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());

                        try
                        {
                            op
                                .Enrich("InstallDirectory", installDirectory)
                                .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                                .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                                .Enrich("ScriptPath", path)
                                .Enrich("GameTitle", gameManifest.Title)
                                .Enrich("GameId", gameManifest.Id)
                                .Enrich("RedistributableName", redistributableManifest.Name)
                                .Enrich("RedistributableId", redistributableManifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }
                        
                        if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                        {
                            foreach (var customField in gameManifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        var extractionPath = Path.Combine(GameService.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                        script.UseWorkingDirectory(extractionPath);
                        script.UseFile(path);

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();

                        op.Complete();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Detect Install script");
            }

            return result;
        }
        
        public async Task<int> RunBeforeStartScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
        {
            int result = default;

            try
            {
                var gameManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
                var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);
                
                var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.BeforeStart);

                using (var op = _logger.BeginOperation("Executing before start script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.BeforeStart);
                        var playerAlias = await GameService.GetPlayerAliasAsync(installDirectory, gameId);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", gameManifest);
                        script.AddVariable("RedistributableManifest", redistributableManifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());
                        script.AddVariable("PlayerAlias", playerAlias);

                        try
                        {
                            op
                                .Enrich("InstallDirectory", installDirectory)
                                .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                                .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                                .Enrich("ScriptPath", path)
                                .Enrich("PlayerAlias", playerAlias)
                                .Enrich("GameTitle", gameManifest.Title)
                                .Enrich("GameId", gameManifest.Id)
                                .Enrich("RedistributableName", redistributableManifest.Name)
                                .Enrich("RedistributableId", redistributableManifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }
                        
                        if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                        {
                            foreach (var customField in gameManifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        var extractionPath = Path.Combine(GameService.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                        script.UseWorkingDirectory(extractionPath);
                        script.UseFile(path);

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No before start script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Before Start script");
            }

            return result;
        }

        public async Task<int> RunAfterStopScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
        {
            int result = default;

            try
            {
                var gameManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
                var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);
                
                var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.AfterStop);

                using (var op = _logger.BeginOperation("Executing after stop script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.AfterStop);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", gameManifest);
                        script.AddVariable("RedistributableManifest", redistributableManifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());
                        script.AddVariable("PlayerAlias", GameService.GetPlayerAlias(installDirectory, gameId));

                        try
                        {
                            op
                                .Enrich("InstallDirectory", installDirectory)
                                .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                                .Enrich("RedistributableManifestPath",
                                    ManifestHelper.GetPath(installDirectory, redistributableId))
                                .Enrich("ScriptPath", path)
                                .Enrich("GameTitle", gameManifest.Title)
                                .Enrich("GameId", gameManifest.Id)
                                .Enrich("RedistributableName", redistributableManifest.Name)
                                .Enrich("RedistributableId", redistributableManifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }
                        
                        if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                        {
                            foreach (var customField in gameManifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        var extractionPath = Path.Combine(GameService.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                        script.UseWorkingDirectory(extractionPath);
                        script.UseFile(path);

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No after stop script found");
                    }

                    op.Complete();
                }

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run an After Stop script");
            }

            return result;
        }

        public async Task<int> RunNameChangeScriptAsync(string installDirectory, Guid gameId, Guid redistributableId, string newName)
        {
            int result = default;

            try
            {
                var gameManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
                var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);
                
                var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.NameChange);

                using (var op = _logger.BeginOperation("Executing name change script"))
                {
                    if (File.Exists(path))
                    {
                        var oldName = await GameService.GetPlayerAliasAsync(installDirectory, gameId);

                        if (oldName == newName)
                            oldName = string.Empty;

                        if (!string.IsNullOrWhiteSpace(oldName))
                            _logger?.LogTrace("Old Name: {OldName}", oldName);

                        _logger?.LogTrace("New Name: {NewName}", newName);

                        var script = new PowerShellScript(Enums.ScriptType.NameChange);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", gameManifest);
                        script.AddVariable("RedistributableManifest", redistributableManifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());
                        script.AddVariable("OldPlayerAlias", oldName);
                        script.AddVariable("NewPlayerAlias", newName);

                        try
                        {
                            op.Enrich("InstallDirectory", installDirectory)
                              .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                              .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                              .Enrich("ScriptPath", path)
                              .Enrich("OldPlayerAlias", oldName)
                              .Enrich("NewPlayerAlias", newName)
                              .Enrich("GameTitle", gameManifest.Title)
                              .Enrich("GameId", gameManifest.Id)
                              .Enrich("RedistributableName", redistributableManifest.Name)
                              .Enrich("RedistributableId", redistributableManifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }

                        if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                        {
                            foreach (var customField in gameManifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        var extractionPath = Path.Combine(GameService.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                        script.UseWorkingDirectory(extractionPath);
                        script.UseFile(path);

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No name change script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Name Change script");
            }

            return result;
        }

        #endregion

        #region Games
        public async Task<int> RunInstallScriptAsync(string installDirectory, Guid gameId)
        {
            int result = default;

            try
            {
                var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Install);

                using (var op = _logger.BeginOperation("Executing install script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.Install);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());

                        if (manifest.CustomFields != null && manifest.CustomFields.Any())
                        {
                            foreach (var customField in manifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        script.UseFile(path);

                        try
                        {
                            op.Enrich("InstallDirectory", installDirectory)
                              .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                              .Enrich("ScriptPath", path)
                              .Enrich("GameTitle", manifest.Title)
                              .Enrich("GameId", manifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No install script found for game");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run an Install script");
            }

            return result;
        }

        public async Task<int> RunUninstallScriptAsync(string installDirectory, Guid gameId)
        {
            int result = default;

            try
            {
                var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Uninstall);

                using (var op = _logger.BeginOperation("Executing uninstall script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.Uninstall);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());
                        
                        if (manifest.CustomFields != null && manifest.CustomFields.Any())
                        {
                            foreach (var customField in manifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        script.UseFile(path);

                        try
                        {
                            op.Enrich("InstallDirectory", installDirectory)
                              .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                              .Enrich("ScriptPath", path)
                              .Enrich("GameTitle", manifest.Title)
                              .Enrich("GameId", manifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No uninstall script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to get an Uninstall script");
            }

            return result;
        }

        public async Task<int> RunBeforeStartScriptAsync(string installDirectory, Guid gameId)
        {
            int result = default;

            try
            {
                var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.BeforeStart);

                using (var op = _logger.BeginOperation("Executing before start script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.BeforeStart);
                        var playerAlias = GameService.GetPlayerAlias(installDirectory, gameId);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());
                        script.AddVariable("PlayerAlias", playerAlias);
                        
                        if (manifest.CustomFields != null && manifest.CustomFields.Any())
                        {
                            foreach (var customField in manifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        script.UseFile(path);

                        try
                        {
                            op.Enrich("InstallDirectory", installDirectory)
                              .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                              .Enrich("ScriptPath", path)
                              .Enrich("PlayerAlias", playerAlias)
                              .Enrich("GameTitle", manifest.Title)
                              .Enrich("GameId", manifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No before start script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Before Start script");
            }

            return result;
        }

        public async Task<int> RunAfterStopScriptAsync(string installDirectory, Guid gameId)
        {
            int result = default;

            try
            {
                var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.AfterStop);

                using (var op = _logger.BeginOperation("Executing after stop script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.AfterStop);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());
                        script.AddVariable("PlayerAlias", GameService.GetPlayerAlias(installDirectory, gameId));
                        
                        if (manifest.CustomFields != null && manifest.CustomFields.Any())
                        {
                            foreach (var customField in manifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        script.UseFile(path);

                        try
                        {
                            op.Enrich("InstallDirectory", installDirectory)
                              .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                              .Enrich("ScriptPath", path)
                              .Enrich("GameTitle", manifest.Title)
                              .Enrich("GameId", manifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No after stop script found");
                    }

                    op.Complete();
                }

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run an After Stop script");
            }

            return result;
        }

        public async Task<int> RunNameChangeScriptAsync(string installDirectory, Guid gameId, string newName)
        {
            int result = default;

            try
            {
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.NameChange);
                var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);

                using (var op = _logger.BeginOperation("Executing name change script"))
                {
                    if (File.Exists(path))
                    {
                        var oldName = await GameService.GetPlayerAliasAsync(installDirectory, gameId);

                        if (oldName == newName)
                            oldName = string.Empty;

                        if (!string.IsNullOrWhiteSpace(oldName))
                            _logger?.LogTrace("Old Name: {OldName}", oldName);

                        _logger?.LogTrace("New Name: {NewName}", newName);

                        var script = new PowerShellScript(Enums.ScriptType.NameChange);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());
                        script.AddVariable("OldPlayerAlias", oldName);
                        script.AddVariable("NewPlayerAlias", newName);
                        
                        if (manifest.CustomFields != null && manifest.CustomFields.Any())
                        {
                            foreach (var customField in manifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }

                        try
                        {
                            op.Enrich("InstallDirectory", installDirectory)
                              .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                              .Enrich("ScriptPath", path)
                              .Enrich("OldPlayerAlias", oldName)
                              .Enrich("NewPlayerAlias", newName)
                              .Enrich("GameTitle", manifest.Title)
                              .Enrich("GameId", manifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }

                        script.UseFile(path);

                        GameService.UpdatePlayerAlias(installDirectory, gameId, newName);

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No name change script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Name Change script");
            }

            return result;
        }

        public async Task<int> RunKeyChangeScriptAsync(string installDirectory, Guid gameId, string key)
        {
            int result = default;

            try
            {
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.KeyChange);
                var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);

                using (var op = _logger.BeginOperation("Executing key change script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.KeyChange);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        _logger?.LogTrace("New key is {Key}", key);

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", _client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", _client.BaseUrl.ToString());
                        script.AddVariable("AllocatedKey", key);
                        
                        if (manifest.CustomFields != null && manifest.CustomFields.Any())
                        {
                            foreach (var customField in manifest.CustomFields)
                            {
                                script.AddVariable(customField.Name, customField.Value);
                            }
                        }
                        
                        script.UseFile(path);

                        try
                        {
                            op.Enrich("InstallDirectory", installDirectory)
                              .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                              .Enrich("ScriptPath", path)
                              .Enrich("AllocatedKey", key)
                              .Enrich("GameTitle", manifest.Title)
                              .Enrich("GameId", manifest.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not enrich logs");
                        }

                        GameService.UpdateCurrentKey(installDirectory, gameId, key);

                        try
                        {
                            if (Debug)
                            {
                                script.EnableDebug();
                                script.OnDebugBreak = OnDebugBreak;
                                script.OnOutput = OnOutput;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Could not debug script");
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        _logger?.LogTrace("No key change script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Key Change script");
            }

            return result;
        }

        public async Task<Package> RunPackageScriptAsync(Script packageScript, Game game)
        {
            try
            {
                using (var op = _logger.BeginOperation("Executing game package script"))
                {
                    var script = new PowerShellScript(Enums.ScriptType.Package);

                    script.AddVariable("Game", game);

                    script.UseInline(packageScript.Contents);

                    try
                    {
                        op
                            .Enrich("GameId", game.Id)
                            .Enrich("GameTitle", game.Title)
                            .Enrich("ScriptId", packageScript.Id)
                            .Enrich("ScriptName", packageScript.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Could not enrich logs");
                    }

                    return await script.ExecuteAsync<Package>();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not execute game package script");
            }

            return null;
        }
        #endregion
    }
}
