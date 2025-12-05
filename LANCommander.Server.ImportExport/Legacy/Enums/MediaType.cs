namespace LANCommander.Server.ImportExport.Legacy.Enums;

internal enum MediaType
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