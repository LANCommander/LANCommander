using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LANCommander.SDK;

public class AsyncEventHandler<T>
{
    public event Func<T, Task>? EventRaised;

    public async Task InvokeAsync(T args)
    {
        if (EventRaised is null)
            return;

        var subscribers = EventRaised.GetInvocationList()
            .Cast<Func<T, Task>>();

        var tasks = new List<Task>();
        foreach (var sub in subscribers)
            tasks.Add(SafeInvoke(sub, args));

        await Task.WhenAll(tasks);
    }

    public async Task InvokeSequentialAsync(T args)
    {
        if (EventRaised is null)
            return;

        var subscribers = EventRaised.GetInvocationList()
            .Cast<Func<T, Task>>();

        foreach (var sub in subscribers)
            await SafeInvoke(sub, args);
    }

    private async Task SafeInvoke(Func<T, Task> subscriber, T args)
    {
        try {
            await subscriber.Invoke(args);
        }
        catch (Exception ex) {
            Console.WriteLine($"Async event subscriber failed: {ex.Message}");
        }
    }
}