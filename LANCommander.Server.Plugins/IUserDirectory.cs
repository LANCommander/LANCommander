namespace LANCommander.Server.Plugins;

/// <summary>
/// Reads the server's user accounts, for example to let users sign in to something the plugin
/// hosts with their LANCommander account. Provided by the server; inject it into a scoped plugin
/// service or page.
/// </summary>
/// <remarks>
/// The directory is read-only and never exposes passwords or password hashes. A plugin that
/// authenticates users can either keep a credential of its own or check the user's LANCommander
/// password with <see cref="CheckPasswordAsync"/>, which applies the server's sign-in rules itself.
/// A plugin with a credential of its own should refuse any account whose
/// <see cref="UserAccount.CanSignIn"/> is false.
/// </remarks>
public interface IUserDirectory
{
    /// <summary>Every account on the server, ordered by user name.</summary>
    Task<IReadOnlyList<UserAccount>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>The account with the given id, or null.</summary>
    Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The account with the given user name, compared case-insensitively, or null.</summary>
    Task<UserAccount?> FindAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a user's LANCommander password, for plugins that use the server as their
    /// authentication store. Only a <see cref="PasswordCheckStatus.Succeeded"/> result means the
    /// user is authenticated; the result then carries their account.
    /// </summary>
    /// <remarks>
    /// The server's sign-in rules apply: locked-out accounts are refused without their password being
    /// checked, as are unapproved ones where the server requires approval, and nothing succeeds while
    /// password sign-in is turned off. Repeated failures for a user name are rate limited, whether or
    /// not the user exists; a successful check clears them.
    /// </remarks>
    /// <param name="userName">The user name, compared case-insensitively.</param>
    /// <param name="password">The password to check.</param>
    Task<PasswordCheckResult> CheckPasswordAsync(string userName, string password, CancellationToken cancellationToken = default);
}

/// <summary>A user account on the server.</summary>
public sealed record UserAccount
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; }

    /// <summary>The name the user chose to be shown by, if any.</summary>
    public string? Alias { get; init; }

    public string? Email { get; init; }

    /// <summary>The names of the roles the user is in.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Whether the user is in the built-in administrator role.</summary>
    public bool IsAdministrator { get; init; }

    /// <summary>
    /// Whether the server would let the user sign in right now: the account is not locked out and,
    /// where the server requires it, has been approved by an administrator. Read from the account
    /// each time, so a lockout or a withdrawn approval takes effect immediately.
    /// </summary>
    public bool CanSignIn { get; init; }

    public DateTime CreatedOn { get; init; }
}

/// <summary>The outcome of <see cref="IUserDirectory.CheckPasswordAsync"/>.</summary>
public sealed record PasswordCheckResult
{
    public required PasswordCheckStatus Status { get; init; }

    /// <summary>The authenticated account. Set only when the check succeeded.</summary>
    public UserAccount? Account { get; init; }

    /// <summary>When rate limited, how long until the user name may be tried again.</summary>
    public TimeSpan? RetryAfter { get; init; }

    public bool Succeeded => Status == PasswordCheckStatus.Succeeded;
}

public enum PasswordCheckStatus
{
    /// <summary>The password is correct and the account may sign in.</summary>
    Succeeded,

    /// <summary>
    /// The user name or password is wrong. Deliberately does not say which, so the result cannot be
    /// used to find out whether a user exists.
    /// </summary>
    Failed,

    /// <summary>The account is locked out. Its password was not checked.</summary>
    LockedOut,

    /// <summary>The account is waiting for an administrator's approval. Its password was not checked.</summary>
    NotApproved,

    /// <summary>Too many recent failures for this user name. Nothing was checked; see <see cref="PasswordCheckResult.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>The server has password sign-in turned off.</summary>
    Disabled,
}
