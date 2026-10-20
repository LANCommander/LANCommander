using Bunit;
using Bunit.TestDoubles;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Base class for bUnit component tests. Renders Blazor components in-process and synchronously,
/// eliminating the SignalR circuit round-trips that make the Playwright suite flaky.
///
/// Server services (GameService, EF, etc.) are resolved from the real DI container
/// created by <see cref="BUnitServerFixture"/> via a fallback service provider. A fresh scope is
/// created per test so scoped services (and their DbContexts) behave like a single request.
/// </summary>
public abstract class BUnitTestContext : BunitContext
{
    private readonly IServiceScope _scope;

    protected BUnitServerFixture Fixture { get; }

    protected BUnitTestContext(BUnitServerFixture fixture)
    {
        Fixture = fixture;

        // A per-test scope so scoped services (GameService, DbContext) resolve correctly when the
        // fallback provider is hit during rendering.
        _scope = fixture.Factory.RealServices.CreateScope();

        // Components issue JS interop calls for DOM measurement; loose mode returns defaults so
        // rendering can proceed without a browser.
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Radzen charts ask the browser for their size before drawing; loose mode would return null
        JSInterop.Setup<Radzen.Blazor.Rendering.Rect>("Radzen.createChart", _ => true)
            .SetResult(new Radzen.Blazor.Rendering.Rect { Width = 600, Height = 300 });

        // Admin pages are gated with [Authorize(Roles = Administrator)]. Provide an authenticated
        // admin so AuthorizeView/cascading auth state behave as in a logged-in session.
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized(TestConstants.AdminUserName);
        authContext.SetRoles(RoleService.AdministratorRoleName);

        // Register the LANCommander.Server.UI services (Radzen dialog/notification/tooltip services,
        // ScriptProvider, UploadTracker) in bUnit's own container so they capture its mock
        // IJSRuntime. Resolved from the fallback (real server) container they would capture the
        // circuit-bound RemoteJSRuntime and throw "JS interop calls cannot be issued at this time".
        Services.AddLANCommanderServerUI();

        // Resolve domain services (GameService, EF, metadata, ...) not registered above from the
        // real server container.
        Services.AddFallbackServiceProvider(_scope.ServiceProvider);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _scope.Dispose();

        base.Dispose(disposing);
    }
}
