using System;
using System.Collections.Generic;
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

                if (kvp.Value.Options != null && kvp.Value.Options.Count > 0)
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
        public string Default { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public List<OptionChoice> Choices { get; set; }
        public Dictionary<string, OptionDefinition> Options { get; set; }
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
