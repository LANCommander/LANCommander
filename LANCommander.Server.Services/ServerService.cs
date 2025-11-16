using LANCommander.Server.Data;
using AutoMapper;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using Microsoft.Extensions.Logging;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ServerService(
        ILogger<ServerService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        IServiceProvider serviceProvider,
        UserService userService) : BaseDatabaseService<Data.Models.Server>(logger, settingsProvider, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Data.Models.Server> AddAsync(Data.Models.Server entity)
        {
            await cache.ExpireGameCacheAsync();

            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(s => s.Actions);
                await context.UpdateRelationshipAsync(s => s.Game);
                await context.UpdateRelationshipAsync(s => s.HttpPaths);
                await context.UpdateRelationshipAsync(s => s.Pages);
                await context.UpdateRelationshipAsync(s => s.Scripts);
                await context.UpdateRelationshipAsync(s => s.ServerConsoles);
            });
        }

        public override async Task<Data.Models.Server> UpdateAsync(Data.Models.Server entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);

            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(s => s.Actions);
                await context.UpdateRelationshipAsync(s => s.Game);
                await context.UpdateRelationshipAsync(s => s.HttpPaths);
                await context.UpdateRelationshipAsync(s => s.Pages);
                await context.UpdateRelationshipAsync(s => s.Scripts);
                await context.UpdateRelationshipAsync(s => s.ServerConsoles);
            });
        }

        public async Task<SDK.Models.Manifest.Server> GetManifestAsync(Guid serverId)
        {
            var server = await AsNoTracking()
                .AsSplitQuery()
                .Query(q =>
                {
                    return q
                        .Include(s => s.Actions)
                        .Include(s => s.HttpPaths)
                        .Include(s => s.ServerConsoles)
                        .Include(s => s.Scripts);

                })
                .GetAsync(serverId);

            return mapper.Map<SDK.Models.Manifest.Server>(server);
        }

        public async Task StartAsync(Guid serverId)
        {
            var serverEngines = serviceProvider.GetServices<IServerEngine>();

            foreach (var serverEngine in serverEngines)
            {
                if (serverEngine.IsManaging(serverId))
                    await serverEngine.StartAsync(serverId);
            }
        }

        public async Task StopAsync(Guid serverId)
        {
            var serverEngines = serviceProvider.GetServices<IServerEngine>();

            foreach (var serverEngine in serverEngines)
            {
                if (serverEngine.IsManaging(serverId))
                    await serverEngine.StopAsync(serverId);
            }
        }

        public async Task<ServerProcessStatus> GetStatusAsync(Guid serverId)
        {
            var serverEngines = serviceProvider.GetServices<IServerEngine>();

            foreach (var serverEngine in serverEngines)
            {
                if (serverEngine.IsManaging(serverId))
                    return await serverEngine.GetStatusAsync(serverId);
            }

            return ServerProcessStatus.Stopped;
        }

        public async Task RunGameStartedScriptsAsync(Guid serverId, Guid userId)
        {
            var user = await userService.GetAsync(userId);
            var server = await
                Include(s => s.Game)
                    .Include(s => s.Scripts)
                    .FirstOrDefaultAsync(s => s.Id == serverId);

            foreach (var script in server.Scripts.Where(s => s.Type == ScriptType.GameStarted))
            {
                try
                {
                    var scriptContext = new PowerShellScript(ScriptType.GameStarted);

                    scriptContext.AddVariable("Server", mapper.Map<SDK.Models.Server>(server));
                    scriptContext.AddVariable("Game", mapper.Map<SDK.Models.Game>(server.Game));
                    scriptContext.AddVariable("User", mapper.Map<SDK.Models.User>(user));

                    scriptContext.UseWorkingDirectory(server.WorkingDirectory);
                    scriptContext.UseInline(script.Contents);
                    scriptContext.UseShellExecute();

                    _logger?.LogInformation("Executing script \"{ScriptName}\"", script.Name); 

                    await scriptContext.ExecuteAsync<int>();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", script.Name, server.Name);
                }
            }
        }
        
        public async Task RunGameStoppedScriptsAsync(Guid serverId, Guid userId)
        {
            var user = await userService.GetAsync(userId);
            var server = await
                Include(s => s.Game)
                    .Include(s => s.Scripts)
                    .FirstOrDefaultAsync(s => s.Id == serverId);

            foreach (var script in server.Scripts.Where(s => s.Type == ScriptType.GameStopped))
            {
                try
                {
                    var scriptContext = new PowerShellScript(ScriptType.GameStopped);

                    scriptContext.AddVariable("Server", mapper.Map<SDK.Models.Server>(server));
                    scriptContext.AddVariable("Game", mapper.Map<SDK.Models.Game>(server.Game));
                    scriptContext.AddVariable("User", mapper.Map<SDK.Models.User>(user));

                    scriptContext.UseWorkingDirectory(server.WorkingDirectory);
                    scriptContext.UseInline(script.Contents);
                    scriptContext.UseShellExecute();

                    _logger?.LogInformation("Executing script \"{ScriptName}\"", script.Name); 

                    await scriptContext.ExecuteAsync<int>();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", script.Name, server.Name);
                }
            }
        }
    }
}
