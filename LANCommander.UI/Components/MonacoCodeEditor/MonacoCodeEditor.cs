using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LANCommander.UI.Components;

public class MonacoCodeEditor : StandaloneCodeEditor
{
    private static bool _completionsRegistered;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public string Value { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public EventCallback OnSave { get; set; }

    protected override void OnInitialized()
    {
        OnDidChangeModelContent = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<ModelContentChangedEvent>(this, OnChanged);
        OnDidInit = Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, OnInit);

        base.OnInitialized();
    }

    private async Task OnInit()
    {
        await AddCommand((int)BlazorMonaco.KeyMod.CtrlCmd | (int)BlazorMonaco.KeyCode.KeyS, async (editor) =>
        {
            if (OnSave.HasDelegate)
                await OnSave.InvokeAsync();
        }, null);

        if (!_completionsRegistered)
        {
            _completionsRegistered = true;

            var module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/LANCommander.UI/bundle.js");
            await module.InvokeVoidAsync("registerPowerShellCompletions");
        }
    }

    private async Task OnChanged(ModelContentChangedEvent e)
    {
        Value = await GetValue();

        if (ValueChanged.HasDelegate)
            await ValueChanged.InvokeAsync(Value);
    }
}