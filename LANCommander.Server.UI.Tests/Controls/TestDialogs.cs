using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components.Rendering;

namespace LANCommander.Server.UI.Tests.Controls;

public sealed record EchoOptions(string Text);

/// <summary>Echoes its options back as the result; behaviour on OK is configurable per test.</summary>
public sealed class EchoDialog : Dialog<EchoOptions, string>
{
    public static Func<EchoDialog, Task<bool>>? OnOk { get; set; }

    public static bool Confirmable { get; set; } = true;

    protected override bool CanConfirm => Confirmable;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "echo");
        builder.AddContent(2, Options.Text);
        builder.CloseElement();
    }

    protected override Task<bool> OnOkAsync()
    {
        Result = Options.Text.ToUpperInvariant();

        return OnOk?.Invoke(this) ?? Task.FromResult(true);
    }

    public Task CloseWithAsync(string result) => CloseAsync(result);
}
