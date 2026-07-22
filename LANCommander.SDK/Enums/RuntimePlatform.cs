using System;

namespace LANCommander.SDK.Enums
{
    [Flags]
    public enum RuntimePlatform
    {
        None = 0,
        Windows = 1 << 0,
        Linux = 1 << 1,
        macOS = 1 << 2,
    }
}
