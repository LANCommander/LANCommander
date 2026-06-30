using System.Data.Common;
using System.Text;
using LANCommander.SDK.Providers;
using LANCommander.Server.Data;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Tests.Mocks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServerSettings = LANCommander.Server.Settings.Settings;

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

            #region JWT signing key alignment
            // The server snapshots the JWT signing secret from configuration when AddIdentity runs,
            // but ValidateSettings regenerates the secret at startup whenever it is missing (which is
            // always the case in tests, where no persisted settings file provides one). That leaves the
            // bearer validation key pinned to the empty pre-regeneration value while tokens are signed
            // with the regenerated secret, so every authenticated API call fails with 401. Re-bind the
            // validation key from the live settings provider once the regenerated secret exists.
            services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, AlignJwtSigningKey>();
            #endregion
        });
    }

    private sealed class AlignJwtSigningKey(SettingsProvider<ServerSettings> settingsProvider)
        : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            var secret = settingsProvider.CurrentValue.Server.Authentication.TokenSecret;

            options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        }
    }
}