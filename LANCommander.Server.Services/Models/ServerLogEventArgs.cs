using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Models;

public class ServerLogEventArgs : EventArgs
{
    public string Line { get; private set; }
    public ServerConsole Log { get; private set; }

    public ServerLogEventArgs(string line, ServerConsole console)
    {
        Line = line;
        Log = console;
    }
}