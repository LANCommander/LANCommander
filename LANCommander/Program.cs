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

Logger Logger = LogManager.GetCurrentClassLogger();

var builder = WebApplication.CreateBuilder(args);

ConfigurationManager configuration = builder.Configuration;

// Add services to the container.
Logger.Debug("Loading settings");
var settings = SettingService.GetSettings();
Logger.Debug("Loaded!");

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

builder.Services.AddSingleton<ServerProcessService>();

if (settings.Beacon)
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
if (!Directory.Exists("Upload"))
    Directory.CreateDirectory("Upload");

if (!Directory.Exists("Icon"))
    Directory.CreateDirectory("Icon");

if (!Directory.Exists("Saves"))
    Directory.CreateDirectory("Saves");

if (!Directory.Exists("Snippets"))
    Directory.CreateDirectory("Snippets");

// Migrate
Logger.Debug("Migrating database if required");
await using var scope = app.Services.CreateAsyncScope();
using var db = scope.ServiceProvider.GetService<DatabaseContext>();
await db.Database.MigrateAsync();

// Autostart any server processes
Logger.Debug("Autostarting Servers");
var serverService = scope.ServiceProvider.GetService<ServerService>();
var serverProcessService = scope.ServiceProvider.GetService<ServerProcessService>();

foreach (var server in await serverService.Get(s => s.Autostart).ToListAsync())
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