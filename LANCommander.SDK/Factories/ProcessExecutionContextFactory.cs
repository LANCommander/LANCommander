using System;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Factories;

public class ProcessExecutionContextFactory(IServiceProvider serviceProvider)
{
    public ProcessExecutionContext Create()
    {
        return new ProcessExecutionContext(
            serviceProvider.GetService<ILogger<ProcessExecutionContext>>(),
            serviceProvider.GetService<LobbyService>());
    }
}