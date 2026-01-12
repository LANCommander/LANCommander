using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LANCommander.UI.Providers;

public class ScriptProvider(IJSRuntime js)
{
    private const string ScriptAsset = "./_content/LANCommander.UI/bundle.js";
    
    private IJSObjectReference? _module;

    public async Task InitializeAsync()
        => _module ??= await js.InvokeAsync<IJSObjectReference>("import", ScriptAsset);

    public async Task<IJSObjectReference> ImportModuleAsync<T, TComponent>(TComponent component, params object[] args) where TComponent : class, IComponent
    {
        var mergedArgs = new object[args.Length + 1];
        
        mergedArgs[0] = DotNetObjectReference.Create(component);
        Array.Copy(args, 0, mergedArgs, 1, args.Length);
        
        return await ImportModuleAsync<T>(args);
    }

    public async Task<IJSObjectReference> ImportModuleAsync<TComponent>(TComponent component, string moduleName,
        params object[] args) where TComponent : class, IComponent
    {
        var mergedArgs = new object[args.Length + 1];
        
        mergedArgs[0] = DotNetObjectReference.Create(component);
        Array.Copy(args, 0, mergedArgs, 1, args.Length);
        
        return await ImportModuleAsync(moduleName, mergedArgs);
    }

    public async Task<IJSObjectReference> ImportModuleAsync<T>(params object[] args)
    {
        var type = typeof(T);

        return await ImportModuleAsync(type.Name, args);
    }

    public async Task<IJSObjectReference> ImportModuleAsync(string moduleName, params object[] args)
    {
        await InitializeAsync();
        
        return await _module!.InvokeAsync<IJSObjectReference>($"{moduleName}.Create", args);
    }

    public async Task InvokeVoidAsync(string method, params object[] args)
    {
        await InitializeAsync();
        
        await _module!.InvokeVoidAsync(method, args);
    }

    public async Task<T> InvokeAsync<T>(string method, params object[] args)
    {
        await InitializeAsync();
        
        return await _module!.InvokeAsync<T>(method, args);
    }
}