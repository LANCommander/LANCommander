using LANCommander.Server.Data.Models;
using LANCommander.SDK.Helpers;
using System.IO.Compression;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Services.Extensions;

namespace LANCommander.Server.Services.Importers;

public class GameImporter(
    ArchiveService archiveService,
    MediaService mediaService,
    EngineService engineService,
    TagService tagService,
    CompanyService companyService,
    GenreService genreService,
    StorageLocationService storageLocationService,
    CollectionService collectionService,
    ScriptService scriptService,
    GameService gameService) : IImporter<Game>
{
    public async Task<Game> ImportAsync(Guid objectKey, ZipArchive importZip)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);
        var storageLocation = await storageLocationService.GetAsync(importArchive.StorageLocationId);
        
        var manifest = ManifestHelper.Deserialize<GameManifest>(await importZip.ReadAllTextAsync(ManifestHelper.ManifestFilename));

        var game = await InitializeGameFromManifest(manifest);
        
        game = await UpdateActions(game, manifest);
        game = await UpdateEngine(game, manifest);
        game = await UpdateTags(game, manifest);
        game = await UpdateGenres(game, manifest);
        game = await UpdateDevelopers(game, manifest);
        game = await UpdatePublishers(game, manifest);
        game = await UpdateMultiplayerModes(game, manifest);
        game = await UpdateSavePaths(game, manifest);
        game = await UpdateKeys(game, manifest);
        game = await UpdateScripts(game, manifest, importZip);
        game = await UpdateMedia(game, manifest, importZip);
        game = await UpdateArchives(game, manifest, importZip, storageLocation);
        game = await UpdateCollections(game, manifest);
        game = await UpdateDependentGames(game, manifest);

        await archiveService.DeleteAsync(importArchive, storageLocation);

        return game;
    }

    private async Task<Game> InitializeGameFromManifest(GameManifest manifest)
    {
        var game = await gameService.GetAsync(manifest.Id);
        
        var exists = game != null;
        
        if (!exists)
            game = new Game();
        else
        {
            game = await gameService.Include(g => g.Actions)
                .Include(g => g.Archives)
                .Include(g => g.BaseGame)
                .Include(g => g.Categories)
                .Include(g => g.Collections)
                .Include(g => g.DependentGames)
                .Include(g => g.Developers)
                .Include(g => g.Engine)
                .Include(g => g.Genres)
                .Include(g => g.Media)
                .Include(g => g.MultiplayerModes)
                .Include(g => g.Platforms)
                .Include(g => g.Publishers)
                .Include(g => g.Redistributables)
                .Include(g => g.Tags)
                .FirstOrDefaultAsync(g => g.Id == manifest.Id);
        }

        game.Id = manifest.Id;
        game.Description = manifest.Description;
        game.Notes = manifest.Notes;
        game.ReleasedOn = manifest.ReleasedOn;
        game.Singleplayer = manifest.Singleplayer;
        game.SortTitle = manifest.SortTitle;
        game.Title = manifest.Title;
        
        if (!exists)
            game = await gameService.AddAsync(game);
        else
            game = await gameService.UpdateAsync(game);

        return game;
    }

    private async Task<Game> UpdateActions(Game game, GameManifest manifest)
    {
        if (game.Actions == null)
            game.Actions = new List<Data.Models.Action>();

        var updatedActions = new List<Data.Models.Action>();

        if (manifest.Actions != null && manifest.Actions.Count() > 0)
        {
            foreach (var action in manifest.Actions)
            {
                updatedActions.Add(new Data.Models.Action()
                {
                    Name = action.Name,
                    Arguments = action.Arguments,
                    Path = action.Path,
                    WorkingDirectory = action.WorkingDirectory,
                    PrimaryAction = action.IsPrimaryAction,
                    SortOrder = action.SortOrder,
                });
            }
        }

        game.Actions = updatedActions;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateEngine(Game game, GameManifest manifest)
    {
        if (!string.IsNullOrEmpty(manifest.Engine))
        {
            var engine = await engineService.AddMissingAsync(e => e.Name == manifest.Engine, new Engine 
            { 
                Name = manifest.Engine 
            });
            game.Engine = engine.Value;
        }
        else
        {
            game.Engine = null;
        }
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateTags(Game game, GameManifest manifest)
    {
        if (game.Tags == null)
            game.Tags = new List<Data.Models.Tag>();

        var updatedTags = new List<Tag>();

        if (manifest.Tags != null)
        {
            foreach (var tagName in manifest.Tags)
            {
                var existingTag = game.Tags.FirstOrDefault(t => t.Name == tagName);
                if (existingTag != null)
                    updatedTags.Add(existingTag);
                else
                {
                    var newTag = (await tagService.AddMissingAsync(t => t.Name == tagName, new Tag()
                    {
                        Name = tagName
                    })).Value;
                    updatedTags.Add(newTag);
                }
            }
        }

        game.Tags = updatedTags;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateGenres(Game game, GameManifest manifest)
    {
        if (game.Genres == null)
            game.Genres = new List<Data.Models.Genre>();

        var updatedGenres = new List<Genre>();

        if (manifest.Genre != null)
        {
            foreach (var genreName in manifest.Genre)
            {
                var existingGenre = game.Genres.FirstOrDefault(g => g.Name == genreName);
                if (existingGenre != null)
                    updatedGenres.Add(existingGenre);
                else
                {
                    var newGenre = (await genreService.AddMissingAsync(g => g.Name == genreName, new Genre()
                    {
                        Name = genreName
                    })).Value;
                    updatedGenres.Add(newGenre);
                }
            }
        }

        game.Genres = updatedGenres;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateDevelopers(Game game, GameManifest manifest)
    {
        if (game.Developers == null)
            game.Developers = new List<Data.Models.Company>();

        var updatedDevelopers = new List<Company>();

        if (manifest.Developers != null)
        {
            foreach (var developer in manifest.Developers)
            {
                var existingDeveloper = game.Developers.FirstOrDefault(c => c.Name == developer);
                if (existingDeveloper != null)
                    updatedDevelopers.Add(existingDeveloper);
                else
                {
                    var newDeveloper = (await companyService.AddMissingAsync(c => c.Name == developer, new Company()
                    {
                        Name = developer
                    })).Value;
                    updatedDevelopers.Add(newDeveloper);
                }
            }
        }

        game.Developers = updatedDevelopers;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdatePublishers(Game game, GameManifest manifest)
    {
        if (game.Publishers == null)
            game.Publishers = new List<Data.Models.Company>();

        var updatedPublishers = new List<Company>();

        if (manifest.Publishers != null)
        {
            foreach (var publisher in manifest.Publishers)
            {
                var existingPublisher = game.Publishers.FirstOrDefault(c => c.Name == publisher);
                if (existingPublisher != null)
                    updatedPublishers.Add(existingPublisher);
                else
                {
                    var newPublisher = (await companyService.AddMissingAsync(c => c.Name == publisher, new Company()
                    {
                        Name = publisher
                    })).Value;
                    updatedPublishers.Add(newPublisher);
                }
            }
        }

        game.Publishers = updatedPublishers;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateMultiplayerModes(Game game, GameManifest manifest)
    {
        if (game.MultiplayerModes == null)
            game.MultiplayerModes = new List<Data.Models.MultiplayerMode>();

        var updatedMultiplayerModes = new List<MultiplayerMode>();

        if (manifest.LanMultiplayer != null)
            updatedMultiplayerModes.Add(new MultiplayerMode()
            {
                GameId = game.Id,
                Type = MultiplayerType.LAN,
                MinPlayers = manifest.LanMultiplayer.MinPlayers,
                MaxPlayers = manifest.LanMultiplayer.MaxPlayers,
                Description = manifest.LanMultiplayer.Description,
                NetworkProtocol = manifest.LanMultiplayer.NetworkProtocol,
            });

        if (manifest.LocalMultiplayer != null)
            updatedMultiplayerModes.Add(new MultiplayerMode()
            {
                GameId = game.Id,
                Type = MultiplayerType.Local,
                MinPlayers = manifest.LocalMultiplayer.MinPlayers,
                MaxPlayers = manifest.LocalMultiplayer.MaxPlayers,
                Description = manifest.LocalMultiplayer.Description,
                NetworkProtocol = manifest.LocalMultiplayer.NetworkProtocol,
            });

        if (manifest.OnlineMultiplayer != null)
            updatedMultiplayerModes.Add(new MultiplayerMode()
            {
                GameId = game.Id,
                Type = MultiplayerType.Online,
                MinPlayers = manifest.OnlineMultiplayer.MinPlayers,
                MaxPlayers = manifest.OnlineMultiplayer.MaxPlayers,
                Description = manifest.OnlineMultiplayer.Description,
                NetworkProtocol = manifest.OnlineMultiplayer.NetworkProtocol,
            });

        game.MultiplayerModes = updatedMultiplayerModes;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateSavePaths(Game game, GameManifest manifest)
    {
        if (game.SavePaths == null)
            game.SavePaths = new List<SavePath>();

        var updatedSavePaths = new List<SavePath>();

        foreach (var path in game.SavePaths.ToList())
        {
            var manifestSavePath = manifest.SavePaths?.FirstOrDefault(sp => sp.Id == path.Id);

            if (manifestSavePath != null)
            {
                path.Path = manifestSavePath.Path;
                path.WorkingDirectory = manifestSavePath.WorkingDirectory;
                path.IsRegex = manifestSavePath.IsRegex;
                path.Type = manifestSavePath.Type;
                updatedSavePaths.Add(path);
            }
        }

        if (manifest.SavePaths != null)
        {
            foreach (var manifestSavePath in manifest.SavePaths.Where(msp => !game.SavePaths.Any(sp => sp.Id == msp.Id)))
            {
                updatedSavePaths.Add(new SavePath()
                {
                    Path = manifestSavePath.Path,
                    WorkingDirectory = manifestSavePath.WorkingDirectory,
                    IsRegex = manifestSavePath.IsRegex,
                    Type = manifestSavePath.Type
                });
            }
        }

        game.SavePaths = updatedSavePaths;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateKeys(Game game, GameManifest manifest)
    {
        if (game.Keys == null)
            game.Keys = new List<Data.Models.Key>();

        var updatedKeys = new List<Key>();

        if (manifest.Keys != null)
        {
            foreach (var manifestKey in manifest.Keys)
            {
                var existingKey = game.Keys.FirstOrDefault(k => k.Value == manifestKey);
                if (existingKey != null)
                    updatedKeys.Add(existingKey);
                else
                    updatedKeys.Add(new Key() { Value = manifestKey });
            }
        }

        game.Keys = updatedKeys;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateScripts(Game game, GameManifest manifest, ZipArchive importZip)
    {
        if (game.Scripts == null)
            game.Scripts = new List<Data.Models.Script>();

        var updatedScripts = new List<Data.Models.Script>();

        foreach (var script in game.Scripts.ToList())
        {
            var manifestScript = manifest.Scripts?.FirstOrDefault(s => s.Id == script.Id);

            if (manifestScript != null)
            {
                script.Contents = await importZip.ReadAllTextAsync($"Scripts/{script.Id}");
                script.Description = manifestScript.Description;
                script.Name = manifestScript.Name;
                script.RequiresAdmin = manifestScript.RequiresAdmin;
                script.Type = (ScriptType)(int)manifestScript.Type;
                script.GameId = game.Id;
                updatedScripts.Add(script);
            }
        }

        if (manifest.Scripts != null)
        {
            foreach (var manifestScript in manifest.Scripts.Where(ms => !game.Scripts.Any(s => s.Id == ms.Id)))
            {
                var newScript = new Script()
                {
                    Id = manifestScript.Id,
                    GameId = game.Id,
                    Contents = await importZip.ReadAllTextAsync($"Scripts/{manifestScript.Id}"),
                    Description = manifestScript.Description,
                    Name = manifestScript.Name,
                    RequiresAdmin = manifestScript.RequiresAdmin,
                    Type = (ScriptType)(int)manifestScript.Type,
                    CreatedOn = manifestScript.CreatedOn,
                };
                
                newScript = await scriptService.AddAsync(newScript);
                
                updatedScripts.Add(newScript);
            }
        }

        game.Scripts = updatedScripts;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateMedia(Game game, GameManifest manifest, ZipArchive importZip)
    {
        var mediaStorageLocation = await storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);
        
        if (game.Media == null)
            game.Media = new List<Data.Models.Media>();

        var updatedMedia = new List<Media>();

        foreach (var media in game.Media.ToList())
        {
            var manifestMedia = manifest.Media?.FirstOrDefault(s => s.Id == media.Id);

            if (manifestMedia != null)
            {
                media.SourceUrl = manifestMedia.SourceUrl;
                media.FileId = manifestMedia.FileId;
                media.Type = manifestMedia.Type;
                media.MimeType = manifestMedia.MimeType;
                media.CreatedOn = manifestMedia.CreatedOn;
                media.GameId = game.Id;

                importZip.ExtractEntry($"Media/{media.FileId}", MediaService.GetMediaPath(media), true);
                media.Crc32 = SDK.Services.MediaService.CalculateChecksum(MediaService.GetMediaPath(media));
                await mediaService.UpdateAsync(media);
                
                updatedMedia.Add(media);
            }
        }

        if (manifest.Media != null)
        {
            foreach (var manifestMedia in manifest.Media.Where(mm => !game.Media.Any(m => m.Id == mm.Id)))
            {
                var media = new Media()
                {
                    FileId = Guid.NewGuid(),
                    MimeType = manifestMedia.MimeType,
                    SourceUrl = manifestMedia.SourceUrl,
                    Type = manifestMedia.Type,
                    CreatedOn = manifestMedia.CreatedOn,
                    StorageLocationId = mediaStorageLocation.Id,
                    GameId = game.Id,
                };

                var mediaPath = MediaService.GetMediaPath(media, mediaStorageLocation);
                importZip.ExtractEntry($"Media/{manifestMedia.FileId}", mediaPath, true);
                media.Crc32 = SDK.Services.MediaService.CalculateChecksum(mediaPath);
                media = await mediaService.AddAsync(media);
                
                updatedMedia.Add(media);
            }
        }

        game.Media = updatedMedia;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateArchives(Game game, GameManifest manifest, ZipArchive importZip, StorageLocation storageLocation)
    {
        if (game.Archives == null)
            game.Archives = new List<Data.Models.Archive>();

        var updatedArchives = new List<Archive>();

        foreach (var archive in game.Archives.ToList())
        {
            var manifestArchive = manifest.Archives?.FirstOrDefault(a => a.Id == archive.Id);

            if (manifestArchive != null)
            {
                archive.Changelog = manifestArchive.Changelog;
                archive.ObjectKey = manifestArchive.ObjectKey;
                archive.Version = manifestArchive.Version;
                archive.CreatedOn = manifestArchive.CreatedOn;

                var extractionLocation = await archiveService.GetArchiveFileLocationAsync(archive);

                importZip.ExtractEntry($"Archives/{archive.ObjectKey}", extractionLocation, true);
                
                var archiveFile = ZipFile.Open(extractionLocation, ZipArchiveMode.Read);

                archive.CompressedSize = new FileInfo(extractionLocation).Length;
                archive.UncompressedSize = archiveFile.Entries.Sum(e => e.Length);

                await archiveService.UpdateAsync(archive);
                
                updatedArchives.Add(archive);
            }
        }

        if (manifest.Archives != null)
        {
            foreach (var manifestArchive in manifest.Archives.Where(ma => !game.Archives.Any(a => a.Id == ma.Id)))
            {
                var archive = new Archive()
                {
                    ObjectKey = Guid.NewGuid().ToString(),
                    Changelog = manifestArchive.Changelog,
                    Version = manifestArchive.Version,
                    CreatedOn = manifestArchive.CreatedOn,
                    StorageLocationId = storageLocation.Id,
                };

                var extractionLocation = await archiveService.GetArchiveFileLocationAsync(archive);

                importZip.ExtractEntry($"Archives/{manifestArchive.ObjectKey}", extractionLocation, true);

                var archiveFile = ZipFile.Open(extractionLocation, ZipArchiveMode.Read);

                archive.CompressedSize = new FileInfo(extractionLocation).Length;
                archive.UncompressedSize = archiveFile.Entries.Sum(e => e.Length);

                archive = await archiveService.AddAsync(archive);
                
                updatedArchives.Add(archive);
            }
        }

        game.Archives = updatedArchives;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateCollections(Game game, GameManifest manifest)
    {
        if (game.Collections == null)
            game.Collections = new List<Data.Models.Collection>();

        var updatedCollections = new List<Collection>();

        if (manifest.Collections != null)
        {
            foreach (var collectionName in manifest.Collections)
            {
                var existingCollection = game.Collections.FirstOrDefault(c => c.Name == collectionName);
                if (existingCollection != null)
                    updatedCollections.Add(existingCollection);
                else
                {
                    var newCollection = (await collectionService.AddMissingAsync(c => c.Name == collectionName, new Collection()
                    {
                        Name = collectionName
                    })).Value;
                    updatedCollections.Add(newCollection);
                }
            }
        }

        game.Collections = updatedCollections;
        return await gameService.UpdateAsync(game);
    }

    private async Task<Game> UpdateDependentGames(Game game, GameManifest manifest)
    {
        if (game.DependentGames == null)
            game.DependentGames = new List<Game>();

        var updatedDependentGames = new List<Game>();

        if (manifest.DependentGames != null)
        {
            foreach (var dependentGameId in manifest.DependentGames)
            {
                var dependentGame = await gameService.GetAsync(dependentGameId);
                if (dependentGame != null)
                    updatedDependentGames.Add(dependentGame);
            }
        }

        game.DependentGames = updatedDependentGames;
        return await gameService.UpdateAsync(game);
    }
} 