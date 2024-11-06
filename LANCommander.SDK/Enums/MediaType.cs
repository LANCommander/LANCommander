using System;
using System.Collections.Generic;
using System.Text;

namespace LANCommander.SDK.Enums
{
    public enum MediaType
    {
        Icon,
        Cover,
        Background,
        Avatar,
        Logo,
        Manual,
        [Obsolete("Thumbnails are no longer stored in the database")]
        Thumbnail,
        PageImage
    }
}
