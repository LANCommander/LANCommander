using MadMilkman.Ini;
using PeanutButter.INI;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using YamlDotNet.Core.Tokens;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Update, "IniValue")]
    [OutputType(typeof(string))]
    public class UpdateIniValueCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        [Alias("s")]
        public string Section { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        [Alias("k")]
        public string Key { get; set; }

        [Parameter(Mandatory = true, Position = 2)]
        [Alias("v")]
        public string Value { get; set; }

        [Parameter(Mandatory = true, Position = 3)]
        [Alias("f")]
        public string FilePath { get; set; }

        [Parameter(Mandatory = false)]
        [Alias("wrap", "quotes")]
        public bool WrapValueInQuotes { get; set; } = false;

        [Parameter(Mandatory = false)]
        [Alias("add")]
        public SwitchParameter UpdateOrAdd { get; set; } = true;
        [Alias("update-only", "only-update")]
        [Parameter(Mandatory = false)]
        public SwitchParameter NoAdd
        {
            get => new(!UpdateOrAdd);
            set => UpdateOrAdd = !value.ToBool();
        }

        [Alias("remove-only")]
        [Parameter(Mandatory = false)]
        public SwitchParameter OnlyRemove { get; set; } = false;

        [Parameter(Mandatory = false)]
        public SwitchParameter Clear { get; set; } = false;

        [Alias("append")]
        [Parameter(Mandatory = false)]
        public SwitchParameter AlwaysAppend { get; set; } = false;

        [Parameter(Mandatory = false)]
        [Alias("insert")]
        public int? InsertIndex { get; set; } = null;

        [Alias("keepkey", "keydup")]
        [Parameter(Mandatory = false)]
        public bool KeepKeyDuplicates { get; set; } = true;
        [Alias("nokey", "nokeydup")]
        [Parameter(Mandatory = false)]
        public SwitchParameter NoKeyDuplicates
        {
            get => new(!KeepKeyDuplicates);
            set => KeepKeyDuplicates = !value.ToBool();
        }

        [Alias("keepsec", "secdup")]
        [Parameter(Mandatory = false)]
        public bool KeepSectionDuplicates { get; set; } = true;
        [Alias("nosec", "nosecdup")]
        [Parameter(Mandatory = false)]
        public SwitchParameter NoSectionDuplicates 
        {
            get => new(!KeepSectionDuplicates);
            set => KeepSectionDuplicates = !value.ToBool();
        }

        [Alias("encoding", "enc")]
        [Parameter(Mandatory = false)]
        public string Codepage { get; set; } = "Latin";

        protected Encoding GetEncoding()
        {
            string page = Codepage?.ToLower();
            switch (page)
            {
                case "uft8": return Encoding.UTF8;
                case "latin":
                case "latin1":
                case "ISO-8859-1":
                    return Encoding.Latin1;
                case "asci":
                case "ascii":
                    return Encoding.ASCII;
                case "unicode":
                    return Encoding.Unicode;
            }

            return Encoding.Default;
        }

        protected override void ProcessRecord()
        {
            if (!File.Exists(FilePath))
                return;

            var iniOptions = new IniOptions()
            {
                Encoding = GetEncoding(),
                SectionDuplicate = KeepSectionDuplicates ? IniDuplication.Allowed : IniDuplication.Ignored,
                KeyDuplicate = KeepKeyDuplicates ? IniDuplication.Allowed : IniDuplication.Ignored,
            };
            var ini = new IniFile(iniOptions);
            ini.Load(FilePath);

            var iniSection = ini.Sections[Section];
            if (iniSection == null && !AlwaysAppend && !UpdateOrAdd)
                return;

            // create new if not existing
            if (iniSection == null)
            {
                iniSection = new IniSection(ini, Section);
                ini.Sections.Add(iniSection);
            }

            bool keyMatcher(IniKey x) => string.Equals(x.Name, Key, StringComparison.OrdinalIgnoreCase);

            if (OnlyRemove || Clear)
            {
                var list = iniSection.Keys.Where(keyMatcher).ToList();
                list.ForEach(x => iniSection.Keys.Remove(x));
            }

            if (!OnlyRemove)
            {
                // assuming most of the engines interpret INI files from top to bottom using the last value of a multiple existing key, we update the last found key
                var firstMatch = iniSection.Keys.LastOrDefault(keyMatcher);
                if (AlwaysAppend.ToBool() || (UpdateOrAdd && firstMatch == null))
                {
                    if (InsertIndex.HasValue && InsertIndex.Value >= 0)
                        iniSection.Keys.Insert(Math.Clamp(InsertIndex.Value, 0, iniSection.Keys.Count - 1), Key, Value);
                    else
                        iniSection.Keys.Add(Key, Value);
                }
                else if (firstMatch != null)
                {
                    var insertIndex = (InsertIndex.HasValue && InsertIndex.Value >= 0) ? Math.Clamp(InsertIndex.Value, 0, iniSection.Keys.Count - 1)  : -1;
                    if (insertIndex >= 0)
                    {
                        iniSection.Keys.Remove(firstMatch);
                        iniSection.Keys.Insert(insertIndex, firstMatch);
                    }
                    firstMatch.Value = Value;
                }
                // no else, as it should be possible to only update an existing value without adding a new one
            }

            ini.Save(FilePath);
        }
    }
}
