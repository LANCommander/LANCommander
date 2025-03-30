namespace LANCommander.Server.Services.Enums;

[Flags]
public enum ImportGameOptions
{
    Actions = 1 << 0,
    Archives = 1 << 1,
    Collections = 1 << 2,
    CustomFields = 1 << 3,
    Developers = 1 << 4,
    Engine = 1 << 5,
    Genres = 1 << 6,
    Keys = 1 << 7,
    Media = 1 << 8,
    MultiplayerModes = 1 << 9,
    Platforms = 1 << 10,
    Publishers = 1 << 11,
    Saves = 1 << 12,
    SavePaths = 1 << 13,
    Scripts = 1 << 14,
    Tags = 1 << 15,
}