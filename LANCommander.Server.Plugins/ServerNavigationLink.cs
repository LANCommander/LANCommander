namespace LANCommander.Server.Plugins;

/// <summary>
/// A ready-made <see cref="IServerNavigationExtension"/>, so a plugin can register any number of
/// navigation entries without writing a class for each:
/// <code>
/// services.AddSingleton&lt;IServerNavigationExtension&gt;(new ServerNavigationLink
/// {
///     Id = "com.mycompany.myplugin.catalog",
///     Label = "Catalog",
///     Href = "/Plugins/MyPlugin/Catalog",
///     Section = "com.mycompany.myplugin",
/// });
/// </code>
/// </summary>
public sealed class ServerNavigationLink : IServerNavigationExtension
{
    /// <inheritdoc />
    public required string Id { get; init; }

    /// <inheritdoc />
    public required string Label { get; init; }

    /// <inheritdoc />
    public required string Href { get; init; }

    /// <inheritdoc />
    public string? Icon { get; init; }

    /// <inheritdoc />
    public string? Section { get; init; }

    /// <inheritdoc />
    public string? After { get; init; }

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public PluginAccessPolicy Access { get; init; } = PluginAccessPolicy.Administrator;
}
