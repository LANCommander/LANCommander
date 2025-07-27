using System;

namespace LANCommander.SDK.Enums;

[Flags]
public enum ExportRecordFlags
{
    None = 0,
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
    PlaySessions = 1 << 11,
    Publishers = 1 << 12,
    Saves = 1 << 13,
    SavePaths = 1 << 14,
    Scripts = 1 << 15,
    ServerConsoles = 1 << 16,
    ServerHttpPaths = 1 << 17,
    Tags = 1 << 18,
}