using LANCommander.Server.Services.Enums;

namespace LANCommander.Server.Services.Models;

public class ServerStatusUpdateEventArgs : EventArgs
{
    public Data.Models.Server Server { get; private set; }
    public ServerProcessStatus Status { get; private set; }
    public Exception Exception { get; private set; }

    public ServerStatusUpdateEventArgs(Data.Models.Server server, ServerProcessStatus status)
    {
        Server = server;
        Status = status;
    }

    public ServerStatusUpdateEventArgs(Data.Models.Server server, ServerProcessStatus status, Exception exception) : this(server, status)
    {
        Exception = exception;
    }
}
