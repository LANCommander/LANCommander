using System.Data.Common;
using LANCommander.Server.Data;
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
            
            services.AddDbContext<DatabaseContext>(optionsBuilder =>
            {
                optionsBuilder.UseInMemoryDatabase("Test");
            });
            #endregion
        });
    }
}