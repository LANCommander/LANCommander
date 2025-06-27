#pragma warning disable CA1416 

using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LANCommander.SDK.Utilities;

/// <summary>
/// Parses and imports a Windows “.reg” file into the live registry.  
/// Supports files encoded as UTF-8 (with BOM) or ANSI.  
/// </summary>
/// <remarks>
/// - Handles key sections ([HIVE\Path\…]) and value lines ("Name"=…, @=…).  
/// - Recognizes REG_SZ, REG_EXPAND_SZ, REG_DWORD, REG_QWORD, REG_MULTI_SZ, REG_BINARY.  
/// - Ignores security/ACLs; only writes values.  
/// </remarks>
/// <example>
/// // 1) Import directly from a file stream (UTF-8 BOM is auto-detected):  
/// var importer = new RegistryImportUtility();  
/// using var fs = File.OpenRead(@"C:\temp\export.reg");  
/// importer.Import(fs);  
///
/// // 2) Or read into a string and import:  
/// string content = File.ReadAllText(@"C:\temp\export.reg", Encoding.UTF8);  
/// importer.ImportFromString(content);  
/// </example>
public class RegistryImportUtility
{
    /// <summary>
    /// Reads the .reg data from <paramref name="regStream"/>, detects a UTF-8 BOM,
    /// and imports all keys & values into the registry.
    /// </summary>
    /// <param name="regStream">Stream containing .reg text (UTF-8 or ANSI).</param>
    public void Import(Stream regStream)
    {
        // detectEncodingFromByteOrderMarks = true will strip BOM for us
        using var reader = new StreamReader(regStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string content = reader.ReadToEnd();
        ImportFromString(content);
    }

    /// <summary>
    /// Parses the raw .reg-format text and writes all keys & values.
    /// </summary>
    /// <param name="regFileContent">The entire .reg file contents as a string.</param>
    /// <remarks>Takes the full .reg text in a string (no encoding concern here).</remarks>
    public void ImportFromString(string regFileContent)
    {
        using var reader = new StringReader(regFileContent);
        string? currentFullPath = null;
        RegistryKey? currentKey = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith(";"))
                continue;

            // Skip header
            if (line.StartsWith("Windows Registry Editor Version", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.Equals("REGEDIT4", StringComparison.OrdinalIgnoreCase))
                continue;

            // New section: [HIVE\SubKey\…]
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                // close previous
                currentKey?.Dispose();
                currentFullPath = line[1..^1];
                var (hive, subKey) = ParseHive(currentFullPath);
                currentKey = hive.CreateSubKey(subKey, writable: true);
                continue;
            }

            // Value line: "Name"=type:data  or @=...
            if (currentKey == null)
                throw new InvalidOperationException("Value outside of any key section.");

            var eq = line.IndexOf('=');
            if (eq < 0)
                continue;

            var nameToken = line[..eq].Trim();
            var dataToken = line[(eq + 1)..].Trim();
            var valueName = nameToken == "@" ? "" : Unquote(nameToken);

            // Dispatch by prefix
            if (dataToken.StartsWith("\""))
            {
                // simple string
                var s = Unquote(dataToken);
                currentKey.SetValue(valueName, UnescapeString(s), RegistryValueKind.String);
            }
            else if (dataToken.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
            {
                var hex = dataToken["dword:".Length..];
                var d = Convert.ToUInt32(hex, 16);
                currentKey.SetValue(valueName, (int)d, RegistryValueKind.DWord);
            }
            else if (dataToken.StartsWith("hex(", StringComparison.OrdinalIgnoreCase))
            {
                // e.g. hex(2):00,FF,00,…  or hex(b):…  or hex(7):…
                var kindEnd = dataToken.IndexOf(')');
                var kindId = dataToken["hex(".Length..kindEnd];
                var payload = dataToken[(kindEnd + 2)..]; // skip "):"
                var bytes = ParseHex(payload);

                var rvk = kindId switch
                {
                    "0" => RegistryValueKind.None,
                    "1" => RegistryValueKind.String,
                    "2" => RegistryValueKind.ExpandString,
                    "3" => RegistryValueKind.Binary,
                    "7" => RegistryValueKind.MultiString,
                    "b" => RegistryValueKind.QWord,
                    _ => RegistryValueKind.Binary
                };

                object finalData = rvk switch
                {
                    RegistryValueKind.ExpandString => Encoding.Unicode.GetString(bytes).TrimEnd('\0'),
                    RegistryValueKind.MultiString => ParseMultiString(bytes),
                    RegistryValueKind.QWord => BitConverter.ToUInt64(bytes, 0),
                    _ => bytes
                };

                currentKey.SetValue(valueName, finalData, rvk);
            }
            else if (dataToken.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
            {
                // raw binary
                var payload = dataToken["hex:".Length..];
                var bytes = ParseHex(payload);
                currentKey.SetValue(valueName, bytes, RegistryValueKind.Binary);
            }
            else
            {
                // unknown, skip
            }
        }

        currentKey?.Dispose();
    }

    private static (RegistryKey hive, string subKey) ParseHive(string full)
    {
        var parts = full.Split(new[] { '\\' }, 2);
        var root = parts[0].ToUpperInvariant().Trim().Trim(':');
        var tail = parts.Length > 1 ? parts[1] : "";

        return root switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => (Registry.LocalMachine, tail),
            "HKCU" or "HKEY_CURRENT_USER" => (Registry.CurrentUser, tail),
            "HKCR" or "HKEY_CLASSES_ROOT" => (Registry.ClassesRoot, tail),
            "HKU" or "HKEY_USERS" => (Registry.Users, tail),
            "HKCC" or "HKEY_CURRENT_CONFIG" => (Registry.CurrentConfig, tail),
            _ => throw new ArgumentException($"Unknown hive: {root}")
        };
    }

    private static string Unquote(string s) =>
      s.Length >= 2 && s[0] == '"' && s[^1] == '"'
        ? s[1..^1]
        : s;

    private static string UnescapeString(string s) =>
      s.Replace(@"\\", @"\").Replace("\\\"", "\"");

    private static byte[] ParseHex(string hexData)
    {
        // split by commas, remove any trailing commas or backslashes
        var tokens = hexData
          .TrimEnd('\\')
          .Split(',')
          .Where(tok => tok.Length > 0)
          .ToArray();

        var bytes = new byte[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
            bytes[i] = Convert.ToByte(tokens[i], 16);
        return bytes;
    }

    private static string[] ParseMultiString(byte[] raw)
    {
        // UTF-8, strings nul-terminated, ends with double null
        var all = Encoding.UTF8.GetString(raw);
        return all
          .TrimEnd('\0')
          .Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
