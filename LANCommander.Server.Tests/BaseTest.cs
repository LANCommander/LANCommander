using System.Formats.Asn1;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Tests;

public abstract class BaseTest : IClassFixture<ApplicationFixture>, IDisposable
{
    protected readonly SDK.Client Client = ApplicationFixture.Instance.Client;
    protected readonly IServiceProvider ServiceProvider;
    
    private AsyncServiceScope? _scope;
    
    public BaseTest(ApplicationFixture fixture)
    {
        _scope = ApplicationFixture.Instance.ServiceProvider.CreateAsyncScope();
        
        ServiceProvider = _scope?.ServiceProvider;
    }
    
    protected T GetService<T>() => ServiceProvider.GetService<T>();

    public void Dispose()
    {
        _scope?.Dispose();
    }
}