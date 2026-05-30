namespace LANCommander.Packager.Models;

public class RegistryChangeEntry
{
    public string Verb { get; set; } = string.Empty;
    public string KeyPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
}
