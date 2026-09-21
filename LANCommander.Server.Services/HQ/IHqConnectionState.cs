namespace LANCommander.Server.Services.HQ;

/// <summary>
/// The read-side of the HQ connection, which is what the UI and the metadata/media providers
/// actually need. Split out from the concrete service so components can be tested against a stub.
/// </summary>
public interface IHqConnectionState
{
    /// <summary>The most recent verified view of the connection.</summary>
    HqConnectionSnapshot Current { get; }

    /// <summary>Whether a credential is stored at all — i.e. Connect vs Unlink.</summary>
    bool HasCredential { get; }

    /// <summary>Whether HQ should be offered as a working provider right now.</summary>
    bool IsUsable { get; }

    /// <summary>Raised when the connection meaningfully changes. May fire on a background thread.</summary>
    event EventHandler<HqConnectionSnapshot>? ConnectionChanged;

    Task<HqConnectionSnapshot> VerifyAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
