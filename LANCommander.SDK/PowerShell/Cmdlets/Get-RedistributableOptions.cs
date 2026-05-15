using LANCommander.SDK.Helpers;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "RedistributableOptions")]
    [OutputType(typeof(PSObject))]
    public class GetRedistributableOptionsCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The install directory path where the game manifest is located.")]
        public string Path { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The unique identifier (GUID) of the game.")]
        public Guid Id { get; set; }

        [Parameter(Mandatory = true, HelpMessage = "The name of the redistributable to get options for.")]
        public string Name { get; set; }

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

            SDK.Models.Manifest.Redistributable target = null;

            foreach (var redist in manifest.Redistributables)
            {
                if (string.Equals(redist.Name, Name, StringComparison.OrdinalIgnoreCase))
                {
                    target = redist;
                    break;
                }
            }

            if (target == null)
            {
                WriteError(new ErrorRecord(
                    new Exception($"Redistributable '{Name}' not found in manifest."),
                    "RedistributableNotFound",
                    ErrorCategory.ObjectNotFound,
                    Name));
                return;
            }

            if (string.IsNullOrWhiteSpace(target.OptionSchema))
            {
                WriteObject(new PSObject());
                return;
            }

            SDK.Models.OptionSchema schema;

            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                schema = deserializer.Deserialize<SDK.Models.OptionSchema>(target.OptionSchema);
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "SchemaParseError", ErrorCategory.ParserError, target.OptionSchema));
                return;
            }

            // Build resolved flat options: defaults then per-game values
            var flatOptions = schema.GetFlattenedOptions();
            var resolved = new Dictionary<string, string>();

            foreach (var kvp in flatOptions)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Value.Default))
                    resolved[kvp.Key] = kvp.Value.Default;
            }

            if (target.Options != null)
            {
                foreach (var kvp in target.Options)
                {
                    resolved[kvp.Key] = kvp.Value;
                }
            }

            // Build a nested PSObject from dot-notation keys
            var result = new PSObject();

            foreach (var kvp in resolved)
            {
                SetNestedProperty(result, kvp.Key, kvp.Value);
            }

            WriteObject(result);
        }

        private static void SetNestedProperty(PSObject obj, string dotPath, string value)
        {
            var parts = dotPath.Split('.');

            if (parts.Length == 1)
            {
                obj.Properties.Add(new PSNoteProperty(parts[0], value));
                return;
            }

            // Navigate/create intermediate PSObjects
            var current = obj;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var existing = current.Properties[parts[i]];

                if (existing != null && existing.Value is PSObject nested)
                {
                    current = nested;
                }
                else
                {
                    var child = new PSObject();

                    if (existing != null)
                        current.Properties.Remove(parts[i]);

                    current.Properties.Add(new PSNoteProperty(parts[i], child));
                    current = child;
                }
            }

            current.Properties.Add(new PSNoteProperty(parts[parts.Length - 1], value));
        }
    }
}
