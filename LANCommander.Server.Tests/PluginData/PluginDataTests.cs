using LANCommander.Server.Data;
using LANCommander.Server.Plugins.Data;
using LANCommander.Server.Settings.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LANCommander.Server.Tests.PluginData;

/// <summary>
/// The plugin data layer end to end: a plugin with a context is loaded, checked against every engine,
/// migrated on SQLite, and its context handed out only when ready.
/// </summary>
[Collection(PluginDataCollection.Name)]
public sealed class PluginDataTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lancommander-plugin-data-tests", Guid.NewGuid().ToString("N"));
    private readonly DatabaseProvider _provider = DatabaseContext.Provider;
    private readonly string _connectionString = DatabaseContext.ConnectionString;

    public PluginDataTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        DatabaseContext.Provider = _provider;
        DatabaseContext.ConnectionString = _connectionString;

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Plugin assemblies loaded from here stay locked until the process exits
        }
    }

    [Fact]
    public async Task AValidPluginIsMigratedAndItsContextStoresData()
    {
        await using var host = new PluginDataTestHost(_root);

        await host.Manager.PrepareAllAsync();

        host.Gate.Get(host.ContextType).Status.ShouldBe(PluginDataStatus.Ready);
        host.Loader.GetDisabledReasons(host.Plugin.Manifest.Id).ShouldBeNull();

        // Named by the host, with the plugin's own history table
        host.Tables().ShouldContain("Plugin_Sample_Notes");
        host.Tables().ShouldContain("Plugin_Sample_NoteTag");
        host.Tables().ShouldContain("__EFMigrationsHistory_Plugin_Sample");
        host.Tables().ShouldNotContain("Notes");

        var noteType = host.Type("Note");
        var id = Guid.NewGuid();
        var edited = new DateTimeOffset(2026, 10, 28, 14, 30, 0, TimeSpan.FromHours(2));

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(host.ContextType);
            var note = Activator.CreateInstance(noteType)!;

            noteType.GetProperty("Id")!.SetValue(note, id);
            noteType.GetProperty("Title")!.SetValue(note, "Hello");
            noteType.GetProperty("TitleKey")!.SetValue(note, "hello");
            noteType.GetProperty("CreatedOn")!.SetValue(note, new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc));
            noteType.GetProperty("EditedOn")!.SetValue(note, edited);
            noteType.GetProperty("Rating")!.SetValue(note, 4.25m);

            context.Add(note);
            await context.SaveChangesAsync();
        }

        // Read back through the factory, in a fresh context
        var factory = host.Services.GetRequiredService(typeof(IDbContextFactory<>).MakeGenericType(host.ContextType));
        await using var fresh = (DbContext)factory.GetType().GetMethod("CreateDbContext")!.Invoke(factory, null)!;

        var stored = (await fresh.FindAsync(noteType, id))!;

        noteType.GetProperty("Title")!.GetValue(stored).ShouldBe("Hello");
        noteType.GetProperty("Rating")!.GetValue(stored).ShouldBe(4.25m);
        ((DateTimeOffset?)noteType.GetProperty("EditedOn")!.GetValue(stored)).ShouldBe(edited);
        ((DateTime)noteType.GetProperty("CreatedOn")!.GetValue(stored)!).Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ARestartAppliesNothingTwiceAndKeepsTheData()
    {
        await using (var first = new PluginDataTestHost(_root))
            await first.Manager.PrepareAllAsync();

        await using var second = new PluginDataTestHost(_root);

        await second.Manager.PrepareAllAsync();

        second.Gate.Get(second.ContextType).Status.ShouldBe(PluginDataStatus.Ready);
    }

    [Fact]
    public async Task APluginMissingAnEnginesMigrationsIsTurnedOffBeforeItStarts()
    {
        await using var host = new PluginDataTestHost(_root, deploy: file => !file.Contains("Migrations.PostgreSql"));

        await host.Manager.PrepareAllAsync();
        await host.Loader.InitializeAllAsync(host.Services);

        var state = host.Gate.Get(host.ContextType);

        state.Status.ShouldBe(PluginDataStatus.Invalid);
        state.Errors.ShouldContain(error => error.Contains("Migrations.PostgreSql.dll") && error.Contains("isn't in the plugin's folder"));
        state.Errors.ShouldContain(error => error.Contains("no PostgreSql migrations"));

        host.Loader.GetDisabledReasons(host.Plugin.Manifest.Id).ShouldNotBeNull();
        host.Initializations.ShouldBe(0);
        host.Tables().ShouldNotContain("Plugin_Sample_Notes");

        await using var scope = host.Services.CreateAsyncScope();

        var refused = Should.Throw<PluginDataUnavailableException>(() => scope.ServiceProvider.GetRequiredService(host.ContextType));
        refused.State.Status.ShouldBe(PluginDataStatus.Invalid);
    }

    [Fact]
    public async Task ADatabaseAheadOfThePluginIsADowngradeAndIsRefused()
    {
        await using (var first = new PluginDataTestHost(_root))
            await first.Manager.PrepareAllAsync();

        await using var host = new PluginDataTestHost(_root);

        host.Execute("INSERT INTO \"__EFMigrationsHistory_Plugin_Sample\" (\"MigrationId\", \"ProductVersion\") VALUES ('29990101000000_FromANewerVersion', '9.0.19')");

        await host.Manager.PrepareAllAsync();

        var state = host.Gate.Get(host.ContextType);

        state.Status.ShouldBe(PluginDataStatus.Invalid);
        state.Errors.ShouldContain(error => error.Contains("29990101000000_FromANewerVersion") && error.Contains("downgrades aren't supported"));
    }

    [Fact]
    public async Task BeforeSetupTheDataIsValidatedOnlyAndSetUpOnceThereIsADatabase()
    {
        await using var host = new PluginDataTestHost(_root, provider: DatabaseProvider.Unknown);

        await host.Manager.PrepareAllAsync();
        await host.Loader.InitializeAllAsync(host.Services);

        host.Gate.Get(host.ContextType).Status.ShouldBe(PluginDataStatus.Validated);
        host.Initializations.ShouldBe(0);

        // What first-time setup does once it has a database
        DatabaseContext.Provider = DatabaseProvider.SQLite;

        await host.Manager.PrepareAllAsync(initialize: true);

        host.Gate.Get(host.ContextType).Status.ShouldBe(PluginDataStatus.Ready);
        host.Initializations.ShouldBe(1);
    }

    [Fact]
    public async Task AContextKeptPastItsPluginBeingTurnedOffCantConnect()
    {
        await using var host = new PluginDataTestHost(_root);

        await host.Manager.PrepareAllAsync();
        await using var scope = host.Services.CreateAsyncScope();

        var context = (DbContext)scope.ServiceProvider.GetRequiredService(host.ContextType);

        host.Gate.Set(host.ContextType, new PluginDataState(PluginDataStatus.MigrationFailed, ["Turned off for the test."]));

        var refused = await Should.ThrowAsync<PluginDataUnavailableException>(() => context.FindAsync(host.Type("Note"), Guid.NewGuid()).AsTask());
        refused.Message.ShouldContain("Turned off for the test.");
    }

    [Fact]
    public async Task ARetryAfterAFixStartsThePlugin()
    {
        await using var host = new PluginDataTestHost(_root, provider: DatabaseProvider.Unknown);

        await host.Manager.PrepareAllAsync();
        await host.Loader.InitializeAllAsync(host.Services);

        DatabaseContext.Provider = DatabaseProvider.SQLite;

        (await host.Manager.RetryAsync(host.Plugin.Manifest.Id)).ShouldBeTrue();

        host.Loader.GetDisabledReasons(host.Plugin.Manifest.Id).ShouldBeNull();
        host.Initializations.ShouldBe(1);
        host.Manager.GetState(host.Plugin.Manifest.Id)!.Status.ShouldBe(PluginDataStatus.Ready);
    }

    [Fact]
    public async Task APluginCantConstructItsContextItself()
    {
        await using var host = new PluginDataTestHost(_root);

        var options = new DbContextOptionsBuilder().UseSqlite(host.ConnectionString).Options;

        var refused = Should.Throw<System.Reflection.TargetInvocationException>(() => Activator.CreateInstance(host.ContextType, options));
        refused.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldContain("can only be created by the LANCommander host");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(DatabaseEngine.Sqlite)]
    [InlineData(DatabaseEngine.MySql)]
    [InlineData(DatabaseEngine.PostgreSql)]
    public async Task APluginWithMigrationsForEveryEnginePassesWhicheverTheServerUses(DatabaseEngine? active)
    {
        await using var host = new PluginDataTestHost(_root, provider: DatabaseProvider.Unknown);

        var result = PluginMigrationValidator.Validate(host.ContextType, "Sample", host.MigrationAssemblies, active);

        result.Errors.ShouldBeEmpty();
        result.Assemblies.Keys.ShouldBe([DatabaseEngine.Sqlite, DatabaseEngine.MySql, DatabaseEngine.PostgreSql], ignoreOrder: true);
    }

    [Fact]
    public async Task OnlyTheServersOwnEngineIsCheckedAgainstTheModel()
    {
        await using var host = new PluginDataTestHost(_root, provider: DatabaseProvider.Unknown);

        // Another DataId renames every table, so no snapshot matches the model; only the engine the
        // server uses is held to that
        var result = PluginMigrationValidator.Validate(host.ContextType, "Renamed", host.MigrationAssemblies, DatabaseEngine.Sqlite);

        result.Errors.ShouldBe(["Sqlite: the model has changes with no migration. Add the migration for every engine."]);

        // And before setup there is no such engine, so the migrations only have to exist
        PluginMigrationValidator.Validate(host.ContextType, "Renamed", host.MigrationAssemblies, null).Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task EachEnginesMigrationsAreReadWithoutItsProvider()
    {
        await using var host = new PluginDataTestHost(_root, provider: DatabaseProvider.Unknown);

        foreach (var assembly in host.MigrationAssemblies)
            PluginMigrationValidator.ReadMigrationNames(assembly, host.ContextType).ShouldBe(["Initial"]);

        PluginMigrationValidator.ReadMigrationNames(host.MigrationAssemblies[0], host.Type("UnmigratedContext")).ShouldBeEmpty();
    }

    [Fact]
    public async Task AContextWithoutMigrationsIsCaught()
    {
        await using var host = new PluginDataTestHost(_root, provider: DatabaseProvider.Unknown);

        var result = PluginMigrationValidator.Validate(host.Type("UnmigratedContext"), "Sample", host.MigrationAssemblies, DatabaseEngine.Sqlite);

        result.Errors.ShouldBe(
        [
            "UnmigratedContext: no Sqlite migrations.",
            "UnmigratedContext: no MySql migrations.",
            "UnmigratedContext: no PostgreSql migrations.",
        ]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sample")]
    [InlineData("lancommander.ldap")]
    [InlineData("TwentyFiveCharactersLong1")]
    [InlineData("Has_Underscore")]
    [InlineData("9Lives")]
    public async Task ADataIdBreakingTheRulesIsCaught(string? dataId)
    {
        await using var host = new PluginDataTestHost(_root, provider: DatabaseProvider.Unknown);

        PluginMigrationValidator.Validate(host.ContextType, dataId, host.MigrationAssemblies, DatabaseEngine.Sqlite).Errors.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ANonPortableModelIsCaughtRuleByRule()
    {
        await using var host = new PluginDataTestHost(_root, provider: DatabaseProvider.Unknown);

        // Every engine, since a server could use any of them: PostgreSQL and MySQL truncate long
        // generated names themselves, so only SQLite's model shows the column name that's too long
        var errors = new List<string>();

        foreach (var engine in PluginMigrationValidator.RequiredEngines)
        {
            var options = PluginContextOptionsFactory.CreateOffline(engine, "Sample", host.PluginAssembly);

            await using var context = (DbContext)Activator.CreateInstance(host.Type("NonPortableContext"), options)!;

            errors.AddRange(PortabilityRules.Check(context.Model, engine, "Sample"));
        }

        var reported = string.Join(Environment.NewLine, errors);

        errors.ShouldContain(e => e.Contains("sequence Counter"), reported);
        errors.ShouldContain(e => e.Contains("Unportable is in schema custom"), reported);
        errors.ShouldContain(e => e.Contains("Unportable.Code uses HasColumnType"), reported);
        errors.ShouldContain(e => e.Contains("Unportable.Created uses HasDefaultValueSql"), reported);
        errors.ShouldContain(e => e.Contains("Unportable.Amount is a Decimal with no converter"), reported);
        errors.ShouldContain(e => e.Contains("Unportable.Name is in a key or index"), reported);
        errors.ShouldContain(e => e.Contains("AVeryLongPropertyName") && e.Contains("the most is 63"), reported);
    }

    [Theory]
    [InlineData("Entries", "Plugin_Timesheet_Entries")]
    [InlineData("PK_Entries", "PK_Plugin_Timesheet_Entries")]
    [InlineData("AK_Entries_Code", "AK_Plugin_Timesheet_Entries_Code")]
    [InlineData("IX_Entries_Name", "IX_Plugin_Timesheet_Entries_Name")]
    [InlineData("FK_Entries_Projects_ProjectId", "FK_Plugin_Timesheet_Entries_Projects_ProjectId")]
    [InlineData("MyUniqueName", "Plugin_Timesheet_MyUniqueName")]
    [InlineData("Plugin_Timesheet_Entries", "Plugin_Timesheet_Entries")]
    [InlineData("PK_Plugin_Timesheet_Entries", "PK_Plugin_Timesheet_Entries")]
    public void NamesArePrefixedOnceKeepingTheirMarkerFirst(string name, string expected)
        => PluginDataIds.Prefixed("Timesheet", name).ShouldBe(expected);

    [Fact]
    public void MigrationSequencesAreComparedByNameInOrder()
    {
        PluginMigrationValidator.MigrationName("20261029003542_Initial").ShouldBe("Initial");

        PluginMigrationValidator.DescribeDifference(DatabaseEngine.Sqlite, ["Initial", "AddTags"], ["Initial"])
            .ShouldBe("Migration 2 is missing where Sqlite has AddTags.");

        PluginMigrationValidator.DescribeDifference(DatabaseEngine.Sqlite, ["Initial", "AddTags"], ["Initial", "AddLabels", "AddTags"])
            .ShouldBe("Migration 2 is AddLabels where Sqlite has AddTags.");
    }
}
