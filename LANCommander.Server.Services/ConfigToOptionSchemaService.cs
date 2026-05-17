using System.Text.Json;
using System.Xml.Linq;
using LANCommander.SDK.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Services;

public class ConfigToOptionSchemaService
{
    public string Convert(string content, string format = "auto")
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        if (format == "auto")
            format = DetectFormat(content);

        var schema = format switch
        {
            "ini" => ParseIni(content),
            "json" => ParseJson(content),
            "xml" => ParseXml(content),
            _ => throw new ArgumentException($"Unknown format: {format}")
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections)
            .Build();

        return serializer.Serialize(schema);
    }

    private string DetectFormat(string content)
    {
        var trimmed = content.TrimStart();

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return "json";

        if (trimmed.StartsWith('<'))
            return "xml";

        return "ini";
    }

    private OptionSchema ParseIni(string content)
    {
        var schema = new OptionSchema
        {
            Options = new Dictionary<string, OptionDefinition>()
        };

        string? currentSection = null;
        Dictionary<string, OptionDefinition>? currentOptions = null;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            // Section header
            if (line.StartsWith('[') && line.Contains(']'))
            {
                // Save previous section
                if (currentSection != null && currentOptions != null && currentOptions.Count > 0)
                {
                    schema.Options[SanitizeKey(currentSection)] = new OptionDefinition
                    {
                        Description = currentSection,
                        Options = currentOptions
                    };
                }

                currentSection = line.TrimStart('[').Split(']')[0].Trim();
                currentOptions = new Dictionary<string, OptionDefinition>();
                continue;
            }

            // Key=Value pair
            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line.Substring(0, eqIndex).Trim();
            var value = line.Substring(eqIndex + 1).Trim();

            var optionDef = new OptionDefinition
            {
                Type = InferType(value),
                Default = value,
                Description = key
            };

            if (currentSection != null && currentOptions != null)
            {
                currentOptions[SanitizeKey(key)] = optionDef;
            }
            else
            {
                // Global key (no section)
                schema.Options[SanitizeKey(key)] = optionDef;
            }
        }

        // Save last section
        if (currentSection != null && currentOptions != null && currentOptions.Count > 0)
        {
            schema.Options[SanitizeKey(currentSection)] = new OptionDefinition
            {
                Description = currentSection,
                Options = currentOptions
            };
        }

        return schema;
    }

    private OptionSchema ParseJson(string content)
    {
        var schema = new OptionSchema
        {
            Options = new Dictionary<string, OptionDefinition>()
        };

        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            ParseJsonObject(doc.RootElement, schema.Options);
        }

        return schema;
    }

    private void ParseJsonObject(JsonElement element, Dictionary<string, OptionDefinition> options)
    {
        foreach (var prop in element.EnumerateObject())
        {
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    var groupDef = new OptionDefinition
                    {
                        Description = prop.Name,
                        Options = new Dictionary<string, OptionDefinition>()
                    };
                    ParseJsonObject(prop.Value, groupDef.Options);
                    options[SanitizeKey(prop.Name)] = groupDef;
                    break;

                case JsonValueKind.Array:
                    var choices = new List<string>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            choices.Add(item.GetString() ?? "");
                        else
                            choices.Add(item.GetRawText());
                    }

                    options[SanitizeKey(prop.Name)] = new OptionDefinition
                    {
                        Type = "choice",
                        Description = prop.Name,
                        Choices = choices
                    };
                    break;

                default:
                    var value = prop.Value.GetRawText().Trim('"');
                    options[SanitizeKey(prop.Name)] = new OptionDefinition
                    {
                        Type = InferTypeFromJson(prop.Value),
                        Default = value,
                        Description = prop.Name
                    };
                    break;
            }
        }
    }

    private OptionSchema ParseXml(string content)
    {
        var schema = new OptionSchema
        {
            Options = new Dictionary<string, OptionDefinition>()
        };

        var doc = XDocument.Parse(content);

        if (doc.Root != null)
        {
            ParseXmlElement(doc.Root, schema.Options);
        }

        return schema;
    }

    private void ParseXmlElement(XElement element, Dictionary<string, OptionDefinition> options)
    {
        // Add attributes as options
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.LocalName.StartsWith("xmlns"))
                continue;

            options[SanitizeKey(attr.Name.LocalName)] = new OptionDefinition
            {
                Type = InferType(attr.Value),
                Default = attr.Value,
                Description = attr.Name.LocalName
            };
        }

        // Group child elements
        foreach (var child in element.Elements())
        {
            if (child.HasElements || child.HasAttributes)
            {
                var groupDef = new OptionDefinition
                {
                    Description = child.Name.LocalName,
                    Options = new Dictionary<string, OptionDefinition>()
                };
                ParseXmlElement(child, groupDef.Options);
                options[SanitizeKey(child.Name.LocalName)] = groupDef;
            }
            else
            {
                var value = child.Value.Trim();
                options[SanitizeKey(child.Name.LocalName)] = new OptionDefinition
                {
                    Type = InferType(value),
                    Default = string.IsNullOrEmpty(value) ? null : value,
                    Description = child.Name.LocalName
                };
            }
        }
    }

    private string InferType(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "string";

        if (bool.TryParse(value, out _))
            return "bool";

        if (int.TryParse(value, out _))
            return "int";

        if (double.TryParse(value, out _))
            return "int";

        return "string";
    }

    private string InferTypeFromJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True or JsonValueKind.False => "bool",
            JsonValueKind.Number => "int",
            _ => "string"
        };
    }

    private string SanitizeKey(string key)
    {
        // Remove characters that would be problematic in YAML keys
        return key.Replace(" ", "").Replace(".", "_").Replace("-", "_");
    }

}
