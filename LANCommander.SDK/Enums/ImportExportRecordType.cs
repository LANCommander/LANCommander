using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Enums;

public enum ImportExportRecordType
{
    Game,
    Redistributable,
    Server,
    
    [Display(Name = "Actions")]
    Action,
    [Display(Name = "Archives")]
    Archive,
    [Display(Name = "Collections")]
    Collection,
    [Display(Name = "Custom Fields")]
    CustomField,
    [Display(Name = "Developers")]
    Developer,
    [Display(Name = "Publishers")]
    Publisher,
    [Display(Name = "Engines")]
    Engine,
    [Display(Name = "Genres")]
    Genre,
    [Display(Name = "Keys")]
    Key,
    [Display(Name = "Media")]
    Media,
    [Display(Name = "Multiplayer Modes")]
    MultiplayerMode,
    [Display(Name = "Platforms")]
    Platform,
    [Display(Name = "Play Sessions")]
    PlaySession,
    [Display(Name = "Saves")]
    Save,
    [Display(Name = "Save Paths")]
    SavePath,
    [Display(Name = "Scripts")]
    Script,
    [Display(Name = "Consoles")]
    ServerConsole,
    [Display(Name = "HTTP Paths")]
    ServerHttpPath,
    [Display(Name = "Tag")]
    Tag,
}