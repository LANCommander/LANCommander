using LANCommander.Server.Endpoints;
using Serilog;

namespace LANCommander.Server.Startup;

public static class Endpoints
{
    public static void AddControllers(this WebApplicationBuilder builder)
    {
        Log.Debug("Initializing Controllers");
        
        builder.Services.AddControllers().AddJsonOptions(static x =>
        {
            x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });
    }

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        Log.Debug("Registering Endpoints");
        
        app.UseMvcWithDefaultRoute();
        
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDownloadEndpoints();
            endpoints.MapSaveEndpoints();
            endpoints.MapControllers();
            endpoints.MapFallbackToPage("/_Host");
        });

        return app;
    }
}