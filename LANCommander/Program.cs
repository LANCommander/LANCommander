using BeaconLib;
using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Hubs;
using LANCommander.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NLog.Web;
using System.Text;
using Hangfire;
using NLog;
using LANCommander.Services.MediaGrabbers;
using Microsoft.Data.Sqlite;

namespace LANCommander
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Logger Logger = LogManager.GetCurrentClassLogger();

            var builder = WebApplication.CreateBuilder(args);

            ConfigurationManager configuration = builder.Configuration;

            // Add services to the container.
            Logger.Debug("Loading settings");
            var settings = SettingService.GetSettings(true);
            Logger.Debug("Loaded!");

            #region Validate Settings
            Logger.Debug("Validating settings");
            if (settings?.Authentication?.TokenSecret?.Length < 16)
            {
                Logger.Debug("JWT token secret is too short. Regenerating...");
                settings.Authentication.TokenSecret = Guid.NewGuid().ToString();
                SettingService.SaveSettings(settings);
            }
            Logger.Debug("Done validating settings");
            #endregion

            Logger.Debug("Configuring MVC and Blazor");
            builder.Services.AddMvc(options => options.EnableEndpointRouting = false);
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor().AddCircuitOptions(option =>
            {
                option.DetailedErrors = true;
            }).AddHubOptions(option =>
            {
                option.MaximumReceiveMessageSize = 1024 * 1024 * 11;
                option.DisableImplicitFromServicesParameters = true;
            });

            builder.Services.AddAutoMapper(typeof(AutoMapper));

            Logger.Debug("Starting web server on port {Port}", settings.Port);
            builder.WebHost.ConfigureKestrel(options =>
            {
                // Configure as HTTP only
                options.ListenAnyIP(settings.Port);
            });

            Logger.Debug("Initializing DatabaseContext with connection string {ConnectionString}", settings.DatabaseConnectionString);
            builder.Services.AddDbContext<LANCommander.Data.DatabaseContext>(b =>
            {
                b.UseLazyLoadingProxies();
                b.UseSqlite(settings.DatabaseConnectionString);
            });

            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            Logger.Debug("Initializing Identity");
            builder.Services.AddDefaultIdentity<User>((IdentityOptions options) =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;

                options.Password.RequireNonAlphanumeric = settings.Authentication.PasswordRequireNonAlphanumeric;
                options.Password.RequireLowercase = settings.Authentication.PasswordRequireLowercase;
                options.Password.RequireUppercase = settings.Authentication.PasswordRequireUppercase;
                options.Password.RequireDigit = settings.Authentication.PasswordRequireDigit;
                options.Password.RequiredLength = settings.Authentication.PasswordRequiredLength;
            })
                .AddRoles<Role>()
                .AddEntityFrameworkStores<LANCommander.Data.DatabaseContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddAuthentication(options =>
            {
                /*options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;*/
            })
                .AddJwtBearer(options =>
                {
                    options.SaveToken = true;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        // ValidAudience = configuration["JWT:ValidAudience"],
                        // ValidIssuer = configuration["JWT:ValidIssuer"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Authentication.TokenSecret))
                    };
                });

            Logger.Debug("Initializing Controllers");
            builder.Services.AddControllers().AddJsonOptions(x =>
            {
                x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });

            Logger.Debug("Initializing Hangfire");
            builder.Services.AddHangfire(configuration =>
                configuration
                    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings()
                    .UseInMemoryStorage());
            builder.Services.AddHangfireServer();

            Logger.Debug("Registering Swashbuckle");
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            Logger.Debug("Registering AntDesign Blazor");
            builder.Services.AddAntDesign();

            builder.Services.AddHttpClient();

            Logger.Debug("Registering Services");
            builder.Services.AddScoped<SettingService>();
            builder.Services.AddScoped<ArchiveService>();
            builder.Services.AddScoped<CategoryService>();
            builder.Services.AddScoped<CollectionService>();
            builder.Services.AddScoped<GameService>();
            builder.Services.AddScoped<ScriptService>();
            builder.Services.AddScoped<GenreService>();
            builder.Services.AddScoped<KeyService>();
            builder.Services.AddScoped<TagService>();
            builder.Services.AddScoped<CompanyService>();
            builder.Services.AddScoped<IGDBService>();
            builder.Services.AddScoped<ServerService>();
            builder.Services.AddScoped<ServerConsoleService>();
            builder.Services.AddScoped<GameSaveService>();
            builder.Services.AddScoped<PlaySessionService>();
            builder.Services.AddScoped<MediaService>();
            builder.Services.AddScoped<RedistributableService>();
            builder.Services.AddScoped<IMediaGrabberService, SteamGridDBMediaGrabber>();

            builder.Services.AddSingleton<ServerProcessService>();
            builder.Services.AddSingleton<IPXRelayService>();

            if (settings.Beacon?.Enabled ?? false)
            {
                Logger.Debug("The beacons have been lit! LANCommander calls for players!");
                builder.Services.AddHostedService<BeaconService>();
            }

            builder.WebHost.UseStaticWebAssets();

            builder.WebHost.UseKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 1024 * 1024 * 150;
            });

            builder.Host.UseNLog();

            Logger.Debug("Building Application");
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                Logger.Debug("App has been run in a development environment");
                app.UseMigrationsEndPoint();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHangfireDashboard();

            // app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMvcWithDefaultRoute();

            app.MapHub<GameServerHub>("/hubs/gameserver");

            Logger.Debug("Registering Endpoints");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapControllers();
            });

            Logger.Debug("Ensuring required directories exist");
            if (!Directory.Exists(settings.Archives.StoragePath))
                Directory.CreateDirectory(settings.Archives.StoragePath);

            if (!Directory.Exists("Icon"))
                Directory.CreateDirectory("Icon");

            if (!Directory.Exists(settings.UserSaves.StoragePath))
                Directory.CreateDirectory(settings.UserSaves.StoragePath);

            if (!Directory.Exists(settings.Media.StoragePath))
                Directory.CreateDirectory(settings.Media.StoragePath);

            if (!Directory.Exists("Snippets"))
                Directory.CreateDirectory("Snippets");

            if (!Directory.Exists("Backups"))
                Directory.CreateDirectory("Backups");

            // Migrate
            Logger.Debug("Migrating database if required");
            await using var scope = app.Services.CreateAsyncScope();
            using var db = scope.ServiceProvider.GetService<DatabaseContext>();

            if ((await db.Database.GetPendingMigrationsAsync()).Any())
            {
                var dataSource = new SqliteConnectionStringBuilder(settings.DatabaseConnectionString).DataSource;

                if (File.Exists(dataSource))
                    File.Copy(dataSource, Path.Combine("Backups", $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}"));

                await db.Database.MigrateAsync();
            }

            // Autostart any server processes
            Logger.Debug("Autostarting Servers");
            var serverService = scope.ServiceProvider.GetService<ServerService>();
            var serverProcessService = scope.ServiceProvider.GetService<ServerProcessService>();

            foreach (var server in await serverService.Get(s => s.Autostart && s.AutostartMethod == Data.Enums.ServerAutostartMethod.OnApplicationStart).ToListAsync())
            {
                try
                {
                    Logger.Debug("Autostarting server {ServerName} with a delay of {AutostartDelay} seconds", server.Name, server.AutostartDelay);

                    if (server.AutostartDelay > 0)
                        await Task.Delay(server.AutostartDelay);

                    serverProcessService.StartServerAsync(server);
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "An unexpected error occurred while trying to autostart the server {ServerName}", server.Name);
                }
            }

            app.Run();
        }
    }
}

