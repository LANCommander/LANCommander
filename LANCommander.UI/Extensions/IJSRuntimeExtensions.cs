using Microsoft.JSInterop;

namespace LANCommander.UI.Extensions;

public static class IJSRuntimeExtensions
{
    private const string ScriptAsset = "./_content/LANCommander.UI/bundle.js";
    
    private static IJSObjectReference? _module;

    public static async Task InitializeAsync(this IJSRuntime js) 
        => _module ??= await js.InvokeAsync<IJSObjectReference>("import", ScriptAsset);

    public static async Task<IJSObjectReference> ExecuteAsync(this IJSRuntime js, string method, params object[] args)
    {
        await js.InitializeAsync();
        
        return await _module!.InvokeAsync<IJSObjectReference>(method, args);
    }

    public static async Task ExecuteVoidAsync(this IJSRuntime js, string method, params object[] args)
    {
        await js.InitializeAsync();
        
        await _module!.InvokeVoidAsync(method, args);
    }

    public static async Task<IJSObjectReference> CreateAsync(this IJSRuntime js, string type, params object[] args)
        => await js.ExecuteAsync($"Create{type}", args);
}