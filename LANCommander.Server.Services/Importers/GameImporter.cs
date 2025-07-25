using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class GameImporter(
    IMapper mapper,
    GameService gameService,
    UserService userService) : BaseImporter<Game, Data.Models.Game>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Game record)
    {
        return new ImportItemInfo
        {
            Name = record.Title,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Game record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Name = record.Title,
        };
    }

    public override bool CanImport(Game record) => true;
    public override bool CanExport(Game record) => true;

    public override async Task<Data.Models.Game> AddAsync(Game record)
    {
        var game = mapper.Map<Data.Models.Game>(record);

        try
        {
            return await gameService.AddAsync(game);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to add game", ex);
        }
    }

    public override async Task<Data.Models.Game> UpdateAsync(Game record)
    {
        var existing = await gameService.FirstOrDefaultAsync(g => g.Id == record.Id || g.Title == record.Title);

        try
        {
            existing.Title = record.Title;
            existing.SortTitle = record.SortTitle;
            existing.Description = record.Description;
            existing.Notes = record.Notes;
            existing.ReleasedOn = record.ReleasedOn;
            existing.Singleplayer = record.Singleplayer;
            existing.Type = record.Type;
            existing.IGDBId = record.IGDBId;
            existing.CreatedBy = await userService.GetAsync(record.CreatedBy);
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedBy = await userService.GetAsync(record.UpdatedBy);
            existing.DirectoryName = record.DirectoryName;

            existing = await gameService.UpdateAsync(existing);
            
            // importContext.UseRecord(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to update game", ex);
        }
    }

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

    public override async Task<bool> ExistsAsync(Game record)
    {
        return await gameService.ExistsAsync(g => g.Id == record.Id || g.Title == record.Title);
    }
}
