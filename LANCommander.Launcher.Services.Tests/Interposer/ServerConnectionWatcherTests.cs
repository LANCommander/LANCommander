using System.Net;
using LANCommander.Launcher.Services.Interposer;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Interposer;

public class ServerConnectionWatcherTests
{
    private static readonly Guid Cs = Guid.NewGuid();
    private static readonly Guid AnyPort = Guid.NewGuid();

    private static readonly ServerConnectionWatcher.KnownServer[] Servers =
    [
        new(Cs, "cs.lan", [IPAddress.Parse("10.0.0.5")], 27015),
        new(AnyPort, "10.0.0.9", [IPAddress.Parse("10.0.0.9")], 0),
    ];

    [Theory]
    [InlineData("10.0.0.5", 27015, true)]
    [InlineData("::ffff:10.0.0.5", 27015, true)]
    [InlineData("10.0.0.5", 0, true)]
    [InlineData("10.0.0.5", 27016, false)]
    [InlineData("CS.LAN", 27015, true)]
    [InlineData("10.0.0.6", 27015, false)]
    public void MatchesTheServersAddressAndPort(string address, int port, bool matches) =>
        (ServerConnectionWatcher.Match(Servers, address, port) == Cs).ShouldBe(matches);

    [Fact]
    public void AServerWithoutAPortMatchesAnyPort() =>
        ServerConnectionWatcher.Match(Servers, "10.0.0.9", 5000).ShouldBe(AnyPort);
}
