namespace LANCommander.Server.Startup;

public static class Cors
{
    public static WebApplicationBuilder AddCors(this WebApplicationBuilder builder)
    {
        builder.Services.AddCors(static options => 
            options.AddPolicy("CorsPolicy", static builder =>
            {
                builder.AllowAnyHeader()
                    .AllowAnyMethod()
                    .SetIsOriginAllowed(static (host) => true)
                    .AllowCredentials();
            })
        );

        return builder;
    }
}