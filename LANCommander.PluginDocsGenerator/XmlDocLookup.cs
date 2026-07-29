using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace LANCommander.PluginDocsGenerator;

/// <summary>
/// Loads the XML documentation files that sit alongside the given assemblies and exposes their
/// &lt;summary&gt; text keyed by XML documentation member id.
/// </summary>
internal sealed class XmlDocLookup
{
    private readonly Dictionary<string, string> _summaries = new(StringComparer.Ordinal);

    public XmlDocLookup(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(xmlPath))
                continue;

            foreach (var member in XDocument.Load(xmlPath).Descendants("member"))
            {
                var name = member.Attribute("name")?.Value;
                var summary = member.Element("summary");
                if (name is null || summary is null)
                    continue;

                _summaries[name] = Render(summary);
            }
        }
    }

    public string? GetSummary(string memberId) => _summaries.TryGetValue(memberId, out var value) ? value : null;

    // Flattens a <summary> element into plain text, resolving the common inline doc tags.
    private static string Render(XElement summary)
    {
        var sb = new StringBuilder();
        RenderNodes(summary, sb);

        // Collapse the incidental whitespace/indentation that XML doc comments carry.
        var text = string.Join(" ", sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Trim();
    }

    private static void RenderNodes(XElement element, StringBuilder sb)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    sb.Append(text.Value);
                    break;
                case XElement child:
                    RenderElement(child, sb);
                    break;
            }
        }
    }

    private static void RenderElement(XElement element, StringBuilder sb)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            case "seealso":
                var reference = element.Attribute("cref")?.Value ?? element.Attribute("href")?.Value;
                sb.Append('`').Append(ShortName(reference)).Append('`');
                break;
            case "paramref":
            case "typeparamref":
                sb.Append('`').Append(element.Attribute("name")?.Value).Append('`');
                break;
            case "c":
                sb.Append('`').Append(element.Value).Append('`');
                break;
            default:
                RenderNodes(element, sb);
                break;
        }
    }

    // "T:LANCommander.SDK.Plugins.IPlugin" -> "IPlugin", "IPlugin.InitializeAsync" -> "InitializeAsync".
    private static string ShortName(string? cref)
    {
        if (string.IsNullOrEmpty(cref))
            return "";

        var withoutPrefix = cref.Length > 1 && cref[1] == ':' ? cref[2..] : cref;
        var withoutParameters = withoutPrefix.Split('(')[0];
        var segments = withoutParameters.Split('.');
        return segments[^1];
    }
}
