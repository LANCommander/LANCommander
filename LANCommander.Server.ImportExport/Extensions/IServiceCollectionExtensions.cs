using LANCommander.Server.ImportExport.Exporters;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.ImportExport.Importers;
using LANCommander.Server.ImportExport.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.ImportExport.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderImportExport(this IServiceCollection services)
    {
        services.AddSingleton<ImportService>();
        services.AddSingleton<ExportService>();

        #region Import
        services.AddScoped<ImportContextFactory>();
        services.AddScoped<ImportContext>();
        
        services.AddScoped<GameImporter>();
        services.AddScoped<RedistributableImporter>();
        services.AddScoped<ServerImporter>();
        services.AddScoped<ToolImporter>();
        
        services.AddScoped<ActionImporter>();
        services.AddScoped<ArchiveImporter>();
        services.AddScoped<CollectionImporter>();
        services.AddScoped<CustomFieldImporter>();
        services.AddScoped<DeveloperImporter>();
        services.AddScoped<CollectionImporter>();
        services.AddScoped<EngineImporter>();
        services.AddScoped<GenreImporter>();
        services.AddScoped<KeyImporter>();
        services.AddScoped<MediaImporter>();
        services.AddScoped<MultiplayerModeImporter>();
        services.AddScoped<PlatformImporter>();
        services.AddScoped<PlaySessionImporter>();
        services.AddScoped<PublisherImporter>();
        services.AddScoped<SaveImporter>();
        services.AddScoped<SavePathImporter>();
        services.AddScoped<ScriptImporter>();
        services.AddScoped<ServerConsoleImporter>();
        services.AddScoped<ServerHttpPathImporter>();
        services.AddScoped<TagImporter>();
        #endregion
        
        #region Export
        services.AddScoped<ExportContextFactory>();
        services.AddScoped<ExportContext>();
        
        services.AddScoped<GameExporter>();
        services.AddScoped<RedistributableExporter>();
        services.AddScoped<ServerExporter>();
        services.AddScoped<ToolExporter>();
        
        services.AddScoped<ActionExporter>();
        services.AddScoped<ArchiveExporter>();
        services.AddScoped<CollectionExporter>();
        services.AddScoped<CustomFieldExporter>();
        services.AddScoped<DeveloperExporter>();
        services.AddScoped<CollectionExporter>();
        services.AddScoped<EngineExporter>();
        services.AddScoped<GenreExporter>();
        services.AddScoped<KeyExporter>();
        services.AddScoped<MediaExporter>();
        services.AddScoped<MultiplayerModeExporter>();
        services.AddScoped<PlatformExporter>();
        services.AddScoped<PlaySessionExporter>();
        services.AddScoped<PublisherExporter>();
        services.AddScoped<SaveExporter>();
        services.AddScoped<SavePathExporter>();
        services.AddScoped<ScriptExporter>();
        services.AddScoped<ServerConsoleExporter>();
        services.AddScoped<ServerHttpPathExporter>();
        services.AddScoped<TagExporter>();
        #endregion

        return services;
    }
}