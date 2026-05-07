using LANCommander.SDK.Models.Manifest;
using HqModels = LANCommander.HQ.SDK.Models;

namespace LANCommander.Server.Services.HQ;

public static class HqGameMapper
{
    public static Game ToGame(HqModels.GameDto dto)
    {
        var game = new Game
        {
            Id = dto.Id,
            IGDBId = dto.IGDBId,
            Title = dto.Title,
            SortTitle = dto.SortTitle ?? dto.Title,
            Description = dto.Description,
            ReleasedOn = dto.ReleasedOn ?? default,
            Singleplayer = dto.Singleplayer,
            Type = MapGameType(dto.Type),
        };

        if (dto.Engine is not null)
            game.Engine = new Engine { Name = dto.Engine.Name };

        if (dto.Genres.Any())
            game.Genres = dto.Genres.Select(g => new Genre { Name = g.Name }).ToList();

        if (dto.Tags.Any())
            game.Tags = dto.Tags.Select(t => new Tag { Name = t.Name }).ToList();

        if (dto.Developers.Any())
            game.Developers = dto.Developers.Select(d => new Company { Name = d.Name }).ToList();

        if (dto.Publishers.Any())
            game.Publishers = dto.Publishers.Select(p => new Company { Name = p.Name }).ToList();

        if (dto.MultiplayerModes.Any())
            game.MultiplayerModes = dto.MultiplayerModes.Select(MapMultiplayerMode).ToList();

        if (dto.SavePaths.Any())
            game.SavePaths = dto.SavePaths.Select(MapSavePath).ToList();

        return game;
    }

    private static SDK.Enums.GameType MapGameType(HqModels.GameType hqType) => hqType switch
    {
        HqModels.GameType.MainGame => SDK.Enums.GameType.MainGame,
        HqModels.GameType.Addon => SDK.Enums.GameType.Expansion,
        HqModels.GameType.Expansion => SDK.Enums.GameType.Expansion,
        HqModels.GameType.StandaloneExpansion => SDK.Enums.GameType.StandaloneExpansion,
        HqModels.GameType.Mod => SDK.Enums.GameType.Mod,
        _ => SDK.Enums.GameType.MainGame,
    };

    private static MultiplayerMode MapMultiplayerMode(HqModels.MultiplayerModeDto dto) => new()
    {
        Type = (SDK.Enums.MultiplayerType)(int)dto.Type,
        NetworkProtocol = MapNetworkProtocol(dto.NetworkProtocol),
        MinPlayers = dto.MinPlayers ?? 0,
        MaxPlayers = dto.MaxPlayers ?? 0,
    };

    private static SDK.Enums.NetworkProtocol MapNetworkProtocol(HqModels.NetworkProtocol? hqProtocol) => hqProtocol switch
    {
        HqModels.NetworkProtocol.TCP => SDK.Enums.NetworkProtocol.TCPIP,
        HqModels.NetworkProtocol.UDP => SDK.Enums.NetworkProtocol.TCPIP,
        HqModels.NetworkProtocol.IPX => SDK.Enums.NetworkProtocol.IPX,
        _ => SDK.Enums.NetworkProtocol.TCPIP,
    };

    private static SavePath MapSavePath(HqModels.SavePathDto dto) => new()
    {
        Type = (SDK.Enums.SavePathType)(int)dto.Type,
        Path = dto.Path,
        WorkingDirectory = dto.WorkingDirectory,
        IsRegex = dto.IsRegex,
    };
}
