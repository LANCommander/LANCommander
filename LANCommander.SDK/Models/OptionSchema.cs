using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Models
{
    public class OptionSchema
    {
        public string CommandTemplate { get; set; }
        public Dictionary<string, OptionDefinition> Options { get; set; } = new Dictionary<string, OptionDefinition>();

        /// <summary>
        /// Flattens nested options into dot-notation keys (e.g., "Proton.Path").
        /// Leaf options (those with a Type) are included; group nodes (those with only child Options) are not.
        /// List options are treated as leaves — their <see cref="OptionDefinition.Fields"/> are not flattened
        /// because they describe per-item shape, not sibling options.
        /// </summary>
        public Dictionary<string, OptionDefinition> GetFlattenedOptions()
        {
            var result = new Dictionary<string, OptionDefinition>();

            if (Options != null)
                FlattenOptions(Options, "", result);

            return result;
        }

        private static void FlattenOptions(Dictionary<string, OptionDefinition> options, string prefix, Dictionary<string, OptionDefinition> result)
        {
            foreach (var kvp in options)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

                if (!string.IsNullOrWhiteSpace(kvp.Value.Type))
                    result[key] = kvp.Value;

                if (!kvp.Value.IsList && kvp.Value.Options != null && kvp.Value.Options.Count > 0)
                    FlattenOptions(kvp.Value.Options, key, result);
            }
        }
    }

    public class OptionChoice
    {
        public string Value { get; set; }
        public string DisplayName { get; set; }

        [YamlIgnore]
        public string Label => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Value;

        public OptionChoice() { }

        public OptionChoice(string value)
        {
            Value = value;
        }

        public OptionChoice(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }
    }

    public class OptionDefinition
    {
        public string Type { get; set; }
        public string DisplayName { get; set; }
        public bool IsEnvironmentVariable { get; set; }

        /// <summary>
        /// The default value for this option. For scalar options this is a string;
        /// for <c>list</c> options this is a YAML sequence (deserialized as <c>IList&lt;object&gt;</c>)
        /// of either scalars (scalar list) or mappings (composite list).
        /// </summary>
        public object Default { get; set; }

        public string Description { get; set; }
        public bool Required { get; set; }
        public List<OptionChoice> Choices { get; set; }
        public Dictionary<string, OptionDefinition> Options { get; set; }

        /// <summary>
        /// For scalar <c>list</c> options: the type of each item (<c>string</c>, <c>int</c>, <c>bool</c>).
        /// Ignored for composite lists (which use <see cref="Fields"/>) and non-list options.
        /// </summary>
        public string ItemType { get; set; }

        /// <summary>
        /// For composite <c>list</c> options: the schema of each row. Presence of <c>Fields</c>
        /// distinguishes composite lists from scalar lists.
        /// </summary>
        public Dictionary<string, OptionDefinition> Fields { get; set; }

        /// <summary>
        /// Minimum number of items required for a list option. Null means no minimum.
        /// </summary>
        public int? MinItems { get; set; }

        /// <summary>
        /// Maximum number of items allowed for a list option. Null means no maximum.
        /// </summary>
        public int? MaxItems { get; set; }

        [YamlIgnore]
        public bool IsList => string.Equals(Type, "list", StringComparison.OrdinalIgnoreCase);

        [YamlIgnore]
        public bool IsCompositeList => IsList && Fields != null && Fields.Count > 0;

        /// <summary>
        /// Returns the default value in the canonical string form used by per-game / per-action option storage.
        /// For non-list options this is the raw string. For list options this is a JSON-encoded array
        /// of either scalars (scalar list) or objects (composite list).
        /// </summary>
        public string GetDefaultAsString()
        {
            if (Default == null)
                return null;

            if (IsList)
            {
                var normalized = NormalizeYamlValueForJson(Default);

                if (normalized == null)
                    return null;

                return JsonSerializer.Serialize(normalized);
            }

            return Default.ToString();
        }

        /// <summary>
        /// YamlDotNet decodes untyped objects as nested <c>Dictionary&lt;object,object&gt;</c> /
        /// <c>List&lt;object&gt;</c>. Convert those to <c>Dictionary&lt;string,object&gt;</c> /
        /// <c>List&lt;object&gt;</c> so System.Text.Json emits clean JSON.
        /// </summary>
        private static object NormalizeYamlValueForJson(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string s:
                    return s;
                case IDictionary dict:
                    var map = new Dictionary<string, object>();
                    foreach (DictionaryEntry entry in dict)
                        map[entry.Key?.ToString() ?? string.Empty] = NormalizeYamlValueForJson(entry.Value);
                    return map;
                case IEnumerable enumerable:
                    var list = new List<object>();
                    foreach (var item in enumerable)
                        list.Add(NormalizeYamlValueForJson(item));
                    return list;
                default:
                    return value;
            }
        }
    }

    /// <summary>
    /// Handles deserializing OptionChoice from both plain strings and mapping objects.
    /// Plain string "foo" becomes OptionChoice { Value = "foo" }.
    /// Mapping { Value: "foo", DisplayName: "Foo" } becomes OptionChoice { Value = "foo", DisplayName = "Foo" }.
    /// </summary>
    public class OptionChoiceYamlConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(OptionChoice);

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            if (parser.TryConsume<Scalar>(out var scalar))
            {
                return new OptionChoice(scalar.Value);
            }

            if (parser.TryConsume<MappingStart>(out _))
            {
                var choice = new OptionChoice();

                while (!parser.TryConsume<MappingEnd>(out _))
                {
                    var key = parser.Consume<Scalar>();
                    var value = parser.Consume<Scalar>();

                    switch (key.Value)
                    {
                        case "Value":
                            choice.Value = value.Value;
                            break;
                        case "DisplayName":
                            choice.DisplayName = value.Value;
                            break;
                    }
                }

                return choice;
            }

            throw new YamlException("Expected a scalar or mapping for OptionChoice");
        }

        public void WriteYaml(IEmitter emitter, object value, Type type, ObjectSerializer serializer)
        {
            var choice = (OptionChoice)value;

            if (string.IsNullOrWhiteSpace(choice.DisplayName))
            {
                emitter.Emit(new Scalar(choice.Value));
            }
            else
            {
                emitter.Emit(new MappingStart());
                emitter.Emit(new Scalar("Value"));
                emitter.Emit(new Scalar(choice.Value));
                emitter.Emit(new Scalar("DisplayName"));
                emitter.Emit(new Scalar(choice.DisplayName));
                emitter.Emit(new MappingEnd());
            }
        }
    }
}
