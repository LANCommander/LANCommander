using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Factories
{
    public class DatabaseServiceFactory
    {
        private readonly IServiceProvider ServiceProvider;

        public DatabaseServiceFactory(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public T Create<T>() where T : BaseService
        {
            var service = ServiceProvider.GetService<T>();

            return service;
        }
    }
}
