using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public class AppCommon
{
    [JsonPropertyName("associations")]
    public Dictionary<string, AppAssociation>? Associations { get; set; }
    
    [JsonPropertyName("category")]
    public Dictionary<string, string>? Category { get; set; }
    
    [JsonPropertyName("clienticns")]
    public string? ClientIcons { get; set; }
    
    [JsonPropertyName("clienticon")]
    public string? ClientIcon { get; set; }
    
    [JsonPropertyName("clienttga")]
    public string? ClientTGA { get; set; }
    
    [JsonPropertyName("community_hub_visible")]
    public string? CommunityHubVisible { get; set; }
    
    [JsonPropertyName("community_visible_stats")]
    public string? CommunityVisibleStats { get; set; }
    
    [JsonPropertyName("content_descriptors")]
    public Dictionary<string, string>? ContentDescriptors { get; set; }
    
    [JsonPropertyName("content_descriptors_including_dlc")]
    public Dictionary<string, string>? ContentDescriptorsIncludingDLC { get; set; }
    
    [JsonPropertyName("controllertagwizard")]
    public string? ControllerTagWizard { get; set; }
    
    [JsonPropertyName("exfgls")]
    public string? Exfgls { get; set; }
    
    [JsonPropertyName("gameid")]
    public string? GameId { get; set; }
    
    [JsonPropertyName("genres")]
    public Dictionary<string, string>? Genres { get; set; }
    
    [JsonPropertyName("header_image")]
    public LocalizedImageMap? HeaderImageMap { get; set; }
    
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
    
    [JsonPropertyName("languages")]
    public Dictionary<string, string>? Languages { get; set; }
    
    [JsonPropertyName("library_assets")]
    public LibraryAssets? LibraryAssets { get; set; }
    
    [JsonPropertyName("library_assets_null")]
    public LibraryAssetsFull? LibraryAssetsFull { get; set; }
    
    [JsonPropertyName("linuxclienticon")]
    public string? LinuxClientIcon { get; set; }
    
    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
    
    [JsonPropertyName("logo_small")]
    public string? LogoSmall { get; set; }
    
    [JsonPropertyName("market_presence")]
    public string? MarketPresence { get; set; }
    
    [JsonPropertyName("metacritic_name")]
    public string? MetacriticName { get; set; }
    
    [JsonPropertyName("metacritic_score")]
    public int? MetacriticScore { get; set; }
    
    [JsonPropertyName("metacritic_url")]
    public string? MetacriticUrl { get; set; }
    
    [JsonPropertyName("metacritic_fullurl")]
    public string? MetacriticFullUrl { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("name_localized")]
    public Dictionary<string, string>? NameLocalized { get; set; }
    
    [JsonPropertyName("osarch")]
    public string? OperatingSystemArchitecture { get; set; }
    
    [JsonPropertyName("osextended")]
    public string? OperatingSystemExtended { get; set; }
    
    [JsonPropertyName("oslist")]
    public string? OperatingSystemList { get; set; }
    
    [JsonPropertyName("original_release_date")]
    public long? OriginalReleaseDate { get; set; }
    
    [JsonPropertyName("primary_genre")]
    public int? PrimaryGenre { get; set; }
    
    [JsonPropertyName("releasestatesteamchina")]
    public string? ReleaseStateSteamChina { get; set; }
    
    [JsonPropertyName("review_percentage")]
    public int? ReviewPercentage { get; set; }
    
    [JsonPropertyName("review_score")]
    public int? ReviewScore { get; set; }
    
    [JsonPropertyName("small_capsule")]
    public LocalizedImageMap? SmallCapsule { get; set; }
    
    [JsonPropertyName("steam_deck_compatibility")]
    public string? SteamDeckCompatibility { get; set; }
    
    [JsonPropertyName("steam_release_date")]
    public string? SteamReleaseDate { get; set; }
    
    [JsonPropertyName("steamchinaapproved")]
    public string? SteamChinaApproved { get; set; }
    
    [JsonPropertyName("store_asset_mtime")]
    public string? StoreAssetMTime { get; set; }
    
    [JsonPropertyName("store_tags")]
    public Dictionary<string, string>? StoreTags { get; set; }
    
    [JsonPropertyName("supported_languages")]
    public Dictionary<string, string>? SupportedLanguages { get; set; }
    
    [JsonPropertyName("timeline_marker_svg")]
    public string? TimelineMarkerSvg { get; set; }
    
    [JsonPropertyName("timeline_marker_updated")]
    public string? TimelineMarkerUpdated { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("workshop_visible")]
    public string? WorkshopVisible { get; set; }
}