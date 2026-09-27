using System.ComponentModel.DataAnnotations;

namespace LANCommander.Server.UI.Controls;

/// <summary>The kinds of option a redistributable's option schema can declare.</summary>
public enum OptionKind
{
    String,
    [Display(Name = "Boolean")]
    Bool,
    [Display(Name = "Integer")]
    Int,
    Choice,
    List,
}

/// <summary>What each item of a list option is.</summary>
public enum OptionListShape
{
    [Display(Name = "Scalar (list of values)")]
    Scalar,
    [Display(Name = "Composite (list of records)")]
    Composite,
}

/// <summary>The formats <see cref="ConfigImportDialog"/> can read a schema from.</summary>
public enum ConfigFormat
{
    [Display(Name = "Auto-detect")]
    Auto,
    [Display(Name = "INI")]
    Ini,
    [Display(Name = "JSON")]
    Json,
    [Display(Name = "XML")]
    Xml,
}

public static class OptionSchemaNames
{
    /// <summary>The name the schema's YAML uses for a kind, e.g. "bool".</summary>
    public static string YamlName(this OptionKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>The name <c>ConfigToOptionSchemaService</c> expects, e.g. "ini".</summary>
    public static string ServiceName(this ConfigFormat format) => format.ToString().ToLowerInvariant();
}
