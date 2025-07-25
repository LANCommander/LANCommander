using MadMilkman.Ini;

namespace LANCommander.SDK.Tests.IniHandling
{
    using SectionKey = KeyValuePair<string, IList<string>>;

    public class ConfigurationTest
    {
        public required string Ini { get; set; } 

        public IList<string> RequiredSections { get; set; } = [];
        public IList<KeyValuePair<string, int>> RequiredSectionsCount { get; set; } = [];


        public IList<SectionKey> RequiredKeys { get; set; } = [];
        public IList<KeyValuePair<string, IList<KeyValuePair<string, int>>>> RequiredKeysCount { get; set; } = [];

        public IList<SectionKeyValue> RequiredKeyValues { get; set; } = [];

        public IList<UpdateSectionKeyValue> UpdateKeyValues { get; set; } = [];


        public class SectionKeyValue(string section)
        {
            public class KeyValue
            {
                public string Key { get; set; }
                public string? Value { get; set; }
                public IList<string>? Values { get; set; }

                public KeyValue(string key, string? value)
                {
                    Key = key;
                    Value = value;
                }

                public KeyValue(string key, IList<string>? values)
                {
                    Key = key;
                    Values = values;
                }
            }

            public string Section { get; set; } = section;
            public IniOptions? Options { get; set; }

            public SectionKeyValue(string section, IniOptions options) : this(section) => Options = options;

            public IList<KeyValue> KeyValues { get; set; } = [];
        }

        public class UpdateSectionKeyValue(string section)
        {
            public class UpdateKeyValue
            {
                public required string Key { get; set; }
                public string? OldValue { get; set; }
                public string? NewValue { get; set; }

                public IList<string>? ExpectedValues { get; set; }
            }

            public string Section { get; set; } = section;
            public IniOptions? Options { get; set; }

            public UpdateSectionKeyValue(string section, IniOptions options) : this(section) => Options = options;

            public IList<UpdateKeyValue> UpdateKeyValues { get; set; } = [];
            public IList<SectionKeyValue> CheckKeyValues { get; set; } = [];
        }

    }
}
