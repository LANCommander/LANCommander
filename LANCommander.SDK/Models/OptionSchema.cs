using System.Collections.Generic;

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

    public class OptionDefinition
    {
        public string Type { get; set; }
        public bool IsEnvironmentVariable { get; set; }
        public string Default { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public List<string> Choices { get; set; }
        public Dictionary<string, OptionDefinition> Options { get; set; }
    }
}
