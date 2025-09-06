namespace LANCommander.UI;

internal class ClassBuilder
{
    private List<string> Classes = new();

    public ClassBuilder Add(string className)
    {
        Classes.Add(className);

        return this;
    }

    public ClassBuilder If(Func<bool> condition, string className)
    {
        if (condition())
            Classes.Add(className);

        return this;
    }

    public string Build()
    {
        return String.Join(' ', Classes);
    }
}