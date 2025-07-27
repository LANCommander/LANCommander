using AutoMapper;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Exporters;

public class GameExporter(
    GameService gameService) : BaseExporter<Game, Data.Models.Game>
{
    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Game record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Name = record.Title,
        };
    }

    public override bool CanExport(Game record) => true;

    public override async Task<Game> ExportAsync(Guid id)
    {
        // Retrieve the game again, but only get the minimal amount of data required
        var manifest = await gameService.GetAsync<Game>(id);
        
        manifest.Actions ??= new List<SDK.Models.Manifest.Action>();
        manifest.Archives ??= new List<SDK.Models.Manifest.Archive>();
        manifest.Collections ??= new List<SDK.Models.Manifest.Collection>();
        manifest.CustomFields ??= new List<SDK.Models.Manifest.GameCustomField>();
        manifest.Developers ??= new List<SDK.Models.Manifest.Company>();
        manifest.Genres ??= new List<SDK.Models.Manifest.Genre>();
        manifest.Keys ??= new List<SDK.Models.Manifest.Key>();
        manifest.Media ??= new List<SDK.Models.Manifest.Media>();
        manifest.MultiplayerModes ??= new List<SDK.Models.Manifest.MultiplayerMode>();
        manifest.Platforms ??= new List<SDK.Models.Manifest.Platform>();
        manifest.PlaySessions ??= new List<SDK.Models.Manifest.PlaySession>();
        manifest.Publishers ??= new List<SDK.Models.Manifest.Company>();
        manifest.Saves ??= new List<SDK.Models.Manifest.Save>();
        manifest.SavePaths ??= new List<SDK.Models.Manifest.SavePath>();
        manifest.Scripts ??= new List<SDK.Models.Manifest.Script>();
        manifest.Tags ??= new List<SDK.Models.Manifest.Tag>();

        return manifest;
    }
} 