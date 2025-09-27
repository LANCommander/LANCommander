using LANCommander.SDK.Services;

namespace LANCommander.SDK;

public class Client(
    AuthenticationClient authenticationClient,
    BeaconClient beaconClient,
    ChatClient chatClient,
    IConnectionClient connectionClient,
    DepotClient depotClient,
    GameClient gameClient,
    IssueClient issueClient,
    LauncherClient launcherClient,
    LibraryClient libraryClient,
    LobbyClient lobbyClient,
    MediaClient mediaClient,
    PlaySessionClient playSessionClient,
    ProfileClient profileClient,
    RedistributableClient redistributableClient,
    SaveClient saveClient,
    ScriptClient scriptClient,
    ServerClient serverClient,
    TagClient tagClient)
{
    public AuthenticationClient Authentication = authenticationClient;
    public BeaconClient Beacon = beaconClient;
    public ChatClient Chat = chatClient;
    public IConnectionClient Connection = connectionClient;
    public DepotClient Depot = depotClient;
    public GameClient Games = gameClient;
    public IssueClient Issues = issueClient;
    public LauncherClient Launcher = launcherClient;
    public LibraryClient Library = libraryClient;
    public LobbyClient Lobbies = lobbyClient;
    public MediaClient Media = mediaClient;
    public PlaySessionClient PlaySessions = playSessionClient;
    public ProfileClient Profile = profileClient;
    public RedistributableClient Redistributables = redistributableClient;
    public SaveClient Saves = saveClient;
    public ScriptClient Scripts = scriptClient;
    public ServerClient Servers = serverClient;
    public TagClient Tags = tagClient;
}