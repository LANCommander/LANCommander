using System.Reflection;
using System.Text;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests.IniHandling
{
    public class IniHandlingTests_MadMilkman
    {
        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void Parse(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = IniHelper.FromString(configuration.Ini);
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
            var ini = IniHelper.FromString(configuration.Ini);

            foreach (var requiredKey in configuration.RequiredSections)
            {
                Assert.True(ini.Sections.Contains(requiredKey));
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
            var ini = IniHelper.FromString(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredSectionsCount)
            {
                Assert.Equal(requiredSection.Value, ini.Sections.Count(x => string.Equals(x.Name, requiredSection.Key, StringComparison.InvariantCultureIgnoreCase)));
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
            var ini = IniHelper.FromString(configuration.Ini);

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
            var ini = IniHelper.FromString(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredKeysCount)
            {
                var section = ini.Sections[requiredSection.Key];
                Assert.NotNull(section);

                foreach (var requiredKeys in requiredSection.Value)
                {
                    var count = (requiredKeys.Key == "")
                        ? section.Keys.Count
                        : section.Keys.Count(x => string.Equals(x.Name, requiredKeys.Key, StringComparison.InvariantCultureIgnoreCase));

                    Assert.Equal(requiredKeys.Value, count);
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
            var ini = IniHelper.FromString(configuration.Ini);

            foreach (var requiredSection in configuration.RequiredKeyValues)
            {
                var section = ini.Sections[requiredSection.Section];
                Assert.NotNull(section);

                foreach (var requiredKeyValue in requiredSection.KeyValues)
                {
                    var match = section.Keys.FirstOrDefault(key => {
                        return string.Equals(key.Name, requiredKeyValue.Key, StringComparison.InvariantCultureIgnoreCase)
                            && string.Equals(key.Value, requiredKeyValue.Value, StringComparison.InvariantCulture);
                    });

                    Assert.NotNull(match);
                }
            }
        }


        [Theory]
        [InlineData(nameof(ConfigurationTests.Test_SingleSection))]
        [InlineData(nameof(ConfigurationTests.Test_TwoSections))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayStatic))]
        [InlineData(nameof(ConfigurationTests.Test_ArrayMultiValue))]
        public void UpdateSectionKeyValues(string configurationName)
        {
            ConfigurationTest configuration = GetConfiguration(configurationName);
            var ini = IniHelper.FromString(configuration.Ini);
            Assert.NotNull(ini);

            foreach (var requiredSection in configuration.UpdateKeyValues)
            {
                var options = requiredSection.Options ?? IniHelper.DefaultIniOptions;
                ini = IniHelper.FromString(configuration.Ini, options);
                var section = ini.Sections[requiredSection.Section];
                Assert.NotNull(section);

                foreach (var updateKeyValue in requiredSection.UpdateKeyValues)
                {
                    bool found = false;

                    foreach (var iniKey in section.Keys.Reverse())
                    {
                        if (string.Equals(iniKey.Name, updateKeyValue.Key, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (updateKeyValue.OldValue == null || string.Equals(updateKeyValue.Key, iniKey.Value, StringComparison.InvariantCultureIgnoreCase))
                            {
                                iniKey.Value = updateKeyValue.NewValue;
                                Assert.Equal(iniKey.Value, updateKeyValue.NewValue);
                                found = true;
                                break;
                            }
                        }
                    }

                    Assert.True(found);
                }

                // check if values are stored properly

                foreach (var requiredKeyValue in requiredSection.UpdateKeyValues)
                {
                    IList<KeyValuePair<string, string?>> searches = [];
                    searches.Add(new KeyValuePair<string, string?>(requiredKeyValue.Key, requiredKeyValue.NewValue));

                    if (requiredKeyValue.ExpectedValues != null)
                    {
                        foreach (var expected in requiredKeyValue.ExpectedValues)
                        {
                            searches.Add(new KeyValuePair<string, string?>(requiredKeyValue.Key, expected));
                        }
                    }

                    foreach (var search in searches)
                    {
                        var match = section.Keys.FirstOrDefault(key =>
                        {
                            return string.Equals(key.Name, requiredKeyValue.Key, StringComparison.InvariantCultureIgnoreCase)
                                && string.Equals(key.Value, requiredKeyValue.NewValue, StringComparison.InvariantCulture);
                        });

                        Assert.NotNull(match);
                    }
                }

                // compare newly generated INI

                string iniContentNew = IniHelper.ToString(ini, Encoding.Default);
                var iniNew = IniHelper.FromString(iniContentNew, options);
                Assert.NotNull(iniNew);

                foreach (var checkSection in requiredSection.CheckKeyValues)
                {
                    var sectionNew = iniNew.Sections[checkSection.Section];
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
                            var match = sectionNew.Keys.FirstOrDefault(key =>
                            {
                                return string.Equals(key.Name, search.Key, StringComparison.InvariantCultureIgnoreCase)
                                    && string.Equals(key.Value, search.Value, StringComparison.InvariantCulture);
                            });

                            Assert.NotNull(match);
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
