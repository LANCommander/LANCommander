#pragma warning disable CA1416 

using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LANCommander.SDK.Utilities;

/// <summary>
/// Recursively reads one or more Windows registry paths and builds a single “.reg”-format text blob.
/// </summary>
/// <remarks>
/// - Emits the standard "Windows Registry Editor Version 5.00" header (UTF-8 with BOM). <br/> 
/// - Walks each key and all its subkeys, serializing REG_SZ, REG_EXPAND_SZ, REG_DWORD,
///   REG_QWORD, REG_MULTI_SZ and REG_BINARY with correct syntax.  <br/>
/// - Does not call any external process or use temp files—pure .NET managed code.  <br/>
/// </remarks>
/// <example>
/// // 1) Export two registry branches into a single .reg string  
/// var exporter = new RegistryExportUtility();  
/// var paths    = new[] {  
///     @"HKEY_CURRENT_USER\Software\MyApp",  
///     @"HKLM\SOFTWARE\Vendor\Product"  
/// };  
/// string blob = exporter.Export(paths);  
///
/// // 2) Persist to disk (must be UTF-8 for regedit.exe)  
/// var utf8 = new UTF8Encoding(true);  
/// File.WriteAllBytes(@"C:\temp\myexport.reg", utf8.GetPreamble()  
///     .Concat(unicode.GetBytes(blob))  
///     .ToArray());  
/// </example>
public class RegistryExportUtility
{
    /// <summary>
    /// Builds a single .reg-format string from multiple registry paths.
    /// </summary>
    /// <param name="registryPaths">Enumerable of registry key paths to export.</param>
    /// <returns>Fully-formed .reg text (excluding BOM/preamble).</returns>
    public string Export(params string[] registryPaths)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();

        foreach (var fullPath in registryPaths)
        {
            var (hive, subKey) = ParseHive(fullPath);
            using (var key = hive.OpenSubKey(subKey))
            {
                if (key != null)
                    ExportKeyRecursive(key, sb);
            }
        }

        return sb.ToString();
    }

    private void ExportKeyRecursive(RegistryKey key, StringBuilder sb)
    {
        sb.AppendLine($"[{key.Name}]");

        foreach (var name in key.GetValueNames())
            sb.AppendLine(FormatValue(key, name));

        sb.AppendLine();

        foreach (var child in key.GetSubKeyNames())
        {
            using (var sub = key.OpenSubKey(child))
            {
                if (sub != null)
                    ExportKeyRecursive(sub, sb);
            }
        }
    }

    private string FormatValue(RegistryKey key, string name)
    {
        var kind = key.GetValueKind(name);
        var data = key.GetValue(name);
        var label = string.IsNullOrEmpty(name) ? "@" : $"\"{name}\"";

        switch (kind)
        {
            case RegistryValueKind.String:
                return $"{label}=\"{Escape((string)data)}\"";

            case RegistryValueKind.ExpandString:
                return $"{label}=hex(2):{ToHex(Encoding.Unicode.GetBytes((string)data + "\0"))}";

            case RegistryValueKind.DWord:
                return $"{label}=dword:{((uint)(int)data):x8}";

            case RegistryValueKind.QWord:
                return $"{label}=hex(b):{ToHex(BitConverter.GetBytes((ulong)data))}";

            case RegistryValueKind.MultiString:
                return $"{label}=hex(7):{ToHex(EncodeMulti((string[])data))}";

            case RegistryValueKind.Binary:
                return $"{label}=hex:{ToHex((byte[])data)}";

            default:
                // fallback as raw binary
                var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as byte[] ?? Array.Empty<byte>();
                return $"{label}=hex:{ToHex(raw)}";
        }
    }

    private static byte[] EncodeMulti(string[] values)
    {
        using var ms = new MemoryStream();
        foreach (var s in values)
            ms.Write(Encoding.Unicode.GetBytes(s + "\0"));
        // extra null terminator
        ms.Write(new byte[2], 0, 2);
        return ms.ToArray();
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string ToHex(byte[] data) =>
        string.Join(",", data.Select(b => b.ToString("x2")));

    private (RegistryKey hive, string subKey) ParseHive(string full)
    {
        var parts = full.Split(['\\'], 2);
        var root = parts[0]?.ToUpperInvariant().Trim().Trim(':') ?? "HKCU";
        var tail = parts.Length > 1 ? parts[1] : string.Empty;

        return root switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => (Registry.LocalMachine, tail),
            "HKCU" or "HKEY_CURRENT_USER" => (Registry.CurrentUser, tail),
            "HKCR" or "HKEY_CLASSES_ROOT" => (Registry.ClassesRoot, tail),
            "HKU" or "HKEY_USERS" => (Registry.Users, tail),
            "HKCC" or "HKEY_CURRENT_CONFIG" => (Registry.CurrentConfig, tail),
            _ => throw new ArgumentException($"Unknown registry hive: {root}")
        };
    }
}
