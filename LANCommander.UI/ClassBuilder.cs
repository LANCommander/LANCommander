namespace LANCommander.UI;

internal class ClassBuilder
{
    private readonly List<string> _classes = new();

    public ClassBuilder Add(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return this;
        
        _classes.Add(className);

        return this;
    }

    public ClassBuilder If(Func<bool> condition, string className)
    {
        if (condition())
            _classes.Add(className);

        return this;
    }

    public string Build()
    {
        return String.Join(' ', _classes);
    }
}