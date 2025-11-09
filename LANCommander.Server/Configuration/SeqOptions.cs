using LANCommander.Server.Configuration;

namespace LANCommander.Server.Parsers;

public class SeqOptions
{
    [ConnectionStringKey("Server")]
    public string ServerUrl { get; set; }
    
    [ConnectionStringKey("ApiKey")]
    public string ApiKey { get; set; }

    public string ToConnectionString() =>
        new ConnectionStringBuilder()
            .Add("Server", ServerUrl)
            .AddIf(() => ApiKey.Length > 0, "ApiKey", ApiKey)
            .Build();

    public static string DefaultConnectionString => "Server=http://localhost:5341;ApiKey=ABC1234;";
}