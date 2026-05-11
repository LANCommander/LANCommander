using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LANCommander.UI.Components;

public class MonacoCodeEditor : StandaloneCodeEditor
{
    private static bool _completionsRegistered;
    private IJSObjectReference? _module;
    private string? _previousScriptType;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public string Value { get; set; }

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public EventCallback OnSave { get; set; }

    [Parameter]
    public string? ScriptType { get; set; }

    protected override void OnInitialized()
    {
        OnDidChangeModelContent = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<ModelContentChangedEvent>(this, OnChanged);
        OnDidInit = Microsoft.AspNetCore.Components.EventCallback.Factory.Create(this, OnInit);

        base.OnInitialized();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_module != null && ScriptType != _previousScriptType)
        {
            _previousScriptType = ScriptType;
            await _module.InvokeVoidAsync("setScriptType", ScriptType);
            await RunValidationAsync();
        }

        await base.OnParametersSetAsync();
    }

    private async Task OnInit()
    {
        await AddCommand((int)BlazorMonaco.KeyMod.CtrlCmd | (int)BlazorMonaco.KeyCode.KeyS, async (editor) =>
        {
            if (OnSave.HasDelegate)
                await OnSave.InvokeAsync();
        }, null);

        _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/LANCommander.UI/bundle.js");

        if (!_completionsRegistered)
        {
            _completionsRegistered = true;
            await _module.InvokeVoidAsync("registerPowerShellCompletions");
        }

        if (ScriptType != null)
        {
            _previousScriptType = ScriptType;
            await _module.InvokeVoidAsync("setScriptType", ScriptType);
        }
    }

    private async Task OnChanged(ModelContentChangedEvent e)
    {
        Value = await GetValue();

        if (ValueChanged.HasDelegate)
            await ValueChanged.InvokeAsync(Value);

        await RunValidationAsync();
    }

    public async Task InsertSnippetAsync(string snippetText)
    {
        if (_module != null)
            await _module.InvokeVoidAsync("insertSnippet", snippetText);
    }

    private async Task RunValidationAsync()
    {
        if (_module == null)
            return;

        await _module.InvokeVoidAsync("validateScript");
    }
}