using LANCommander.SDK;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Startup;

public static class ApplicationSettings
{
    public static WebApplicationBuilder AddSettings(this WebApplicationBuilder builder)
    {
        var filePath = Path.Join(AppPaths.GetConfigDirectory(), SDK.Models.Settings.SETTINGS_FILE_NAME);
        var settings = new Settings.Settings();

        if (!File.Exists(filePath))
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();
            
            File.WriteAllText(filePath, serializer.Serialize(settings));
        }

        var boostrap = new ConfigurationBuilder()
            .AddYamlFile(filePath, false, true)
            .Build();
        
        builder.Services.Configure<Settings.Settings>(boostrap);
        builder.Configuration.Bind(settings);

        Log.Debug("Validating settings");
        
        if (settings.Server.Authentication.TokenSecret.Length < 16)
        {
            Log.Debug("JWT token secret is too short. Regenerating...");
            settings.Server.Authentication.TokenSecret = Guid.NewGuid().ToString();
        }

        return builder;
    }
}