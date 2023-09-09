namespace LANCommander.Components.FileManagerComponents
{
    public class FileManagerFile : FileManagerEntry
    {
        public string Extension => Name.Contains('.') ? Name.Split('.').Last() : Name;
    }
}
