namespace LANCommander.Server.Services.Pages;

public record PageSearchResult
{
    public Guid Id { get; init; }

    public string Title { get; init; } = "";

    public string Route { get; init; } = "";

    /// <summary>Titles of the pages above this one, outermost first</summary>
    public IReadOnlyList<string> Path { get; init; } = [];

    /// <summary>Plain text around the first match, with an ellipsis where it was cut</summary>
    public string Snippet { get; init; } = "";
}
