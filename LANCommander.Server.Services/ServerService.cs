using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Helpers;
using System.Diagnostics;
using System.IO.Compression;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Models;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class ServerService : BaseDatabaseService<Data.Models.Server>
    {
        private readonly GameService GameService;
        private readonly ArchiveService ArchiveService;
        private readonly StorageLocationService StorageLocationService;

        private readonly Settings Settings = SettingService.GetSettings();

        public ServerService(
            ILogger<ServerService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory,
            GameService gameService,
            ArchiveService archiveService,
            StorageLocationService storageLocationService) : base(logger, cache, repositoryFactory)
        {
            GameService = gameService;
            ArchiveService = archiveService;
            StorageLocationService = storageLocationService;
        }

        public override async Task<Data.Models.Server> UpdateAsync(Data.Models.Server entity)
        {
            await Cache.ExpireGameCacheAsync(entity.GameId);

            return await base.UpdateAsync(entity);
        }

        public async Task<Data.Models.Server> ImportAsync(Guid objectKey)
        {
            var importArchive = await ArchiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
            var importArchivePath = await ArchiveService.GetArchiveFileLocationAsync(importArchive);
            var storageLocation = await StorageLocationService.GetAsync(importArchive.StorageLocationId);

            Data.Models.Server server;

            using (var importZip = ZipFile.OpenRead(importArchivePath))
            {
                var manifest = ManifestHelper.Deserialize<SDK.Models.Server>(await importZip.ReadAllTextAsync(ManifestHelper.ManifestFilename));

                var exists = await ExistsAsync(manifest.Id);

                if (!exists)
                    server = new Data.Models.Server()
                    {
                        Id = manifest.Id,
                    };
                else
                {
                    server = await Include(s => s.Game)
                        .Include(s => s.Actions)
                        .Include(s => s.Scripts)
                        .Include(s => s.HttpPaths)
                        .Include(s => s.ServerConsoles)
                        .FirstOrDefaultAsync(s => s.Id == manifest.Id);  
                }

                server.Name = manifest.Name;
                server.Autostart = manifest.Autostart;
                server.AutostartMethod = (ServerAutostartMethod)(int)manifest.AutostartMethod;
                server.AutostartDelay = manifest.AutostartDelay;
                server.WorkingDirectory = Path.Combine(Settings.Servers.StoragePath, server.Name.SanitizeFilename());
                server.Path = manifest.Path.Replace(manifest.WorkingDirectory, server.WorkingDirectory);
                server.Arguments = manifest.Arguments.Replace(manifest.WorkingDirectory, server.WorkingDirectory);
                server.UseShellExecute = manifest.UseShellExecute;
                server.ProcessTerminationMethod = manifest.ProcessTerminationMethod;
                server.OnStartScriptPath = manifest.OnStartScriptPath;
                server.OnStopScriptPath = manifest.OnStopScriptPath;
                server.Port = manifest.Port;
                
                #region Game

                if (manifest.Game != null)
                {
                    var game = await GameService.GetAsync(manifest.Game.Id);

                    server.Game = game;
                }
                #endregion

                #region Consoles
                if (server.ServerConsoles == null)
                    server.ServerConsoles = new List<ServerConsole>();

                foreach (var serverConsole in server.ServerConsoles)
                {
                    var manifestConsole = manifest.ServerConsoles.FirstOrDefault(c => c.Id == serverConsole.Id);

                    if (manifestConsole != null)
                    {
                        serverConsole.ServerId = server.Id;
                        serverConsole.Name = manifestConsole.Name;
                        serverConsole.Type = (ServerConsoleType)(int)manifestConsole.Type;
                        serverConsole.Path = manifestConsole.Path;
                        serverConsole.Host = manifestConsole.Host;
                        serverConsole.Port = manifestConsole.Port;
                        // serverConsole.Password = manifestConsole.Password;
                    }
                    else
                        server.ServerConsoles.Remove(serverConsole);
                }

                if (manifest.ServerConsoles != null)
                {
                    foreach (var manifestConsole in manifest.ServerConsoles.Where(mc => !server.ServerConsoles.Any(c => c.Id != mc.Id)))
                    {
                        server.ServerConsoles.Add(new ServerConsole()
                        {
                            Id = manifestConsole.Id,
                            ServerId = server.Id,
                            Name = manifestConsole.Name,
                            Type = (ServerConsoleType)(int)manifestConsole.Type,
                            Path = manifestConsole.Path,
                            Host = manifestConsole.Host,
                            Port = manifestConsole.Port,
                            // Password = manifestConsole.Password
                        });
                    }
                }
                #endregion

                #region HTTP Paths
                if (server.HttpPaths == null)
                    server.HttpPaths = new List<ServerHttpPath>();

                foreach (var httpPath in server.HttpPaths)
                {
                    var manifestHttpPath = manifest.HttpPaths.FirstOrDefault(p => p.Id == httpPath.Id);

                    if (manifestHttpPath != null)
                    {
                        httpPath.LocalPath = manifestHttpPath.LocalPath.Replace(manifest.WorkingDirectory, server.WorkingDirectory);
                        httpPath.Path = manifestHttpPath.Path;
                    }
                    else
                        server.HttpPaths.Remove(httpPath);
                }

                if (manifest.HttpPaths != null)
                {
                    foreach (var manifestPath in manifest.HttpPaths.Where(mp => !server.HttpPaths.Any(p => p.Id != mp.Id)))
                    {
                        server.HttpPaths.Add(new ServerHttpPath()
                        {
                            Id = manifestPath.Id,
                            Path = manifestPath.Path,
                            LocalPath = manifestPath.LocalPath
                        });
                    }
                }
                #endregion

                #region Scripts
                if (server.Scripts == null)
                    server.Scripts = new List<Script>();

                foreach (var script in server.Scripts)
                {
                    var manifestScript = manifest.Scripts.FirstOrDefault(s => s.Id == script.Id);

                    if (manifestScript != null)
                    {
                        script.Contents = await importZip.ReadAllTextAsync($"Scripts/{manifestScript.Id}");
                        script.Description = manifestScript.Description;
                        script.Name = manifestScript.Name;
                        script.RequiresAdmin = manifestScript.RequiresAdmin;
                        script.Type = (ScriptType)(int)manifestScript.Type;
                    }
                    else
                        server.Scripts.Remove(script);
                }

                if (manifest.Scripts != null)
                {
                    foreach (var manifestScript in manifest.Scripts.Where(ms => !server.Scripts.Any(s => s.Id == ms.Id)))
                    {
                        server.Scripts.Add(new Script()
                        {
                            Id = manifestScript.Id,
                            Contents = await importZip.ReadAllTextAsync($"Scripts/{manifestScript.Id}"),
                            Description = manifestScript.Description,
                            Name = manifestScript.Name,
                            RequiresAdmin = manifestScript.RequiresAdmin,
                            Type = (ScriptType)(int)manifestScript.Type,
                            CreatedOn = manifestScript.CreatedOn,
                        });
                    }
                }
                #endregion

                #region Actions
                server.Actions = new List<Data.Models.Action>();

                if (manifest.Actions != null && manifest.Actions.Count() > 0)
                foreach (var manifestAction in manifest.Actions)
                {
                    new Data.Models.Action()
                    {
                        Name = manifestAction.Name,
                        Arguments = manifestAction.Arguments,
                        Path = manifestAction.Path,
                        WorkingDirectory = manifestAction.WorkingDirectory,
                        PrimaryAction = manifestAction.IsPrimaryAction,
                        SortOrder = manifestAction.SortOrder,
                    };
                }
                #endregion

                #region Extract Files
                foreach (var entry in importZip.Entries.Where(a => a.FullName.StartsWith("Files/")))
                {
                    var destination = entry.FullName
                        .Substring(6, entry.FullName.Length - 6)
                        .TrimEnd('/')
                        .Replace('/', Path.DirectorySeparatorChar);

                    destination = Path.Combine(server.WorkingDirectory, destination);
                    
                    var directory = Path.GetDirectoryName(destination);
                    
                    if (!String.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    if (!entry.FullName.EndsWith('/'))
                        entry.ExtractToFile(destination, true);
                }
                #endregion

                if (exists)
                    server = await UpdateAsync(server);
                else
                    server = await AddAsync(server);

                return server;
            }
        }
    }
}
