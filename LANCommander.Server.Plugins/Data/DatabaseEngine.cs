namespace LANCommander.Server.Plugins.Data;

/// <summary>The database engines a LANCommander server can run on.</summary>
/// <remarks>
/// A plugin with a <see cref="PluginDbContext"/> ships migrations for every one of them, whichever
/// the installation it's on uses, so the same package installs everywhere.
/// </remarks>
public enum DatabaseEngine
{
    Sqlite,
    MySql,
    PostgreSql,
}
