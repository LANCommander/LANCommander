using Superpower;
using Superpower.Display;
using Superpower.Model;
using Superpower.Parsers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.Parsers.Ini
{
    enum IniToken
    {
        [Token(Example = "[")]
        SectionHeaderStart,

        [Token(Example = "]")]
        SectionHeaderEnd,

        [Token(Example = ";")]
        CommentStart,

        [Token(Example = "=")]
        KeyValueDelimiter
    }

    static class IniTokenizer
    {
        static TextParser<Unit> IniStringToken { get; } =
            from open in Character.EqualTo('"')
            from content in Character.EqualTo('\\').IgnoreThen(Character.AnyChar).Value(Unit.Value).Try()
                .Or(Character.Except('"').Value(Unit.Value))
                .IgnoreMany()
            from close in Character.EqualTo('"')
            select Unit.Value;
    }

    // ── Data model ───────────────────────────────────────────────────────────────

    /// <summary>Represents a single key-value pair inside an INI section.</summary>
    public class IniKey
    {
        public string Name { get; }
        public string? Value { get; set; }

        public IniKey(string name, string? value = null)
        {
            Name = name;
            Value = value;
        }
    }

    /// <summary>An ordered, LINQ-queryable collection of <see cref="IniKey"/> entries.</summary>
    public class IniKeyCollection : IEnumerable<IniKey>
    {
        private readonly List<IniKey> _keys = new();

        /// <summary>Total number of keys, including duplicates.</summary>
        public int Count => _keys.Count;

        /// <summary>Returns <c>true</c> if any key has the given name (case-insensitive).</summary>
        public bool Contains(string name) =>
            _keys.Any(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns the first key with the given name, or <c>null</c>.</summary>
        public IniKey? this[string name] =>
            _keys.FirstOrDefault(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));

        public void Add(IniKey key) => _keys.Add(key);

        public void Add(string name, string? value) => _keys.Add(new IniKey(name, value));

        public void Insert(int index, IniKey key) => _keys.Insert(index, key);

        public void Insert(int index, string name, string? value) => _keys.Insert(index, new IniKey(name, value));

        public bool Remove(IniKey key) => _keys.Remove(key);

        public IEnumerator<IniKey> GetEnumerator() => _keys.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Represents an INI section with a name and a collection of keys.</summary>
    public class IniSection
    {
        public string Name { get; }
        public IniKeyCollection Keys { get; } = new();

        public IniSection(string name) => Name = name;
    }

    /// <summary>An ordered, LINQ-queryable collection of <see cref="IniSection"/> entries.</summary>
    public class IniSectionCollection : IEnumerable<IniSection>
    {
        private readonly List<IniSection> _sections = new();

        /// <summary>Total number of sections.</summary>
        public int Count => _sections.Count;

        /// <summary>Returns <c>true</c> if any section has the given name (case-insensitive).</summary>
        public bool Contains(string name) =>
            _sections.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns the first section with the given name, or <c>null</c>.</summary>
        public IniSection? this[string name] =>
            _sections.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        public void Add(IniSection section) => _sections.Add(section);

        public bool Remove(IniSection section) => _sections.Remove(section);

        public IEnumerator<IniSection> GetEnumerator() => _sections.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>The parsed representation of an INI document.</summary>
    public class IniDocument
    {
        public IniSectionCollection Sections { get; } = new();

        /// <summary>Serializes the document back to INI format.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();

            foreach (var section in Sections)
            {
                sb.AppendLine($"[{section.Name}]");

                foreach (var key in section.Keys)
                {
                    sb.AppendLine($"{key.Name}={key.Value}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /// <summary>Options controlling INI parse/write behavior.</summary>
    public class IniParseOptions
    {
        /// <summary>If false, duplicate keys within a section are merged (first value wins).</summary>
        public bool AllowDuplicateKeys { get; set; } = true;

        /// <summary>If false, duplicate sections are merged (keys combined).</summary>
        public bool AllowDuplicateSections { get; set; } = true;

        /// <summary>Encoding for file I/O operations.</summary>
        public Encoding Encoding { get; set; } = Encoding.Default;
    }

    // ── Parser ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// A Superpower-based parser for INI-formatted text.
    /// <para>
    /// Supports:
    /// <list type="bullet">
    ///   <item>Sections: <c>[SectionName]</c> (names may contain <c>.</c>)</item>
    ///   <item>Key-value pairs: <c>key = value</c> or <c>key=value</c></item>
    ///   <item>Keys with brackets: <c>Aliases[0]</c>, bare digits: <c>0</c></item>
    ///   <item>Values containing <c>=</c>, <c>"</c>, <c>()</c>, <c>|</c> — split on first <c>=</c></item>
    ///   <item>Duplicate keys within the same section (multi-value)</item>
    ///   <item>Comment lines beginning with <c>;</c> or <c>#</c></item>
    ///   <item>Blank lines (ignored)</item>
    /// </list>
    /// </para>
    /// </summary>
    public class IniParser
    {
        // Consume any characters that are not a line-ending character.
        private static readonly TextParser<char[]> RestOfLine =
            Character.ExceptIn('\r', '\n').Many();

        // Section header: optional leading whitespace, '[', section name (trimmed), ']',
        // then any trailing content on the same line is consumed and discarded.
        private static readonly TextParser<string> SectionHeaderParser =
            from _ws   in Character.In(' ', '\t').Many()
            from _ob   in Character.EqualTo('[')
            from name  in Character.ExceptIn(']', '\r', '\n').AtLeastOnce()
                                .Select(chars => new string(chars).Trim())
            from _cb   in Character.EqualTo(']')
            from _rest in RestOfLine
            select name;

        // Key-value line.
        //   Key   = everything before the first '=' on the line, trimmed.
        //   Value = everything after  the first '=' on the line, trimmed.
        // The Where guard rejects lines whose key part is entirely whitespace
        // (e.g. a stray '=' with no key), letting them fall through to the
        // comment/blank skip path.
        private static readonly TextParser<(string Key, string Value)> KeyValueLineParser =
            from key in Character.ExceptIn('=', '\r', '\n').AtLeastOnce()
                            .Select(chars => new string(chars).Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
            from _eq in Character.EqualTo('=')
            from value in RestOfLine.Select(chars => new string(chars).Trim())
            select (key, value);

        /// <summary>
        /// Parses an INI-formatted string and returns an <see cref="IniDocument"/>.
        /// </summary>
        /// <param name="text">The INI file content to parse.</param>
        /// <returns>A parsed <see cref="IniDocument"/>.</returns>
        public static IniDocument Parse(string text) => Parse(text, new IniParseOptions());

        /// <summary>
        /// Parses an INI-formatted string with options and returns an <see cref="IniDocument"/>.
        /// </summary>
        public static IniDocument Parse(string text, IniParseOptions options)
        {
            var doc = new IniDocument();
            IniSection? currentSection = null;

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var rawLine in lines)
            {
                var span = new TextSpan(rawLine);

                // 1. Section header?
                var sectionResult = SectionHeaderParser(span);
                if (sectionResult.HasValue)
                {
                    var sectionName = sectionResult.Value;

                    if (!options.AllowDuplicateSections)
                    {
                        var existing = doc.Sections[sectionName];
                        if (existing != null)
                        {
                            currentSection = existing;
                            continue;
                        }
                    }

                    currentSection = new IniSection(sectionName);
                    doc.Sections.Add(currentSection);
                    continue;
                }

                // 2. Key-value pair (only inside a section)?
                var kvResult = KeyValueLineParser(span);
                if (kvResult.HasValue && currentSection is not null)
                {
                    if (!options.AllowDuplicateKeys)
                    {
                        var existingKey = currentSection.Keys[kvResult.Value.Key];
                        if (existingKey != null)
                        {
                            // Merge: keep first value (ignore duplicates)
                            continue;
                        }
                    }

                    currentSection.Keys.Add(new IniKey(kvResult.Value.Key, kvResult.Value.Value));
                    continue;
                }

                // 3. Comment or blank line — skip.
            }

            return doc;
        }

        /// <summary>
        /// Loads and parses an INI file from disk.
        /// </summary>
        public static IniDocument Load(string filePath, IniParseOptions? options = null)
        {
            var opts = options ?? new IniParseOptions();
            var text = File.ReadAllText(filePath, opts.Encoding);
            return Parse(text, opts);
        }

        /// <summary>
        /// Saves an <see cref="IniDocument"/> to disk.
        /// </summary>
        public static void Save(IniDocument document, string filePath, Encoding? encoding = null)
        {
            File.WriteAllText(filePath, document.Serialize(), encoding ?? Encoding.Default);
        }
    }
}
