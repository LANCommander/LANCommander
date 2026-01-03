using Microsoft.JSInterop;

namespace LANCommander.UI.Extensions;

public static class IJSRuntimeExtensions
{
    private const string ScriptAsset = "./js/bundle.min.js";
    
    private static IJSObjectReference? _module;

    public static async Task<IJSObjectReference> CreateAsync(this IJSRuntime js, string type, params object[] args)
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", ScriptAsset);
        
        return await _module.InvokeAsync<IJSObjectReference>($"Create{type}", args);
    }
}