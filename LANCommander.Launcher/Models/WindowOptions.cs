using LANCommander.Launcher.Enums;

namespace LANCommander.Launcher.Models;

public class WindowOptions
{
    public Type? RootComponentType { get; set; }
    public string? Title { get; set; }
    public WindowType Type { get; set; }
    public bool CustomWindow { get; set; } = true;
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 960;
}