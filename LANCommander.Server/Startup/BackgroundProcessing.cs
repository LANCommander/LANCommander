using System.Reflection;
using Hangfire;
using LANCommander.Server.Jobs.Recurring;

namespace LANCommander.Server.Startup;

public static class BackgroundProcessing
{
    public static WebApplicationBuilder AddHangfire(this WebApplicationBuilder builder)
    {
        builder.Services.AddHangfire(static (sp, configuration) =>
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Initializing Hangfire");
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseInMemoryStorage();
        });
        
        builder.Services.AddHangfireServer();
        
        // Register recurring jobs with DI
        foreach (var recurringJobType in GetRecurringJobTypes())
            builder.Services.AddTransient(recurringJobType);
        
        return builder;
    }

    public static WebApplication UseHangfire(this WebApplication app)
    {
        app.UseHangfireDashboard();
        
        using var scope = app.Services.CreateScope();
        
        var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        foreach (var recurringJobType in GetRecurringJobTypes())
        {
            var instance = (BaseRecurringJob)scope.ServiceProvider.GetRequiredService(recurringJobType);

            var method = recurringJobType.GetMethod(nameof(BaseRecurringJob.ExecuteAsync))!;
            var job = new Hangfire.Common.Job(recurringJobType, method, args: Array.Empty<object?>());

            var options = new RecurringJobOptions
            {
                TimeZone = instance.TimeZone,
            };
            
            manager.AddOrUpdate(instance.JobId, job, instance.CronExpression, options);
        }
        
        return app;
    }

    internal static IEnumerable<Type> GetRecurringJobTypes()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(BaseRecurringJob).IsAssignableFrom(t));
    }
}