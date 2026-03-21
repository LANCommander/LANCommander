using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class AppConfig
{
    [JsonPropertyName("app_mappings")]
    public Dictionary<string, AppMapping>? AppMappings { get; set; }

    [JsonPropertyName("cegpublickey")]
    public string? CegPublicKey { get; set; }

    [JsonPropertyName("checkforupdatesbeforelaunch")]
    public string? CheckForUpdatesBeforeLaunch { get; set; }

    [JsonPropertyName("checkguid")]
    public string? CheckGuid { get; set; }

    [JsonPropertyName("contenttype")]
    public string? ContentType { get; set; }

    [JsonPropertyName("duration_control_show_interstitial")]
    public string? DurationControlShowInterstitial { get; set; }

    [JsonPropertyName("enable_duration_control")]
    public string? EnableDurationControl { get; set; }

    [JsonPropertyName("enabletextfiltering")]
    public string? EnableTextFiltering { get; set; }

    [JsonPropertyName("externalarguments")]
    public ExternalArguments? ExternalArguments { get; set; }

    [JsonPropertyName("gameoverlay_testmode")]
    public string? GameOverlayTestMode { get; set; }

    [JsonPropertyName("installdir")]
    public string? InstallDirectory { get; set; }

    [JsonPropertyName("installscriptoverride")]
    public string? InstallScriptOverride { get; set; }

    [JsonPropertyName("installscriptsignature")]
    public string? InstallScriptSignature { get; set; }

    [JsonPropertyName("launch")]
    public Dictionary<string, LaunchEntry>? Launch { get; set; }

    [JsonPropertyName("launchwithoutworkshopupdates")]
    public string? LaunchWithoutWorkshopUpdates { get; set; }

    [JsonPropertyName("matchmaking_mms_appidinvitenf")]
    public string? MatchmakingMmsAppIdInviteNf { get; set; }

    [JsonPropertyName("matchmaking_rate_limit")]
    public string? MatchmakingRateLimit { get; set; }

    [JsonPropertyName("matchmaking_uptodate")]
    public string? MatchmakingUpToDate { get; set; }

    [JsonPropertyName("sdr-groups")]
    public string? SdrGroups { get; set; }

    [JsonPropertyName("sdr-groups-global")]
    public string? SdrGroupsGlobal { get; set; }

    [JsonPropertyName("signaturescheckedonlaunch")]
    public Dictionary<string, Dictionary<string, string>>? SignaturesCheckedOnLaunch { get; set; }

    [JsonPropertyName("signedfiles")]
    public Dictionary<string, string>? SignedFiles { get; set; }

    [JsonPropertyName("steam_china_only")]
    public Dictionary<string, string>? SteamChinaOnly { get; set; }

    [JsonPropertyName("steamconfigurator3rdpartynative")]
    public string? SteamConfigurator3rdPartyNative { get; set; }

    [JsonPropertyName("steamcontrollertemplateindex")]
    public string? SteamControllerTemplateIndex { get; set; }

    [JsonPropertyName("steamdecktouchscreen")]
    public string? SteamDeckTouchscreen { get; set; }

    [JsonPropertyName("systemprofile")]
    public string? SystemProfile { get; set; }

    [JsonPropertyName("uselaunchcommandline")]
    public string? UseLaunchCommandLine { get; set; }

    [JsonPropertyName("usemms")]
    public string? UseMms { get; set; }

    [JsonPropertyName("usesfrenemies")]
    public string? UseSfrenemies { get; set; }

    [JsonPropertyName("vacmodulefilename")]
    public string? VacModuleFileName { get; set; }

    [JsonPropertyName("vacmodulefilename_macos")]
    public string? VacModuleFileNameMacos { get; set; }

    [JsonPropertyName("verifyupdates")]
    public string? VerifyUpdates { get; set; }

    [JsonPropertyName("depots")]
    public Dictionary<string, Depot>? Depots { get; set; }

    [JsonPropertyName("appmanagesdlc")]
    public string? AppManagesDlc { get; set; }

    [JsonPropertyName("baselanguages")]
    public string? BaseLanguages { get; set; }

    [JsonPropertyName("branches")]
    public Dictionary<string, Branch>? Branches { get; set; }

    [JsonPropertyName("depotdeltapatches")]
    public string? DepotDeltaPatches { get; set; }

    [JsonPropertyName("hasdepotsindlc")]
    public string? HasDepotsInDlc { get; set; }

    [JsonPropertyName("overridescddb")]
    public string? OverridesCddb { get; set; }

    [JsonPropertyName("privatebranches")]
    public string? PrivateBranches { get; set; }

    [JsonPropertyName("workshopdepot")]
    public string? WorkshopDepot { get; set; }
}