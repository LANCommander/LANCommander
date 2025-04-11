using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class PlaySessionImporter(
    PlaySessionService playSessionService,
    UserService userService,
    ImportContext importContext) : IImporter<PlaySession, Data.Models.PlaySession>
{
    public async Task<ImportItemInfo> InfoAsync(PlaySession record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.PlaySessions,
            Name = $"{record.User} - {record.Start}-{record.End}",
        };
    }

    public bool CanImport(PlaySession record) => importContext.DataRecord is Data.Models.Game;

    public async Task<Data.Models.PlaySession> AddAsync(PlaySession record)
    {
        try
        {
            var playSession = new Data.Models.PlaySession
            {
                Start = record.Start,
                End = record.End,
                User = await userService.GetAsync(record.User),
                Game = importContext.DataRecord as Data.Models.Game,
            };

            playSession = await playSessionService.AddAsync(playSession);

            return playSession;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<PlaySession>(record, "An unknown error occured while importing playSession", ex);
        }
    }

    public async Task<Data.Models.PlaySession> UpdateAsync(PlaySession record)
    {
        var game = importContext.DataRecord as Data.Models.Game;
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

    public async Task<bool> ExistsAsync(PlaySession record)
    {
        var game = importContext.DataRecord as Data.Models.Game;
        var user = await userService.GetAsync(record.User);
        
        return await playSessionService.ExistsAsync(ps => (ps.Game.Id == game.Id || ps.Game.Title == game.Title) && ps.Start == record.Start && ps.UserId == user.Id);
    }
}