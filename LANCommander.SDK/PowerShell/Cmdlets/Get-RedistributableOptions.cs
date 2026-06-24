using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "RedistributableOptions")]
    [OutputType(typeof(PSObject))]
    public class GetRedistributableOptionsCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The install directory path where the game manifest is located.")]
        public string Path { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The unique identifier (GUID) of the game.")]
        public Guid Id { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The name of the redistributable to get options for.")]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            var manifest = ManifestHelper.Read<SDK.Models.Manifest.Game>(Path, Id);

            if (manifest == null)
            {
                WriteError(new ErrorRecord(
                    new Exception("Could not read game manifest."),
                    "ManifestNotFound",
                    ErrorCategory.ObjectNotFound,
                    Path));
                return;
            }

            SDK.Models.Manifest.Redistributable target = null;

            foreach (var redist in manifest.Redistributables)
            {
                if (string.Equals(redist.Name, Name, StringComparison.OrdinalIgnoreCase))
                {
                    target = redist;
                    break;
                }
            }

            if (target == null)
            {
                WriteError(new ErrorRecord(
                    new Exception($"Redistributable '{Name}' not found in manifest."),
                    "RedistributableNotFound",
                    ErrorCategory.ObjectNotFound,
                    Name));
                return;
            }

            if (string.IsNullOrWhiteSpace(target.OptionSchema))
            {
                WriteObject(new PSObject());
                return;
            }

            SDK.Models.OptionSchema schema;

            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .WithTypeConverter(new SDK.Models.OptionChoiceYamlConverter())
                    .IgnoreUnmatchedProperties()
                    .Build();

                schema = deserializer.Deserialize<SDK.Models.OptionSchema>(target.OptionSchema);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "SchemaParseError", ErrorCategory.ParserError, target.OptionSchema));
                return;
            }

            // Build resolved flat options: defaults then per-game values
            var flatOptions = schema.GetFlattenedOptions();
            var resolved = new Dictionary<string, string>();

            foreach (var kvp in flatOptions)
            {
                var defaultValue = kvp.Value.GetDefaultAsString();
                if (!string.IsNullOrWhiteSpace(defaultValue))
                    resolved[kvp.Key] = defaultValue;
            }

            if (target.Options != null)
            {
                foreach (var kvp in target.Options)
                {
                    resolved[kvp.Key] = kvp.Value;
                }
            }

            // Build a nested PSObject from dot-notation keys.
            // For list-typed options, hydrate the JSON-encoded value into a native array.
            var result = new PSObject();

            foreach (var kvp in resolved)
            {
                object hydrated = kvp.Value;

                if (flatOptions.TryGetValue(kvp.Key, out var definition) && definition.IsList)
                    hydrated = HydrateList(definition, kvp.Value);

                SetNestedProperty(result, kvp.Key, hydrated);
            }

            WriteObject(result);
        }

        private static object HydrateList(OptionDefinition definition, string jsonValue)
        {
            if (string.IsNullOrWhiteSpace(jsonValue))
                return definition.IsCompositeList ? (object)Array.Empty<PSObject>() : Array.Empty<string>();

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(jsonValue);
                root = doc.RootElement.Clone();
            }
            catch
            {
                // Malformed JSON — surface the raw string rather than throwing in a script.
                return jsonValue;
            }

            if (root.ValueKind != JsonValueKind.Array)
                return jsonValue;

            if (definition.IsCompositeList)
            {
                var rows = new List<PSObject>();
                foreach (var item in root.EnumerateArray())
                {
                    var row = new PSObject();
                    foreach (var field in definition.Fields)
                    {
                        string value = null;
                        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(field.Key, out var fieldEl))
                            value = ReadJsonScalar(fieldEl);
                        else if (!string.IsNullOrWhiteSpace(field.Value?.Default?.ToString()))
                            value = field.Value.Default.ToString();

                        row.Properties.Add(new PSNoteProperty(field.Key, CoerceScalar(value, field.Value?.Type)));
                    }
                    rows.Add(row);
                }
                return rows.ToArray();
            }

            // Scalar list
            var itemType = string.IsNullOrWhiteSpace(definition.ItemType) ? "string" : definition.ItemType;
            var scalars = new List<object>();
            foreach (var item in root.EnumerateArray())
                scalars.Add(CoerceScalar(ReadJsonScalar(item), itemType));

            return scalars.ToArray();
        }

        private static string ReadJsonScalar(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };

        private static object CoerceScalar(string value, string type)
        {
            if (value == null)
                return null;

            switch ((type ?? "string").ToLowerInvariant())
            {
                case "int":
                    return int.TryParse(value, out var i) ? i : (object)value;
                case "bool":
                    return bool.TryParse(value, out var b) ? b : (object)value;
                default:
                    return value;
            }
        }

        private static void SetNestedProperty(PSObject obj, string dotPath, object value)
        {
            var parts = dotPath.Split('.');

            if (parts.Length == 1)
            {
                obj.Properties.Add(new PSNoteProperty(parts[0], value));
                return;
            }

            // Navigate/create intermediate PSObjects
            var current = obj;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var existing = current.Properties[parts[i]];

                if (existing != null && existing.Value is PSObject nested)
                {
                    current = nested;
                }
                else
                {
                    var child = new PSObject();

                    if (existing != null)
                        current.Properties.Remove(parts[i]);

                    current.Properties.Add(new PSNoteProperty(parts[i], child));
                    current = child;
                }
            }

            current.Properties.Add(new PSNoteProperty(parts[parts.Length - 1], value));
        }
    }
}
