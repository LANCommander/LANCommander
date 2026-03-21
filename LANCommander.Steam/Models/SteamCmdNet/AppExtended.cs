using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class AppExtended
{
    [JsonPropertyName("aliases")]
    public string? Aliases { get; set; }

    [JsonPropertyName("developer")]
    public string? Developer { get; set; }
    
    [JsonPropertyName("developer_url")]
    public string? DeveloperUrl { get; set; }

    [JsonPropertyName("dlcavailableonstore")]
    public string? DLCAvailableOnStore { get; set; }

    [JsonPropertyName("gamedir")]
    public string? GameDirectory { get; set; }
    
    [JsonPropertyName("gamemanualurl")]
    public string? GameManualUrl { get; set; }

    [JsonPropertyName("homepage")]
    public string? HomePage { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
    
    [JsonPropertyName("installscript")]
    public string? InstallScript { get; set; }
    
    [JsonPropertyName("installscript_macos")]
    public string? InstallScriptMacOS { get; set; }
    
    [JsonPropertyName("installscript_osx")]
    public string? InstallScriptOSX { get; set; }

    [JsonPropertyName("isfreeapp")]
    public string? IsFreeApp { get; set; }

    [JsonPropertyName("languages_macos")]
    public string? LanguagesMacOS { get; set; }
    
    [JsonPropertyName("launcheula")]
    public string? LaunchEula { get; set; }
    
    [JsonPropertyName("launchulamask")]
    public string? LaunchEulaMask { get; set; }
    
    [JsonPropertyName("legacykeyregistrationmethod")]
    public string? LegacyKeyRegistrationMethod { get; set; }
    
    [JsonPropertyName("legacykeyregistrylocation")]
    public string? LegacyKeyRegistryLocation { get; set; }

    [JsonPropertyName("listofdlc")]
    public string? ListOfDLC { get; set; }

    [JsonPropertyName("loadallbeforelaunch")]
    public string? LoadAllBeforeLaunch { get; set; }

    [JsonPropertyName("minclientversion")]
    public string? MinimumClientVersion { get; set; }

    [JsonPropertyName("minclientversion_pw_csgo")]
    public string? MinimumClientVersionPwCsgo { get; set; }

    [JsonPropertyName("noservers")]
    public string? NoServers { get; set; }
    
    [JsonPropertyName("order")]
    public int? Order { get; set; }

    [JsonPropertyName("primarycache")]
    public long? PrimaryCache { get; set; }
    
    [JsonPropertyName("primarycache_mac")]
    public long? PrimaryCacheMac { get; set; }

    [JsonPropertyName("primarycache_macos")]
    public long? PrimaryCacheMacOS { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("serverbrowsername")]
    public string? ServerBrowserName { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("vacmacmodulecache")]
    public string? VacMacModuleCache { get; set; }

    [JsonPropertyName("vacmodulecache")]
    public string? VacModuleCache { get; set; }

    [JsonPropertyName("vacmodulefilename")]
    public string? VacModuleFileName { get; set; }

    [JsonPropertyName("validoslist")]
    public string? ValidOperatingSystemList { get; set; }
}