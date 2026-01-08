using Microsoft.JSInterop;

namespace LANCommander.UI.Extensions;

public static class IJSRuntimeExtensions
{
    private const string ScriptAsset = "./_content/LANCommander.UI/bundle.js";
    
    private static IJSObjectReference? _module;

    public static async Task InitializeAsync(this IJSRuntime js) 
        => _module ??= await js.InvokeAsync<IJSObjectReference>("import", ScriptAsset);

    public static async Task<IJSObjectReference> ImportModuleAsync<T>(this IJSRuntime js, params object[] args)
    {
        var type = typeof(T);

        return await js.ImportModuleAsync(type.Name, args);
    }
    
    public static async Task<IJSObjectReference> ImportModuleAsync(this IJSRuntime js, string type, params object[] args)
    {
        await js.InitializeAsync();
        
        return await _module!.InvokeAsync<IJSObjectReference>($"{type}.Create", args);
    }
}