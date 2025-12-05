using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Legacy.Enums;

namespace LANCommander.Server.ImportExport.Legacy.Models;

internal class GameManifest : IKeyedModel
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? SortTitle { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public GameType Type { get; set; }
    public DateTime ReleasedOn { get; set; }
    public string? Engine { get; set; }
    public IEnumerable<string>? Genre { get; set; }
    public IEnumerable<string>? Tags { get; set; }
    public IEnumerable<string>? Publishers { get; set; }
    public IEnumerable<string>? Developers { get; set; }
    public IEnumerable<string>? Collections { get; set; }
    public string? Version { get; set; }
    public IEnumerable<Action>? Actions { get; set; }
    public bool Singleplayer { get; set; }
    public MultiplayerInfo? LocalMultiplayer { get; set; }
    public MultiplayerInfo? LanMultiplayer { get; set; }
    public MultiplayerInfo? OnlineMultiplayer { get; set; }
    public IEnumerable<SavePath>? SavePaths { get; set; }
    public IEnumerable<string>? Keys { get; set; }
    public IEnumerable<Script>? Scripts { get; set; }
    public IEnumerable<Media>? Media { get; set; }
    public IEnumerable<Archive>? Archives { get; set; }
    public IEnumerable<Guid>? DependentGames { get; set; }
    public IEnumerable<Redistributable>? Redistributables { get; set; }
    public IEnumerable<GameCustomField>? CustomFields { get; set; }

    public Game UpdateManifest()
    {
        var manifest = new Game
        {
            Id = Id,
            Title = Title,
            SortTitle = SortTitle,
            Description = Description,
            Notes = Notes,
            Type = (SDK.Enums.GameType)(int)Type,
            ReleasedOn = ReleasedOn,
            Version = Version,
            Singleplayer = Singleplayer,
        };

        if (!String.IsNullOrEmpty(Engine))
            manifest.Engine = new Engine
            {
                Name = Engine,
            };

        if (Genre != null && Genre.Any())
            manifest.Genres = Genre.Select(g => new Genre { Name = g }).ToList();
        
        if (Tags != null && Tags.Any())
            manifest.Tags = Tags.Select(t => new Tag { Name = t }).ToList();
        
        if (Publishers != null && Publishers.Any())
            manifest.Publishers = Publishers.Select(p => new Company { Name = p }).ToList();
        
        if (Developers != null && Developers.Any())
            manifest.Developers = Developers.Select(d => new Company { Name = d }).ToList();
        
        if (Collections != null && Collections.Any())
            manifest.Collections = Collections.Select(c => new Collection { Name = c }).ToList();
        
        if (Actions != null && Actions.Any())
            manifest.Actions = Actions.Select(a => new SDK.Models.Manifest.Action
            {
                Name = a.Name,
                Arguments = a.Arguments,
                IsPrimaryAction = a.IsPrimaryAction,
                Path = a.Path,
                SortOrder = a.SortOrder,
                WorkingDirectory = a.WorkingDirectory,
            }).ToList();
        
        if (SavePaths != null && SavePaths.Any())
            manifest.SavePaths = SavePaths.Select(s => new SDK.Models.Manifest.SavePath
            {
                Id = s.Id,
                Path = s.Path,
                WorkingDirectory = s.WorkingDirectory,
                Type = (SDK.Enums.SavePathType)(int)s.Type,
                IsRegex = s.IsRegex,
                Entries = s.Entries != null || s.Entries.Any() ? s.Entries.Select(e => new SDK.Models.SavePathEntry
                {
                    ActualPath = e.ActualPath,
                    ArchivePath = e.ArchivePath,
                }) : [],
            }).ToList();
        
        if (Keys != null && Keys.Any())
            manifest.Keys = Keys.Select(k => new SDK.Models.Manifest.Key { Value = k }).ToList();

        if (Scripts != null && Scripts.Any())
            manifest.Scripts = Scripts.Select(s => new SDK.Models.Manifest.Script
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                RequiresAdmin = s.RequiresAdmin,
                Type = (SDK.Enums.ScriptType)(int)s.Type,
            }).ToList();

        if (Media != null && Media.Any())
            manifest.Media = Media.Select(m => new SDK.Models.Manifest.Media
            {
                Id = m.Id,
                FileId = m.FileId,
                Name = m.Name,
                Crc32 = m.Crc32,
                SourceUrl = m.SourceUrl,
                MimeType = m.MimeType,
                Type = (SDK.Enums.MediaType)(int)m.Type,
            }).ToList();

        if (Archives != null && Archives.Any())
            manifest.Archives = Archives.Select(a => new SDK.Models.Manifest.Archive
            {
                Id = a.Id,
                Version = a.Version,
                Changelog = a.Changelog,
                CompressedSize = a.CompressedSize,
                UncompressedSize = a.UncompressedSize,
                ObjectKey = a.ObjectKey,
            }).ToList();

        if (CustomFields != null && CustomFields.Any())
            manifest.CustomFields = CustomFields.Select(cf => new SDK.Models.Manifest.GameCustomField
            {
                Name = cf.Name,
                Value = cf.Value,
            }).ToList();

        manifest.MultiplayerModes = new List<MultiplayerMode>();
        
        if (LanMultiplayer != null)
            manifest.MultiplayerModes.Add(new MultiplayerMode
            {
                Description = LanMultiplayer.Description,
                MaxPlayers = LanMultiplayer.MaxPlayers,
                MinPlayers = LanMultiplayer.MinPlayers,
                NetworkProtocol = (SDK.Enums.NetworkProtocol)(int)LanMultiplayer.NetworkProtocol,
            });
        
        if (LocalMultiplayer != null)
            manifest.MultiplayerModes.Add(new MultiplayerMode
            {
                Description = LocalMultiplayer.Description,
                MaxPlayers = LocalMultiplayer.MaxPlayers,
                MinPlayers = LocalMultiplayer.MinPlayers,
                NetworkProtocol = (SDK.Enums.NetworkProtocol)(int)LocalMultiplayer.NetworkProtocol,
            });
        
        if (OnlineMultiplayer != null)
            manifest.MultiplayerModes.Add(new MultiplayerMode
            {
                Description = OnlineMultiplayer.Description,
                MaxPlayers = OnlineMultiplayer.MaxPlayers,
                MinPlayers = OnlineMultiplayer.MinPlayers,
                NetworkProtocol = (SDK.Enums.NetworkProtocol)(int)OnlineMultiplayer.NetworkProtocol,
            });

        return manifest;
    }
}