using System.Net;
using LANCommander.HQ.SDK;
using LANCommander.Server.Services.HQ;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests.Services;

public class HqConnectionServiceTests : IDisposable
{
    // Its own settings file: the default path is process-wide and writes are synchronous now.
    private readonly string _settingsPath = Path.Combine(
        Path.GetTempPath(), $"lc-hq-conn-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
            File.Delete(_settingsPath);
    }

    // ── Fakes ────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeHqAuthApi : IHqAuthApi
    {
        public int CurrentUserCalls;
        public int ExchangeCalls;
        public int RevokeCalls;
        public int MaxConcurrentCalls;

        public string? ExchangedCode;
        public string? ExchangedClientName;
        public string? RevokedRefreshToken;

        private int _inFlight;

        public HqUserProfile? Profile { get; set; } = new("tester", true, false, "en-US");
        public Exception? ThrowOnCurrentUser { get; set; }
        public Exception? ThrowOnExchange { get; set; }
        public Exception? ThrowOnRevoke { get; set; }
        public TimeSpan CallDuration { get; set; } = TimeSpan.Zero;

        /// <summary>Stands in for the token store the real implementation writes through.</summary>
        public Action? OnExchanged { get; set; }

        public async Task<HqUserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var concurrent = Interlocked.Increment(ref _inFlight);

            InterlockedMax(ref MaxConcurrentCalls, concurrent);

            try
            {
                Interlocked.Increment(ref CurrentUserCalls);

                if (CallDuration > TimeSpan.Zero)
                    await Task.Delay(CallDuration, cancellationToken);

                if (ThrowOnCurrentUser is not null)
                    throw ThrowOnCurrentUser;

                return Profile;
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public Task ExchangeCodeAsync(string code, string clientName, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ExchangeCalls);

            ExchangedCode = code;
            ExchangedClientName = clientName;

            if (ThrowOnExchange is not null)
                throw ThrowOnExchange;

            OnExchanged?.Invoke();

            return Task.CompletedTask;
        }

        public Task RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref RevokeCalls);

            RevokedRefreshToken = refreshToken;

            if (ThrowOnRevoke is not null)
                throw ThrowOnRevoke;

            return Task.CompletedTask;
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current;

