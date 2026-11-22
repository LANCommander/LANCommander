using System.Security.Claims;
using LANCommander.SDK.Enums;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using DataMedia = LANCommander.Server.Data.Models.Media;
using DataUser = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.Tests.Endpoints;

/// <summary>
/// The launcher's profile page changes the player's alias and avatar through these.
/// </summary>
/// <remarks>
/// The handlers are invoked directly rather than over HTTP, which keeps these cases off the
/// fixture's shared authentication token, and each gets its own settings so toggling a permission
/// doesn't leak into other test classes.
/// </remarks>
[Collection("Application")]
public class ProfileEndpointsTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private async Task<DataUser> AddUserAsync() =>
        await GetService<UserService>().AddAsync(new DataUser
        {
            UserName = Unique("profile"),
            Approved = true,
        });

    private static ClaimsPrincipal SignedInAs(DataUser user) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, user.UserName!)], "Test"));

    private static SettingsProvider<Settings.Settings> SettingsWith(bool allowAliasChange = true, bool allowAvatarUpload = true)
    {
        var settings = new Settings.Settings();
        settings.Server.Authentication.AllowAliasChange = allowAliasChange;
        settings.Server.Authentication.AllowAvatarUpload = allowAvatarUpload;

        return new SettingsProvider<Settings.Settings>(
            new StubOptionsMonitor<Settings.Settings>(settings),
            Path.Combine(Path.GetTempPath(), $"profile-settings-{Guid.NewGuid():N}.yml"));
    }

    private sealed class StubOptionsMonitor<T>(T value) : Microsoft.Extensions.Options.IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static IFormFile FormFile(byte[] contents, string fileName = "avatar.png") =>
        new FormFile(new MemoryStream(contents), 0, contents.Length, "file", fileName);

    private static byte[] Png(int width = 8, int height = 8)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(200, 40, 40));
        using var stream = new MemoryStream();

        image.SaveAsPng(stream);

        return stream.ToArray();
    }

    private async Task<IResult> UploadAsync(DataUser user, IFormFile file, SettingsProvider<Settings.Settings>? settings = null) =>
        await ProfileEndpoints.UploadAvatarAsync(
            file,
            SignedInAs(user),
            GetService<UserService>(),
            GetService<MediaService>(),
            settings ?? SettingsWith(),
            GetService<ILoggerFactory>(),
            CancellationToken.None);

    private Task<DataMedia?> AvatarOfAsync(DataUser user) =>
        GetService<MediaService>().FirstOrDefaultAsync(m => m.Type == MediaType.Avatar && m.UserId == user.Id);

    [Fact]
    public async Task AnAliasIsTrimmedAndStored()
    {
        var user = await AddUserAsync();

        var result = await ProfileEndpoints.ChangeAliasAsync(
            new ChangeAliasRequest { Alias = "  Sniper  " }, SignedInAs(user), GetService<UserService>(), SettingsWith());

        result.ShouldBeOfType<Ok<string>>().Value.ShouldBe("Sniper");
        (await GetService<UserService>().GetAsync(user.Id))!.Alias.ShouldBe("Sniper");
    }

    [Fact]
    public async Task ABlankAliasClearsIt()
    {
        var user = await AddUserAsync();
        var userService = GetService<UserService>();

        await ProfileEndpoints.ChangeAliasAsync(new ChangeAliasRequest { Alias = "Sniper" }, SignedInAs(user), userService, SettingsWith());

        var result = await ProfileEndpoints.ChangeAliasAsync(
            new ChangeAliasRequest { Alias = "   " }, SignedInAs(user), userService, SettingsWith());

        result.ShouldBeOfType<Ok<string>>().Value.ShouldBe(string.Empty);
        (await userService.GetAsync(user.Id))!.Alias.ShouldBeNull();
    }

    [Fact]
    public async Task AliasChangesCanBeTurnedOff()
    {
        var user = await AddUserAsync();

        var result = await ProfileEndpoints.ChangeAliasAsync(
            new ChangeAliasRequest { Alias = "Sniper" }, SignedInAs(user), GetService<UserService>(), SettingsWith(allowAliasChange: false));

        result.ShouldBeOfType<StatusCodeHttpResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        (await GetService<UserService>().GetAsync(user.Id))!.Alias.ShouldBeNull();
    }

    [Fact]
    public async Task AnUploadedImageBecomesTheAvatar()
    {
        await EnsureStorageLocationsExistAsync();

        var user = await AddUserAsync();

        // Named as a JPEG; the type comes from the contents
        var result = await UploadAsync(user, FormFile(Png(), "avatar.jpg"));

        result.StatusCode().ShouldBe(StatusCodes.Status200OK);

        var avatar = (await AvatarOfAsync(user)).ShouldNotBeNull();
        avatar.MimeType.ShouldBe("image/png");
        File.Exists(MediaService.GetMediaPath(avatar)).ShouldBeTrue();
    }

    [Fact]
    public async Task ReplacingTheAvatarGivesItANewFile()
    {
        await EnsureStorageLocationsExistAsync();

        var user = await AddUserAsync();

        await UploadAsync(user, FormFile(Png(8, 8)));
        var first = (await AvatarOfAsync(user))!;
        var firstPath = MediaService.GetMediaPath(first);

        await UploadAsync(user, FormFile(Png(16, 16)));
        var second = (await AvatarOfAsync(user))!;

        second.Id.ShouldBe(first.Id);
        second.FileId.ShouldNotBe(first.FileId);
        second.Crc32.ShouldNotBe(first.Crc32);
        File.Exists(firstPath).ShouldBeFalse();
        File.Exists(MediaService.GetMediaPath(second)).ShouldBeTrue();
    }

    [Fact]
    public async Task SomethingThatIsNotAnImageIsRefused()
    {
        await EnsureStorageLocationsExistAsync();

        var user = await AddUserAsync();

        var result = await UploadAsync(user, FormFile("not an image"u8.ToArray()));

        result.StatusCode().ShouldBe(StatusCodes.Status415UnsupportedMediaType);
        (await AvatarOfAsync(user)).ShouldBeNull();
    }

    [Fact]
    public async Task AnImageOverTheSizeLimitIsRefused()
    {
        var user = await AddUserAsync();

        var settings = SettingsWith();
        settings.CurrentValue.Server.Media.GetMediaTypeConfig(MediaType.Avatar).MaxFileSize = 16;

        var result = await UploadAsync(user, FormFile(Png()), settings);

        result.StatusCode().ShouldBe(StatusCodes.Status413PayloadTooLarge);
        (await AvatarOfAsync(user)).ShouldBeNull();
    }

    [Fact]
    public async Task AvatarUploadsCanBeTurnedOff()
    {
        var user = await AddUserAsync();

        var result = await UploadAsync(user, FormFile(Png()), SettingsWith(allowAvatarUpload: false));

        result.StatusCode().ShouldBe(StatusCodes.Status403Forbidden);
        (await AvatarOfAsync(user)).ShouldBeNull();
    }

    [Fact]
    public async Task RemovingTheAvatarDeletesItAndItsFile()
    {
        await EnsureStorageLocationsExistAsync();

        var user = await AddUserAsync();

        await UploadAsync(user, FormFile(Png()));
        var path = MediaService.GetMediaPath((await AvatarOfAsync(user))!);

        var result = await ProfileEndpoints.RemoveAvatarAsync(
            SignedInAs(user), GetService<UserService>(), GetService<MediaService>(), SettingsWith());

        result.ShouldBeOfType<NoContent>();
        (await AvatarOfAsync(user)).ShouldBeNull();
        File.Exists(path).ShouldBeFalse();
    }
}

internal static class ResultStatusExtensions
{
    /// <summary>The status code of results that carry one, whatever their concrete type.</summary>
    public static int? StatusCode(this IResult result) => (result as IStatusCodeHttpResult)?.StatusCode;
}
