using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services
{
    public abstract class BaseService
    {
        protected readonly SDK.Client Client;
        protected readonly ILogger Logger;

        protected BaseService(SDK.Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }
    }
}
