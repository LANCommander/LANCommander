using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.HQ;

/// <summary>
/// Process-wide, in-memory state of the server's link to LANCommander HQ.
///
/// Settings.yml holds the credential; this holds whether that credential currently works. The two
/// used to be the same thing — "connected" meant "AccessToken is a non-empty string" — so an
/// expired or revoked token still advertised HQ as an available provider while every call quietly
/// failed.
///
/// State here is derived only from real evidence: a probe against HQ, or the outcome of a live
/// request. It is never persisted.
///
/// Token lifetime is deliberately not this class's business. The SDK renews the access token from
/// the rotating refresh token and persists each new set through <see cref="SettingsHqTokenStore"/>;
/// this only observes the result.
/// </summary>
public sealed class HqConnectionService(
    IHqAuthApi api,
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<HqConnectionService> logger) : IHqConnectionState
{
    private HqConnectionSnapshot _snapshot = HqConnectionSnapshot.Unknown;

    // Coalesces concurrent verifications so a burst of page loads makes one call to HQ.
    private readonly SemaphoreSlim _verifyGate = new(1, 1);

    public event EventHandler<HqConnectionSnapshot>? ConnectionChanged;

    public HqConnectionSnapshot Current => Volatile.Read(ref _snapshot);

    // Never cache CurrentValue: SettingsProvider.Update mutates in place and the save that follows
    // re-binds a fresh instance via AddYamlFile(reloadOnChange: true).
    public bool HasCredential => settingsProvider.CurrentValue.Server.HQ.HasCredential;

    /// <summary>
    /// Whether HQ should be treated as a working provider. Deliberately optimistic about
    /// <see cref="HqConnectionStatus.Unreachable"/> and <see cref="HqConnectionStatus.Unknown"/>:
    /// a brief network problem, or the few seconds before the first probe completes, should not
    /// make HQ vanish from the metadata picker, and individual calls already fail safe. Only a
    /// credential we know to be bad or absent hides it.
    /// </summary>
    public bool IsUsable => HasCredential
        && Current.Status is not (HqConnectionStatus.Disconnected or HqConnectionStatus.Unauthorized);

    /// <summary>The label this server presents to HQ, shown to the user on their account page.</summary>
    public string ClientName
    {
        get
        {
            var configured = settingsProvider.CurrentValue.Server.HQ.ClientName;

            return string.IsNullOrWhiteSpace(configured)
                ? $"LANCommander Server ({Environment.MachineName})"
                : configured;
        }
    }

    /// <summary>Establishes connection state by actually talking to HQ.</summary>
    public async Task<HqConnectionSnapshot> VerifyAsync(CancellationToken cancellationToken = default)
    {
        await _verifyGate.WaitAsync(cancellationToken);

        try
        {
            var now = DateTimeOffset.UtcNow;

            if (!settingsProvider.CurrentValue.Server.HQ.HasCredential)
                return Publish(HqConnectionSnapshot.Disconnected with { LastCheckedAt = now });

            try
            {
                var profile = await api.GetCurrentUserAsync(cancellationToken);

                if (profile is null)
                {
                    return Publish(new HqConnectionSnapshot(
                        HqConnectionStatus.Unauthorized,
                        null, false, false, null,
                        now, CurrentExpiry(),
                        "LANCommander HQ did not return an account for this token."));
                }

                return Publish(new HqConnectionSnapshot(
                    HqConnectionStatus.Connected,
                    profile.Username, profile.IsPremium, profile.IsEditor, profile.PreferredLocale,
                    now, CurrentExpiry(), null));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Our own shutdown, not an HQ outage. Leave state untouched.
                throw;
            }
            catch (Exception ex)
            {
                return Publish(Classify(ex, now));
            }
        }
        finally
        {
            _verifyGate.Release();
        }
    }

    /// <summary>
    /// Exchanges the single-use code from an interactive HQ login for a self-renewing credential,
    /// then verifies it. Called by the OAuth callback so the UI can react to a real result instead
    /// of polling for a changed token string.
    /// </summary>
    public async Task<HqConnectionSnapshot> AcceptAuthorizationCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await api.ExchangeCodeAsync(code, ClientName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to exchange the LANCommander HQ authorization code.");

            return Publish(Classify(ex, DateTimeOffset.UtcNow));
        }

        return await VerifyAsync(cancellationToken);
    }

    /// <summary>Records that a live HQ request succeeded, so state recovers without waiting for a poll.</summary>
    public void ReportSuccess()
    {
        var current = Current;

        if (current.Status == HqConnectionStatus.Connected)
            return;

        // The token was accepted but we have no profile to show. Do not invent one — go and verify.
        if (current.Username is null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await VerifyAsync();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Opportunistic LANCommander HQ re-verification failed.");
                }
            });

            return;
        }

        Publish(current with
        {
            Status = HqConnectionStatus.Connected,
            LastCheckedAt = DateTimeOffset.UtcNow,
            LastError = null,
        });
    }

    /// <summary>
    /// Records that a live HQ request failed, so a dead session is noticed the moment it happens
    /// rather than at the next poll.
    /// </summary>
    public void ReportFailure(Exception exception)
    {
        var status = HqStatusMapper.Map(exception);

        if (status is null)
            return;

        var now = DateTimeOffset.UtcNow;

        if (status == HqConnectionStatus.Unauthorized)
        {
            Publish(Classify(exception, now));
            return;
        }

        // Only downgrade from a healthy state. An unreachable blip must not overwrite the more
        // specific knowledge that the session was rejected.
        if (Current.Status is HqConnectionStatus.Connected or HqConnectionStatus.Unknown)
        {
            Publish(Current with
            {
                Status = HqConnectionStatus.Unreachable,
                LastCheckedAt = now,
                LastError = HqStatusMapper.Describe(exception),
            });
        }
    }

    /// <summary>
    /// Unlinks the server. Revokes the session at HQ first so the credential is genuinely dead
    /// rather than merely forgotten here, then clears local storage either way.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = settingsProvider.CurrentValue.Server.HQ.RefreshToken;

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                await api.RevokeSessionAsync(refreshToken, cancellationToken);
            }
            catch (Exception ex)
            {
                // An admin who clicked Unlink expects to be unlinked. If HQ is unreachable we still
                // drop the local credential; the session then lapses on its own inactivity window.
                logger.LogWarning(ex, "Could not revoke the LANCommander HQ session. Clearing the local credential anyway.");
            }
        }

        settingsProvider.Update(s =>
        {
            s.Server.HQ.AccessToken = string.Empty;
            s.Server.HQ.AccessTokenExpiresAt = null;
            s.Server.HQ.RefreshToken = string.Empty;
            s.Server.HQ.RefreshTokenExpiresAt = null;
        });

        await settingsProvider.FlushAsync(cancellationToken);

        Publish(HqConnectionSnapshot.Disconnected with { LastCheckedAt = DateTimeOffset.UtcNow });
    }

    private HqConnectionSnapshot Classify(Exception exception, DateTimeOffset now)
    {
        var status = HqStatusMapper.Map(exception) ?? HqConnectionStatus.Unreachable;
        var detail = HqStatusMapper.Describe(exception);

        if (status == HqConnectionStatus.Unreachable)
        {
            logger.LogWarning(exception, "Could not reach LANCommander HQ.");

            // Unreachable is not unauthenticated: keep the last known-good profile and, critically,
            // keep the credential.
            return Current with
            {
                Status = HqConnectionStatus.Unreachable,
                LastCheckedAt = now,
                LastError = detail,
            };
        }

        if (HqStatusMapper.IsTerminal(exception))
        {
            // The SDK has already cleared the token store, so the settings no longer hold anything
            // usable. Say so plainly rather than implying a retry might help.
            logger.LogWarning(exception,
                "The LANCommander HQ session has ended and the stored credential has been cleared. An administrator must reconnect.");
        }
        else
        {
            logger.LogWarning(exception, "LANCommander HQ rejected this server's credential.");
        }

        return new HqConnectionSnapshot(
            HqConnectionStatus.Unauthorized,
            null, false, false, null,
            now, CurrentExpiry(), detail);
    }

    private DateTimeOffset? CurrentExpiry()
    {
        var expiresAt = settingsProvider.CurrentValue.Server.HQ.AccessTokenExpiresAt;

        return expiresAt is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc));
    }

    private HqConnectionSnapshot Publish(HqConnectionSnapshot next)
    {
        var previous = Interlocked.Exchange(ref _snapshot, next);

        // Only notify on meaningful change, so a healthy server polling on an interval raises no
        // events and never wakes idle Blazor circuits.
        var changed = previous.Status != next.Status
                      || previous.Username != next.Username
                      || previous.IsPremium != next.IsPremium
                      || previous.IsEditor != next.IsEditor;

        if (changed)
            RaiseChanged(next);

        return next;
    }

    private void RaiseChanged(HqConnectionSnapshot snapshot)
    {
        var handlers = ConnectionChanged?.GetInvocationList();

        if (handlers is null)
            return;

        // One misbehaving subscriber (e.g. a circuit torn down mid-notify) must not stop the others
        // or kill the background monitor.
        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler<HqConnectionSnapshot>)handler)(this, snapshot);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "A LANCommander HQ connection subscriber threw.");
            }
        }
    }

    /// <summary>Exposed for the subscription-leak test.</summary>
    internal int SubscriberCount => ConnectionChanged?.GetInvocationList().Length ?? 0;
}
