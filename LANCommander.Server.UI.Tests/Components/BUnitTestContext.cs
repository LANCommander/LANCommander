using Bunit;
using Bunit.TestDoubles;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Base class for bUnit component tests. Renders Blazor components in-process and synchronously,
/// eliminating the SignalR circuit round-trips that make the Playwright suite flaky.
///
/// Server services (GameService, AntDesign, EF, etc.) are resolved from the real DI container
/// created by <see cref="BUnitServerFixture"/> via a fallback service provider. A fresh scope is
/// created per test so scoped services (and their DbContexts) behave like a single request.
/// </summary>
public abstract class BUnitTestContext : Bunit.TestContext
{
    private readonly IServiceScope _scope;

    protected BUnitServerFixture Fixture { get; }

    protected BUnitTestContext(BUnitServerFixture fixture)
    {
        Fixture = fixture;

        // A per-test scope so scoped services (GameService, DbContext) resolve correctly when the
        // fallback provider is hit during rendering.
        _scope = fixture.Factory.RealServices.CreateScope();

        // AntDesign components issue many JS interop calls for DOM measurement; loose mode returns
        // defaults so rendering can proceed without a browser.
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Select.SetDropdownStyleAsync (OnAfterRenderAsync) dereferences the bounding-rect result;
        // loose mode would hand back a null DomRect and throw. Return a real (zero-sized) rect so
        // AntDesign Select/DatePicker components render without a browser.
        JSInterop
            .Setup<AntDesign.JsInterop.DomRect>(
                "AntDesign.interop.domInfoHelper.getBoundingClientRect",
                _ => true)
            .SetResult(new AntDesign.JsInterop.DomRect());

        // TextArea (AutoSize off) dereferences the text-area metrics on first render.
        JSInterop
            .Setup<AntDesign.Internal.TextAreaInfo>(
                "AntDesign.interop.inputHelper.getTextAreaInfo",
                _ => true)
            .SetResult(new AntDesign.Internal.TextAreaInfo());

        // Row (used internally by FormItem) dereferences the window dimensions on first render.
        JSInterop
            .Setup<AntDesign.JsInterop.Window>(
                "AntDesign.interop.domInfoHelper.getWindow",
                _ => true)
            .SetResult(new AntDesign.JsInterop.Window());

        // Admin pages are gated with [Authorize(Roles = Administrator)]. Provide an authenticated
        // admin so AuthorizeView/cascading auth state behave as in a logged-in session.
        var authContext = this.AddTestAuthorization();
        authContext.SetAuthorized(TestConstants.AdminUserName);
        authContext.SetRoles(RoleService.AdministratorRoleName);

        // Register AntDesign in bUnit's own container so its services (ModalService, MessageService,
        // ClientDimensionService, ...) resolve here and use bUnit's mock IJSRuntime. If they were
        // resolved from the fallback (real server) container they would capture the circuit-bound
        // RemoteJSRuntime and throw "JS interop calls cannot be issued at this time".
        Services.AddAntDesign();

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
