#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Globalization;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// Turns live PowerShell values into display-ready <see cref="VariableInfo"/> nodes, one level at
/// a time, and keeps a handle table so the UI can request the next level lazily.
/// </summary>
/// <remarks>
/// <para>
/// Every member runs on the PS-Pipeline thread. That is not incidental: reading a property can
/// invoke a user-defined ScriptProperty getter, which is script execution and must happen in the
/// engine's own call context.
/// </para>
/// <para>
/// Expansion is lazy rather than eager precisely because of that. Walking every property of every
/// local at every stop would run arbitrary user code on a hot path.
/// </para>
/// </remarks>
public sealed class VariableExpander
{
    private const int MaxChildren = 200;
    private const int MaxValueLength = 300;

    private readonly Dictionary<int, object?> _handles = new();
    private int _nextHandle;

    /// <summary>
    /// Automatic and preference variables that would drown the panel. Note that $_, $PSItem,
    /// $args, $PSBoundParameters, $PSScriptRoot and $PSCommandPath are deliberately NOT hidden:
    /// they are among the most useful things to see while debugging.
    /// </summary>
    private static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "PID", "PSHOME", "PSCulture", "PSUICulture", "PSVersionTable",
        "PSEdition", "ShellId", "ConsoleFileName", "ExecutionContext", "Host", "HOME", "NestedPromptLevel",
        "MaximumAliasCount", "MaximumDriveCount", "MaximumErrorCount", "MaximumFunctionCount",
        "MaximumVariableCount", "OutputEncoding", "ProgressPreference", "ConfirmPreference",
        "DebugPreference", "ErrorActionPreference", "ErrorView", "FormatEnumerationLimit",
        "InformationPreference", "VerbosePreference", "WarningPreference", "WhatIfPreference",
        "PSDefaultParameterValues", "PSEmailServer", "PSSessionApplicationName",
        "PSSessionConfigurationName", "PSSessionOption", "PSNativeCommandUseErrorActionPreference",
        "IsCoreCLR", "IsLinux", "IsMacOS", "IsWindows", "StackTrace", "PSStyle",
        "EnabledExperimentalFeatures",

        // LANCommander plumbing: services stashed for cmdlets, the banner, and the debugger's own
        // compiled script and output capture.
        "Logo", "LANCommander.SDK.PSHostUI",
        ScriptServicesProvider.ProfileClientKey, ScriptServicesProvider.ApiRequestFactoryKey,
        ScriptServicesProvider.SettingsProviderKey,
        DebugSession.CompiledScriptVariable, DebugSession.OutputCaptureVariable,

        // Punctuation automatics ($?, $^, $$) and the debugger's own plumbing. PSDebugContext in
        // particular only exists because we are stopped, so showing it is pure noise.
        "?", "^", "$", "PSDebugContext",

