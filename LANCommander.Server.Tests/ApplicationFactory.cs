using System.Data.Common;
using LANCommander.Server.Data;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Tests.Mocks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Tests;

public class ApplicationFactory<TProgram>
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            #region DatabaseContext
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == 
                     typeof(IDbContextOptionsConfiguration<DatabaseContext>));

            services.Remove(dbContextDescriptor);

            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType ==
                     typeof(DbConnection));

            services.Remove(dbConnectionDescriptor);
            
            services.AddDbContextFactory<DatabaseContext>(optionsBuilder =>
            {
                optionsBuilder.UseInMemoryDatabase("Test");
            });
            #endregion
            
            #region IVersionProvider

            var versionProviderDescriptor = services.SingleOrDefault(
                d => typeof(IVersionProvider).IsAssignableFrom(d.ServiceType));
            
            services.Remove(versionProviderDescriptor);

            services.AddSingleton<IVersionProvider, VersionProviderMock>();
            #endregion
            
            #region GitHubService
            var gitHubServiceDescriptor = services.SingleOrDefault(
                d => typeof(IGitHubService).IsAssignableFrom(d.ServiceType));
            
            services.Remove(gitHubServiceDescriptor);

            services.AddSingleton(GitHubServiceMockFactory.Create());
            #endregion
        });
    }
}