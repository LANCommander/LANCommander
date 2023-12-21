using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Models
{
    public class ServerConsole : BaseModel
    {
        public string Name { get; set; } = "";
        public ServerConsoleType Type { get; set; }

        public string Path { get; set; } = "";

        public string Host { get; set; } = "";
        public int? Port { get; set; }
    }
}