        // Present in every single frame and rarely what anyone is looking for. $args,
        // $PSBoundParameters, $PSScriptRoot and $PSCommandPath are kept: those do get used.
        "input", "MyInvocation",
    };

    /// <summary>Drop every handle. Called on resume, when the objects behind them start mutating.</summary>
    public void Reset()
    {
        _handles.Clear();
        _nextHandle = 0;
    }

    public static bool IsHidden(string name) => Hidden.Contains(name);

    /// <summary>Describe one value as a node, allocating a handle if it can be drilled into.</summary>
    public VariableInfo Describe(string name, object? value)
    {
        var unwrapped = Unwrap(value);
        var expandable = CanExpand(unwrapped);

        int? handle = null;
        if (expandable)
        {
            handle = _nextHandle++;
            _handles[handle.Value] = value;
        }

        return new VariableInfo
        {
            Name = name,
            TypeName = DescribeType(unwrapped),
            Value = FormatValue(unwrapped),
            HasChildren = expandable,
            Handle = handle,
        };
    }

    /// <summary>Produce the children of a previously-described node.</summary>
    public IReadOnlyList<VariableInfo> Expand(int handle)
    {
        if (!_handles.TryGetValue(handle, out var raw))
            return Array.Empty<VariableInfo>();

        var value = Unwrap(raw);
        var children = new List<VariableInfo>();
        var truncated = false;

        switch (value)
        {
            case null:
                break;

            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (children.Count >= MaxChildren) { truncated = true; break; }
                    children.Add(Describe(entry.Key?.ToString() ?? "<null key>", entry.Value));
                }
                break;

            case IEnumerable enumerable and not string:
                var index = 0;
                foreach (var element in enumerable)
                {
                    if (children.Count >= MaxChildren) { truncated = true; break; }
                    children.Add(Describe("[" + index++ + "]", element));
                }
                break;

            default:
                truncated = AddProperties(raw, children);
                break;
        }

        // PSCustomObject reaches the default branch above, but a plain object that happens to be
        // enumerable does not, so fall back to properties when enumeration yielded nothing.
        if (children.Count == 0 && value is not null)
            truncated |= AddProperties(raw, children);

        if (truncated)
        {
            children.Add(new VariableInfo
            {
                Name = "...",
                TypeName = string.Empty,
                Value = "(truncated at " + MaxChildren + " items)",
                HasChildren = false,
            });
        }

        return children;
    }

    /// <returns>True if the child list was truncated.</returns>
    private bool AddProperties(object? raw, List<VariableInfo> children)
    {
        if (raw is null) return false;

        PSObject pso;
        try { pso = PSObject.AsPSObject(raw); }
        catch (Exception) { return false; }

        foreach (var property in pso.Properties)
        {
            if (children.Count >= MaxChildren) return true;

            try
            {
                if (!property.IsGettable) continue;
                children.Add(Describe(property.Name, property.Value));
            }
            catch (Exception ex)
            {
                // A ScriptProperty getter is user code and can throw. One bad property must not
                // blank out the whole node.
                children.Add(new VariableInfo
                {
                    Name = property.Name,
                    TypeName = string.Empty,
                    Value = "<error: " + ex.GetBaseException().Message + ">",
                    HasChildren = false,
                });
            }
        }

        return false;
    }

    private static object? Unwrap(object? value) =>
        value is PSObject pso && pso.BaseObject is not PSCustomObject ? pso.BaseObject : value;

    private static bool CanExpand(object? value)
    {
        if (value is null) return false;

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum) return false;

        return value is not (string or decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Uri or Version);
    }

    private static string DescribeType(object? value)
    {
        if (value is null) return string.Empty;

        if (value is PSObject pso)
            return pso.TypeNames.FirstOrDefault()?.Split('.').Last() ?? "PSObject";

        var type = value.GetType();
        if (!type.IsGenericType)
            return type.Name;

        var bare = type.Name.Split(GenericArityMarker)[0];
        var arguments = string.Join(",", type.GetGenericArguments().Select(a => a.Name));
        return bare + "<" + arguments + ">";
    }

    /// <summary>The backtick that separates a generic type name from its arity, e.g. List`1.</summary>
    private const char GenericArityMarker = '\u0060';

    public static string FormatValue(object? value)
    {
        var text = FormatCore(value);
        return text.Length > MaxValueLength ? text[..MaxValueLength] + "..." : text;
    }

    private static string FormatCore(object? value)
    {
        switch (Unwrap(value))
        {
            case null:
                return "$null";

            case bool b:
                return b ? "$true" : "$false";

            case string s:
                return "\"" + s.Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

            case char c:
                return "'" + c + "'";

            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

            case IDictionary dictionary:
                return "{" + dictionary.Count + (dictionary.Count == 1 ? " entry}" : " entries}");

            case ICollection collection:
                return DescribeType(collection) + "[" + collection.Count + "]";

            case PSCustomObject:
                return "[PSCustomObject]";

            case IEnumerable enumerable:
                return DescribeType(enumerable) + " (unenumerated)";

            case { } other:
                try
                {
                    var text = other.ToString();
                    return string.IsNullOrEmpty(text) || text == other.GetType().FullName
                        ? "[" + DescribeType(other) + "]"
                        : text;
                }
                catch (Exception)
                {
                    return "[" + DescribeType(other) + "]";
                }
        }
    }
}
