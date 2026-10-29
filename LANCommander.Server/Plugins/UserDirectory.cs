using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins;

/// <summary>Gives plugins the server's accounts without exposing the database model or password hashes.</summary>
internal sealed class UserDirectory(
    UserService userService,
    SettingsProvider<Settings.Settings> settingsProvider) : IUserDirectory
{
    public async Task<IReadOnlyList<UserAccount>> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rows = await userService.GetAsync(Project);

        return rows
            .Select(ToAccount)
            .OrderBy(account => account.UserName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var row = await userService.FirstOrDefaultAsync(user => user.Id == id, Project);

        return row == null ? null : ToAccount(row);
    }

    public async Task<UserAccount?> FindAsync(string userName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userName))
            return null;

        var row = await userService.GetAsync(userName, Project);

        return row == null ? null : ToAccount(row);
    }

    static IQueryable<UserRow> Project(IQueryable<User> users) =>
        users.Select(user => new UserRow(
            user.Id,
            user.UserName!,
            user.Alias,
            user.Email,
            user.Approved,
            user.LockoutEnabled,
            user.LockoutEnd,
            user.CreatedOn,
            user.UserRoles.Select(userRole => userRole.Role.Name!).ToList()));

    // The same rules as the server's own sign-in: locked-out accounts are refused, and unapproved
    // ones too where approval is required, unless they are administrators
    UserAccount ToAccount(UserRow row)
    {
        var isAdministrator = row.Roles.Any(role => string.Equals(role, RoleService.AdministratorRoleName, StringComparison.OrdinalIgnoreCase));
        var lockedOut = row.LockoutEnabled && row.LockoutEnd > DateTimeOffset.UtcNow;
        var approved = row.Approved || isAdministrator || !settingsProvider.CurrentValue.Server.Authentication.RequireApproval;

        return new UserAccount
        {
            Id = row.Id,
            UserName = row.UserName,
            Alias = row.Alias,
            Email = row.Email,
            Roles = row.Roles.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            IsAdministrator = isAdministrator,
            CanSignIn = approved && !lockedOut,
            CreatedOn = row.CreatedOn,
        };
    }

    sealed record UserRow(
        Guid Id,
        string UserName,
        string? Alias,
        string? Email,
        bool Approved,
        bool LockoutEnabled,
        DateTimeOffset? LockoutEnd,
        DateTime CreatedOn,
        List<string> Roles);
}
