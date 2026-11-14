using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    /// <summary>
    /// Writes game option values back into the game manifest, the counterpart of <c>Get-GameOptions</c>.
    /// Meant for After Stop scripts: when a player changes a setting in the game itself, the script reads the
    /// game's config and records the value here, and the launcher copies it into the player's stored options
    /// once the game has exited, so the next launch doesn't overwrite it.
    /// </summary>
    [Cmdlet(VerbsData.Update, "GameOptions", DefaultParameterSetName = SingleParameterSet)]
    [OutputType(typeof(void))]
    public class UpdateGameOptionsCmdlet : Cmdlet
    {
        private const string SingleParameterSet = "Single";
        private const string BulkParameterSet = "Bulk";

        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The install directory path where the game manifest is located.")]
        public string Path { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The unique identifier (GUID) of the game.")]
        public Guid Id { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = SingleParameterSet, HelpMessage = "The dot-notation key of the option to update, e.g. 'Video.Width'.")]
        public string Name { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = SingleParameterSet, HelpMessage = "The new value. Pass $null to clear the stored value so the schema default applies.")]
        [AllowNull]
        [AllowEmptyString]
        public object Value { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = BulkParameterSet, ValueFromPipeline = true, HelpMessage = "A hashtable or object of option values, nested by group (as returned by Get-GameOptions) or keyed by dot-notation names.")]
        public object Options { get; set; }

        protected override void ProcessRecord()
        {
            var manifest = ManifestHelper.Read<SDK.Models.Manifest.Game>(Path, Id);

            if (manifest == null)
            {
                WriteError(new ErrorRecord(
                    new Exception("Could not read game manifest."),
                    "ManifestNotFound",
                    ErrorCategory.ObjectNotFound,
                    Path));
                return;
            }

            OptionSchema schema;

            try
            {
                schema = GameOptionsHelper.ParseSchema(manifest.OptionSchema);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "SchemaParseError", ErrorCategory.ParserError, manifest.OptionSchema));
                return;
            }

            var flatOptions = schema?.GetFlattenedOptions() ?? new Dictionary<string, OptionDefinition>();

            if (flatOptions.Count == 0)
            {
                WriteError(new ErrorRecord(
                    new InvalidOperationException("The game does not define any options."),
                    "NoOptionSchema",
                    ErrorCategory.InvalidOperation,
                    Id));
                return;
            }

            var updates = new Dictionary<string, object>();

            // Name is mandatory in the single-value set, so its absence means -Options was used
            if (Name == null)
                Flatten(Normalize(Options), string.Empty, flatOptions, updates);
            else
                updates[Name] = Normalize(Value);

            var options = manifest.Options ?? new Dictionary<string, string>();
            var changed = false;

            foreach (var update in updates)
            {
                var key = flatOptions.Keys.FirstOrDefault(k => string.Equals(k, update.Key, StringComparison.OrdinalIgnoreCase));

                if (key == null)
                {
                    WriteError(new ErrorRecord(
                        new ArgumentException($"'{update.Key}' is not an option defined by the game's option schema."),
                        "UnknownOption",
                        ErrorCategory.InvalidArgument,
                        update.Key));
                    continue;
                }

                var definition = flatOptions[key];
                string serialized;

                try
                {
                    serialized = GameOptionsHelper.SerializeValue(definition, update.Value);
                }
                catch (ArgumentException ex)
                {
                    WriteError(new ErrorRecord(ex, "InvalidOptionValue", ErrorCategory.InvalidArgument, update.Key));
                    continue;
                }

                if (serialized != null && definition.Choices != null && definition.Choices.Count > 0
                    && !string.IsNullOrEmpty(serialized)
                    && !definition.Choices.Any(c => c.Value == serialized))
                {
                    WriteWarning($"'{serialized}' is not one of the choices for option '{key}'.");
                }

                if (serialized == null)
                    changed |= options.Remove(key);
                else if (!options.TryGetValue(key, out var existing) || existing != serialized)
                {
                    options[key] = serialized;
                    changed = true;
                }
            }

            if (!changed)
                return;

            manifest.Options = options;

            ManifestHelper.Write(manifest, Path);
        }

        /// <summary>
        /// Walks a nested set of values, matching dot-notation keys against the schema. A key that names an
        /// option is taken whole (so a list's rows aren't mistaken for groups); anything else that's an object
        /// is treated as a group and descended into.
        /// </summary>
        private static void Flatten(object value, string prefix, Dictionary<string, OptionDefinition> flatOptions, Dictionary<string, object> result)
        {
            if (value is not IDictionary dictionary)
            {
                if (!string.IsNullOrEmpty(prefix))
                    result[prefix] = value;

                return;
            }

            foreach (DictionaryEntry entry in dictionary)
            {
                var name = entry.Key?.ToString();

                if (string.IsNullOrEmpty(name))
                    continue;

                var key = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";

                if (flatOptions.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) || entry.Value is not IDictionary)
                    result[key] = entry.Value;
                else
                    Flatten(entry.Value, key, flatOptions, result);
            }
        }

        /// <summary>
        /// Converts PowerShell values into plain CLR shapes: custom objects and hashtables become ordered
        /// string-keyed dictionaries, collections become lists, and wrapped scalars are unwrapped.
        /// </summary>
        private static object Normalize(object value)
        {
            if (value is PSObject psObject)
            {
                if (psObject.BaseObject is PSCustomObject)
                {
                    var properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    foreach (var property in psObject.Properties)
                        properties[property.Name] = Normalize(property.Value);

                    return properties;
                }

                value = psObject.BaseObject;
            }

            switch (value)
            {
                case null:
                    return null;
                case string _:
                    return value;
                case IDictionary dictionary:
                    var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    foreach (DictionaryEntry entry in dictionary)
                        map[entry.Key?.ToString() ?? string.Empty] = Normalize(entry.Value);

                    return map;
                case IEnumerable enumerable:
                    var list = new List<object>();

                    foreach (var item in enumerable)
                        list.Add(Normalize(item));

                    return list;
                default:
                    return value;
            }
        }
    }
}
