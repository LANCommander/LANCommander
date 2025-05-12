using MadMilkman.Ini;

namespace LANCommander.SDK.Tests.IniHandling
{
    using KeyValueInt = KeyValuePair<string, int>;
    using SectionKey = KeyValuePair<string, IList<string>>;

    public static class ConfigurationTests
    {
        public static ConfigurationTest Test_SingleSection = new ConfigurationTest
        {
            Ini = @"; last modified 1 April 2001 by John Doe
[owner]
name = John Doe
organization = Acme Widgets Inc.",

            RequiredSections = { "owner" },
            RequiredKeys =
            {
                new SectionKey("owner", ["name", "organization"]),
            },
            RequiredKeyValues =
            {
                new ConfigurationTest.SectionKeyValue("owner")
                {
                    KeyValues = [
                        new ConfigurationTest.SectionKeyValue.KeyValue("name", "John Doe"),
                        new ConfigurationTest.SectionKeyValue.KeyValue("organization", "Acme Widgets Inc."),
                    ],
                },
            }
        };

        public static ConfigurationTest Test_TwoSections = new ConfigurationTest
        {
            Ini = @"[person1]
name=Mikey
age=4

[person2]
name=Becky
age=46",

            RequiredSections = { "person1", "person2" },
            RequiredKeys =
            {
                new SectionKey("person1", ["name", "age"]),
                new SectionKey("person2", ["name", "age"]),
            },
            RequiredKeyValues =
            {
                new ConfigurationTest.SectionKeyValue("person1")
                {
                    KeyValues = [
                        new ConfigurationTest.SectionKeyValue.KeyValue("name", "Mikey"),
                        new ConfigurationTest.SectionKeyValue.KeyValue("age", "4"),
                    ],
                },
                new ConfigurationTest.SectionKeyValue("person2")
                {
                    KeyValues = [
                        new ConfigurationTest.SectionKeyValue.KeyValue("name", "Becky"),
                        new ConfigurationTest.SectionKeyValue.KeyValue("age", "46"),
                    ],
                },
            }
        };

        public static ConfigurationTest Test_ArrayStatic = new ConfigurationTest
        {
            Ini = @"[Engine.Input]
Aliases[0]=(Command=""Button bFire | Fire"",Alias=Fire)
Aliases[1]=(Command=""Button bAltFire | AltFire"",Alias=AltFire)
Aliases[2]=(Command=""Axis aBaseY  Speed=+300.0"",Alias=MoveForward)
0=SwitchWeapon 0
1=SwitchWeapon 1
2=SwitchWeapon 2
3=SwitchWeapon 3
"
,

            RequiredSections = { "Engine.Input" },
            RequiredKeys =
            {
                new SectionKey("Engine.Input", ["Aliases[0]", "Aliases[1]", "0", "1", "2", "3"]),
            },
            RequiredKeysCount =
            {
                new KeyValuePair<string, IList<KeyValueInt>>("Engine.Input", [
                    new KeyValueInt("", 7)
                ]),
            },
            RequiredKeyValues =
            {
                new ConfigurationTest.SectionKeyValue("Engine.Input")
                {
                    KeyValues = [
                        new ConfigurationTest.SectionKeyValue.KeyValue("Aliases[0]", "(Command=\"Button bFire | Fire\",Alias=Fire)"),
                        new ConfigurationTest.SectionKeyValue.KeyValue("0", "SwitchWeapon 0"),
                    ],
                },
            },

            UpdateKeyValues =
            {
                new ConfigurationTest.UpdateSectionKeyValue("Engine.Input")
                {
                    UpdateKeyValues = [
                        new ConfigurationTest.UpdateSectionKeyValue.UpdateKeyValue { Key = "Aliases[0]", NewValue = "(Command=\"Button bAltFire | AltFire\",Alias=AltFire)" },
                        new ConfigurationTest.UpdateSectionKeyValue.UpdateKeyValue { Key = "Aliases[1]", NewValue = "(Command=\"Button bFire | Fire\",Alias=Fire)" },
                    ],
                    CheckKeyValues = [
                        new ConfigurationTest.SectionKeyValue("Engine.Input")
                        {
                            KeyValues = [
                                new ConfigurationTest.SectionKeyValue.KeyValue("Aliases[0]", "(Command=\"Button bAltFire | AltFire\",Alias=AltFire)"),
                            ],
                        },
                    ],
                },
            }
        };

