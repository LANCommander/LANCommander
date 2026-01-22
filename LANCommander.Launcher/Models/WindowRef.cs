using LANCommander.Launcher.Enums;
using Photino.NET;

namespace LANCommander.Launcher.Models;

public class WindowRef
{
    public WindowType Type { get; set; }
    public PhotinoWindow Window { get; set; }
}