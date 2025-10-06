using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Services;
using System.Runtime.InteropServices;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using Serilog.Events;

// Map the Microsoft.Extensions.Logging.LogLevel to Serilog.LogEventLevel.

using var logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Components", LogEventLevel.Warning)
    .MinimumLevel.Override("AntDesign", LogEventLevel.Warning)
    .Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name)
    .WriteTo.Console()
#if DEBUG
    .WriteTo.Seq("http://localhost:5341")
#endif
    .CreateLogger();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddSerilog(logger);
});

builder.Services.AddLANCommanderClient<LANCommander.SDK.Models.Settings>();

using IHost host = builder.Build();

host.Services.InitializeLANCommander();

using var scope = host.Services.CreateScope();

var commandLineService = scope.ServiceProvider.GetService<CommandLineService>();

await commandLineService.ParseCommandLineAsync(args);


/// <summary>
/// Maps Microsoft.Extensions.Logging.LogLevel to Serilog.Events.LogEventLevel.
/// </summary>
static LogEventLevel MapLogLevel(LogLevel level)
{
    return level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Critical => LogEventLevel.Fatal,
        // LogLevel.None indicates logging should be disabled.
        // Serilog does not have a direct "Off" level so you might choose to
        // either bypass logging configuration or set it high enough to ignore messages.
        LogLevel.None => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };
}