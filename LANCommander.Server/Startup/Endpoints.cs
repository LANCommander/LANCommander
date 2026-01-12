using LANCommander.Server.Endpoints;

namespace LANCommander.Server.Startup;

public static class Endpoints
{
    public static void AddControllers(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers().AddJsonOptions(static x =>
        {
            x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });
    }

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapAuthenticationEndpoints();
            endpoints.MapAuthApiEndpoints();
            endpoints.MapChatEndpoints();
            endpoints.MapDownloadEndpoints();
            endpoints.MapGameEndpoints();
            endpoints.MapDepotEndpoints();
            endpoints.MapArchivesEndpoints();
            endpoints.MapUploadEndpoints();
            endpoints.MapProfileEndpoints();
            endpoints.MapPlaySessionsEndpoints();
            endpoints.MapMediaEndpoints();
            endpoints.MapKeysEndpoints();
            endpoints.MapLauncherEndpoints();
            endpoints.MapLibraryEndpoints();
            endpoints.MapRedistributablesEndpoints();
            endpoints.MapIssueEndpoints();
            endpoints.MapSaveEndpoints();
            endpoints.MapServerEndpoints();
            endpoints.MapSettingsEndpoints();
            endpoints.MapTagEndpoints();
            endpoints.MapControllers();
            endpoints.MapFallbackToPage("/_Host");
        });

        return app;
    }
}