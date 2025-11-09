using System.Text;

namespace LANCommander.Server.Parsers;

public class ConnectionStringBuilder
{
    private Dictionary<string, string> _keyValuePairs = new();

    public ConnectionStringBuilder Add(string key, string value)
    {
        _keyValuePairs.Add(key, value);

        return this;
    }
    
    public ConnectionStringBuilder AddIf(Func<bool> condition, string key, object value)
    {
        if (condition())
            _keyValuePairs.Add(key, value.ToString() ?? string.Empty);

        return this;
    }

    public string Build() => String.Join(';', _keyValuePairs.Select(kvp => $"{kvp.Key}={kvp.Value}"));
}