using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Importers;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Readers;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace LANCommander.Server.ImportExport;

public class ImportContext : IDisposable
{
    private Guid? Id { get; set; }
    public object Manifest { get; private set; }
    public BaseModel DataRecord { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; set; }
    public IArchive Archive { get; private set; }

    public IImportItemInfo CurrentItem { get; set; }

    /// <summary>Every error reported while importing: records skipped or that failed.</summary>
    public List<string> Errors { get; } = new();

    public int Processed;
    public int Total;

    private Queue<IImportItemInfo> Queue { get; } = new();
    /// <summary>Keys of the records chosen for import; null imports everything.</summary>
    private HashSet<string>? SelectedKeys { get; set; }

    public AsyncEventHandler<ImportStatusUpdate> OnImportStarted { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportStatusUpdate { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportComplete { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportError = new();
    
    private readonly ImportService _importService;
    private readonly StorageLocationService _storageLocationService;
    private readonly ILogger<ImportContext> _logger;

    #region Importers
    private readonly ActionImporter _actions;
    private readonly ArchiveImporter _archives;
    private readonly CollectionImporter _collections;
    private readonly CustomFieldImporter _customFields;
    private readonly DeveloperImporter _developers;
    private readonly EngineImporter _engines;
    private readonly GameImporter _games;
    private readonly GenreImporter _genres;
    private readonly KeyImporter _keys;
    private readonly MediaImporter _media;
    private readonly MultiplayerModeImporter _multiplayerModes;
    private readonly PlatformImporter _platforms;
    private readonly PlaySessionImporter _playSessions;
    private readonly PublisherImporter _publishers;
    private readonly RedistributableImporter _redistributables;
    private readonly SaveImporter _saves;
    private readonly SavePathImporter _savePaths;
    private readonly ScriptImporter _scripts;
    private readonly ServerConsoleImporter _serverConsoles;
    private readonly ServerHttpPathImporter _serverHttpPaths;
    private readonly ServerImporter _servers;
    private readonly TagImporter _tags;
    private readonly ToolImporter _tools;
    #endregion

    /// <summary>The scope this context's importers live in, when the context owns one.</summary>
    private readonly IServiceScope? _scope;

    /// <summary>Takes ownership of <paramref name="scope"/>, disposing it with the context.</summary>
    public ImportContext(IServiceScope scope) : this(scope.ServiceProvider)
    {
        _scope = scope;
    }

    public ImportContext(IServiceProvider serviceProvider)
    {
        _actions = serviceProvider.GetRequiredService<ActionImporter>();
        _archives = serviceProvider.GetRequiredService<ArchiveImporter>();
        _collections = serviceProvider.GetRequiredService<CollectionImporter>();
        _customFields = serviceProvider.GetRequiredService<CustomFieldImporter>();
        _developers = serviceProvider.GetRequiredService<DeveloperImporter>();
        _engines = serviceProvider.GetRequiredService<EngineImporter>();
        _games = serviceProvider.GetRequiredService<GameImporter>();
        _genres = serviceProvider.GetRequiredService<GenreImporter>();
        _keys = serviceProvider.GetRequiredService<KeyImporter>();
        _media = serviceProvider.GetRequiredService<MediaImporter>();
        _multiplayerModes = serviceProvider.GetRequiredService<MultiplayerModeImporter>();
        _platforms = serviceProvider.GetRequiredService<PlatformImporter>();
        _playSessions = serviceProvider.GetRequiredService<PlaySessionImporter>();
        _publishers = serviceProvider.GetRequiredService<PublisherImporter>();
        _redistributables = serviceProvider.GetRequiredService<RedistributableImporter>();
        _saves = serviceProvider.GetRequiredService<SaveImporter>();
        _savePaths = serviceProvider.GetRequiredService<SavePathImporter>();
        _scripts = serviceProvider.GetRequiredService<ScriptImporter>();
        _serverConsoles = serviceProvider.GetRequiredService<ServerConsoleImporter>();
        _serverHttpPaths = serviceProvider.GetRequiredService<ServerHttpPathImporter>();
        _servers = serviceProvider.GetRequiredService<ServerImporter>();
        _tags = serviceProvider.GetRequiredService<TagImporter>();
        _tools = serviceProvider.GetRequiredService<ToolImporter>();
        
        _importService = serviceProvider.GetRequiredService<ImportService>();
        _storageLocationService = serviceProvider.GetRequiredService<StorageLocationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<ImportContext>>();
    }

    internal bool InQueue<TRecord>(TRecord record, BaseImporter<TRecord> importer)
        where TRecord : class =>
        Queue.Any(qi => qi.Key == importer.GetKey(record));

    public void SetId(Guid id) => Id = id;

    #region Initialize Import
    public async Task<IEnumerable<IImportItemInfo>> InitializeImportAsync(
        string archivePath,
        ManifestType? manifestType = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _actions.UseContext(this);
        _archives.UseContext(this);
        _collections.UseContext(this);
        _customFields.UseContext(this);
        _developers.UseContext(this);
        _engines.UseContext(this);
        _games.UseContext(this);
        _genres.UseContext(this);
        _keys.UseContext(this);
        _media.UseContext(this);
        _multiplayerModes.UseContext(this);
        _platforms.UseContext(this);
        _playSessions.UseContext(this);
        _publishers.UseContext(this);
        _redistributables.UseContext(this);
        _saves.UseContext(this);
        _savePaths.UseContext(this);
        _scripts.UseContext(this);
        _serverConsoles.UseContext(this);
        _serverHttpPaths.UseContext(this);
        _servers.UseContext(this);
        _tags.UseContext(this);
        _tools.UseContext(this);

        Archive = ZipArchive.OpenArchive(archivePath, new ReaderOptions());

        var manifestEntry = Archive.Entries.FirstOrDefault(e => e.Key == ManifestHelper.ManifestFilename);

        // Legacy purposes
        if (manifestEntry == null)
            manifestEntry = Archive.Entries.FirstOrDefault(e => e.Key == "_manifest.yml");

        if (manifestEntry == null)
            throw new InvalidOperationException("Invalid import file, cannot load manifest");

        using (var reader = new StreamReader(manifestEntry.OpenEntryStream()))
        {
            var manifestContents = await reader.ReadToEndAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var manifestMeta = ManifestHelper.Deserialize<SDK.Models.Manifest.BaseManifest>(manifestContents);

            // Check for legacy manifest. A missing version alone doesn't make one: a current-format
            // manifest written by hand or by a tool may leave it out, and reading that as legacy
            // drops everything the old format didn't have, like multiplayer modes and saves
            if (manifestMeta.IsLegacyManifest() && HasLegacyLayout(manifestContents))
            {
                if (ManifestHelper.TryDeserialize<Legacy.Models.GameManifest>(manifestContents, out var legacyGameManifest))
                    return await InitializeLegacyGameImportAsync(legacyGameManifest);
            }

            manifestType ??= DetectManifestType(manifestContents);

            if (manifestType.HasValue)
                return await InitializeByTypeAsync(manifestType.Value, manifestContents);

            if (ManifestHelper.TryDeserialize<SDK.Models.Manifest.Game>(manifestContents, out var gameManifest) && !String.IsNullOrWhiteSpace(gameManifest.Title))
                return await InitializeGameImportAsync(gameManifest);

            if (ManifestHelper.TryDeserialize<SDK.Models.Manifest.Redistributable>(manifestContents, out var redistributableManifest) && !String.IsNullOrWhiteSpace(redistributableManifest.Name))
                return await InitializeRedistributableImportAsync(redistributableManifest);

            if (ManifestHelper.TryDeserialize<SDK.Models.Manifest.Server>(manifestContents, out var serverManifest) && !String.IsNullOrWhiteSpace(serverManifest.Name))
                return await InitializeServerImportAsync(serverManifest);

            if (ManifestHelper.TryDeserialize<SDK.Models.Manifest.Tool>(manifestContents, out var toolManifest) && !String.IsNullOrWhiteSpace(toolManifest.Name))
                return await InitializeToolImportAsync(toolManifest);

            throw new InvalidOperationException("Unknown manifest file");
        }
    }

    /// <summary>
    /// Tells what a manifest describes from the keys only that type writes. Server, tool and
    /// redistributable manifests all have a Name, so trying each type in turn reads a server or a
    /// tool as a redistributable. Exporters write every property, so these keys are always there;
    /// returns null for a sparse hand-written manifest, which falls back to trying each type.
    /// </summary>
    private static ManifestType? DetectManifestType(string manifestContents)
    {
        var document = ReadTopLevelKeys(manifestContents);

        if (document == null)
            return null;

        if (document.ContainsKey(nameof(SDK.Models.Manifest.Game.Title)))
            return ManifestType.Game;

        if (document.ContainsKey(nameof(SDK.Models.Manifest.Server.ServerConsoles))
            || document.ContainsKey(nameof(SDK.Models.Manifest.Server.HttpPaths)))
            return ManifestType.Server;

        if (document.ContainsKey(nameof(SDK.Models.Manifest.Tool.Games)))
            return ManifestType.Tool;

        if (document.ContainsKey(nameof(SDK.Models.Manifest.Redistributable.OptionSchema)))
            return ManifestType.Redistributable;

        return null;
    }

    /// <summary>The manifest's top-level keys, or null if it isn't a YAML mapping.</summary>
    private static Dictionary<string, object>? ReadTopLevelKeys(string manifestContents)
    {
        try
        {
            return new YamlDotNet.Serialization.DeserializerBuilder()
                .Build()
                .Deserialize<Dictionary<string, object>>(manifestContents);
        }
        catch
        {
            return null;
        }
    }

    // Keys only the old game manifest format has, and keys only the current one has
    private static readonly string[] LegacyOnlyKeys = ["Genre", "LanMultiplayer", "LocalMultiplayer", "OnlineMultiplayer", "DependentGames"];
    private static readonly string[] CurrentOnlyKeys = ["Genres", "MultiplayerModes", "Saves", "PlaySessions", "InstallTo", "ShowInLibrary"];

    /// <summary>Whether a manifest without a version is laid out the old way rather than the current one.</summary>
    private static bool HasLegacyLayout(string manifestContents)
    {
        var document = ReadTopLevelKeys(manifestContents);

        if (document == null)
            return true;

        return LegacyOnlyKeys.Any(document.ContainsKey) && !CurrentOnlyKeys.Any(document.ContainsKey);
    }

    private async Task<IEnumerable<IImportItemInfo>> InitializeByTypeAsync(ManifestType manifestType, string manifestContents)
    {
        switch (manifestType)
        {
            case ManifestType.Game:
                var gameManifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Game>(manifestContents);
                return await InitializeGameImportAsync(gameManifest);

            case ManifestType.Redistributable:
                var redistributableManifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Redistributable>(manifestContents);
                return await InitializeRedistributableImportAsync(redistributableManifest);

            case ManifestType.Server:
                var serverManifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Server>(manifestContents);
                return await InitializeServerImportAsync(serverManifest);

            case ManifestType.Tool:
                var toolManifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Tool>(manifestContents);
                return await InitializeToolImportAsync(toolManifest);

            default:
                throw new InvalidOperationException($"Unsupported manifest type: {manifestType}");
        }
    }

    public async Task InitializeMetadataUpdateAsync(SDK.Models.Manifest.Game gameManifest)
    {
        _developers.UseContext(this);
        _engines.UseContext(this);
        _games.UseContext(this);
        _genres.UseContext(this);
        _multiplayerModes.UseContext(this);
        _publishers.UseContext(this);
        _savePaths.UseContext(this);
        _tags.UseContext(this);

        Manifest = gameManifest;

        await AddAsync(gameManifest.Developers, _developers);

        if (gameManifest.Engine != null)
            await AddAsync(gameManifest.Engine, _engines);

        await AddAsync(gameManifest.Genres, _genres);
        await AddAsync(gameManifest.MultiplayerModes, _multiplayerModes);
        await AddAsync(gameManifest.Publishers, _publishers);
        await AddAsync(gameManifest.SavePaths, _savePaths);
        await AddAsync(gameManifest.Tags, _tags);
        await AddAsync(gameManifest, _games);
    }

    private async Task<IEnumerable<IImportItemInfo>> InitializeLegacyGameImportAsync(
        Legacy.Models.GameManifest gameManifest)
    {
        var manifest = gameManifest.UpdateManifest();

        if (gameManifest.Scripts != null)
            foreach (var script in gameManifest.Scripts)
            {
                _scripts.AddAsset(new ImportAssetArchiveEntry
                {
                    Name = script.Name,
                    RecordId = script.Id,
                    Path = $"Scripts/{script.Id}",
                });
            }
        
        if (gameManifest.Media != null)
            foreach (var media in gameManifest.Media)
            {
                _media.AddAsset(new ImportAssetArchiveEntry
                {
                    RecordId = media.Id,
                    Name = media.Type.ToString(),
                    Path = $"Media/{media.FileId}",
                });
            }
        
        if (gameManifest.Archives != null)
            foreach (var archive in gameManifest.Archives)
            {
                _archives.AddAsset(new ImportAssetArchiveEntry
                {
                    RecordId = archive.Id,
                    Name = archive.Version,
                    Path = $"Archives/{archive.ObjectKey}",
                });
            }
                    
        return await InitializeGameImportAsync(manifest);
    }

    private async Task<IEnumerable<IImportItemInfo>> InitializeGameImportAsync(SDK.Models.Manifest.Game gameManifest)
    {
        Manifest = gameManifest;
        
        var importItemInfo = new List<IImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Actions, _actions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Archives, _archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Collections, _collections).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.CustomFields, _customFields).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Developers, _developers).ToListAsync());
        
