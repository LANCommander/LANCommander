using LANCommander.Server.Data.Models;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Services.Importers;

public class ServerImporter(
    ArchiveService archiveService,
    GameService gameService,
    StorageLocationService storageLocationService,
    ServerService serverService,
    Settings settings) : IImporter<Data.Models.Server>
{
    public async Task<Data.Models.Server> ImportAsync(Guid objectKey, ZipArchive importZip)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);
        var storageLocation = await storageLocationService.GetAsync(importArchive.StorageLocationId);
        var manifest = ManifestHelper.Deserialize<SDK.Models.Server>(await importZip.ReadAllTextAsync(ManifestHelper.ManifestFilename));

        var server = await InitializeServerFromManifest(manifest);

        server = await UpdateGame(server, manifest);
        server = await UpdateConsoles(server, manifest);
        server = await UpdateHttpPaths(server, manifest);
        server = await UpdateScripts(server, manifest, importZip);
        server = await UpdateActions(server, manifest);
        await ExtractFiles(server, importZip);

        if (await serverService.ExistsAsync(server.Id))
            server = await serverService.UpdateAsync(server);
        else
            server = await serverService.AddAsync(server);

        return server;
    }

    private async Task<Data.Models.Server> InitializeServerFromManifest(SDK.Models.Server manifest)
    {
        var exists = await serverService.ExistsAsync(manifest.Id);

        Data.Models.Server server;

        if (!exists)
            server = new Data.Models.Server()
            {
                Id = manifest.Id,
            };
        else
        {
            server = await serverService.Include(s => s.Game)
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
        server.WorkingDirectory = Path.Combine(settings.Servers.StoragePath, server.Name.SanitizeFilename());
        server.Path = manifest.Path.Replace(manifest.WorkingDirectory, server.WorkingDirectory);
        server.Arguments = manifest.Arguments.Replace(manifest.WorkingDirectory, server.WorkingDirectory);
        server.UseShellExecute = manifest.UseShellExecute;
        server.ProcessTerminationMethod = manifest.ProcessTerminationMethod;
        server.OnStartScriptPath = manifest.OnStartScriptPath;
        server.OnStopScriptPath = manifest.OnStopScriptPath;
        server.Port = manifest.Port;

        return server;
    }

    private async Task<Data.Models.Server> UpdateGame(Data.Models.Server server, SDK.Models.Server manifest)
    {
        if (manifest.Game != null)
        {
            var game = await gameService.GetAsync(manifest.Game.Id);
            server.Game = game;
        }

        return server;
    }

    private async Task<Data.Models.Server> UpdateConsoles(Data.Models.Server server, SDK.Models.Server manifest)
    {
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
            }
            else
                server.ServerConsoles.Remove(serverConsole);
        }

        if (manifest.ServerConsoles != null)
        {
            foreach (var manifestConsole in manifest.ServerConsoles.Where(mc => !server.ServerConsoles.Any(c => c.Id != mc.Id)))
            {
                server.ServerConsoles.Add(new ServerConsole
                {
                    Id = manifestConsole.Id,
                    Server = server,
                    Name = manifestConsole.Name,
                    Type = (ServerConsoleType)(int)manifestConsole.Type,
                    Path = manifestConsole.Path,
                    Host = manifestConsole.Host,
                    Port = manifestConsole.Port,
                });
            }
        }

        return server;
    }

    private async Task<Data.Models.Server> UpdateHttpPaths(Data.Models.Server server, SDK.Models.Server manifest)
    {
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

        return server;
    }

    private async Task<Data.Models.Server> UpdateScripts(Data.Models.Server server, SDK.Models.Server manifest, ZipArchive importZip)
    {
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

        return server;
    }

    private async Task<Data.Models.Server> UpdateActions(Data.Models.Server server, SDK.Models.Server manifest)
    {
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

        return server;
    }

    private async Task ExtractFiles(Data.Models.Server server, ZipArchive importZip)
    {
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
    }
} 