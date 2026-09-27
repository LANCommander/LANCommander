using Bunit;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Controls;

/// <summary>
/// The UI services open overlays through the <see cref="ComponentHost"/>, so each test renders one
/// and asserts on what appears in it.
/// </summary>
public class ServiceTests : ControlsTestContext
{
    private readonly IRenderedComponent<ComponentHost> _host;

    public ServiceTests()
    {
        _host = Render<ComponentHost>();

        EchoDialog.OnOk = null;
        EchoDialog.Confirmable = true;
    }

    private T Service<T>() where T : notnull => Services.GetRequiredService<T>();

    private void ClickDialogButton(string text) =>
        _host.FindAll(".lc-dialog-footer button").Single(b => b.TextContent.Trim() == text).Click();

    [Fact]
    public void Notification_Success_ShowsMessageAndDetail()
    {
        Service<NotificationService>().Success("Game saved", "Arena Blitz was updated");

        _host.WaitForAssertion(() =>
        {
            Assert.Contains("Game saved", _host.Markup);
            Assert.Contains("Arena Blitz was updated", _host.Markup);
            Assert.NotEmpty(_host.FindAll(".lc-notification-summary svg.lc-icon"));
        });
    }

    [Fact]
    public void Notification_ErrorFromException_UsesExceptionMessageAsDetail()
    {
        Service<NotificationService>().Error(new InvalidOperationException("Disk full"), "Could not save game");

        _host.WaitForAssertion(() =>
        {
            Assert.Contains("Could not save game", _host.Markup);
            Assert.Contains("Disk full", _host.Markup);
        });
    }

    [Fact]
    public void Notification_Persistent_UpdatesAndCloses()
    {
        var handle = Service<NotificationService>().ShowPersistent(NotificationLevel.Info, "Uploading", "0%");

        _host.WaitForAssertion(() => Assert.Contains("Uploading", _host.Markup));

        handle.Update(NotificationLevel.Info, "Uploading", "50%");
        _host.WaitForAssertion(() => Assert.Contains("50%", _host.Markup));

        handle.Close();
        _host.WaitForAssertion(() => Assert.DoesNotContain("Uploading", _host.Markup));
        Assert.False(handle.IsOpen);
    }

    [Fact]
    public void Notification_Action_RunsAndCloses()
    {
        var ran = false;

        var handle = Service<NotificationService>().ShowAction(NotificationLevel.Success, "Import Ready", "arena.lcx has finished uploading", "Continue Import", () =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        _host.WaitForAssertion(() => Assert.Contains("arena.lcx has finished uploading", _host.Markup));

        _host.FindAll("button").Single(b => b.TextContent.Contains("Continue Import")).Click();

        Assert.True(ran);
        Assert.False(handle.IsOpen);
    }

    [Fact]
    public async Task Dialog_Ok_ReturnsResult()
    {
        var result = Service<DialogService>().OpenAsync<EchoDialog, EchoOptions, string>("Echo", new EchoOptions("hello"));

        _host.WaitForAssertion(() => Assert.Equal("hello", _host.Find("p.echo").TextContent));

        ClickDialogButton("OK");

        Assert.Equal("HELLO", await result.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Dialog_Cancel_ReturnsDefault()
    {
        var result = Service<DialogService>().OpenAsync<EchoDialog, EchoOptions, string>("Echo", new EchoOptions("hello"));

        _host.WaitForAssertion(() => _host.Find("p.echo"));

        ClickDialogButton("Cancel");

        Assert.Null(await result.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Dialog_OkReturningFalse_StaysOpen()
    {
        EchoDialog.OnOk = _ => Task.FromResult(false);

        var result = Service<DialogService>().OpenAsync<EchoDialog, EchoOptions, string>("Echo", new EchoOptions("hello"));

        _host.WaitForAssertion(() => _host.Find("p.echo"));
        ClickDialogButton("OK");

        Assert.False(result.IsCompleted);
        Assert.NotNull(_host.Find("p.echo"));
    }

    [Fact]
    public void Dialog_OkThrowing_ShowsErrorAndStaysOpen()
    {
        EchoDialog.OnOk = _ => throw new InvalidOperationException("Name already taken");

        var result = Service<DialogService>().OpenAsync<EchoDialog, EchoOptions, string>("Echo", new EchoOptions("hello"));

        _host.WaitForAssertion(() => _host.Find("p.echo"));
        ClickDialogButton("OK");

        _host.WaitForAssertion(() => Assert.Contains("Name already taken", _host.Find(".lc-dialog-body .lc-alert").TextContent));
        Assert.False(result.IsCompleted);
    }

    [Fact]
    public void Dialog_CannotConfirm_DisablesOk()
    {
        EchoDialog.Confirmable = false;

        _ = Service<DialogService>().OpenAsync<EchoDialog, EchoOptions, string>("Echo", new EchoOptions("hello"));

        _host.WaitForAssertion(() =>
        {
            var ok = _host.FindAll(".lc-dialog-footer button").Single(b => b.TextContent.Trim() == "OK");
            Assert.True(ok.HasAttribute("disabled"));
        });
    }

    [Fact]
    public async Task Dialog_SettingsChangeFooter()
    {
        var result = Service<DialogService>().OpenAsync<EchoDialog, EchoOptions, string>("Echo", new EchoOptions("hello"),
            new DialogSettings { OkText = "Delete", Danger = true, NoCancel = true });

        _host.WaitForAssertion(() =>
        {
            var buttons = _host.FindAll(".lc-dialog-footer button");
            var delete = Assert.Single(buttons);
            Assert.Equal("Delete", delete.TextContent.Trim());
            Assert.Contains("rz-danger", delete.ClassList);
        });

        ClickDialogButton("Delete");

        Assert.Equal("HELLO", await result.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData("OK", true)]
    [InlineData("Cancel", false)]
    public async Task Alert_Confirm_ReturnsWhetherConfirmed(string button, bool expected)
    {
        var confirmed = Service<AlertService>().ConfirmAsync("Delete Arena Blitz?", danger: true);

        _host.WaitForAssertion(() => Assert.Contains("Delete Arena Blitz?", _host.Find(".lc-confirm-message").TextContent));

        ClickDialogButton(button);

        Assert.Equal(expected, await confirmed.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