            while (value > (current = Volatile.Read(ref target)))
            {
                if (Interlocked.CompareExchange(ref target, value, current) == current)
                    return;
            }
        }
    }

    private sealed class StubOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static HQApiException ApiError(HttpStatusCode statusCode) =>
        new(statusCode, string.Empty, $"HQ returned {(int)statusCode}");

    private static HQAuthenticationException SessionEnded() =>
        new("The refresh token was revoked.", string.Empty);

    private sealed record Harness(
        HqConnectionService Service,
        ServerSettings Settings,
        FakeHqAuthApi Api);

    private Harness Create(bool connected = true)
    {
        var settings = new ServerSettings();

        if (connected)
        {
            settings.Server.HQ.AccessToken = "access-token";
            settings.Server.HQ.AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
            settings.Server.HQ.RefreshToken = "refresh-token";
            settings.Server.HQ.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(60);
        }

        var api = new FakeHqAuthApi();

        var service = new HqConnectionService(
            api,
            new SettingsProvider<ServerSettings>(
                new StubOptionsMonitor<ServerSettings>(settings), _settingsPath),
            NullLogger<HqConnectionService>.Instance);

        return new Harness(service, settings, api);
    }

    // ── No credential ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WithoutACredentialTheServerIsDisconnectedAndNeverCallsHq()
    {
        var harness = Create(connected: false);

        var snapshot = await harness.Service.VerifyAsync();

        snapshot.Status.ShouldBe(HqConnectionStatus.Disconnected);
        harness.Api.CurrentUserCalls.ShouldBe(0);
        harness.Service.IsUsable.ShouldBeFalse();
    }

    [Fact]
    public async Task ARefreshTokenAloneCountsAsACredential()
    {
        // The state after a restart: the access token lapsed while the server was off, and the SDK
        // will mint a new one from the refresh token on the first call. Short-circuiting to
        // Disconnected here would refuse to even try.
        var harness = Create(connected: false);

        harness.Settings.Server.HQ.RefreshToken = "refresh-token";

        var snapshot = await harness.Service.VerifyAsync();

        snapshot.Status.ShouldBe(HqConnectionStatus.Connected);
        harness.Api.CurrentUserCalls.ShouldBe(1);
    }

    // ── Happy path ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AVerifiedCredentialPopulatesTheProfile()
    {
        var harness = Create();

        var snapshot = await harness.Service.VerifyAsync();

        snapshot.Status.ShouldBe(HqConnectionStatus.Connected);
        snapshot.Username.ShouldBe("tester");
        snapshot.IsPremium.ShouldBeTrue();
        snapshot.IsEditor.ShouldBeFalse();
        snapshot.PreferredLocale.ShouldBe("en-US");
        snapshot.LastError.ShouldBeNull();
        harness.Service.IsUsable.ShouldBeTrue();
    }

    [Fact]
    public async Task ConnectingRaisesTheChangeEventExactlyOnce()
    {
        var harness = Create();
        var raised = 0;

        harness.Service.ConnectionChanged += (_, _) => Interlocked.Increment(ref raised);

        await harness.Service.VerifyAsync();

        raised.ShouldBe(1);
    }

    [Fact]
    public async Task ReverifyingAnUnchangedConnectionRaisesNoEvent()
    {
        // A healthy server polls on an interval; waking every open Blazor circuit each time would
        // be pure noise.
        var harness = Create();

        await harness.Service.VerifyAsync();

        var raised = 0;
        harness.Service.ConnectionChanged += (_, _) => Interlocked.Increment(ref raised);

        await harness.Service.VerifyAsync();

        raised.ShouldBe(0);
    }

    // ── Rejected credentials ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ARejectedRequestIsUnauthorizedButLeavesTheCredentialAlone()
    {
        // A plain 401 is one unauthorised request, not a dead session. The SDK invalidates and
        // retries behind us; we must not pre-emptively discard a credential that may still work.
        var harness = Create();

        harness.Api.ThrowOnCurrentUser = ApiError(HttpStatusCode.Unauthorized);

        var snapshot = await harness.Service.VerifyAsync();

        snapshot.Status.ShouldBe(HqConnectionStatus.Unauthorized);
        harness.Settings.Server.HQ.RefreshToken.ShouldBe("refresh-token");
        harness.Service.IsUsable.ShouldBeFalse();
    }

    [Fact]
    public async Task AnEndedSessionIsUnauthorizedAndTerminal()
    {
        // HQAuthenticationException means the refresh token was revoked, idled out, or flagged as
        // reused. The SDK has already cleared the token store, so no retry can help.
        var harness = Create();

        harness.Api.ThrowOnCurrentUser = SessionEnded();

        var snapshot = await harness.Service.VerifyAsync();

        snapshot.Status.ShouldBe(HqConnectionStatus.Unauthorized);
        snapshot.LastError.ShouldNotBeNull();
        harness.Service.IsUsable.ShouldBeFalse();
    }

    // ── Unreachable is not unauthenticated ───────────────────────────────────────────────────

    [Fact]
    public async Task ANetworkFailureKeepsTheLastKnownProfileAndStaysUsable()
    {
        // The headline invariant. A WAN blip must not make HQ vanish from the metadata picker or
        // prompt the admin to re-link.
        var harness = Create();

        await harness.Service.VerifyAsync();

        harness.Api.ThrowOnCurrentUser = new HttpRequestException("no such host");

        var snapshot = await harness.Service.VerifyAsync();

        snapshot.Status.ShouldBe(HqConnectionStatus.Unreachable);
        snapshot.Username.ShouldBe("tester");
        snapshot.IsPremium.ShouldBeTrue();
        harness.Settings.Server.HQ.RefreshToken.ShouldBe("refresh-token");
        harness.Service.IsUsable.ShouldBeTrue();
    }

    [Fact]
    public async Task AServerErrorFromHqIsUnreachableNotUnauthorized()
    {
        var harness = Create();

        harness.Api.ThrowOnCurrentUser = ApiError(HttpStatusCode.InternalServerError);

        var snapshot = await harness.Service.VerifyAsync();

        snapshot.Status.ShouldBe(HqConnectionStatus.Unreachable);
    }

    // ── Live request outcomes ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ADeadSessionOnALiveRequestFlipsStateImmediately()
    {
        var harness = Create();

        await harness.Service.VerifyAsync();
        harness.Service.Current.Status.ShouldBe(HqConnectionStatus.Connected);

        harness.Service.ReportFailure(SessionEnded());

        // No waiting for the next poll cycle.
        harness.Service.Current.Status.ShouldBe(HqConnectionStatus.Unauthorized);
        harness.Service.IsUsable.ShouldBeFalse();
    }

    [Fact]
    public async Task ATransportFailureDoesNotOverwriteAKnownRejection()
    {
        var harness = Create();

        harness.Api.ThrowOnCurrentUser = SessionEnded();
        await harness.Service.VerifyAsync();

        harness.Service.ReportFailure(new HttpRequestException("no such host"));

        // Unauthorized is the more specific, more actionable fact; a blip must not blur it.
        harness.Service.Current.Status.ShouldBe(HqConnectionStatus.Unauthorized);
    }

    [Fact]
    public async Task AnUnrelatedExceptionLeavesStateAlone()
    {
        var harness = Create();

        await harness.Service.VerifyAsync();

        harness.Service.ReportFailure(new InvalidOperationException("a bug in our own code"));

        harness.Service.Current.Status.ShouldBe(HqConnectionStatus.Connected);
    }

    // ── Bootstrapping from an authorization code ─────────────────────────────────────────────

    [Fact]
    public async Task AcceptingAnAuthorizationCodeExchangesItAndVerifies()
    {
        var harness = Create(connected: false);

        // The real exchange writes the token pair through the store; emulate that side effect.
        harness.Api.OnExchanged = () =>
        {
            harness.Settings.Server.HQ.AccessToken = "fresh-access";
            harness.Settings.Server.HQ.RefreshToken = "fresh-refresh";
        };

        var snapshot = await harness.Service.AcceptAuthorizationCodeAsync("one-time-code");

        harness.Api.ExchangeCalls.ShouldBe(1);
        harness.Api.ExchangedCode.ShouldBe("one-time-code");
        snapshot.Status.ShouldBe(HqConnectionStatus.Connected);
        snapshot.Username.ShouldBe("tester");
    }

    [Fact]
    public async Task TheExchangeIsLabelledSoUsersCanIdentifyThisServer()
    {
        // ClientName is what a user sees on their HQ account page when revoking a connection.
        var harness = Create(connected: false);

        await harness.Service.AcceptAuthorizationCodeAsync("one-time-code");

        harness.Api.ExchangedClientName.ShouldNotBeNullOrWhiteSpace();
        harness.Api.ExchangedClientName.ShouldContain(Environment.MachineName);
    }

    [Fact]
    public async Task AConfiguredClientNameOverridesTheDefault()
    {
        var harness = Create(connected: false);

        harness.Settings.Server.HQ.ClientName = "lancommander-server-01";

        await harness.Service.AcceptAuthorizationCodeAsync("one-time-code");

        harness.Api.ExchangedClientName.ShouldBe("lancommander-server-01");
    }

    [Fact]
    public async Task AFailedExchangeIsReportedRatherThanSilentlySwallowed()
    {
        // Codes are single-use and expire in about a minute, so a failure here is worth surfacing.
        var harness = Create(connected: false);

        harness.Api.ThrowOnExchange = ApiError(HttpStatusCode.BadRequest);

        var snapshot = await harness.Service.AcceptAuthorizationCodeAsync("stale-code");

        snapshot.Status.ShouldBe(HqConnectionStatus.Unreachable);
        harness.Api.CurrentUserCalls.ShouldBe(0);
    }

    // ── Concurrency ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentVerificationsCoalesceIntoSerialCalls()
    {
        var harness = Create();

        harness.Api.CallDuration = TimeSpan.FromMilliseconds(20);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => harness.Service.VerifyAsync()));

        harness.Api.MaxConcurrentCalls.ShouldBe(1);
        harness.Service.Current.Status.ShouldBe(HqConnectionStatus.Connected);
    }

    // ── Subscribers ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AThrowingSubscriberDoesNotStopTheOthersOrTheVerification()
    {
        // A Blazor circuit torn down mid-notify must not take down the background monitor.
        var harness = Create();
        var secondSubscriberRan = false;

        harness.Service.ConnectionChanged += (_, _) => throw new InvalidOperationException("boom");
        harness.Service.ConnectionChanged += (_, _) => secondSubscriberRan = true;

        var snapshot = await harness.Service.VerifyAsync();

        secondSubscriberRan.ShouldBeTrue();
        snapshot.Status.ShouldBe(HqConnectionStatus.Connected);
    }

    [Fact]
    public void UnsubscribingLeavesNoHandlersBehind()
    {
        // HqConnectionService is an application singleton, so a leaked handler pins a whole circuit
        // for the lifetime of the process.
        var harness = Create();

        void Handler(object? sender, HqConnectionSnapshot snapshot) { }

        harness.Service.ConnectionChanged += Handler;
        harness.Service.SubscriberCount.ShouldBe(1);

        harness.Service.ConnectionChanged -= Handler;
        harness.Service.SubscriberCount.ShouldBe(0);
    }

    // ── Disconnect ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisconnectingRevokesTheSessionAtHqAndClearsBothTokens()
    {
        // Previously unlinking only forgot the credential locally, leaving it valid on HQ.
        var harness = Create();

        await harness.Service.VerifyAsync();
        await harness.Service.DisconnectAsync();

        harness.Api.RevokeCalls.ShouldBe(1);
        harness.Api.RevokedRefreshToken.ShouldBe("refresh-token");
        harness.Settings.Server.HQ.AccessToken.ShouldBeEmpty();
        harness.Settings.Server.HQ.RefreshToken.ShouldBeEmpty();
        harness.Settings.Server.HQ.RefreshTokenExpiresAt.ShouldBeNull();
        harness.Service.Current.Status.ShouldBe(HqConnectionStatus.Disconnected);
        harness.Service.IsUsable.ShouldBeFalse();
    }

    [Fact]
    public async Task DisconnectingStillClearsLocallyWhenHqCannotBeReached()
    {
        // An admin who clicked Unlink expects to be unlinked; the HQ session then lapses on its own
        // inactivity window.
        var harness = Create();

        harness.Api.ThrowOnRevoke = new HttpRequestException("no such host");

        await harness.Service.DisconnectAsync();

        harness.Settings.Server.HQ.RefreshToken.ShouldBeEmpty();
        harness.Service.Current.Status.ShouldBe(HqConnectionStatus.Disconnected);
    }

    [Fact]
    public async Task DisconnectingWithoutARefreshTokenSkipsTheRevokeCall()
    {
        var harness = Create(connected: false);

        await harness.Service.DisconnectAsync();

        harness.Api.RevokeCalls.ShouldBe(0);
        harness.Service.Current.Status.ShouldBe(HqConnectionStatus.Disconnected);
    }
}
