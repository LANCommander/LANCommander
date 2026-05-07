using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

[JsonConverter(typeof(DepotSectionJsonConverter))]
public sealed class DepotSection
{
    public string? BaseLanguages { get; set; }

    public Dictionary<string, Branch>? Branches { get; set; }

    public string? PrivateBranches { get; set; }

    public string? WorkshopDepot { get; set; }

    public string? DepotDeltaPatches { get; set; }

    public string? HasDepotsInDlc { get; set; }

    public string? OverridesCddb { get; set; }

    public Dictionary<string, Depot>? Depots { get; set; }
}
