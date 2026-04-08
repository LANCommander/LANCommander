using System;

namespace LANCommander.SDK.Models.Pack;

[Flags]
public enum PackFlags : ushort
{
    None = 0,
    HasDirectory = 1 << 0,
}
