using MadMilkman.Ini;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    /// <summary>
    /// Cmdlet for updating an INI file value. This cmdlet updates, adds, or removes a key-value pair within a specified section 
    /// of an INI file, based on provided parameters.
    /// </summary>
    [Cmdlet(VerbsData.Update, "IniValue")]
    [OutputType(typeof(string))]
    public class UpdateIniValueCmdlet : Cmdlet
    {
        /// <summary>
        /// Gets or sets the section in the INI file that contains the key to be updated.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "Specifies the section in the INI file that contains the key to be updated.")]
        [Alias("s")]
        public string Section { get; set; }

        /// <summary>
        /// Gets or sets the key within the section whose value should be updated.
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, HelpMessage = "Specifies the key within the section whose value should be updated.")]
        [Alias("k")]
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the new value to assign to the provided key.
        /// </summary>
        [Parameter(Mandatory = true, Position = 2, HelpMessage = "Specifies the new value to assign to the provided key.")]
        [Alias("v")]
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the full file path to the INI file that should be processed.
        /// </summary>
        [Parameter(Mandatory = true, Position = 3, HelpMessage = "Specifies the file path to the INI file that should be processed.")]
        [Alias("f")]
        public string FilePath { get; set; }

        /// <summary>
        /// If set, controls whether the value should be wrapped in quotes.
        /// If <c>null</c>, the cmdlet will add quotes if the value is not already quoted.
        /// If <c>true</c>, quotes will be enforced.
        /// If <c>false</c>, any surrounding quotes will be removed.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "If set, controls whether the value will be wrapped in quotes. Nullable: if null, adds quotes if missing; if true, enforces quotes; if false, removes quotes.")]
        [Alias("wrap", "quotes")]
        public bool? WrapValueInQuotes { get; set; } = null;

        /// <summary>
        /// Gets or sets a switch parameter that updates an existing key or adds a new one if it does not exist.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "If set, the cmdlet will update an existing key or add a new one if it does not exist.")]
        [Alias("add")]
        public SwitchParameter UpdateOrAdd { get; set; } = true;

        /// <summary>
        /// Gets or sets a switch parameter that restricts the operation to updating existing keys only; no new key will be added.
        /// </summary>
        [Alias("update-only", "only-update")]
        [Parameter(Mandatory = false, HelpMessage = "If set, restricts the operation to updating existing keys only; no new key will be added.")]
        public SwitchParameter NoAdd
        {
            get => new(!UpdateOrAdd);
            set => UpdateOrAdd = !value.ToBool();
        }

        /// <summary>
        /// Gets or sets a switch parameter that specifies the key(s) will only be removed from the section without updating or adding any value.
        /// </summary>
        [Alias("remove-only")]
        [Parameter(Mandatory = false, HelpMessage = "If set, the key(s) will only be removed from the section without updating or adding any value.")]
        public SwitchParameter OnlyRemove { get; set; } = false;

        /// <summary>
        /// Gets or sets a switch parameter that clears all instances of the specified key in the section.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "If set, all instances of the specified key in the section will be cleared.")]
        public SwitchParameter Clear { get; set; } = false;

        /// <summary>
        /// Gets or sets a switch parameter that specifies a new key-value pair will always be appended, 
        /// even if the key already exists.
        /// </summary>
        [Alias("append")]
        [Parameter(Mandatory = false, HelpMessage = "If set, a new key-value pair will always be appended, even if the key already exists.")]
        public SwitchParameter AlwaysAppend { get; set; } = false;

        /// <summary>
        /// Gets or sets the index position at which the new key-value pair should be inserted.
        /// If not provided, the key is added at the end.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Specifies the index position at which the new key-value pair should be inserted. If not provided, the key is added at the end.")]
        [Alias("insert")]
        public int? InsertIndex { get; set; } = null;

        /// <summary>
        /// Gets or sets a value indicating whether duplicate keys are allowed within the section.
        /// </summary>
        [Alias("keepkey", "keydup")]
        [Parameter(Mandatory = false, HelpMessage = "If true, duplicate keys are allowed within the section.")]
        public bool KeepKeyDuplicates { get; set; } = true;

        /// <summary>
        /// Gets or sets a switch parameter that prevents duplicate keys in the section.
        /// </summary>
        [Alias("nokey", "nokeydup")]
        [Parameter(Mandatory = false, HelpMessage = "If set, duplicate keys in the section are prevented.")]
        public SwitchParameter NoKeyDuplicates
        {
            get => new(!KeepKeyDuplicates);
            set => KeepKeyDuplicates = !value.ToBool();
        }

        /// <summary>
        /// Gets or sets a value indicating whether duplicate sections in the INI file are allowed.
        /// </summary>
        [Alias("keepsec", "secdup")]
        [Parameter(Mandatory = false, HelpMessage = "If true, duplicate sections in the INI file are allowed.")]
        public bool KeepSectionDuplicates { get; set; } = true;

        /// <summary>
        /// Gets or sets a switch parameter that prevents duplicate section names in the INI file.
        /// </summary>
        [Alias("nosec", "nosecdup")]
        [Parameter(Mandatory = false, HelpMessage = "If set, duplicate section names are not permitted in the INI file.")]
        public SwitchParameter NoSectionDuplicates
        {
            get => new(!KeepSectionDuplicates);
            set => KeepSectionDuplicates = !value.ToBool();
        }

        /// <summary>
        /// Gets or sets the encoding (codepage) for reading and writing the INI file.
        /// Examples include 'UTF8', 'Latin', 'ASCII', or 'Unicode'.
        /// </summary>
        [Alias("encoding", "enc")]
        [Parameter(Mandatory = false, HelpMessage = "Specifies the encoding (codepage) for reading and writing the INI file (e.g., 'UTF8', 'Latin', 'ASCII', 'Unicode').")]
        public string Codepage { get; set; } = "Latin";

        /// <summary>
        /// Returns the appropriate <see cref="Encoding"/> instance based on the specified Codepage.
        /// </summary>
        /// <returns>The <see cref="Encoding"/> for the specified codepage.</returns>
        protected Encoding GetEncoding()
        {
            string page = Codepage?.ToLower();
            switch (page)
            {
                case "uft8":
                    return Encoding.UTF8;
                case "latin":
                case "latin1":
                case "iso-8859-1":
                    return Encoding.Latin1;
                case "asci":
                case "ascii":
                    return Encoding.ASCII;
                case "unicode":
                    return Encoding.Unicode;
            }

            return Encoding.Default;
        }

        /// <summary>
        /// Processes the record by loading the specified INI file, updating, adding, or removing the specified key/value pair,
        /// and then saving the file.
        /// </summary>
        protected override void ProcessRecord()
        {
            // Check if the specified INI file exists; if not, exit the method.
            if (!File.Exists(FilePath))
                return;

            // Configure INI file options based on provided parameters.
            var iniOptions = new IniOptions()
            {
                Encoding = GetEncoding(),
                SectionDuplicate = KeepSectionDuplicates ? IniDuplication.Allowed : IniDuplication.Ignored,
                KeyDuplicate = KeepKeyDuplicates ? IniDuplication.Allowed : IniDuplication.Ignored,
            };

            // Load the INI file with the specified options.
            var ini = new IniFile(iniOptions);
            ini.Load(FilePath);

            // Retrieve the specified section; if not found and appending/updating is not allowed, exit.
            var iniSection = ini.Sections[Section];
            if (iniSection == null && !AlwaysAppend && !UpdateOrAdd)
                return;

            // Create a new section if it does not exist.
            if (iniSection == null)
            {
                iniSection = new IniSection(ini, Section);
                ini.Sections.Add(iniSection);
            }

            // Function for matching a key using case-insensitive comparison.
            bool keyMatcher(IniKey x) => string.Equals(x.Name, Key, StringComparison.OrdinalIgnoreCase);

            // If the operation is to remove or clear keys, perform deletion of matching keys.
            if (OnlyRemove || Clear)
            {
                var list = iniSection.Keys.Where(keyMatcher).ToList();
                list.ForEach(x => iniSection.Keys.Remove(x));
            }

            // If removal is not the sole operation, proceed with updating or adding the value.
            if (!OnlyRemove)
            {
                // assuming most of the engines interpret INI files from top to bottom using the last value of a multiple existing key, we update the last found key
                var firstMatch = iniSection.Keys.LastOrDefault(keyMatcher);
                var iniValue = Value;

                // Adjust the value's surrounding quotes based on the WrapValueInQuotes parameter.
                iniValue = ApplyQuoteWrapping(iniValue, firstMatch?.Value);

                // Insert a new key-value pair if appending is enforced or the key does not exist.
                if (AlwaysAppend.ToBool() || (UpdateOrAdd && firstMatch == null))
                {
                    if (InsertIndex.HasValue && InsertIndex.Value >= 0)
                        iniSection.Keys.Insert(Math.Clamp(InsertIndex.Value, 0, iniSection.Keys.Count - 1), Key, iniValue);
                    else
                        iniSection.Keys.Add(Key, iniValue);
                }
                else if (firstMatch != null)
                {
                    // Handle updating an existing key, optionally repositioning it if an insertion index is specified.
                    var insertIndex = (InsertIndex.HasValue && InsertIndex.Value >= 0)
                        ? Math.Clamp(InsertIndex.Value, 0, iniSection.Keys.Count - 1)
                        : -1;

                    if (insertIndex >= 0)
                    {
                        iniSection.Keys.Remove(firstMatch);
                        iniSection.Keys.Insert(insertIndex, firstMatch);
                    }
                    // Update the existing key's value.
                    firstMatch.Value = iniValue;
                }
                // No else clause – updating without adding a new key should be possible.
            }

            // Save the modified INI file.
            ini.Save(FilePath);
        }

        /// <summary>
        /// Applies quote wrapping on the given string based on the <see cref="WrapValueInQuotes"/> parameter.
        /// <list type="bullet">
        /// <item>
        /// <description>If <c>null</c>: uses the quoting format of the existing INI value if available.</description>
        /// </item>
        /// <item>
        /// <description>If <c>true</c>: enforces quotes around the value.</description>
        /// </item>
        /// <item>
        /// <description>If <c>false</c>: ensures the value is stored without quotes.</description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="newValue">The new value to be processed.</param>
        /// <param name="existingValue">The existing INI value (if any) for reference.</param>
        /// <returns>The processed string with quotes applied or removed according to the settings.</returns>
        private string ApplyQuoteWrapping(string newValue, string existingValue)
        {
            if (string.IsNullOrEmpty(newValue))
                return newValue;

            bool isNewValueQuoted = newValue.StartsWith("\"") && newValue.EndsWith("\"") ||
                                    newValue.StartsWith("'") && newValue.EndsWith("'");

            bool isExistingValueQuoted = !string.IsNullOrEmpty(existingValue) &&
                                         (existingValue.StartsWith("\"") && existingValue.EndsWith("\"") ||
                                          existingValue.StartsWith("'") && existingValue.EndsWith("'"));

            if (WrapValueInQuotes == null)
            {
                // If null, use the quoting style of the existing value if available.
                return isExistingValueQuoted ? $"\"{newValue}\"" : newValue;
            }
            else if (WrapValueInQuotes.Value)
            {
                // If true, enforce quotes.
                return isNewValueQuoted ? newValue : $"\"{newValue}\"";
            }
            else
            {
                // If false, remove any surrounding quotes.
                return isNewValueQuoted ? newValue.Substring(1, newValue.Length - 2) : newValue;
            }
        }
    }
}