        public static ConfigurationTest Test_ArrayMultiValue = new ConfigurationTest
        {
            Ini = @"[Startup]
Package=Core
Package=Engine

[User]
Name=Player
Team=1
"
,

            RequiredSections = { "Startup" },
            RequiredKeys =
            {
                new SectionKey("Startup", ["Package"]),
            },
            RequiredKeysCount =
            {
                new KeyValuePair<string, IList<KeyValueInt>>("Startup", [
                    new KeyValueInt("Package", 2)
                ]),
            },
            RequiredKeyValues =
            {
                new ConfigurationTest.SectionKeyValue("Startup")
                {
                    KeyValues = [
                        new ConfigurationTest.SectionKeyValue.KeyValue("Package", "Core"),
                        new ConfigurationTest.SectionKeyValue.KeyValue("Package", "Engine"),
                    ],
                },
            },

            UpdateKeyValues =
            {
                new ConfigurationTest.UpdateSectionKeyValue("User")
                {
                    UpdateKeyValues = [new ConfigurationTest.UpdateSectionKeyValue.UpdateKeyValue { Key = "Name", NewValue = "Admin" }],
                    CheckKeyValues = [
                        new ConfigurationTest.SectionKeyValue("Startup")
                        {
                            KeyValues = [
                                new ConfigurationTest.SectionKeyValue.KeyValue("Package", ["Core", "Engine"]),
                            ],
                        },
                        new ConfigurationTest.SectionKeyValue("User")
                        {
                            KeyValues = [
                                new ConfigurationTest.SectionKeyValue.KeyValue("Name", "Admin"),
                                new ConfigurationTest.SectionKeyValue.KeyValue("Team", "1"),
                            ],
                        },
                    ],
                },

                new ConfigurationTest.UpdateSectionKeyValue("Startup")
                {
                    Options = new IniOptions
                    {
                        KeyDuplicate = IniDuplication.Ignored, // merges, takes first value of a multi-value key
                    },
                    UpdateKeyValues = [
                        new ConfigurationTest.UpdateSectionKeyValue.UpdateKeyValue {
                            Key = "Package",
                            NewValue = "UI",
                            ExpectedValues = ["UI"],
                        },
                    ],
                    CheckKeyValues = [
                        new ConfigurationTest.SectionKeyValue("Startup")
                        {
                            KeyValues = [
                                new ConfigurationTest.SectionKeyValue.KeyValue("Package", "UI"),
                            ],
                        },
                        new ConfigurationTest.SectionKeyValue("User")
                        {
                            KeyValues = [
                                new ConfigurationTest.SectionKeyValue.KeyValue("Name", "Player"),
                                new ConfigurationTest.SectionKeyValue.KeyValue("Team", "1"),
                            ],
                        },
                    ],
                },

                new ConfigurationTest.UpdateSectionKeyValue("Startup")
                {
                    Options = new IniOptions
                    {
                        KeyDuplicate = IniDuplication.Allowed, // keep multi-value keys
                    },
                    UpdateKeyValues = [new ConfigurationTest.UpdateSectionKeyValue.UpdateKeyValue {
                        Key = "Package",
                        NewValue = "UI",
                        ExpectedValues = ["Core", "UI"],
                    }],
                    CheckKeyValues = [
                        new ConfigurationTest.SectionKeyValue("Startup")
                        {
                            KeyValues = [
                                new ConfigurationTest.SectionKeyValue.KeyValue("Package", ["Core", "UI"]),
                            ],
                        },
                        new ConfigurationTest.SectionKeyValue("User")
                        {
                            KeyValues = [
                                new ConfigurationTest.SectionKeyValue.KeyValue("Name", "Player"),
                                new ConfigurationTest.SectionKeyValue.KeyValue("Team", "1"),
                            ],
                        },
                    ],
                },
            }
        };
    }
}
