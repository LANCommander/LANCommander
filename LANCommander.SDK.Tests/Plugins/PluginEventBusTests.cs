using System;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Plugins;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.SDK.Tests.Plugins;

public class PluginEventBusTests
{
    private sealed record SampleEvent(int Value);
    private sealed record OtherEvent(string Name);

    private static PluginEventBus CreateBus() => new(NullLogger<PluginEventBus>.Instance);

    [Fact]
    public async Task PublishAsync_InvokesSubscribedHandler()
    {
        var bus = CreateBus();
        var received = 0;

        bus.Subscribe<SampleEvent>((e, _) => { received = e.Value; return Task.CompletedTask; });

        await bus.PublishAsync(new SampleEvent(42));

        Assert.Equal(42, received);
    }

    [Fact]
    public async Task PublishAsync_OnlyInvokesHandlersForMatchingType()
    {
        var bus = CreateBus();
        var sampleCalled = false;

        bus.Subscribe<SampleEvent>((_, _) => { sampleCalled = true; return Task.CompletedTask; });

        await bus.PublishAsync(new OtherEvent("nope"));

        Assert.False(sampleCalled);
    }

    [Fact]
    public async Task PublishAsync_IsolatesThrowingHandlers()
    {
        var bus = CreateBus();
        var secondCalled = false;

        bus.Subscribe<SampleEvent>((_, _) => throw new InvalidOperationException("boom"));
        bus.Subscribe<SampleEvent>((_, _) => { secondCalled = true; return Task.CompletedTask; });

        // Must not throw, and the second handler must still run.
        await bus.PublishAsync(new SampleEvent(1));

        Assert.True(secondCalled);
    }

    [Fact]
    public async Task Dispose_Unsubscribes()
    {
        var bus = CreateBus();
        var count = 0;

        var subscription = bus.Subscribe<SampleEvent>((_, _) => { count++; return Task.CompletedTask; });

        await bus.PublishAsync(new SampleEvent(1));
        subscription.Dispose();
        await bus.PublishAsync(new SampleEvent(1));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PublishAsync_NoSubscribers_DoesNothing()
    {
        var bus = CreateBus();
        await bus.PublishAsync(new SampleEvent(1)); // should simply return
    }
}
