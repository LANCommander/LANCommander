using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class PlaySessionImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<PlaySession, Data.Models.PlaySession>
{
    PlaySessionService _playSessionService = serviceProvider.GetRequiredService<PlaySessionService>();
    UserService _userService = serviceProvider.GetRequiredService<UserService>();
    
    public async Task<Data.Models.PlaySession> AddAsync(PlaySession record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<PlaySession>(record, $"Cannot import playSessions for a {typeof(TParentRecord).Name}");
        
        try
        {
            var playSession = new Data.Models.PlaySession
            {
                Start = record.Start,
                End = record.End,
                User = await _userService.GetAsync(record.User),
            };

            playSession = await _playSessionService.AddAsync(playSession);

            return playSession;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<PlaySession>(record, "An unknown error occured while importing playSession", ex);
        }
    }

    public async Task<Data.Models.PlaySession> UpdateAsync(PlaySession record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<PlaySession>(record, $"Cannot import playSessions for a {typeof(TParentRecord).Name}");
        
        var user = await _userService.GetAsync(record.User);

        var existing = await _playSessionService.FirstOrDefaultAsync(ps => ps.GameId == game.Id && ps.Start == record.Start && ps.UserId == user.Id);

        try
        {
            existing.Start = record.Start;
            existing.End = record.End;
            
            existing = await _playSessionService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<PlaySession>(record, "An unknown error occured while importing playSession", ex);
        }
    }

    public async Task<bool> ExistsAsync(PlaySession record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<PlaySession>(record, $"Cannot import play sessions for a {typeof(TParentRecord).Name}");
        
        var user = await _userService.GetAsync(record.User);
        
        return await _playSessionService.ExistsAsync(ps => ps.GameId == game.Id && ps.Start == record.Start && ps.UserId == user.Id);
    }
}