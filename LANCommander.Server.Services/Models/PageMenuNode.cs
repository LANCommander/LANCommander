namespace LANCommander.Server.Services.Models
{
    // Lightweight projection of a page used only to render the public sidebar
    // navigation. Carries just the title, route, and nested structure so the
    // full page contents don't need to be loaded or cached.
    public class PageMenuNode
    {
        public string Title { get; set; }
        public string Route { get; set; }
        public IReadOnlyList<PageMenuNode> Children { get; set; } = new List<PageMenuNode>();
    }
}
