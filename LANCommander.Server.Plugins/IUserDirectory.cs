namespace LANCommander.Server.Plugins;

/// <summary>
/// Reads the server's user accounts, for example to let users sign in to something the plugin
/// hosts with their LANCommander account. Provided by the server; inject it into a scoped plugin
/// service or page.
/// </summary>
/// <remarks>
/// The directory is read-only and never exposes passwords or password hashes. A plugin that
/// authenticates users must do so with a credential of its own, and should refuse any account whose
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
