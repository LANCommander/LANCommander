using System.Reflection;
using LANCommander.SDK.Parsers.Ini;
using PeanutButter.INI;

namespace LANCommander.SDK.Tests.IniHandling
{
    public class IniParserTests
    {
        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void Parse(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = IniParser.Parse(configuration.Ini);
            Assert.NotNull(ini);
        }

        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void CheckSections(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = IniParser.Parse(configuration.Ini);

            foreach (var requiredKey in configuration.RequiredSections)
            {
                Assert.Contains(ini.Sections, s => s.Name == requiredKey);
            }
        }

        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void CheckSectionsCount(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = IniParser.Parse(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredSectionsCount)
            {
                Assert.Equal(requiredSection.Value, ini.Sections.Count(s => string.Equals(s.Name, requiredSection.Key, StringComparison.InvariantCultureIgnoreCase)));
            }
        }

        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void CheckSectionKeys(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = IniParser.Parse(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredKeys)
            {
                var section = ini.Sections[requiredSection.Key];
                Assert.NotNull(section);

                foreach (var requiredKey in requiredSection.Value)
                {
                    Assert.True(section.Keys.Contains(requiredKey));
                }
            }
        }


        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void CheckSectionKeysCount(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = IniParser.Parse(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredKeysCount)
            {
                var section = ini.Sections[requiredSection.Key];
                Assert.NotNull(section);

                foreach (var requiredKeys in requiredSection.Value)
                {
                    Assert.Equal(requiredKeys.Value, section.Keys.Count(x => string.Equals(x.Name, requiredKeys.Key, StringComparison.InvariantCultureIgnoreCase)));
                }
            }
        }

        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void CheckSectionKeyValues(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = IniParser.Parse(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredKeyValues)
            {
                var section = ini.Sections[requiredSection.Section];
                Assert.NotNull(section);

                foreach (var requiredKeyValue in requiredSection.KeyValues)
                {
                    var match = section.Keys.FirstOrDefault(key => 
                    {
                        return string.Equals(key.Name, requiredKeyValue.Key, StringComparison.InvariantCultureIgnoreCase)
                            && string.Equals(key.Value, requiredKeyValue.Value, StringComparison.InvariantCulture);
                    });

                    Assert.NotEqual(default, match);
                }
            }
        }

        public static ConfigurationTest GetConfiguration(string configurationName)
        {
            Type configTestsType = typeof(ConfigurationTests);
            FieldInfo? field = configTestsType?.GetField(configurationName, BindingFlags.Public | BindingFlags.Static);

            if (field == null)
            {
                throw new Exception($"The configuration '{configurationName}' does not exist.");
            }

            ConfigurationTest? configurationTest = field.GetValue(null) as ConfigurationTest;
            if (configurationTest == null)
            {
                throw new Exception($"The configuration '{configurationName}' is not of type ConfigurationTest or is null.");
            }

            return configurationTest;
        }
    }
}