        if (gameManifest.Engine != null)
            importItemInfo.AddRange(await GetImportItemInfoAsync([gameManifest.Engine], _engines).ToListAsync());
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Genres, _genres).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Keys, _keys).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Media, _media).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.MultiplayerModes, _multiplayerModes).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Platforms, _platforms).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.PlaySessions, _playSessions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Publishers, _publishers).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Saves, _saves).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.SavePaths, _savePaths).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Scripts, _scripts).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Tags, _tags).ToListAsync());

        return importItemInfo;
    }

    private async Task<IEnumerable<IImportItemInfo>> InitializeRedistributableImportAsync(SDK.Models.Manifest.Redistributable redistributableManifest)
    {
        Manifest = redistributableManifest;
        
        var importItemInfo = new List<IImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Archives, _archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Scripts, _scripts).ToListAsync());

        return importItemInfo;
    }

    private async Task<IEnumerable<IImportItemInfo>> InitializeServerImportAsync(SDK.Models.Manifest.Server serverManifest)
    {
        Manifest = serverManifest;
        
        var importItemInfo = new List<IImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.Actions, _actions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.Scripts, _scripts).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.ServerConsoles, _serverConsoles).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.HttpPaths, _serverHttpPaths).ToListAsync());
        
        return importItemInfo;
    }
    
    private async Task<IEnumerable<IImportItemInfo>> InitializeToolImportAsync(SDK.Models.Manifest.Tool toolManifest)
    {
        Manifest = toolManifest;
        
        var importItemInfo = new List<IImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(toolManifest.Actions, _actions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(toolManifest.Archives, _archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(toolManifest.Scripts, _scripts).ToListAsync());

        return importItemInfo;
    }
    #endregion
    
    /// <summary>Queues the records to import.</summary>
    /// <param name="selectedKeys">
    /// Keys (<see cref="IImportItemInfo.Key"/>) of the records listed by
    /// <see cref="InitializeImportAsync"/> that should be imported, or null for all of them. The
    /// package's own game, redistributable, server or tool is always imported.
    /// </param>
    /// <param name="storageLocationId">Where archives go; the default archive location when not found.</param>
    public async Task PrepareImportQueueAsync(
        IEnumerable<string>? selectedKeys,
        Guid storageLocationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectedKeys = selectedKeys?.ToHashSet();

        // No location chosen (Guid.Empty), e.g. when an import is continued from the upload indicator
        // and the dialog never showed its location picker: archives go to the default location
        ArchiveStorageLocation = await _storageLocationService.GetOrDefaultAsync(storageLocationId, StorageLocationType.Archive);
        cancellationToken.ThrowIfCancellationRequested();

        if (Manifest is SDK.Models.Manifest.Game gameManifest)
            await AddAsync(gameManifest, cancellationToken);
        
        if (Manifest is SDK.Models.Manifest.Redistributable redistributableManifest)
            await AddAsync(redistributableManifest, cancellationToken);
        
        if (Manifest is SDK.Models.Manifest.Server serverManifest)
            await AddAsync(serverManifest, cancellationToken);
        
        if (Manifest is SDK.Models.Manifest.Tool toolManifest)
            await AddAsync(toolManifest, cancellationToken);
    }

    public async Task AddAsync(
        SDK.Models.Manifest.Game game,
        CancellationToken cancellationToken = default)
    {
        await AddAsync(game.Actions, _actions, cancellationToken);
        // Each archive becomes a version, newest last; packages may list them in any order
        await AddAsync(game.Archives?.OrderBy(a => a.CreatedOn), _archives, cancellationToken);
        await AddAsync(game.Collections, _collections, cancellationToken);
        await AddAsync(game.CustomFields, _customFields, cancellationToken);
        await AddAsync(game.Developers, _developers, cancellationToken);
        await AddAsync(game.Engine, _engines, cancellationToken);
        await AddAsync(game.Genres, _genres, cancellationToken);
        await AddAsync(game.Keys, _keys, cancellationToken);
        await AddAsync(game.Media, _media, cancellationToken);
        await AddAsync(game.MultiplayerModes, _multiplayerModes, cancellationToken);
        await AddAsync(game.Platforms, _platforms, cancellationToken);
        await AddAsync(game.PlaySessions, _playSessions, cancellationToken);
        await AddAsync(game.Publishers, _publishers, cancellationToken);
        await AddAsync(game.Saves, _saves, cancellationToken);
        await AddAsync(game.SavePaths, _savePaths, cancellationToken);
        await AddAsync(game.Scripts, _scripts, cancellationToken);
        await AddAsync(game.Tags, _tags, cancellationToken);
        await AddAsync(game, _games, cancellationToken);
    }

    public async Task AddAsync(
        SDK.Models.Manifest.Redistributable redistributable,
        CancellationToken cancellationToken = default)
    {
        await AddAsync(redistributable.Archives, _archives, cancellationToken);
        await AddAsync(redistributable.Scripts, _scripts, cancellationToken);
        await AddAsync(redistributable, _redistributables, cancellationToken);
    }

    public async Task AddAsync(
        SDK.Models.Manifest.Server server,
        CancellationToken cancellationToken = default)
    {
        await AddAsync(server.Actions, _actions, cancellationToken);
        await AddAsync(server.Scripts, _scripts, cancellationToken);
        await AddAsync(server.HttpPaths, _serverHttpPaths, cancellationToken);
        await AddAsync(server.ServerConsoles, _serverConsoles, cancellationToken);
        await AddAsync(server, _servers, cancellationToken);
    }
    
    public async Task AddAsync(
        SDK.Models.Manifest.Tool tool,
        CancellationToken cancellationToken = default)
    {
        await AddAsync(tool.Actions, _actions, cancellationToken);
        await AddAsync(tool.Archives, _archives, cancellationToken);
        await AddAsync(tool.Scripts, _scripts, cancellationToken);
        await AddAsync(tool, _tools, cancellationToken);
    }
    
    private async Task AddAsync<TRecord>(
        IEnumerable<TRecord> records,
        BaseImporter<TRecord> importer,
        CancellationToken cancellationToken = default)
        where TRecord : class
    {
        // A manifest written with an empty key ("Keys:") reads back as null rather than empty
        foreach (var record in records ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AddAsync(record, importer, cancellationToken);
        }
    }

    private async Task AddAsync<TRecord>(
        TRecord? record,
        BaseImporter<TRecord> importer,
        CancellationToken cancellationToken = default)
        where TRecord : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (record == null)
            return;

        // Records unchecked in the import dialog are left out; the package's own record never is
        if (SelectedKeys != null && !ReferenceEquals(record, Manifest) && !SelectedKeys.Contains(importer.GetKey(record)))
            return;

        if (!InQueue(record, importer) && await importer.CanImportAsync(record))
        {
            var importInfo = await importer.GetImportInfoAsync(record);
            
            importInfo.Key = importer.GetKey(record);

            if (ReferenceEquals(record, Manifest))
                _packageRecordKey = importInfo.Key;

            Queue.Enqueue(importInfo);
        }
    }

    /// <summary>Queue key of the package's own game, redistributable, server or tool.</summary>
    private string? _packageRecordKey;

    /// <summary>Whether the package's own record is still waiting to be imported.</summary>
    internal bool PackageRecordQueued => _packageRecordKey != null && Queue.Any(q => q.Key == _packageRecordKey);

    public async Task ImportQueueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Processed = 0;
        Total = Queue.Count;
        
        await OnImportStarted?.InvokeAsync(new ImportStatusUpdate
        {
            Index = Processed,
            Total = Total,
        })!;

        try
        {
            int deferred = 0;
            IImportItemInfo? packageRecordFailed = null;

            while (Queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var queueItem = Queue.Dequeue();

                await OnImportStatusUpdate?.InvokeAsync(new ImportStatusUpdate
                {
                    CurrentItem = queueItem,
                    Index = Processed,
                    Total = Total,
                })!;

                var outcome = await TryImportAsync(queueItem, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (outcome == ImportOutcome.Imported)
                {
                    Processed++;
                    deferred = 0;
                    continue;
                }

                if (outcome is ImportOutcome.Skipped or ImportOutcome.Failed)
                {
                    if (outcome == ImportOutcome.Failed && queueItem.Key == _packageRecordKey)
                        packageRecordFailed = queueItem;

                    deferred = 0;
                    continue;
                }

                // Waiting on the package's own record; retry once the rest of the queue has had a turn
                Queue.Enqueue(queueItem);
                deferred++;

                if (deferred >= Queue.Count)
                {
                    var remaining = Queue.Select(item => $"{item.Type} \"{item.Name}\"").ToList();

                    throw new InvalidOperationException(
                        $"Import could not finish. These records could not be imported: {String.Join(", ", remaining)}. Check the server log for details.");
                }
            }

            // Everything else was still tried, so a game that was saved but failed afterwards keeps
            // what it could, but without its own record the import as a whole didn't succeed
            if (packageRecordFailed != null)
                throw new InvalidOperationException(
                    $"{packageRecordFailed.Type} \"{packageRecordFailed.Name}\" could not be imported. {String.Join(" ", Errors)}".Trim());

            cancellationToken.ThrowIfCancellationRequested();
            await OnImportComplete?.InvokeAsync(new ImportStatusUpdate
            {
                Index = Total,
                Total = Total,
            })!;
        }
        finally
        {
            if (Id.HasValue)
                _importService.RemoveContext(Id.Value);
        }
    }

    private async Task<ImportOutcome> TryImportAsync(
        IImportItemInfo queueItem,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await ImportItemAsync(queueItem, cancellationToken);

            // The importer logged the cause
            if (outcome == ImportOutcome.Failed)
                await ReportErrorAsync(queueItem, $"{queueItem.Type} \"{queueItem.Name}\" could not be imported. Check the server log for details.");

            return outcome;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ImportSkippedException ex)
        {
            _logger.LogWarning("Skipped importing {RecordType} {RecordName}: {Reason}", queueItem.Type, queueItem.Name, ex.Message);

            await ReportErrorAsync(queueItem, $"{queueItem.Type} \"{queueItem.Name}\" was skipped: {ex.Message}");

            return ImportOutcome.Skipped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing record {RecordName}", queueItem.Name);

            await ReportErrorAsync(queueItem, $"{queueItem.Type} \"{queueItem.Name}\" could not be imported: {ex.Message}");

            return ImportOutcome.Failed;
        }
    }

    /// <summary>Records an error against a queued record and tells any listener, e.g. the import dialog.</summary>
    private async Task ReportErrorAsync(IImportItemInfo queueItem, string message)
    {
        Errors.Add(message);

        await OnImportError.InvokeAsync(new ImportStatusUpdate
        {
            CurrentItem = queueItem,
            Index = Processed,
            Total = Total,
            Error = message,
        });
    }

    private async Task<ImportOutcome> ImportItemAsync(
        IImportItemInfo queueItem,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (queueItem.Type)
        {
            case ImportExportRecordType.Action:
                return await _actions.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Archive:
                return await _archives.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Collection:
                return await _collections.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.CustomField:
                return await _customFields.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Developer:
                return await _developers.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Engine:
                return await _engines.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Game:
                return await _games.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Genre:
                return await _genres.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Key:
                return await _keys.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Media:
                return await _media.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.MultiplayerMode:
                return await _multiplayerModes.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Platform:
                return await _platforms.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.PlaySession:
                return await _playSessions.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Publisher:
                return await _publishers.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Redistributable:
                return await _redistributables.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Save:
                return await _saves.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.SavePath:
                return await _savePaths.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Script:
                return await _scripts.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Server:
                return await _servers.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.ServerConsole:
                return await _serverConsoles.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.ServerHttpPath:
                return await _serverHttpPaths.ImportQueuedAsync(queueItem, cancellationToken);

            case ImportExportRecordType.Tag:
                return await _tags.ImportQueuedAsync(queueItem, cancellationToken);
                
            case ImportExportRecordType.Tool:
                return await _tools.ImportQueuedAsync(queueItem, cancellationToken);
        }

        throw new InvalidOperationException($"No importer handles {queueItem.Type} records.");
    }
    
    private async IAsyncEnumerable<ImportItemInfo<TRecord>> GetImportItemInfoAsync<TRecord>(IEnumerable<TRecord> records,
        BaseImporter<TRecord> importer) where TRecord : class
    {
        if (records != null)
            foreach (var record in records)
            {
                if (record != null && record.GetType() == typeof(TRecord))
                {
                    var importInfo = await importer.GetImportInfoAsync(record);
                    
                    importInfo.Key = importer.GetKey(record);
                    
                    yield return importInfo;
                }
            }
    }

    /// <summary>
    /// Closes the package and releases the context, whether or not it ever ran: a cancelled dialog
    /// or a package that failed to read would otherwise stay registered for the life of the server.
    /// </summary>
    public void Dispose()
    {
        if (Archive != null)
            Archive.Dispose();

        if (Id.HasValue)
            _importService.RemoveContext(Id.Value);

        _scope?.Dispose();
    }
}