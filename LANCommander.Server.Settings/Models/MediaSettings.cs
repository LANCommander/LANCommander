using Humanizer.Bytes;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Settings.Models;

    public class MediaSettings
    {
        public string SteamGridDbApiKey { get; set; } = String.Empty;

        public IEnumerable<MediaTypeSettings> MediaTypes { get; set; } =
            new List<MediaTypeSettings>()
            {
                new MediaTypeSettings
                {
                    Type = MediaType.Icon,
                    MaxFileSize = 2 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(32, 32),
                        MaxSize = new ThumbnailSize(128, 128),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Cover,
                    MaxFileSize = 6 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(240, 360),
                        MaxSize = new ThumbnailSize(600, 900),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Background,
                    MaxFileSize = 8 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(690, 310),
                        MaxSize = new ThumbnailSize(3840, 1240),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Avatar,
                    MaxFileSize = 2 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(64, 64),
                        MaxSize = new ThumbnailSize(256, 256),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Logo,
                    MaxFileSize = 4 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(400, 400),
                        MaxSize = new ThumbnailSize(1000, 1000),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Manual,
                    MaxFileSize = 4 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(240, 360),
                        MaxSize = new ThumbnailSize(600, 900),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.PageImage,
                    MaxFileSize = 4 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(480, 480),
                        MaxSize = new ThumbnailSize(1920, 1920),
                    }
                },
            };

        public MediaTypeSettings? GetMediaTypeConfig(MediaType type) => MediaTypes.FirstOrDefault(x => x.Type == type);
    }