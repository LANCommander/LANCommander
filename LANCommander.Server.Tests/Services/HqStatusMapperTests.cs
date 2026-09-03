using System.Net;
using System.Net.Sockets;
using LANCommander.HQ.SDK;
using LANCommander.Server.Services.HQ;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

public class HqStatusMapperTests
{
    private static HQApiException ApiError(HttpStatusCode statusCode) =>
        new(statusCode, string.Empty, $"HQ returned {(int)statusCode}");

    private static HQAuthenticationException SessionEnded() =>
        new("The refresh token was revoked.", string.Empty);

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void RejectedCredentialsAreUnauthorized(HttpStatusCode statusCode)
    {
        HqStatusMapper.Map(ApiError(statusCode)).ShouldBe(HqConnectionStatus.Unauthorized);
    }

    [Fact]
    public void AnEndedSessionIsUnauthorized()
    {
        // HQAuthenticationException derives from HQApiException, so a mapper that checked the base
        // type first would classify the terminal case as a transient outage and keep retrying a
        // credential the SDK has already thrown away.
        HqStatusMapper.Map(SessionEnded()).ShouldBe(HqConnectionStatus.Unauthorized);
    }

    [Fact]
    public void OnlyAnEndedSessionIsTerminal()
    {
        // A plain 401 is one unauthorised request; the SDK invalidates and retries behind us. Only
        // HQAuthenticationException means the credential is gone for good.
        HqStatusMapper.IsTerminal(SessionEnded()).ShouldBeTrue();
        HqStatusMapper.IsTerminal(ApiError(HttpStatusCode.Unauthorized)).ShouldBeFalse();
        HqStatusMapper.IsTerminal(new HttpRequestException("no such host")).ShouldBeFalse();
    }

    [Fact]
    public void AnEndedSessionIsDescribedAsNeedingReconnection()
    {
        HqStatusMapper.Describe(SessionEnded()).ShouldContain("Reconnect");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.NotFound)]
    public void OtherHttpFailuresDoNotImplicateTheCredential(HttpStatusCode statusCode)
    {
        // HQ answered, just not usefully. Treating this as Unauthorized would prompt the admin to
        // re-link over what is really an outage on HQ's side.
        HqStatusMapper.Map(ApiError(statusCode)).ShouldBe(HqConnectionStatus.Unreachable);
    }

    public static TheoryData<Exception> TransportFailures() =>
    [
        new HttpRequestException("no such host"),
        new SocketException((int)SocketError.HostUnreachable),
        new TimeoutException(),
        new TaskCanceledException(),
    ];

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public void TransportFailuresAreUnreachable(Exception exception)
    {
        HqStatusMapper.Map(exception).ShouldBe(HqConnectionStatus.Unreachable);
    }

    [Fact]
    public void UnrelatedExceptionsCarryNoConnectionSignal()
    {
        // A bug in our own code says nothing about HQ, so existing state must be left alone.
        HqStatusMapper.Map(new InvalidOperationException("bug")).ShouldBeNull();
    }

    [Fact]
    public void DescribesApiFailuresWithTheirStatusCode()
    {
        HqStatusMapper.Describe(ApiError(HttpStatusCode.Unauthorized))
            .ShouldContain("401");
    }
}
