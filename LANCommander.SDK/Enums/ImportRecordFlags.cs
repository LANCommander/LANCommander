using System;

namespace LANCommander.SDK.Enums;

[Flags]
public enum ImportRecordFlags
{
    Metadata = 0,
    Redistributables = 1,
    Media = 2,
    Archives = 4,
    Actions = 8,
    MultiplayerModes = 16,
    SavePaths = 32,
    Keys = 64,
    Scripts = 128,
    CustomFields = 256,
    PlaySessions = 512,
    Saves = 1024,
    Addons = 2048,
}