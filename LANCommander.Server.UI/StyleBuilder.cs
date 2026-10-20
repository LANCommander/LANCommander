namespace LANCommander.Server.UI;

public class StyleBuilder
{
    private readonly List<(string Property, string Value)> _styles = [];

    public StyleBuilder Add(string? style)
    {
        if (string.IsNullOrWhiteSpace(style))
            return this;

        foreach (var part in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Split on the first colon only so values like url(https://...) survive
            var separator = part.IndexOf(':');

            if (separator <= 0)
                continue;

            _styles.Add((part[..separator].Trim(), part[(separator + 1)..].Trim()));
        }

        return this;
    }

    public StyleBuilder Add(string property, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _styles.Add((property, value));

        return this;
    }

    public StyleBuilder If(bool condition, string property, string? value)
        => condition ? Add(property, value) : this;

    public StyleBuilder If(Func<bool> condition, string property, string? value)
        => If(condition(), property, value);

    public string? Build()
        => _styles.Count == 0 ? null : string.Join("; ", _styles.Select(s => $"{s.Property}: {s.Value}"));
}
