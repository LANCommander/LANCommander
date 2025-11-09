using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace LANCommander.Server.Configuration;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class ConnectionStringKeyAttribute : Attribute
{
    public string Key { get; }
    public ConnectionStringKeyAttribute(string key) => Key = key;
}

public static class ConnectionStringBinder
{
    public static T Bind<T>(string connectionString) where T : new()
        => (T)Bind(connectionString, typeof(T));

    public static object Bind(string connectionString, Type targetType)
    {
        if (targetType == null) throw new ArgumentNullException(nameof(targetType));
        var instance = Activator.CreateInstance(targetType)
                      ?? throw new InvalidOperationException($"Could not create {targetType}.");

        var dict = Parse(connectionString);

        // Build a lookup from possible keys → property
        var props = targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                              .Where(p => p.CanWrite);

        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in props)
        {
            // Primary key: property name
            map[p.Name] = p;

            // Aliases via attribute(s)
            foreach (var attr in p.GetCustomAttributes<ConnectionStringKeyAttribute>())
                map[attr.Key] = p;
        }

        foreach (var (key, raw) in dict)
        {
            if (!map.TryGetValue(key, out var prop)) continue;
            var converted = ConvertTo(raw, prop.PropertyType);
            prop.SetValue(instance, converted);
        }

        return instance;
    }

    /// <summary>
    /// Parse a connection string into a dictionary of key/value pairs.
    /// Handles quoted values containing semicolons.
    /// Last duplicate key wins.
    /// </summary>
    public static IDictionary<string, string> Parse(string connectionString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(connectionString)) return result;

        int i = 0;
        int n = connectionString.Length;

        while (i < n)
        {
            // Skip leading whitespace and semicolons
            while (i < n && (char.IsWhiteSpace(connectionString[i]) || connectionString[i] == ';')) i++;
            if (i >= n) break;

            // Read key up to '='
            int keyStart = i;
            while (i < n && connectionString[i] != '=') i++;
            if (i >= n)
            {
                // No '=', ignore trailing garbage
                break;
            }

            string key = connectionString.Substring(keyStart, i - keyStart).Trim();
            i++; // skip '='

            // Read value: quoted or unquoted
            string value;
            if (i < n && connectionString[i] == '"')
            {
                i++; // skip opening quote
                int valStart = i;
                bool foundEndQuote = false;
                var sb = new System.Text.StringBuilder();

                while (i < n)
                {
                    char c = connectionString[i];

                    if (c == '"')
                    {
                        // Check for escaped double-quote ("") → "
                        if (i + 1 < n && connectionString[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                            continue;
                        }
                        foundEndQuote = true;
                        i++; // consume ending quote
                        break;
                    }

                    sb.Append(c);
                    i++;
                }

                value = sb.ToString();

                // After quoted value, consume until next ';' or end
                while (i < n && connectionString[i] != ';') i++;
                if (i < n && connectionString[i] == ';') i++; // skip ';'
            }
            else
            {
                // Unquoted: read until ';' or end
                int valStart = i;
                while (i < n && connectionString[i] != ';') i++;
                value = connectionString.Substring(valStart, i - valStart).Trim();
                if (i < n && connectionString[i] == ';') i++; // skip ';'
            }

            if (!string.IsNullOrEmpty(key))
                result[key] = value;
        }

        return result;
    }

    private static object? ConvertTo(string? raw, Type destinationType)
    {
        if (destinationType == typeof(string)) return raw;
        if (string.IsNullOrEmpty(raw))
        {
            if (IsNullable(destinationType)) return null;
            // Empty string into non-nullable → try default(T)
            return destinationType.IsValueType ? Activator.CreateInstance(UnderlyingType(destinationType)) : null;
        }

        var nonNullableType = UnderlyingType(destinationType);

        // Enums: support numeric and named (case-insensitive)
        if (nonNullableType.IsEnum)
        {
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var _))
                return Enum.ToObject(nonNullableType, Convert.ToInt64(raw, CultureInfo.InvariantCulture));

            return Enum.Parse(nonNullableType, raw, ignoreCase: true);
        }

        // Booleans: accept 1/0/true/false/yes/no
        if (nonNullableType == typeof(bool))
        {
            if (bool.TryParse(raw, out var b)) return b;
            if (raw.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("on", StringComparison.OrdinalIgnoreCase))
                return true;
            if (raw.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("off", StringComparison.OrdinalIgnoreCase))
                return false;

            throw new FormatException($"Invalid boolean value '{raw}'.");
        }

        var converter = TypeDescriptor.GetConverter(nonNullableType);
        if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            var val = converter.ConvertFrom(null, CultureInfo.InvariantCulture, raw);
            return val;
        }

        // Fallback: try ChangeType
        return Convert.ChangeType(raw, nonNullableType, CultureInfo.InvariantCulture);
    }

    private static bool IsNullable(Type t) => Nullable.GetUnderlyingType(t) != null || !t.IsValueType;
    private static Type UnderlyingType(Type t) => Nullable.GetUnderlyingType(t) ?? t;
}
