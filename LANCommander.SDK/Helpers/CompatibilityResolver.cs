using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.Helpers
{
    /// <summary>
    /// Describes a compatibility runtime ("shim") attached to a game — a redistributable whose option
    /// schema declares a <see cref="OptionSchema.CommandTemplate"/> that rewrites the launch (e.g. umu/Proton).
    /// </summary>
    public class ShimInfo
    {
        public Guid RedistributableId { get; set; }
        public string Name { get; set; }
        public RuntimePlatform GuestPlatforms { get; set; }
        public bool HasCommandTemplate { get; set; }

        /// <summary>
        /// Friendly label used when disambiguating actions in the UI. Falls back to the redistributable name.
        /// </summary>
        public string Label { get; set; }
    }

    /// <summary>
    /// Determines whether a game action can run on the current runtime, either natively or by being bridged
    /// through an attached compatibility runtime. Centralizes the option-schema parsing so action selection
    /// and launch stay consistent.
    /// </summary>
    public static class CompatibilityResolver
    {
        /// <summary>
        /// Parses the shim redistributables (those with a non-empty option schema) attached to a game manifest.
        /// </summary>
        public static IReadOnlyList<ShimInfo> GetShims(Models.Manifest.Game manifest)
        {
            if (manifest?.Redistributables == null)
                return Array.Empty<ShimInfo>();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeConverter(new OptionChoiceYamlConverter())
                .IgnoreUnmatchedProperties()
                .Build();

            var shims = new List<ShimInfo>();

            foreach (var redistributable in manifest.Redistributables)
            {
                if (string.IsNullOrWhiteSpace(redistributable.OptionSchema))
                    continue;

                OptionSchema schema;

                try
                {
                    schema = deserializer.Deserialize<OptionSchema>(redistributable.OptionSchema);
                }
                catch
                {
                    // A schema that can't be parsed can't bridge anything; skip it.
                    continue;
                }

                if (schema == null)
                    continue;

                shims.Add(new ShimInfo
                {
                    RedistributableId = redistributable.Id,
                    Name = redistributable.Name,
                    GuestPlatforms = schema.GuestPlatforms,
                    HasCommandTemplate = !string.IsNullOrWhiteSpace(schema.CommandTemplate),
                    Label = !string.IsNullOrWhiteSpace(schema.DisplayName) ? schema.DisplayName : redistributable.Name,
                });
            }

            return shims;
        }

        /// <summary>
        /// True when the action's target platform includes the current runtime (or is unspecified).
        /// </summary>
        public static bool IsNativelyRunnable(RuntimePlatform actionPlatforms)
        {
            return EnvironmentHelper.SupportsCurrentRuntime(actionPlatforms);
        }

        /// <summary>
        /// Returns the shim that can bridge this action onto the current runtime, or null when the action is
        /// natively runnable or no attached shim can bridge it.
        /// </summary>
        public static ShimInfo GetBridge(RuntimePlatform actionPlatforms, IReadOnlyList<ShimInfo> shims)
        {
            if (shims == null || shims.Count == 0)
                return null;

            // Natively runnable actions never need a bridge.
            if (IsNativelyRunnable(actionPlatforms))
                return null;

            return shims.FirstOrDefault(shim =>
                shim.HasCommandTemplate &&
                (shim.GuestPlatforms == RuntimePlatform.None || (shim.GuestPlatforms & actionPlatforms) != 0));
        }

        /// <summary>
        /// True when the action can run on the current runtime, either natively or via an attached shim.
        /// </summary>
        public static bool CanRunOnCurrentRuntime(RuntimePlatform actionPlatforms, IReadOnlyList<ShimInfo> shims)
        {
            return IsNativelyRunnable(actionPlatforms) || GetBridge(actionPlatforms, shims) != null;
        }
    }
}
