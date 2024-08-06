using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLANCommander(this IServiceCollection services)
        {
            var settings = SettingService.GetSettings();

            services.AddDbContext<DbContext, DatabaseContext>();

            #region Register Client
            var client = new SDK.Client(settings.Authentication.ServerAddress, settings.Games.DefaultInstallDirectory);

            client.UseToken(new SDK.Models.AuthToken
            {
                AccessToken = settings.Authentication.AccessToken,
                RefreshToken = settings.Authentication.RefreshToken,
            });

            services.AddSingleton(client);
            services.AddSingleton<MessageBusService>();
            #endregion

            services.AddScoped<CollectionService>();
            services.AddScoped<CompanyService>();
            services.AddScoped<EngineService>();
            services.AddScoped<GameService>();
            services.AddScoped<GenreService>();
            services.AddScoped<PlatformService>();
            services.AddScoped<MultiplayerModeService>();
            services.AddScoped<TagService>();
            services.AddScoped<MediaService>();
            services.AddScoped<ProfileService>();
            services.AddScoped<PlaySessionService>();
            services.AddScoped<RedistributableService>();
            services.AddScoped<SaveService>();
            services.AddScoped<ImportService>();
            services.AddScoped<LibraryService>();
            services.AddScoped<DownloadService>();
            services.AddScoped<UpdateService>();

            return services;
        }
    }
}
