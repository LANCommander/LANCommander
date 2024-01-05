namespace LANCommander.UI.Components.FileManagerComponents
{
    public class FileManagerFile : FileManagerEntry
    {
        public string Extension => Name.Contains('.') ? Name.Split('.').Last() : Name;

        public string GetIcon()
        {
            switch (Extension)
            {
                case "":
                    return "folder";

                case "exe":
                    return "code";

                case "zip":
                case "rar":
                case "7z":
                case "gz":
                case "tar":
                    return "file-zip";

                case "wad":
                case "pk3":
                case "pak":
                case "cab":
                    return "file-zip";

                case "txt":
                case "cfg":
                case "config":
                case "ini":
                case "yml":
                case "yaml":
                case "log":
                case "doc":
                case "nfo":
                    return "file-text";

                case "bat":
                case "ps1":
                case "json":
                    return "code";

                case "bik":
                case "avi":
                case "mov":
                case "mp4":
                case "m4v":
                case "mkv":
                case "wmv":
                case "mpg":
                case "mpeg":
                case "flv":
                    return "video-camera";

                case "dll":
                    return "api";

                case "hlp":
                    return "file-unknown";

                case "png":
                case "bmp":
                case "jpeg":
                case "jpg":
                case "gif":
                    return "file-image";

                default:
                    return "file";
            }
        }
    }
}
