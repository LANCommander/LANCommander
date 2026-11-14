using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using LANCommander.SDK.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.Helpers
{
    /// <summary>
    /// Shared handling for game option schemas and the canonical string form option values are stored in
    /// (the launcher's per-game values and the manifest's <c>Options</c> dictionary). Scalars are stored as
    /// strings (<c>"true"</c>/<c>"false"</c> for bools, invariant integers), lists as JSON arrays.
    /// </summary>
    public static class GameOptionsHelper
    {
        /// <summary>
        /// Parses an option schema from YAML. Returns null for an empty schema; throws on malformed YAML.
        /// </summary>
        public static OptionSchema ParseSchema(string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return null;

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeConverter(new OptionChoiceYamlConverter())
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<OptionSchema>(yaml);
        }

        /// <summary>
        /// Converts a value into the canonical stored string for the given option. Accepts plain CLR values:
        /// scalars, <see cref="IDictionary"/> (composite list rows) and <see cref="IEnumerable"/> (lists).
        /// Returns null for a null value, meaning "no stored value". Throws <see cref="ArgumentException"/>
        /// when the value can't be represented as the option's type.
        /// </summary>
        public static string SerializeValue(OptionDefinition definition, object value)
        {
            if (value == null)
                return null;

            if (definition.IsList)
                return SerializeList(definition, value);

            return SerializeScalar(value, definition.Type);
        }

        private static string SerializeList(OptionDefinition definition, object value)
        {
            IEnumerable items;

            switch (value)
            {
                case string s when TryParseJsonArray(s):
                    // Already in stored form
                    return s;
                case string _:
                case IDictionary _:
                    items = new[] { value };
                    break;
                case IEnumerable enumerable:
                    items = enumerable;
                    break;
                default:
                    items = new[] { value };
                    break;
            }

            if (definition.IsCompositeList)
            {
                var rows = new List<Dictionary<string, string>>();

                foreach (var item in items)
                {
                    if (item is not IDictionary row)
                        throw new ArgumentException($"Each item of a composite list must be an object with the fields {string.Join(", ", definition.Fields.Keys)}.");

                    var entry = new Dictionary<string, string>();

                    foreach (var field in definition.Fields)
                    {
                        var key = row.Keys.Cast<object>().FirstOrDefault(k => string.Equals(k?.ToString(), field.Key, StringComparison.OrdinalIgnoreCase));

                        entry[field.Key] = key != null && row[key] != null
                            ? SerializeScalar(row[key], field.Value?.Type)
                            : field.Value?.GetDefaultAsString() ?? string.Empty;
                    }

                    rows.Add(entry);
                }

                return JsonSerializer.Serialize(rows);
            }

            var itemType = string.IsNullOrWhiteSpace(definition.ItemType) ? "string" : definition.ItemType;
            var scalars = new List<string>();

            foreach (var item in items)
                scalars.Add(item == null ? string.Empty : SerializeScalar(item, itemType));

            return JsonSerializer.Serialize(scalars);
        }

        private static string SerializeScalar(object value, string type)
        {
            switch ((type ?? "string").ToLowerInvariant())
            {
                case "bool":
                    switch (value)
                    {
                        case bool b:
                            return b ? "true" : "false";
                        case string s when string.IsNullOrEmpty(s):
                            return string.Empty;
                        case string s when bool.TryParse(s, out var parsed):
                            return parsed ? "true" : "false";
                        default:
                            throw new ArgumentException($"'{value}' is not a valid bool value.");
                    }

                case "int":
                    switch (value)
                    {
                        case string s when string.IsNullOrEmpty(s):
                            return string.Empty;
                        case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                            return parsed.ToString(CultureInfo.InvariantCulture);
                        case bool _:
                        case string _:
                            throw new ArgumentException($"'{value}' is not a valid int value.");
                        case IConvertible convertible:
                            try
                            {
                                return Convert.ToInt64(convertible, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                            }
                            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
                            {
                                throw new ArgumentException($"'{value}' is not a valid int value.", ex);
                            }
                        default:
                            throw new ArgumentException($"'{value}' is not a valid int value.");
                    }

                default:
                    switch (value)
                    {
                        case bool b:
                            return b ? "true" : "false";
                        case IFormattable formattable:
                            return formattable.ToString(null, CultureInfo.InvariantCulture);
                        default:
                            return value.ToString();
                    }
            }
        }

        private static bool TryParseJsonArray(string value)
        {
            var trimmed = value.TrimStart();

            if (!trimmed.StartsWith("["))
                return false;

            try
            {
                using var document = JsonDocument.Parse(value);

                return document.RootElement.ValueKind == JsonValueKind.Array;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
