using System.Security.Claims;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Logging;
using Shouldly;
using DataGame = LANCommander.Server.Data.Models.Game;
using DataIssue = LANCommander.Server.Data.Models.Issue;

namespace LANCommander.Server.Tests.Endpoints;

/// <summary>
/// The launcher reports issues through Open and lists a game's issues on its Issues tab.
/// </summary>
/// <remarks>
/// The handlers are invoked directly rather than over HTTP, which keeps these cases off the
/// fixture's shared authentication token.
/// </remarks>
[Collection("Application")]
public class IssueEndpointsTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private static ClaimsPrincipal Player() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "player")], "Test"));

    private static ClaimsPrincipal Administrator() =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, RoleService.AdministratorRoleName),
            ], "Test"));

    private Task<DataGame> AddGameAsync(bool published = true) =>
        GetService<GameService>().AddAsync(new DataGame { Title = Unique("Game"), Published = published });

    private Task<DataIssue> AddIssueAsync(DataGame game, string title, bool pinned = false, bool resolved = false) =>
        GetService<IssueService>().AddAsync(new DataIssue
        {
            GameId = game.Id,
            Title = title,
            Description = $"{title} description",
            IsPinned = pinned,
            ResolvedOn = resolved ? DateTime.UtcNow : null,
        });

    private async Task<IResult> GetByGameAsync(Guid gameId, ClaimsPrincipal user) =>
        await IssueEndpoints.GetByGameAsync(
            GetService<IssueService>(),
            GetService<GameService>(),
            user,
            gameId);

    private static List<SDK.Models.Issue> Issues(IResult result) =>
        result.ShouldBeOfType<Ok<ICollection<SDK.Models.Issue>>>().Value!.ToList();

    [Fact]
    public async Task OpenStoresTheReportersTitleAndDescription()
    {
        var game = await AddGameAsync();

        var result = await IssueEndpoints.OpenAsync(
            GetService<IssueService>(),
            GetService<GameService>(),
            GetService<ILogger<DataIssue>>(),
            new SDK.Models.Issue { GameId = game.Id, Title = "  Crashes on start  ", Description = "Every time" });

        result.ShouldBeOfType<Ok<bool>>().Value.ShouldBeTrue();

        var issue = Issues(await GetByGameAsync(game.Id, Player())).ShouldHaveSingleItem();

        issue.Title.ShouldBe("Crashes on start");
        issue.Description.ShouldBe("Every time");
        issue.IsPinned.ShouldBeFalse();
        issue.IsResolved.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenTruncatesLongTitles()
    {
        var game = await AddGameAsync();

        await IssueEndpoints.OpenAsync(
            GetService<IssueService>(),
            GetService<GameService>(),
            GetService<ILogger<DataIssue>>(),
            new SDK.Models.Issue { GameId = game.Id, Title = new string('x', 300), Description = "Long" });

        Issues(await GetByGameAsync(game.Id, Player())).ShouldHaveSingleItem().Title.Length.ShouldBe(128);
    }

    [Fact]
    public async Task IssuesArePinnedFirstThenUnresolvedThenNewest()
    {
        var game = await AddGameAsync();

        await AddIssueAsync(game, "older open");
        await AddIssueAsync(game, "resolved", resolved: true);
        await AddIssueAsync(game, "pinned resolved", pinned: true, resolved: true);
        await AddIssueAsync(game, "newer open");

        var other = await AddGameAsync();
        await AddIssueAsync(other, "another game's issue");

        Issues(await GetByGameAsync(game.Id, Player()))
            .Select(i => i.Title)
            .ShouldBe(["pinned resolved", "newer open", "older open", "resolved"]);
    }

    [Fact]
    public async Task IssuesOnUnpublishedGamesAreHiddenFromPlayers()
    {
        var game = await AddGameAsync(published: false);

        await AddIssueAsync(game, "hidden");

        (await GetByGameAsync(game.Id, Player())).ShouldBeOfType<NotFound>();
        Issues(await GetByGameAsync(game.Id, Administrator())).ShouldHaveSingleItem().Title.ShouldBe("hidden");
    }

    // ── Attachments ─────────────────────────────────────────────────────────────────────────

    private static HttpRequest Form(Guid gameId, string title, params (string Name, string Contents)[] files)
    {
        var context = new DefaultHttpContext();
        var formFiles = new FormFileCollection();

        foreach (var (name, contents) in files)
        {
            var bytes = Encoding.UTF8.GetBytes(contents);

            formFiles.Add(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "attachments", name));
        }

        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["gameId"] = gameId.ToString(),
            ["title"] = title,
            ["description"] = "See the attached files",
        }, formFiles);

        return context.Request;
    }

    private async Task<IResult> CreateAsync(HttpRequest request, ClaimsPrincipal? user = null, Settings.Models.IssueSettings? limits = null) =>
        await IssueEndpoints.CreateAsync(
            request,
            user ?? Player(),
            GetService<IssueService>(),
            GetService<IssueAttachmentService>(),
            GetService<GameService>(),
            limits == null ? GetService<SettingsProvider<Settings.Settings>>() : SettingsWith(limits),
            GetService<ILogger<DataIssue>>());

    /// <summary>
    /// A settings provider with the given issue limits, so tests don't change the shared fixture's
    /// settings under the other test classes.
    /// </summary>
    private static SettingsProvider<Settings.Settings> SettingsWith(Settings.Models.IssueSettings issues)
    {
        var settings = new Settings.Settings();
        settings.Server.Issues = issues;

        return new SettingsProvider<Settings.Settings>(
            new StubOptionsMonitor<Settings.Settings>(settings),
            Path.Combine(Path.GetTempPath(), $"issue-settings-{Guid.NewGuid():N}.yml"));
    }

    private sealed class StubOptionsMonitor<T>(T value) : Microsoft.Extensions.Options.IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private async Task<(IResult Result, HttpResponse Response)> DownloadAsync(Guid attachmentId, ClaimsPrincipal? user = null)
    {
        var response = new DefaultHttpContext().Response;

        var result = await IssueEndpoints.DownloadAttachmentAsync(
            response,
            user ?? Player(),
            GetService<IssueAttachmentService>(),
            GetService<IssueService>(),
            GetService<GameService>(),
            attachmentId);

        return (result, response);
    }

    [Fact]
    public async Task CreateSavesAttachmentsAndListsThemWithTheIssue()
    {
        var game = await AddGameAsync();

        var created = (await CreateAsync(Form(game.Id, "Crashes on start", ("shot.png", "png bytes"), ("game.log", "log line"))))
            .ShouldBeOfType<Ok<SDK.Models.Issue>>().Value!;

        created.Attachments.Select(a => a.FileName).ShouldBe(["shot.png", "game.log"]);

        var listed = Issues(await GetByGameAsync(game.Id, Player())).ShouldHaveSingleItem();

        listed.Title.ShouldBe("Crashes on start");
        listed.Attachments.Select(a => (a.FileName, a.Size)).ShouldBe([("shot.png", 9L), ("game.log", 8L)]);

        var service = GetService<IssueAttachmentService>();
        var log = await service.GetAsync(listed.Attachments.Last().Id);

        (await File.ReadAllTextAsync(service.GetPath(log))).ShouldBe("log line");
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("page.html")]
    [InlineData("vector.svg")]
    public async Task CreateRefusesFilesThatArentOnTheAllowList(string fileName)
    {
        var game = await AddGameAsync();

        (await CreateAsync(Form(game.Id, "Bad file", ("game.log", "fine"), (fileName, "not fine"))))
            .ShouldBeOfType<BadRequest<string>>();

        // Nothing is saved when any file is refused
        Issues(await GetByGameAsync(game.Id, Player())).ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateRefusesTooManyFiles()
    {
        var game = await AddGameAsync();

        var files = Enumerable.Range(0, SDK.Models.IssueAttachmentLimits.DefaultMaxFiles + 1)
            .Select(i => ($"log{i}.txt", "x"))
            .ToArray();

        (await CreateAsync(Form(game.Id, "Many files", files))).ShouldBeOfType<BadRequest<string>>();
    }

    [Fact]
    public async Task CreateRequiresATitle()
    {
        var game = await AddGameAsync();

        (await CreateAsync(Form(game.Id, "   "))).ShouldBeOfType<BadRequest<string>>();
    }

    [Fact]
    public async Task DownloadsPickTheirOwnContentTypeAndNeverSniff()
    {
        var game = await AddGameAsync();

        var issue = (await CreateAsync(Form(game.Id, "Files",
                ("shot.png", "png bytes"),
                ("markup.log", "<script>alert(1)</script>"),
                ("bundle.zip", "zip bytes"))))
            .ShouldBeOfType<Ok<SDK.Models.Issue>>().Value!;

        var attachments = issue.Attachments.ToDictionary(a => a.FileName, a => a.Id);

        var (image, imageResponse) = await DownloadAsync(attachments["shot.png"]);
        image.ShouldBeOfType<FileStreamHttpResult>().ContentType.ShouldBe("image/png");
        imageResponse.Headers.ContentDisposition.ToString().ShouldStartWith("inline");
        imageResponse.Headers.XContentTypeOptions.ToString().ShouldBe("nosniff");

        // Markup in a log is served as plain text, so a browser never renders it
        var (log, _) = await DownloadAsync(attachments["markup.log"]);
        log.ShouldBeOfType<FileStreamHttpResult>().ContentType.ShouldBe("text/plain; charset=utf-8");

        var (archive, archiveResponse) = await DownloadAsync(attachments["bundle.zip"]);
        archive.ShouldBeOfType<FileStreamHttpResult>().ContentType.ShouldBe("application/octet-stream");
        archiveResponse.Headers.ContentDisposition.ToString().ShouldStartWith("attachment");

        foreach (var result in new[] { image, log, archive })
            await ((FileStreamHttpResult)result).FileStream.DisposeAsync();
    }

    [Fact]
    public async Task AttachmentsOnUnpublishedGamesAreHiddenFromPlayers()
    {
        var game = await AddGameAsync(published: false);

        var issue = (await CreateAsync(Form(game.Id, "Hidden", ("game.log", "log")), Administrator()))
            .ShouldBeOfType<Ok<SDK.Models.Issue>>().Value!;

        var attachmentId = issue.Attachments.Single().Id;

        (await DownloadAsync(attachmentId, Player())).Result.ShouldBeOfType<NotFound>();

        var (result, _) = await DownloadAsync(attachmentId, Administrator());
        await using var _ = result.ShouldBeOfType<FileStreamHttpResult>().FileStream;
    }

    [Fact]
    public async Task DeletingAnIssueDeletesItsFiles()
    {
        var game = await AddGameAsync();

        var created = (await CreateAsync(Form(game.Id, "Delete me", ("game.log", "log"))))
            .ShouldBeOfType<Ok<SDK.Models.Issue>>().Value!;

        var attachmentService = GetService<IssueAttachmentService>();
        var path = attachmentService.GetPath(await attachmentService.GetAsync(created.Attachments.Single().Id));

        File.Exists(path).ShouldBeTrue();

        var issueService = GetService<IssueService>();
        await issueService.DeleteAsync(await issueService.GetAsync(created.Id));

        File.Exists(path).ShouldBeFalse();
        (await attachmentService.GetAsync(created.Attachments.Single().Id)).ShouldBeNull();
    }

    // ── Configured limits ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUsesTheConfiguredFileCount()
    {
        var game = await AddGameAsync();
        var limits = new Settings.Models.IssueSettings { MaxAttachments = 1, MaxAttachmentSize = 25 };

        (await CreateAsync(Form(game.Id, "Two files", ("a.log", "a"), ("b.log", "b")), limits: limits))
            .ShouldBeOfType<BadRequest<string>>().Value.ShouldBe("You can attach 1 file.");

        (await CreateAsync(Form(game.Id, "One file", ("a.log", "a")), limits: limits))
            .ShouldBeOfType<Ok<SDK.Models.Issue>>();
    }

    [Fact]
    public async Task CreateUsesTheConfiguredFileSize()
    {
        var game = await AddGameAsync();
        var limits = new Settings.Models.IssueSettings { MaxAttachments = 5, MaxAttachmentSize = 1 };

        var justOver = new string('x', 1024 * 1024 + 1);

        (await CreateAsync(Form(game.Id, "Big file", ("big.log", justOver)), limits: limits))
            .ShouldBeOfType<BadRequest<string>>().Value.ShouldBe("big.log is larger than 1 MB.");
    }

    [Fact]
    public async Task ZeroAttachmentsTurnsThemOffButStillAcceptsReports()
    {
        var game = await AddGameAsync();
        var limits = new Settings.Models.IssueSettings { MaxAttachments = 0 };

        (await CreateAsync(Form(game.Id, "With a file", ("a.log", "a")), limits: limits))
            .ShouldBeOfType<BadRequest<string>>().Value.ShouldBe("This server doesn't accept attachments.");

        (await CreateAsync(Form(game.Id, "No files"), limits: limits))
            .ShouldBeOfType<Ok<SDK.Models.Issue>>();
    }

    [Fact]
    public void LimitsEndpointReportsTheSettingsInBytes()
    {
        var limits = IssueEndpoints.GetLimits(SettingsWith(new Settings.Models.IssueSettings { MaxAttachments = 3, MaxAttachmentSize = 10 }))
            .ShouldBeOfType<Ok<SDK.Models.IssueAttachmentLimits>>().Value!;

        limits.MaxFiles.ShouldBe(3);
        limits.MaxFileSize.ShouldBe(10L * 1024 * 1024);
        limits.Enabled.ShouldBeTrue();
    }
}
