using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

public sealed class ServerUpdateNoticeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LANCommander.ServerUpdateNoticeTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void RoundTrips()
    {
        new ServerUpdateNotice { Version = "2.4.0", PreviousVersion = "2.3.0" }.Save(_directory);

        var loaded = ServerUpdateNotice.Load(_directory);

        loaded.ShouldNotBeNull();
        loaded.Version.ShouldBe("2.4.0");
        loaded.PreviousVersion.ShouldBe("2.3.0");
        loaded.Succeeded.ShouldBeTrue();
        loaded.SeenOn.ShouldBeNull();
    }

    [Fact]
    public void TryClaim_GivesTheNoticeToTheFirstAdministratorOnly()
    {
        new ServerUpdateNotice { Version = "2.4.0", PreviousVersion = "2.3.0" }.Save(_directory);

        var first = ServerUpdateNotice.TryClaim(_directory, "admin");

        first.ShouldNotBeNull();
        ServerUpdateNotice.TryClaim(_directory, "other").ShouldBeNull();

        // Still there for the Updates page, marked with who saw it
        var stored = ServerUpdateNotice.Load(_directory);

        stored.ShouldNotBeNull();
        stored.SeenBy.ShouldBe("admin");
        stored.SeenOn.ShouldNotBeNull();
    }

    [Fact]
    public async Task TryClaim_GivesTheNoticeOut_OnceAcrossConcurrentClaims()
    {
        new ServerUpdateNotice { Version = "2.4.0" }.Save(_directory);

        var claims = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => Task.Run(() => ServerUpdateNotice.TryClaim(_directory, $"admin{i}"))));

        claims.Count(c => c != null).ShouldBe(1);
    }

    [Fact]
    public void TryClaim_ReturnsNull_WithoutANotice()
    {
        ServerUpdateNotice.TryClaim(_directory, "admin").ShouldBeNull();
    }
}
