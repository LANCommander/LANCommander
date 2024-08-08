using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Services;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLANCommander();

using IHost host = builder.Build();

using var scope = host.Services.CreateScope();

var commandLineService = scope.ServiceProvider.GetService<CommandLineService>();

await commandLineService.ParseCommandLineAsync(args);