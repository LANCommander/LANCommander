using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LANCommander.SDK.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.Helpers;

/// <summary>
/// Reads and writes the global script variables file. The server keeps the authoritative copy in its
/// config directory and clients sync theirs from it, so every runspace, on either side, reads the
/// same file from <see cref="DefaultPath"/>.
/// </summary>
public static class GlobalVariableStore
{
    public const string FileName = "Variables.yml";

    private static readonly object Lock = new();

    private static readonly Regex NamePattern = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>The variables file in this install's config directory.</summary>
    public static string DefaultPath => AppPaths.GetConfigPath(FileName);

    /// <summary>A name scripts can refer to as <c>$var:Name</c> without braces.</summary>
    public static bool IsValidName(string name) =>
        !String.IsNullOrWhiteSpace(name) && NamePattern.IsMatch(name);

    /// <summary>
    /// The variables in <paramref name="path"/> (the default file when null), ordered by name. A
    /// missing or unreadable file reads as none, so scripts still run without it.
    /// </summary>
    public static List<GlobalVariable> Load(string path = null)
    {
        path ??= DefaultPath;

        lock (Lock)
        {
            if (!File.Exists(path))
                return [];

            var yaml = File.ReadAllText(path);

            return Deserialize(yaml);
        }
    }

    /// <summary>Writes <paramref name="variables"/> to <paramref name="path"/> (the default file when null).</summary>
    public static void Save(IEnumerable<GlobalVariable> variables, string path = null)
    {
        path ??= DefaultPath;

        var yaml = Serialize(variables);

        lock (Lock)
        {
            var directory = Path.GetDirectoryName(path);

            if (!String.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Write beside the file then swap, so a runspace reading it never sees half a file
            var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

            File.WriteAllText(temporary, yaml);

            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
    }

    public static string Serialize(IEnumerable<GlobalVariable> variables)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        return serializer.Serialize(Normalize(variables));
    }

    public static List<GlobalVariable> Deserialize(string yaml)
    {
        if (String.IsNullOrWhiteSpace(yaml))
            return [];

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return Normalize(deserializer.Deserialize<List<GlobalVariable>>(yaml));
    }

    /// <summary>Drops nameless entries and later duplicates of a name, and orders by name.</summary>
    private static List<GlobalVariable> Normalize(IEnumerable<GlobalVariable> variables) =>
        (variables ?? [])
            .Where(v => v != null && !String.IsNullOrWhiteSpace(v.Name))
            .GroupBy(v => v.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var variable = g.First();

                return new GlobalVariable
                {
                    Name = g.Key,
                    Value = variable.Value ?? String.Empty,
                    Description = String.IsNullOrWhiteSpace(variable.Description) ? null : variable.Description,
                };
            })
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
