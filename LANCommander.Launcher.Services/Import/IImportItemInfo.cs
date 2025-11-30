namespace LANCommander.Launcher.Services.Import;

public interface IImportItemInfo
{
    string Key { get; set; }
    string Type { get; set; }
    string Name { get; set; }
    bool Processed { get; set; }
}