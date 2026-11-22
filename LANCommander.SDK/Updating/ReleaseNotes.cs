#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Semver;

namespace LANCommander.SDK.Updating;

/// <summary>The notes for one release, in markdown.</summary>
public sealed record ReleaseNote(SemVersion Version, string Markdown);

/// <summary>
/// Reads the release notes an application carries in its own assembly. Each application keeps one
/// markdown file per release in a ReleaseNotes folder, named for the release without a prerelease
/// label (ReleaseNotes/2.3.0.md), and embeds them with:
/// <code>
/// &lt;EmbeddedResource Include="ReleaseNotes\*.md" LogicalName="ReleaseNotes/%(Filename)%(Extension)" /&gt;
/// </code>
/// Release candidates of 2.3.0 show 2.3.0.md, so the notes can be written as the release takes shape.
/// </summary>
public static class ReleaseNotes
{
    public const string ResourcePrefix = "ReleaseNotes/";

    /// <summary>Every release's notes embedded in <paramref name="assembly"/>, newest first.</summary>
    public static IReadOnlyList<ReleaseNote> Load(Assembly assembly)
    {
        var notes = new List<ReleaseNote>();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            var versionText = name[ResourcePrefix.Length..^".md".Length];

            if (!SemVersion.TryParse(versionText, SemVersionStyles.Any, out var version))
                continue;

            using var stream = assembly.GetManifestResourceStream(name);

            if (stream == null)
                continue;

            using var reader = new StreamReader(stream, Encoding.UTF8);

            var markdown = reader.ReadToEnd().Trim();

            if (markdown.Length > 0)
                notes.Add(new ReleaseNote(version, markdown));
        }

        return notes.OrderByDescending(n => n.Version, SemVersion.PrecedenceComparer).ToList();
    }

    /// <summary>
    /// The notes a user moving from <paramref name="previous"/> to <paramref name="current"/> hasn't
    /// seen, newest first. Prerelease labels are ignored, so moving between two release candidates of
    /// 2.3.0 shows 2.3.0's notes again.
    /// </summary>
    public static IReadOnlyList<ReleaseNote> Between(IEnumerable<ReleaseNote> notes, SemVersion? previous, SemVersion current)
    {
        var to = Release(current);
        var from = previous == null ? null : Release(previous);

        return notes
            .Where(n =>
            {
                var version = Release(n.Version);

                if (version.ComparePrecedenceTo(to) > 0)
                    return false;

                if (from == null || from.ComparePrecedenceTo(to) == 0)
                    return version.ComparePrecedenceTo(to) == 0;

                return version.ComparePrecedenceTo(from) > 0;
            })
            .OrderByDescending(n => n.Version, SemVersion.PrecedenceComparer)
            .ToList();
    }

    /// <summary>Joins notes into one document, each under a heading with its version.</summary>
    public static string ToMarkdown(IEnumerable<ReleaseNote> notes)
    {
        var builder = new StringBuilder();

        foreach (var note in notes)
        {
            if (builder.Length > 0)
                builder.AppendLine().AppendLine();

            builder.Append("## ").AppendLine(Release(note.Version).ToString()).AppendLine();
            builder.Append(note.Markdown);
        }

        return builder.ToString();
    }

    private static SemVersion Release(SemVersion version) => version.WithoutPrereleaseOrMetadata();
}
