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
using LANCommander.Extensions;
using Microsoft.AspNetCore.Http.Features;

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
            builder.Services
                .AddMvc(options => options.EnableEndpointRouting = false)
                .AddRazorOptions(options =>
                {
                    options.ViewLocationFormats.Clear();
                    options.ViewLocationFormats.Add("/UI/Views/{1}/{0}.cshtml");
                    options.ViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
                    options.ViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");

                    options.AreaViewLocationFormats.Clear();
                    options.AreaViewLocationFormats.Add("/Areas/{2}/Views/{1}/{0}.cshtml");
                    options.AreaViewLocationFormats.Add("/Areas/{2}/Views/Shared/{0}.cshtml");
                    options.AreaViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
                    options.AreaViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");

                    options.PageViewLocationFormats.Clear();
                    options.PageViewLocationFormats.Add("/UI/Pages/{1}/{0}.cshtml");
                    options.PageViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");
                    options.PageViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");

                    options.AreaPageViewLocationFormats.Clear();
                    options.AreaPageViewLocationFormats.Add("/Areas/{2}/Pages/{1}/{0}.cshtml");
                    options.AreaPageViewLocationFormats.Add("/Areas/{2}/Pages/Shared/{0}.cshtml");
                    options.AreaPageViewLocationFormats.Add("/Areas/{2}/Views/Shared/{0}.cshtml");
                    options.AreaPageViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");
                    options.AreaPageViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
                });
            builder.Services.AddRazorPages(options =>
            {
                options.RootDirectory = "/UI/Pages";
            });

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

            builder.Services.AddFusionCache();

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
            builder.Services.AddScoped<EngineService>();
            builder.Services.AddScoped<CompanyService>();
            builder.Services.AddScoped<IGDBService>();
            builder.Services.AddScoped<ServerService>();
            builder.Services.AddScoped<ServerConsoleService>();
            builder.Services.AddScoped<GameSaveService>();
            builder.Services.AddScoped<PlaySessionService>();
            builder.Services.AddScoped<MediaService>();
            builder.Services.AddScoped<RedistributableService>();
            builder.Services.AddScoped<IMediaGrabberService, SteamGridDBMediaGrabber>();
            builder.Services.AddScoped<WikiService>();
            builder.Services.AddScoped<UpdateService>();

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
                options.Limits.MaxRequestBodySize = long.MaxValue;
                options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
            });

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = long.MaxValue;
            });

            #region Configure NLog
            NLog.GlobalDiagnosticsContext.Set("StoragePath", settings.Logs.StoragePath);
            NLog.GlobalDiagnosticsContext.Set("ArchiveEvery", settings.Logs.ArchiveEvery.GetDisplayName());
            NLog.GlobalDiagnosticsContext.Set("MaxArchiveFiles", settings.Logs.MaxArchiveFiles.ToString());
            NLog.GlobalDiagnosticsContext.Set("PortNumber", settings.Port.ToString());

            builder.Host.UseNLog();
            #endregion

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

            app.UseMiddleware<ApiMiddleware>();

            app.UseHangfireDashboard();

            // app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseMvcWithDefaultRoute();

            app.MapHub<LoggingHub>("/hubs/logging");
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

            if (!Directory.Exists(settings.UserSaves.StoragePath))
                Directory.CreateDirectory(settings.UserSaves.StoragePath);

            if (!Directory.Exists(settings.Media.StoragePath))
                Directory.CreateDirectory(settings.Media.StoragePath);

            if (!Directory.Exists(settings.Update.StoragePath))
                Directory.CreateDirectory(settings.Update.StoragePath);

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

                var backupName = Path.Combine("Backups", $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}");

                if (File.Exists(dataSource))
                {
                    Logger.Info("Migrations pending, database will be backed up to {BackupName}", backupName);
                    File.Copy(dataSource, backupName);
                }

                await db.Database.MigrateAsync();
            }
            else
                Logger.Debug("No pending migrations are available. Skipping database migration.");

            // Replace autoupdater executable
            if (File.Exists("LANCommander.AutoUpdater.exe.Update"))
            {
                if (File.Exists("LANCommander.AutoUpdater.exe"))
                    File.Delete("LANCommander.AutoUpdater.exe");

                File.Move("LANCommander.AutoUpdater.exe.Update", "LANCommander.AutoUpdater.exe");
            }

            if (File.Exists("LANCommander.AutoUpdater.Update"))
            {
                if (File.Exists("LANCommander.AutoUpdater"))
                    File.Delete("LANCommander.AutoUpdater");

                File.Move("LANCommander.AutoUpdater.Update", "LANCommander.AutoUpdater");
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

                    serverProcessService.StartServerAsync(server.Id);
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex, "An unexpected error occurred while trying to autostart the server {ServerName}", server.Name);
                }
            }

            var targets = NLog.LogManager.Configuration.AllTargets;

            app.Run();
        }
    }
}

