using LANCommander.Launcher.Data.Models;

namespace LANCommander.Launcher.Services.Extensions;

public static class GameExtensions
{
    public static SDK.Models.Game ToSdkGame(this Game game) => new()
    {
        Id               = game.Id,
        Title            = game.Title ?? "Unknown",
        SortTitle        = game.SortTitle,
        Description      = game.Description,
        Notes            = game.Notes,
        ReleasedOn       = game.ReleasedOn ?? DateTime.MinValue,
        Singleplayer     = game.Singleplayer,
        Type             = game.Type,
        BaseGameId       = game.BaseGameId ?? Guid.Empty,
        InstallDirectory = game.InstallDirectory,
        InLibrary        = true,
        CreatedOn        = game.CreatedOn,
        UpdatedOn        = game.UpdatedOn,

        Engine = game.Engine == null
            ? null
            : new SDK.Models.Engine { Id = game.Engine.Id, Name = game.Engine.Name },

        Media = game.Media?
            .OrderBy(m => m.SortOrder)
            .Select(m => new SDK.Models.Media
            {
                Id        = m.Id,
                FileId    = m.FileId,
                Name      = m.Name,
                Type      = m.Type,
                SourceUrl = m.SourceUrl,
                MimeType  = m.MimeType,
                Crc32     = m.Crc32,
                SortOrder = m.SortOrder,
            })
            .ToList(),

        Genres      = game.Genres?.Select(g => new SDK.Models.Genre { Id = g.Id, Name = g.Name }).ToList(),
        Tags        = game.Tags?.Select(t => new SDK.Models.Tag { Id = t.Id, Name = t.Name }).ToList(),
        Platforms   = game.Platforms?.Select(p => new SDK.Models.Platform { Id = p.Id, Name = p.Name }).ToList(),
        Collections = game.Collections?.Select(c => new SDK.Models.Collection { Id = c.Id, Name = c.Name }).ToList(),
        Developers  = game.Developers?.Select(c => new SDK.Models.Company { Id = c.Id, Name = c.Name }).ToList(),
        Publishers  = game.Publishers?.Select(c => new SDK.Models.Company { Id = c.Id, Name = c.Name }).ToList(),

        MultiplayerModes = game.MultiplayerModes?
            .Select(m => new SDK.Models.MultiplayerMode
            {
                Id              = m.Id,
                Type            = m.Type,
                NetworkProtocol = m.NetworkProtocol,
                Description     = m.Description,
                MinPlayers      = m.MinPlayers,
                MaxPlayers      = m.MaxPlayers,
                Spectators      = m.Spectators,
            })
            .ToList(),

        Tools = game.Tools?
            .Select(t => new SDK.Models.Tool
            {
                Id          = t.Id,
                Name        = t.Name,
                Description = t.Description,
                Notes       = t.Notes,
            })
            .ToList(),

        DependentGames = game.DependentGames?.Select(g => g.Id).ToList(),
    };
}
