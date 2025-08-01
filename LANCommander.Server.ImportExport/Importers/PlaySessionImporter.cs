using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class PlaySessionImporter(
    IMapper mapper,
    PlaySessionService playSessionService,
    UserService userService) : BaseImporter<PlaySession, Data.Models.PlaySession>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(PlaySession record)
    {
        return new ImportItemInfo
        {
            Type = ImportExportRecordType.PlaySession,
            Name = $"{record.User} - {record.Start}-{record.End}",
        };
    }

    public override bool CanImport(PlaySession record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.PlaySession> AddAsync(PlaySession record)
    {
        try
        {
            var playSession = new Data.Models.PlaySession
            {
                Start = record.Start,
                End = record.End,
                User = await userService.GetAsync(record.User),
                Game = ImportContext.DataRecord as Data.Models.Game,
            };

            playSession = await playSessionService.AddAsync(playSession);

            return playSession;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<PlaySession>(record, "An unknown error occured while importing playSession", ex);
        }
    }

    public override async Task<Data.Models.PlaySession> UpdateAsync(PlaySession record)
    {
        var game = ImportContext.DataRecord as Data.Models.Game;
        var user = await userService.GetAsync(record.User);

        var existing = await playSessionService.FirstOrDefaultAsync(ps => ps.GameId == game.Id && ps.Start == record.Start && ps.UserId == user.Id);

        try
        {
            existing.Start = record.Start;
            existing.End = record.End;
            
            existing = await playSessionService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<PlaySession>(record, "An unknown error occured while importing playSession", ex);
        }
    }

    public override async Task<bool> ExistsAsync(PlaySession record)
    {
        var game = ImportContext.DataRecord as Data.Models.Game;
        var user = await userService.GetAsync(record.User);
        
        return await playSessionService.ExistsAsync(ps => (ps.Game.Id == game.Id || ps.Game.Title == game.Title) && ps.Start == record.Start && ps.UserId == user.Id);
    }
}