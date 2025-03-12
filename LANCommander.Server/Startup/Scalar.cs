using Scalar.AspNetCore;

namespace LANCommander.Server.Startup;

public static class ConfigureScalar
{
    
    public static void AddOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        });
        
        builder.Services.AddEndpointsApiExplorer();
    }
    
    public static WebApplication MapScalar(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference("/api", options =>
        {
            options.Servers = [];
            
            options
                .WithHttpBearerAuthentication(bearer =>
                {
                    bearer.Token = "your-bearer-token";
                });

            options.Authentication = new ScalarAuthenticationOptions
            {
                PreferredSecurityScheme = "Bearer",
            };
        });

        return app;
    }
}