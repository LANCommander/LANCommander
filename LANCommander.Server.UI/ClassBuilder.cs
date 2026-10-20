namespace LANCommander.Server.UI;

public class ClassBuilder
{
    private readonly List<string> _classes = new();

    public ClassBuilder Add(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return this;

        _classes.Add(className);

        return this;
    }

    public ClassBuilder If(bool condition, string className)
        => condition ? Add(className) : this;

    public ClassBuilder If(Func<bool> condition, string className)
        => If(condition(), className);

    public string? Build()
        => _classes.Count == 0 ? null : string.Join(' ', _classes);
}
