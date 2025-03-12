using System.Diagnostics;

namespace LANCommander.Server.Startup;

public static class Debug
{
    public static WebApplicationBuilder WaitForDebugger(this WebApplicationBuilder builder)
    {
        var currentProcess = Process.GetCurrentProcess();

        Console.WriteLine($"Waiting for debugger to attach... Process ID: {currentProcess.Id}");

        while (!Debugger.IsAttached)
        {
            Thread.Sleep(100);
        }

        Console.WriteLine("Debugger attached.");

        return builder;
    }
}