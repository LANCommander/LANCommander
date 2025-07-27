using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.ImportExport.Importers;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Settings = LANCommander.Server.Services.Models.Settings;

namespace LANCommander.Server.ImportExport.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderImportExport(this IServiceCollection services)
    {
        services.AddSingleton<ImportService>();
        services.AddSingleton<ExportService>();

        // Register importers
        services.AddScoped<ImportContextFactory>();
        services.AddScoped<ImportContext>();

        services.AddScoped<ExportContextFactory>();
        services.AddScoped<ExportContext>();
        
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Game, Data.Models.Game>, GameImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Redistributable, Data.Models.Redistributable>, RedistributableImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Server, Data.Models.Server>, ServerImporter>();
        
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Action, Data.Models.Action>, ActionImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Archive, Data.Models.Archive>, ArchiveImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Collection, Data.Models.Collection>, CollectionImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField>, CustomFieldImporter>();
        services.AddScoped<DeveloperImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Collection, Data.Models.Collection>, CollectionImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Engine, Data.Models.Engine>, EngineImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Genre, Data.Models.Genre>, GenreImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Key, Data.Models.Key>, KeyImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Media, Data.Models.Media>, MediaImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode>, MultiplayerModeImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Platform, Data.Models.Platform>, PlatformImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession>, PlaySessionImporter>();
        services.AddScoped<PublisherImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Save, Data.Models.GameSave>, SaveImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath>, SavePathImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Script, Data.Models.Script>, ScriptImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole>, ServerConsoleImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath>, ServerHttpPathImporter>();
        services.AddScoped<BaseImporter<SDK.Models.Manifest.Tag, Data.Models.Tag>, TagImporter>();

        return services;
    }
}