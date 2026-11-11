namespace LANCommander.Server.Settings.Models;

/// <summary>How the public Pages site looks: its branding, header, footer and outside links.</summary>
public class PageSettings
{
    /// <summary>The site's name, shown in the header beside the logo. Empty shows no title.</summary>
    public string Title { get; set; } = "";

    /// <summary>URL of the logo shown in the header, usually an uploaded page image. Empty shows no logo.</summary>
    public string Logo { get; set; } = "";

    /// <summary>Markdown shown above every page's content.</summary>
    public string Header { get; set; } = "";

    /// <summary>Markdown shown at the bottom of every page.</summary>
    public string Footer { get; set; } = "";

    /// <summary>Links to outside places, such as a Discord server, shown in the header.</summary>
    public List<PageLink> Links { get; set; } = new();
}

public class PageLink
{
    public string Name { get; set; } = "";

    public string Url { get; set; } = "";

    /// <summary>Name of the icon shown beside the link, such as DiscordLogo. Empty shows no icon.</summary>
    public string Icon { get; set; } = "";
}
