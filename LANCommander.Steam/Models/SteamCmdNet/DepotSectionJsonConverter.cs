using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LANCommander.Steam.Models.SteamCmdNet;

public sealed class DepotSectionJsonConverter : JsonConverter<DepotSection>
{
    private static readonly HashSet<string> MetadataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "baselanguages",
        "branches",
        "privatebranches",
        "workshopdepot",
        "depotdeltapatches",
        "hasdepotsindlc",
        "overridescddb"
    };

    public override DepotSection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var section = new DepotSection();
        var depots = new Dictionary<string, Depot>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var key = reader.GetString()!;

            switch (key.ToLowerInvariant())
            {
                case "baselanguages":
                    reader.Read();
                    section.BaseLanguages = reader.GetString();
                    break;

                case "branches":
                    reader.Read();
                    section.Branches = JsonSerializer.Deserialize<Dictionary<string, Branch>>(ref reader, options);
                    break;

                case "privatebranches":
                    reader.Read();
                    section.PrivateBranches = reader.GetString();
                    break;

                case "workshopdepot":
                    reader.Read();
                    section.WorkshopDepot = reader.GetString();
                    break;

                case "depotdeltapatches":
                    reader.Read();
                    section.DepotDeltaPatches = reader.GetString();
                    break;

                case "hasdepotsindlc":
                    reader.Read();
                    section.HasDepotsInDlc = reader.GetString();
                    break;

                case "overridescddb":
                    reader.Read();
                    section.OverridesCddb = reader.GetString();
                    break;

                default:
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.StartObject)
                    {
                        var depot = JsonSerializer.Deserialize<Depot>(ref reader, options);
                        if (depot != null)
                            depots[key] = depot;
                    }
                    else
                    {
                        reader.Skip();
                    }
                    break;
            }
        }

        section.Depots = depots.Count > 0 ? depots : null;
        return section;
    }

    public override void Write(Utf8JsonWriter writer, DepotSection value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.Depots != null)
        {
            foreach (var (key, depot) in value.Depots)
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, depot, options);
            }
        }

        if (value.BaseLanguages != null)
            writer.WriteString("baselanguages", value.BaseLanguages);

        if (value.Branches != null)
        {
            writer.WritePropertyName("branches");
            JsonSerializer.Serialize(writer, value.Branches, options);
        }

        if (value.PrivateBranches != null)
            writer.WriteString("privatebranches", value.PrivateBranches);

        if (value.WorkshopDepot != null)
            writer.WriteString("workshopdepot", value.WorkshopDepot);

        writer.WriteEndObject();
    }
}
