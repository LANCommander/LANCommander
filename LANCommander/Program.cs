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

var builder = WebApplication.CreateBuilder(args);

ConfigurationManager configuration = builder.Configuration;

// Add services to the container.
var settings = SettingService.GetSettings();

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

builder.WebHost.ConfigureKestrel(options =>
{
    // Configure as HTTP only
    options.ListenAnyIP(settings.Port);
});

builder.Services.AddDbContext<LANCommander.Data.DatabaseContext>(b =>
{
    b.UseLazyLoadingProxies();
    b.UseSqlite(settings.DatabaseConnectionString);
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

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

builder.Services.AddControllers().AddJsonOptions(x =>
{
    x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddHangfire(configuration =>
    configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseInMemoryStorage());
builder.Services.AddHangfireServer();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAntDesign();

builder.Services.AddHttpClient();

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
builder.Services.AddScoped<GameSaveService>();

builder.Services.AddSingleton<ServerProcessService>();

if (settings.Beacon)
    builder.Services.AddHostedService<BeaconService>();

builder.WebHost.UseStaticWebAssets();

builder.WebHost.UseKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 150;
});

builder.Host.UseNLog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
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

app.UseEndpoints(endpoints =>
{
    endpoints.MapBlazorHub();
    endpoints.MapFallbackToPage("/_Host");
    endpoints.MapControllers();
});

// Migrate
await using var scope = app.Services.CreateAsyncScope();
using var db = scope.ServiceProvider.GetService<DatabaseContext>();
await db.Database.MigrateAsync();

if (!Directory.Exists("Upload"))
    Directory.CreateDirectory("Upload");

if (!Directory.Exists("Icon"))
    Directory.CreateDirectory("Icon");

if (!Directory.Exists("Saves"))
    Directory.CreateDirectory("Saves");

if (!Directory.Exists("Snippets"))
    Directory.CreateDirectory("Snippets");

app.Run();