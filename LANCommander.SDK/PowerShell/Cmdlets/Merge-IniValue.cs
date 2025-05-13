using MadMilkman.Ini;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    /// <summary>
    /// Cmdlet for merging the contents of a source INI file into a destination INI file.
    /// It supports merging the entire source file, a specified section, or a single key/value pair.
    /// Optionally, the data can be extracted from the source (i.e. removed) after merging.
    /// </summary>
    [Cmdlet(VerbsData.Merge, "Ini")]
    [OutputType(typeof(void))]
    public class MergeIniCmdlet : Cmdlet
    {
        #region Runtime fields

        protected IniOptions IniOptions = new IniOptions();

        #endregion

        #region Mandatory Parameters

        /// <summary>
        /// Specifies the file path to the source INI file.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "Specifies the file path to the source INI file.")]
        [Alias("src")]
        public string SourceFilePath { get; set; }

        /// <summary>
        /// Specifies the file path to the destination INI file.
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, HelpMessage = "Specifies the file path to the destination INI file.")]
        [Alias("dest")]
        public string DestinationFilePath { get; set; }

        #endregion

        #region Optional Extraction and Filter Parameters

        /// <summary>
        /// Specifies the source INI section to extract or merge.
        /// If omitted, the entire source file is merged.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Specifies the source INI section to extract or merge. If omitted, the entire source file is merged.")]
        [Alias("ss", "ssec", "sourcesec")]
        public string SourceSection { get; set; }

        /// <summary>
        /// Specifies the source key within the source section to extract or merge.
        /// Used only when SourceSection is provided.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Specifies the source key to extract or merge from the source section.")]
        [Alias("sk", "skey", "sourcek")]
        public string SourceKey { get; set; }

        /// <summary>
        /// Specifies the destination section to which content should be merged.
        /// If omitted, the source section name is used.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Specifies the destination INI section to merge into. If omitted, the source section name is used.")]
        [Alias("ds", "dsec", "destsection")]
        public string DestinationSection { get; set; }

        /// <summary>
        /// Specifies the destination key name when merging a single key.
        /// If omitted, the source key name is used.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Specifies the destination key name when merging a single key. If omitted, the source key name is used.")]
        [Alias("dk", "dkey", "destkey")]
        public string DestinationKey { get; set; }

        /// <summary>
        /// If set, extracts (removes) the specified section or key from the source INI file
        /// once they have been merged into the destination file.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "If set, extracts the specified section or key from the source INI file after merging.")]
        public SwitchParameter Extract { get; set; } = false;

        #endregion

        #region Merge Behavior Parameters

        /// <summary>
        /// Controls whether the value will be wrapped in quotes.
        /// Nullable: if null, preserves the quoting style of the existing destination value;
        /// if true, always enforces quotes; if false, ensures any surrounding quotes are removed.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Controls whether the value will be wrapped in quotes. Nullable: if null, preserves the destination’s quoting style; if true, enforces quotes; if false, removes quotes.")]
        [Alias("wrap", "quotes")]
        public bool? WrapValueInQuotes { get; set; } = null;

        /// <summary>
        /// Determines whether to update an existing key or add a new one if it does not exist.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "If set, updates an existing key or adds a new one if not found in the destination.")]
        [Alias("addkey")]
        public SwitchParameter UpdateOrAddKey { get; set; } = true;

        /// <summary>
        /// Determines whether to update an existing section or add a new one if it does not exist.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "If set, updates an existing section or adds a new one if not found in the destination.")]
        [Alias("addsection")]
        public SwitchParameter UpdateOrAddSection { get; set; } = true;

        /// <summary>
        /// Clears all instances of a specific key in the destination before performing the merge.
        /// When used with –PreserveKeys, the destination keys are cleared once per key name.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Clears all instances of the specified key in the destination before merging new values.")]
        public SwitchParameter ClearKeys { get; set; } = false;

        /// <summary>
        /// When set, new key-value pairs are always appended in the destination,
        /// even if the key already exists.
        /// </summary>
        [Alias("appendkey", "preserve")]
        [Parameter(Mandatory = false, HelpMessage = "If set, new key-value pairs are always appended in the destination.")]
        public SwitchParameter PreserveKeys { get; set; } = false;

        /// <summary>
        /// When set, new section is always appended in the destination,
        /// even if the section already exists.
        /// </summary>
        [Alias("appendsection", "preserveSec")]
        [Parameter(Mandatory = false, HelpMessage = "If set, new section is always appended in the destination.")]
        public SwitchParameter PreserveSections { get; set; } = false;

        /// <summary>
        /// Specifies the zero-based index position at which to insert new key-value pair(s) in the destination.
        /// If not provided, new entries are added at the end.
        /// </summary>
        [Parameter(Mandatory = false, HelpMessage = "Specifies the index position at which to insert new key-value pair(s) in the destination.")]
        [Alias("insert")]
        public int? InsertIndex { get; set; } = null;

        /// <summary>
        /// Indicates whether duplicate keys are allowed within the destination section.
        /// </summary>
        [Alias("keepkey", "keydup")]
        [Parameter(Mandatory = false, HelpMessage = "If true, duplicate keys are allowed within the destination section.")]
        public bool KeepKeyDuplicates { get; set; } = true;

        /// <summary>
        /// Prevents duplicate keys from being present in the destination section.
        /// </summary>
        [Alias("nokey", "nokeydup")]
        [Parameter(Mandatory = false, HelpMessage = "If set, duplicate keys in the destination section are prevented.")]
        public SwitchParameter NoKeyDuplicates
        {
            get => new(!KeepKeyDuplicates);
            set => KeepKeyDuplicates = !value.ToBool();
        }

        /// <summary>
        /// Indicates whether duplicate sections are allowed in the destination INI file.
        /// </summary>
        [Alias("keepsec", "secdup")]
        [Parameter(Mandatory = false, HelpMessage = "If true, duplicate sections in the destination INI file are allowed.")]
        public bool KeepSectionDuplicates { get; set; } = true;

        /// <summary>
        /// Prevents duplicate sections from occurring in the destination INI file.
        /// </summary>
        [Alias("nosec", "nosecdup")]
        [Parameter(Mandatory = false, HelpMessage = "If set, duplicate sections in the destination INI file are prevented.")]
        public SwitchParameter NoSectionDuplicates
        {
            get => new(!KeepSectionDuplicates);
            set => KeepSectionDuplicates = !value.ToBool();
        }

        /// <summary>
        /// Specifies the encoding for reading and writing INI files.
        /// Examples include 'UTF8', 'Latin', 'ASCII', or 'Unicode'.
        /// </summary>
        [Alias("encoding", "enc")]
        [Parameter(Mandatory = false, HelpMessage = "Specifies the encoding for reading and writing INI files (e.g., UTF8, Latin, ASCII, Unicode).")]
        public string Codepage { get; set; } = "Latin";

        #endregion

        #region Helper Methods

        /// <summary>
        /// Returns the appropriate <see cref="Encoding"/> based on the specified codepage.
        /// </summary>
        /// <returns>The <see cref="Encoding"/> for the chosen codepage.</returns>
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
        /// Applies quote wrapping for a new value based on the <see cref="WrapValueInQuotes"/> parameter.
        /// When <c>null</c>, the quoting style of the existing destination value is preserved.
        /// </summary>
        /// <param name="newValue">The new value to process.</param>
        /// <param name="existingValue">The existing value in the destination (if any) for reference.</param>
        /// <returns>The processed string with quotes applied or removed according to settings.</returns>
        private string ApplyQuoteWrapping(string newValue, string existingValue)
        {
            if (string.IsNullOrEmpty(newValue))
                return newValue;

            bool isNewValueQuoted = (newValue.StartsWith("\"") && newValue.EndsWith("\"")) ||
                                    (newValue.StartsWith("'") && newValue.EndsWith("'"));

            bool isExistingValueQuoted = !string.IsNullOrEmpty(existingValue) &&
                                         ((existingValue.StartsWith("\"") && existingValue.EndsWith("\"")) ||
                                          (existingValue.StartsWith("'") && existingValue.EndsWith("'")));

            if (WrapValueInQuotes == null)
            {
                // Preserve the quoting style of the existing destination value.
                return isExistingValueQuoted ? (isNewValueQuoted ? newValue : $"\"{newValue}\"") : newValue;
            }
            else if (WrapValueInQuotes.Value)
            {
                // Enforce quotes.
                return isNewValueQuoted ? newValue : $"\"{newValue}\"";
            }
            else
            {
                // Remove quotes.
                return isNewValueQuoted ? newValue.Substring(1, newValue.Length - 2) : newValue;
            }
        }

        #endregion

        #region ProcessRecord

        protected override void BeginProcessing()
        {
            base.BeginProcessing();

            // Prepare INI file options.
            IniOptions = new IniOptions()
            {
                Encoding = GetEncoding(),
                SectionDuplicate = KeepSectionDuplicates ? IniDuplication.Allowed : IniDuplication.Ignored,
                KeyDuplicate = KeepKeyDuplicates ? IniDuplication.Allowed : IniDuplication.Ignored,
            };
        }

        /// <summary>
        /// Processes the merge operation between the source and destination INI files based on the selected parameters.
        /// If extraction is enabled and a specific section or key is specified, that content is removed from the source after merging.
        /// </summary>
        protected override void ProcessRecord()
        {
            // Validate source file.
            if (!File.Exists(SourceFilePath))
            {
                WriteWarning("Source INI file not found.");
                return;
            }
            if (string.IsNullOrWhiteSpace(DestinationFilePath) && !Extract)
            {
                WriteWarning("Destination INI file not provided.");
                return;
            }

            // Load the source INI file.
            var srcIni = new IniFile(IniOptions);
            srcIni.Load(SourceFilePath);

            // Load or create the destination INI file.
            var destIni = new IniFile(IniOptions);
            bool isSameFile = false;
            if (File.Exists(DestinationFilePath))
            {
                if (Path.GetFullPath(SourceFilePath) == Path.GetFullPath(DestinationFilePath))
                {
                    destIni = srcIni;
                    isSameFile = true;
                }
                else
                {
                    destIni.Load(DestinationFilePath);
                }
            }

            // Case 1: Merge the entire source file (if no specific section is provided).
            if (string.IsNullOrEmpty(SourceSection))
            {
                foreach (var srcSection in srcIni.Sections)
                {
                    var dstSection = GetSection(destIni, srcSection.Name, isSameFile);
                    if (dstSection == null)
                        continue;

                    MergeSection(srcSection, dstSection);
                }

                if (Extract && !isSameFile)
                {
                    WriteWarning("No source section was extracted. Extract is invalid with no SourceSection provided.");
                }
            }
            else
            {
                // Case 2: A specific source section is provided.
                var srcSec = srcIni.Sections[SourceSection];
                if (srcSec == null)
                {
                    WriteWarning($"Source section '{SourceSection}' not found.");
                    return;
                }

                // Use the provided destination section name if given; otherwise, default to the source section name.
                string destSecName = string.IsNullOrEmpty(DestinationSection) ? srcSec.Name : DestinationSection;
                var destSec = GetSection(destIni, destSecName, isSameFile);

                // Case 2a: Merge the entire section if no specific source key is provided.
                if (string.IsNullOrEmpty(SourceKey))
                {
                    MergeSection(srcSec, destSec);
                    if (Extract && !isSameFile)
                    {
                        var sectionsToRemove = srcIni.Sections.Where(sec => IsMatchingKey(sec.Name, srcSec.Name)).ToList();
                        sectionsToRemove.ForEach(name => srcIni.Sections.Remove(name));
                    }
                }
                else
                {
                    // Case 2b: Merge multiple values for a specific key.
                    var srcKeys = srcSec.Keys.Where(k => IsMatchingKey(k.Name, SourceKey)).ToList();
                    if (srcKeys.Count == 0)
                    {
                        WriteWarning($"Source key '{SourceKey}' not found in section '{SourceSection}'.");
                        return;
                    }

                    string destKeyName = string.IsNullOrEmpty(DestinationKey) ? SourceKey : DestinationKey;
                    bool isSameKey = string.Equals(SourceKey, destKeyName, StringComparison.OrdinalIgnoreCase);

                    if (ClearKeys && (!isSameFile || !isSameKey))
                    {
                        var keysToRemove = destSec.Keys.Where(k => IsMatchingKey(k.Name, destKeyName)).ToList();
                        keysToRemove.ForEach(k => destSec.Keys.Remove(k));
                    }

                    MergeKeys(srcKeys, destSec, destKeyName);

                    // If extraction is enabled for the key, remove all occurrences from the source section.
                    if (Extract && (!isSameFile || !isSameKey))
                    {
                        var keysToRemove = srcSec.Keys.Where(k => IsMatchingKey(k.Name, SourceKey)).ToList();
                        keysToRemove.ForEach(k => srcSec.Keys.Remove(k));
                    }
                }
            }

            // Save the merged destination INI file.
            if (!string.IsNullOrEmpty(DestinationFilePath))
                destIni.Save(DestinationFilePath);

            // If extraction occurred, save the updated source INI file.
            if (Extract && destIni != srcIni)
            {
                srcIni.Save(SourceFilePath);
            }
        }

        /// <summary>
        /// Retrieves or creates an INI section based on the specified section name.
        /// If an existing section matches the name, the last occurrence is returned.
        /// If no matching section is found and conditions allow, a new section is added.
        /// </summary>
        /// <param name="ini">The INI file object from which the section is retrieved or created.</param>
        /// <param name="sectionName">The name of the section to search for or create.</param>
        /// <param name="NoAdd">If true, prevents adding a new section when no match is found.</param>
        /// <returns>
        /// Returns the last matching section if found; otherwise, a new section is created unless <paramref name="NoAdd"/> is set to true.
        /// </returns>
        private IniSection GetSection(IniFile ini, string sectionName, bool NoAdd)
        {
            // assuming most of the engines interpret INI files from top to bottom using the last value of a multiple existing key, we update the last found key
            var lastMatch = ini.Sections.LastOrDefault(sec => IsMatchingKey(sec.Name, sectionName));

            if (PreserveSections.ToBool() || (UpdateOrAddSection && lastMatch == null))
            {
                if (!NoAdd)
                {
                    lastMatch = new IniSection(ini, sectionName);
                    ini.Sections.Add(lastMatch);
                }
            }

            return lastMatch;
        }

        /// <summary>
        /// Merges an entire INI section into the destination file, handling multi-value keys.
        /// </summary>
        private void MergeSection(IniSection srcSection, IniSection dstSection)
        {
            // Use a hash set to track cleared keys so we only clear once per key name.
            var clearedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var srcKey in srcSection.Keys)
            {
                if (ClearKeys && !clearedKeys.Contains(srcKey.Name))
                {
                    var keysToRemove = dstSection.Keys.Where(k => IsMatchingKey(k.Name, srcKey.Name)).ToList();
                    keysToRemove.ForEach(k => dstSection.Keys.Remove(k));
                    clearedKeys.Add(srcKey.Name);
                }

                // assuming most of the engines interpret INI files from top to bottom using the last value of a multiple existing key, we update the last found key
                var lastMatch = dstSection.Keys.LastOrDefault(key => IsMatchingKey(key.Name, srcKey.Name));

                // Adjust the value's surrounding quotes based on the WrapValueInQuotes parameter.
                string newValue = ApplyQuoteWrapping(srcKey.Value, lastMatch?.Value);

                // Append new key if PreserveKeys is set or no key exists (and UpdateOrAddKey allows adding).
                if (PreserveKeys.ToBool() || (UpdateOrAddKey && lastMatch == null))
                {
                    if (InsertIndex.HasValue && InsertIndex.Value >= 0)
                        dstSection.Keys.Insert(Math.Clamp(InsertIndex.Value, 0, dstSection.Keys.Count), srcKey.Name, newValue);
                    else
                        dstSection.Keys.Add(srcKey.Name, newValue);
                }
                else if (lastMatch != null)
                {
                    // Update the existing key's value.
                    lastMatch.Value = newValue;
                }
                // No else clause – updating without adding a new key should be possible.
            }
        }

        /// <summary>
        /// Merges an entire INI section into the destination file, handling multi-value keys.
        /// </summary>
        private void MergeKeys(List<IniKey> srcKeys, IniSection destSec, string destKeyName)
        {
            // Use a hash set to track cleared keys so we only clear once per key name.
            var clearedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var srcKey in srcKeys)
            {
                if (ClearKeys && !clearedKeys.Contains(destKeyName))
                {
                    var keysToRemove = destSec.Keys.Where(k => IsMatchingKey(k.Name, destKeyName)).ToList();
                    keysToRemove.ForEach(k => destSec.Keys.Remove(k));
                    clearedKeys.Add(destKeyName);
                }

                // assuming most of the engines interpret INI files from top to bottom using the last value of a multiple existing key, we update the last found key
                var lastMatch = destSec.Keys.LastOrDefault(key => IsMatchingKey(key.Name, destKeyName));

                // Adjust the value's surrounding quotes based on the WrapValueInQuotes parameter.
                string newValue = ApplyQuoteWrapping(srcKey.Value, lastMatch?.Value);

                // Append new key if PreserveKeys is set or no key exists (and UpdateOrAddKey allows adding).
                if (PreserveKeys.ToBool() || (UpdateOrAddKey && lastMatch == null))
                {
                    if (InsertIndex.HasValue && InsertIndex.Value >= 0)
                        destSec.Keys.Insert(Math.Clamp(InsertIndex.Value, 0, destSec.Keys.Count), destKeyName, newValue);
                    else
                        destSec.Keys.Add(destKeyName, newValue);
                }
                else if (lastMatch != null)
                {
                    // Update the existing key's value.
                    lastMatch.Value = newValue;
                }
                // No else clause – updating without adding a new key should be possible.
            }
        }

        /// <summary>
        /// Determines whether two key names are considered a match using a case-insensitive comparison.
        /// </summary>
        /// <param name="key">The first key to compare.</param>
        /// <param name="otherKey">The second key to compare.</param>
        /// <returns>
        /// Returns <c>true</c> if the key names are equal (ignoring case); otherwise, <c>false</c>.
        /// </returns>
        public bool IsMatchingKey(string key, string otherKey)
        {
            return string.Equals(key, otherKey, IniOptions.KeyNameCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
