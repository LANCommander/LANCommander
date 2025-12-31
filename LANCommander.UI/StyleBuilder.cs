namespace LANCommander.UI;

internal class StyleBuilder
{
    private readonly List<(string Property, string Value)> _styles = [];

    public StyleBuilder Add(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
            return this;
        
        foreach (var part in style.Split(";"))
        {
            var partParts = part.Split(":");
            
            _styles.Add((partParts[0].Trim(), partParts[1].Trim()));
        }

        return this;
    }
    
    public StyleBuilder Add(string property, string value)
    {
        _styles.Add((property, value));

        return this;
    }

    public StyleBuilder If(Func<bool> condition, string property, string value)
    {
        if (condition())
            _styles.Add((property, value));

        return this;
    }

    public string Build()
        => string.Join("; ", _styles.Select(s => string.Join(": ",  s.Property, s.Value)));
}