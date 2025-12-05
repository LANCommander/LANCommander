using System.ComponentModel.DataAnnotations;

namespace LANCommander.Server.ImportExport.Legacy.Enums;

internal enum SavePathType
{
    [Display(Name = "File / Folder")]
    File,
    [Display(Name = "Registry Key")]
    Registry
}