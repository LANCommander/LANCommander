using LANCommander.Server.Data;
using AutoMapper;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class ServerService(
        ILogger<ServerService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        UserService userService) : BaseDatabaseService<Data.Models.Server>(logger, cache, mapper, httpContextAccessor, contextFactory)
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
