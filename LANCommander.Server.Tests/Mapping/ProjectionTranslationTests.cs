using LANCommander.Server.Data;
using LANCommander.Server.Services.Mappers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Entities = global::LANCommander.Server.Data.Models;

namespace LANCommander.Server.Tests.Mapping;

/// <summary>
/// Proves every Mapperly queryable projection (<c>ProjectToX</c>) translates to SQL. These run against
/// a real SQLite database (unlike the InMemory provider, which evaluates LINQ client-side and would
/// silently accept an untranslatable projection). Materializing each projection over an empty table is
/// enough: EF still builds and executes the SQL, so a non-translatable expression throws here.
/// </summary>
public class ProjectionTranslationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatabaseContext _context;
    private readonly SdkMapper _sdk = new();
    private readonly ManifestMapper _manifest = new();

    public ProjectionTranslationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new DatabaseContext(options);
        _context.Database.EnsureCreated();
    }

    // ---- SDK projections -----------------------------------------------------------------------

    [Fact]
    public void ProjectToSdkArchive_Translates()
        => _sdk.ProjectToSdkArchive(_context.Set<Entities.Archive>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToSdkGameSave_Translates()
        => _sdk.ProjectToSdkGameSave(_context.Set<Entities.GameSave>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToSdkUser_Translates()
        => _sdk.ProjectToSdkUser(_context.Set<Entities.User>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToSdkScript_Translates()
        => _sdk.ProjectToSdkScript(_context.Set<Entities.Script>()).ToList().ShouldNotBeNull();

    // ---- Manifest projections ------------------------------------------------------------------

    [Fact]
    public void ProjectToManifestTool_Translates()
        => _manifest.ProjectToManifestTool(_context.Set<Entities.Tool>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestTag_Translates()
        => _manifest.ProjectToManifestTag(_context.Set<Entities.Tag>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestServerHttpPath_Translates()
        => _manifest.ProjectToManifestServerHttpPath(_context.Set<Entities.ServerHttpPath>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestServerConsole_Translates()
        => _manifest.ProjectToManifestServerConsole(_context.Set<Entities.ServerConsole>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestSavePath_Translates()
        => _manifest.ProjectToManifestSavePath(_context.Set<Entities.SavePath>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestCompany_Translates()
        => _manifest.ProjectToManifestCompany(_context.Set<Entities.Company>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestPlaySession_Translates()
        => _manifest.ProjectToManifestPlaySession(_context.Set<Entities.PlaySession>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestPlatform_Translates()
        => _manifest.ProjectToManifestPlatform(_context.Set<Entities.Platform>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestMultiplayerMode_Translates()
        => _manifest.ProjectToManifestMultiplayerMode(_context.Set<Entities.MultiplayerMode>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestMedia_Translates()
        => _manifest.ProjectToManifestMedia(_context.Set<Entities.Media>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestKey_Translates()
        => _manifest.ProjectToManifestKey(_context.Set<Entities.Key>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestGenre_Translates()
        => _manifest.ProjectToManifestGenre(_context.Set<Entities.Genre>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestEngine_Translates()
        => _manifest.ProjectToManifestEngine(_context.Set<Entities.Engine>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestGameCustomField_Translates()
        => _manifest.ProjectToManifestGameCustomField(_context.Set<Entities.GameCustomField>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestCollection_Translates()
        => _manifest.ProjectToManifestCollection(_context.Set<Entities.Collection>()).ToList().ShouldNotBeNull();

    [Fact]
    public void ProjectToManifestAction_Translates()
        => _manifest.ProjectToManifestAction(_context.Set<Entities.Action>()).ToList().ShouldNotBeNull();

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
