namespace LANCommander.Server.Startup;

public static class Razor
{
    public static WebApplicationBuilder AddRazor(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddMvc(static options => options.EnableEndpointRouting = false)
            .AddRazorOptions(static options =>
            {
                options.ViewLocationFormats.Clear();
                options.ViewLocationFormats.Add("/UI/Views/{1}/{0}.cshtml");
                options.ViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
                options.ViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");

                options.AreaViewLocationFormats.Clear();
                options.AreaViewLocationFormats.Add("/Areas/{2}/Views/{1}/{0}.cshtml");
                options.AreaViewLocationFormats.Add("/Areas/{2}/Views/Shared/{0}.cshtml");
                options.AreaViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
                options.AreaViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");

                options.PageViewLocationFormats.Clear();
                options.PageViewLocationFormats.Add("/UI/Pages/{1}/{0}.cshtml");
                options.PageViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");
                options.PageViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");

                options.AreaPageViewLocationFormats.Clear();
                options.AreaPageViewLocationFormats.Add("/Areas/{2}/Pages/{1}/{0}.cshtml");
                options.AreaPageViewLocationFormats.Add("/Areas/{2}/Pages/Shared/{0}.cshtml");
                options.AreaPageViewLocationFormats.Add("/Areas/{2}/Views/Shared/{0}.cshtml");
                options.AreaPageViewLocationFormats.Add("/UI/Pages/Shared/{0}.cshtml");
                options.AreaPageViewLocationFormats.Add("/UI/Views/Shared/{0}.cshtml");
            });

        builder.Services.AddRazorPages(static options => options.RootDirectory = "/UI/Pages");

        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        return builder;
    }
}