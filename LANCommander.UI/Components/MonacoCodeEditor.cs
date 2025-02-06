using BlazorMonaco.Editor;
using Microsoft.AspNetCore.Components;

namespace LANCommander.UI.Components;

public class MonacoCodeEditor : StandaloneCodeEditor
{
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
    }

    private async void OnChanged(ModelContentChangedEvent e)
    {
        Value = await GetValue();
        
        if (ValueChanged.HasDelegate)
            ValueChanged.InvokeAsync(Value);
    }
}