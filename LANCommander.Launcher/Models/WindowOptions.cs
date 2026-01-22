using LANCommander.Launcher.Enums;

namespace LANCommander.Launcher.Models;

public class WindowOptions
{
    public Type? RootComponentType { get; set; }
    public string? Title { get; set; }
    public WindowType Type { get; set; }
}