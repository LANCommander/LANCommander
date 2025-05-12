using System.Reflection;
using PeanutButter.INI;

namespace LANCommander.SDK.Tests.IniHandling
{
    public class IniHandlingTests_PeanutButter
    {
        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void Parse(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = INIFile.FromString(configuration.Ini);
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
            var ini = INIFile.FromString(configuration.Ini);

            foreach (var requiredKey in configuration.RequiredSections)
            {
                Assert.True(ini.HasSection(requiredKey));
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
            var ini = INIFile.FromString(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredSectionsCount)
            {
                Assert.Equal(requiredSection.Value, ini.AllSections.Count(x => string.Equals(x, requiredSection.Key, StringComparison.InvariantCultureIgnoreCase)));
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
            var ini = INIFile.FromString(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredKeys)
            {
                var section = ini.GetSection(requiredSection.Key);
                Assert.NotNull(section);

                foreach (var requiredKey in requiredSection.Value)
                {
                    Assert.True(section.ContainsKey(requiredKey));
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
            var ini = INIFile.FromString(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredKeysCount)
            {
                var section = ini.GetSection(requiredSection.Key);
                Assert.NotNull(section);

                foreach (var requiredKeys in requiredSection.Value)
                {
                    Assert.Equal(requiredKeys.Value, section.Keys.Count(x => string.Equals(x, requiredKeys.Key, StringComparison.InvariantCultureIgnoreCase)));
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
            var ini = INIFile.FromString(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredKeyValues)
            {
                var section = ini.GetSection(requiredSection.Section);
                Assert.NotNull(section);

                foreach (var requiredKeyValue in requiredSection.KeyValues)
                {
                    var match = section.FirstOrDefault(key => 
                    {
                        return string.Equals(key.Key, requiredKeyValue.Key, StringComparison.InvariantCultureIgnoreCase)
                            && string.Equals(key.Value, requiredKeyValue.Value, StringComparison.InvariantCulture);
                    });

                    Assert.NotEqual(match, default);
                }
            }
        }

        [Theory]
        //[InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        //[InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        //[InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void UpdateSectionKeyValues(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = INIFile.FromString(configuration.Ini);
            Assert.NotNull(ini);

            foreach (var requiredSection in configuration.UpdateKeyValues)
            {
                var section = ini.GetSection(requiredSection.Section);
                Assert.NotNull(section);

                foreach (var updateKeyValue in requiredSection.UpdateKeyValues)
                {
                    ini.SetValue(requiredSection.Section, updateKeyValue.Key, updateKeyValue.NewValue);
                    var value = ini.GetValue(requiredSection.Section, updateKeyValue.Key);
                    Assert.True(updateKeyValue.NewValue == null || string.Equals(updateKeyValue.NewValue, value, StringComparison.InvariantCulture));
                }

                // compare newly generated INI
                string iniContentNew = ini.ToString();
                var iniNew = INIFile.FromString(iniContentNew);
                Assert.NotNull(iniNew);

                foreach (var checkSection in requiredSection.CheckKeyValues)
                {
                    var sectionNew = iniNew.GetSection(checkSection.Section);
                    Assert.NotNull(sectionNew);

                    foreach (var checkKeyValue in checkSection.KeyValues)
                    {
                        IList<KeyValuePair<string, string?>> searches = [];
                        if (checkKeyValue.Values != null)
                        {
                            foreach (var expected in checkKeyValue.Values)
                            {
                                searches.Add(new KeyValuePair<string, string?>(checkKeyValue.Key, expected));
                            }
                        }
                        else
                        {
                            searches.Add(new KeyValuePair<string, string?>(checkKeyValue.Key, checkKeyValue.Value));
                        }

                        foreach (var search in searches)
                        {
                            var match = section.FirstOrDefault(key =>
                            {
                                return string.Equals(key.Key, search.Key, StringComparison.InvariantCultureIgnoreCase)
                                    && string.Equals(key.Value, search.Value, StringComparison.InvariantCulture);
                            });

                            Assert.NotEqual(match, default);
                        }
                    }
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
