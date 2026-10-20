namespace LANCommander.Packaging.Changes;

/// <summary>
/// A registry key or value the monitored installer created or wrote.
/// </summary>
/// <remarks>
/// The Interposer hooks report the verb, key path and value name but not the value's data or
/// type, so generated install scripts can recreate the shape of the registry but not its
/// contents. Capturing data requires a change to the Interposer's wire format.
/// </remarks>
public class RegistryChange
{
    /// <summary>Interposer verb, e.g. "REG WRITE" or "REG CREATE".</summary>
    public string Verb { get; set; } = string.Empty;

    /// <summary>Full key path, e.g. "HKEY_LOCAL_MACHINE\SOFTWARE\Example".</summary>
    public string KeyPath { get; set; } = string.Empty;

    /// <summary>Value name, or empty when only the key itself was created.</summary>
    public string ValueName { get; set; } = string.Empty;

    /// <summary>
    /// Architecture of the process that made the write. An x86 process writing
    /// HKLM\Software\Foo physically lands under HKLM\Software\WOW6432Node\Foo, so scripts
    /// generated from x86 captures have to target the 32-bit view.
    /// </summary>
    public ProcessArchitecture SourceArchitecture { get; set; }

    /// <summary>Process the change was observed in, for diagnostics.</summary>
    public int ProcessId { get; set; }
}
