using System.Reflection;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Plugins.Data;
using LANCommander.Server.Settings.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.Tests.PluginData;

/// <summary>
/// Plugin data tests change the server's database settings, which are process-wide, so they run on
/// their own, after everything else.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PluginDataCollection
{
    public const string Name = "PluginData";
}

/// <summary>
/// The sample plugin (LANCommander.Server.Tests.SamplePlugin) deployed to a plugins folder and loaded
/// the way the server loads plugins, into its own load context, against a scratch SQLite database.
/// Creating a second host on the same <see cref="Root"/> is a server restart: a fresh load of the
/// plugin, the same database.
/// </summary>
internal sealed class PluginDataTestHost : IAsyncDisposable
{
    public const string PluginFolder = "LANCommander.Server.Tests.SamplePlugin";
    public const string SampleNamespace = "LANCommander.Server.Tests.SamplePlugin";

    private readonly ServiceProvider _services;

    /// <param name="root">The scratch directory, holding the database.</param>
    /// <param name="deploy">Which of the sample plugin's files to deploy, by file name; all by default.</param>
    /// <param name="provider">The engine the "server" is set up with; Unknown is before first-time setup.</param>
    public PluginDataTestHost(string root, Func<string, bool>? deploy = null, DatabaseProvider provider = DatabaseProvider.SQLite)
    {
        Root = root;

        var pluginsRoot = Path.Combine(root, $"Plugins-{Guid.NewGuid():N}");
        var folder = Path.Combine(pluginsRoot, PluginFolder);

        Directory.CreateDirectory(folder);

        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "SamplePlugin"), "*.dll"))
        {
            if (deploy?.Invoke(Path.GetFileName(file)) != false)
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        }

        DatabaseContext.Provider = provider;
        DatabaseContext.ConnectionString = ConnectionString;

        var services = new ServiceCollection();

        services.AddLogging();

        Loader = new PluginLoaderService();
        Loader.DiscoverAndConfigure(services, PluginHost.Server, pluginsRoot, "2.2.0", NullLogger.Instance);

        services.AddSingleton(Loader);
        Catalog = PluginDataCatalog.Register(services, Loader);

        _services = services.BuildServiceProvider();
    }

    public string Root { get; }

    public string ConnectionString => $"Data Source={Path.Combine(Root, "plugins.db")};Pooling=False";

    public IServiceProvider Services => _services;

    public PluginLoaderService Loader { get; }

    public PluginDataCatalog Catalog { get; }

    public PluginDataManager Manager => _services.GetRequiredService<PluginDataManager>();

    public PluginDataGate Gate => _services.GetRequiredService<PluginDataGate>();

    public LoadedPlugin Plugin => Loader.Plugins.Single();

    public Assembly PluginAssembly => Plugin.Manifest.Assembly;

    public Type ContextType => Type("NotesContext");

    /// <summary>A type from the sample plugin, as the plugin's load context has it.</summary>
    public Type Type(string name) => PluginAssembly.GetType($"{SampleNamespace}.{name}", throwOnError: true)!;

    /// <summary>How many times this load of the plugin has been initialized.</summary>
    public int Initializations => (int)Type("SamplePlugin").GetField("Initializations")!.GetValue(null)!;

    public IReadOnlyList<Assembly> MigrationAssemblies => PluginDataCatalog.LoadMigrationAssemblies(Plugin).Assemblies;

    /// <summary>The tables in the scratch database.</summary>
    public IReadOnlyList<string> Tables()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name";

        using var reader = command.ExecuteReader();
        var tables = new List<string>();

        while (reader.Read())
            tables.Add(reader.GetString(0));

        return tables;
    }

    public void Execute(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
