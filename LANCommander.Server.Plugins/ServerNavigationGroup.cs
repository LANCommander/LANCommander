namespace LANCommander.Server.Plugins;

/// <summary>
/// A ready-made <see cref="IServerNavigationGroupExtension"/>:
/// <code>
/// services.AddSingleton&lt;IServerNavigationGroupExtension&gt;(new ServerNavigationGroup
/// {
///     Id = "com.mycompany.myplugin",
///     Label = "My Plugin",
///     Icon = "Package",
///     Section = ServerNavigationSection.Main,
/// });
/// </code>
/// </summary>
public sealed class ServerNavigationGroup : IServerNavigationGroupExtension
{
    /// <inheritdoc />
    public required string Id { get; init; }

    /// <inheritdoc />
    public required string Label { get; init; }

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
