using BeaconLib;
using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

ConfigurationManager configuration = builder.Configuration;

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var settings = SettingService.GetSettings();

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
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedEmail = false;
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.TokenSecret))
        };
    });

builder.Services.AddControllersWithViews().AddJsonOptions(x =>
{
    x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddServerSideBlazor();

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

if (settings.Beacon)
    builder.Services.AddHostedService<BeaconService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    endpoints.MapBlazorHub();
});

app.MapRazorPages();

if (!Directory.Exists("Upload"))
    Directory.CreateDirectory("Upload");

if (!Directory.Exists("Icon"))
    Directory.CreateDirectory("Icon");

if (!Directory.Exists("Save"))
    Directory.CreateDirectory("Save");

app.Run();