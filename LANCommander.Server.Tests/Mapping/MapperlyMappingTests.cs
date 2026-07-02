using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Mappers;
using LANCommander.Server.Settings.Enums;
using Shouldly;
using SettingsModels = global::LANCommander.Server.Settings.Models;

namespace LANCommander.Server.Tests.Mapping;

/// <summary>
/// Regression tests for the Mapperly mappers (<see cref="SdkMapper"/> / <see cref="ManifestMapper"/>).
/// The original migration verified these mappers against an AutoMapper baseline for full field parity
/// (see git history); AutoMapper has since been removed, so this suite pins the computed / hand-written
/// members that encode real business rules and cannot be caught by Mapperly's compile-time diagnostics.
/// </summary>
public class MapperlyMappingTests
{
    private readonly SdkMapper _sdk = new();
    private readonly ManifestMapper _manifest = new();

    // ---- Forward 1:1 maps: smoke coverage (Mapperly emits compile-time diagnostics for unmapped
    // members; these confirm the generated maps execute without throwing on a fully-populated graph).

    [Fact] public void Sdk_Archive() => _sdk.ToSdk(SampleEntities.Archive()).ShouldNotBeNull();
    [Fact] public void Sdk_Company() => _sdk.ToSdk(SampleEntities.Company()).ShouldNotBeNull();
    [Fact] public void Sdk_Collection() => _sdk.ToSdk(SampleEntities.Collection()).ShouldNotBeNull();
    [Fact] public void Sdk_Engine() => _sdk.ToSdk(SampleEntities.Engine()).ShouldNotBeNull();
    [Fact] public void Sdk_GameSave() => _sdk.ToSdk(SampleEntities.GameSave()).ShouldNotBeNull();
    [Fact] public void Sdk_Genre() => _sdk.ToSdk(SampleEntities.Genre()).ShouldNotBeNull();
    [Fact] public void Sdk_Key() => _sdk.ToSdk(SampleEntities.Key()).ShouldNotBeNull();
    [Fact] public void Sdk_Media() => _sdk.ToSdk(SampleEntities.Media()).ShouldNotBeNull();
    [Fact] public void Sdk_MultiplayerMode() => _sdk.ToSdk(SampleEntities.MultiplayerMode()).ShouldNotBeNull();
    [Fact] public void Sdk_Platform() => _sdk.ToSdk(SampleEntities.Platform()).ShouldNotBeNull();
    [Fact] public void Sdk_PlaySession() => _sdk.ToSdk(SampleEntities.PlaySession()).ShouldNotBeNull();
    [Fact] public void Sdk_ServerConsole() => _sdk.ToSdk(SampleEntities.ServerConsole()).ShouldNotBeNull();
    [Fact] public void Sdk_ServerHttpPath() => _sdk.ToSdk(SampleEntities.ServerHttpPath()).ShouldNotBeNull();
    [Fact] public void Sdk_SavePath() => _sdk.ToSdk(SampleEntities.SavePath()).ShouldNotBeNull();
    [Fact] public void Sdk_Script() => _sdk.ToSdk(SampleEntities.Script()).ShouldNotBeNull();
    [Fact] public void Sdk_Tool() => _sdk.ToSdk(SampleEntities.Tool()).ShouldNotBeNull();
    [Fact] public void Sdk_User() => _sdk.ToSdk(SampleEntities.User()).ShouldNotBeNull();
    [Fact] public void Sdk_GameCustomField() => _sdk.ToSdk(SampleEntities.GameCustomField()).ShouldNotBeNull();
    [Fact] public void Sdk_GameExternalId() => _sdk.ToSdk(SampleEntities.GameExternalId()).ShouldNotBeNull();
    [Fact] public void Sdk_Tag() => _sdk.ToSdk(SampleEntities.Tag()).ShouldNotBeNull();
    [Fact] public void Sdk_Server() => _sdk.ToSdk(SampleEntities.Server()).ShouldNotBeNull();
    [Fact] public void Sdk_Action() => _sdk.ToSdk(SampleEntities.Action()).ShouldNotBeNull();
    [Fact] public void Sdk_Redistributable() => _sdk.ToSdk(SampleEntities.Redistributable()).ShouldNotBeNull();
    [Fact] public void Sdk_Game() => _sdk.ToSdk(SampleEntities.Game()).ShouldNotBeNull();
    [Fact] public void Sdk_DepotGame() => _sdk.ToDepotGame(SampleEntities.Game()).ShouldNotBeNull();
    [Fact] public void Sdk_EntityReference() => _sdk.ToEntityReference(SampleEntities.Game()).ShouldNotBeNull();
    [Fact] public void Sdk_ChatThread() => _sdk.ToSdk(SampleEntities.ChatThread()).ShouldNotBeNull();
    [Fact] public void Sdk_ChatMessage() => _sdk.ToSdk(SampleEntities.ChatMessage()).ShouldNotBeNull();

    [Fact] public void Manifest_Archive() => _manifest.ToManifest(SampleEntities.Archive()).ShouldNotBeNull();
    [Fact] public void Manifest_Collection() => _manifest.ToManifest(SampleEntities.Collection()).ShouldNotBeNull();
    [Fact] public void Manifest_Company() => _manifest.ToManifest(SampleEntities.Company()).ShouldNotBeNull();
    [Fact] public void Manifest_Engine() => _manifest.ToManifest(SampleEntities.Engine()).ShouldNotBeNull();
    [Fact] public void Manifest_GameCustomField() => _manifest.ToManifest(SampleEntities.GameCustomField()).ShouldNotBeNull();
    [Fact] public void Manifest_GameExternalId() => _manifest.ToManifest(SampleEntities.GameExternalId()).ShouldNotBeNull();
    [Fact] public void Manifest_Genre() => _manifest.ToManifest(SampleEntities.Genre()).ShouldNotBeNull();
    [Fact] public void Manifest_Key() => _manifest.ToManifest(SampleEntities.Key()).ShouldNotBeNull();
    [Fact] public void Manifest_Media() => _manifest.ToManifest(SampleEntities.Media()).ShouldNotBeNull();
    [Fact] public void Manifest_MultiplayerMode() => _manifest.ToManifest(SampleEntities.MultiplayerMode()).ShouldNotBeNull();
    [Fact] public void Manifest_Platform() => _manifest.ToManifest(SampleEntities.Platform()).ShouldNotBeNull();
    [Fact] public void Manifest_PlaySession() => _manifest.ToManifest(SampleEntities.PlaySession()).ShouldNotBeNull();
    [Fact] public void Manifest_SavePath() => _manifest.ToManifest(SampleEntities.SavePath()).ShouldNotBeNull();
    [Fact] public void Manifest_Script() => _manifest.ToManifest(SampleEntities.Script()).ShouldNotBeNull();
    [Fact] public void Manifest_ServerConsole() => _manifest.ToManifest(SampleEntities.ServerConsole()).ShouldNotBeNull();
    [Fact] public void Manifest_ServerHttpPath() => _manifest.ToManifest(SampleEntities.ServerHttpPath()).ShouldNotBeNull();
    [Fact] public void Manifest_Tag() => _manifest.ToManifest(SampleEntities.Tag()).ShouldNotBeNull();
    [Fact] public void Manifest_Save() => _manifest.ToManifest(SampleEntities.GameSave()).ShouldNotBeNull();
    [Fact] public void Manifest_Action() => _manifest.ToManifest(SampleEntities.Action()).ShouldNotBeNull();
    [Fact] public void Manifest_Server() => _manifest.ToManifest(SampleEntities.Server()).ShouldNotBeNull();
    [Fact] public void Manifest_Tool() => _manifest.ToManifest(SampleEntities.Tool()).ShouldNotBeNull();
    [Fact] public void Manifest_Redistributable() => _manifest.ToManifest(SampleEntities.Redistributable()).ShouldNotBeNull();
    [Fact] public void Manifest_Game() => _manifest.ToManifest(SampleEntities.Game()).ShouldNotBeNull();

    [Fact]
    public void Sdk_AuthenticationProvider_MapsScalars()
    {
        var src = new SettingsModels.AuthenticationProvider
        {
            Name = "Provider",
            Slug = "provider",
            Type = AuthenticationProviderType.OAuth2,
            Color = "#fff",
            Icon = "icon",
        };
        var mapped = _sdk.ToSdk(src);
        mapped.Name.ShouldBe(src.Name);
        mapped.Slug.ShouldBe(src.Slug);
    }

    // ---- Hand-written reverse maps -------------------------------------------------------------

    [Fact]
    public void Sdk_ChatThread_Reverse()
    {
        var sdkSrc = _sdk.ToSdk(SampleEntities.ChatThread());
        var actual = _sdk.ToData(sdkSrc);

        actual.Id.ShouldBe(sdkSrc.Id);
        actual.Name.ShouldBe(sdkSrc.Name);
        actual.Participants.ShouldNotBeNull();
        actual.Participants.Count.ShouldBe(sdkSrc.Participants.Count);
        actual.Participants.Select(p => p.Id).ShouldBe(sdkSrc.Participants.Select(p => p.Id));
        actual.Participants.Select(p => p.UserName).ShouldBe(sdkSrc.Participants.Select(p => p.UserName));
    }

    [Fact]
    public void Sdk_ChatMessage_Reverse()
    {
        var sdkSrc = _sdk.ToSdk(SampleEntities.ChatMessage());
        var actual = _sdk.ToData(sdkSrc);

        actual.Id.ShouldBe(sdkSrc.Id);
        actual.Content.ShouldBe(sdkSrc.Content);
        actual.CreatedBy.ShouldNotBeNull();
        actual.CreatedBy.Id.ShouldBe(sdkSrc.UserId);
        actual.CreatedBy.UserName.ShouldBe(sdkSrc.UserName);
    }

    [Fact]
    public void Manifest_Save_Reverse()
    {
        var src = _manifest.ToManifest(SampleEntities.GameSave());
        var actual = _manifest.ToData(src);

        actual.Id.ShouldBe(src.Id);
        // Manifest.Save flattens User -> UserName; the reverse cannot rehydrate a Data.User from a name.
        actual.User.ShouldBeNull();
    }

    // ---- Computed / derived members ------------------------------------------------------------

    [Fact]
    public void Sdk_Game_DependentGames_AreIds()
    {
        var game = SampleEntities.Game();
        var expectedIds = game.DependentGames.Select(g => g.Id).ToList();
        var mapped = _sdk.ToSdk(game);
        mapped.DependentGames.ShouldBe(expectedIds);
    }

    [Fact]
    public void Sdk_Game_Scripts_ExcludePackage()
    {
        var mapped = _sdk.ToSdk(SampleEntities.Game());
        mapped.Scripts.ShouldNotBeNull();
        mapped.Scripts.ShouldAllBe(s => s.Type != ScriptType.Package);
        mapped.Scripts.Count().ShouldBe(1);
    }

    [Fact]
    public void Sdk_Redistributable_Version_IsLatestByCreatedOn()
    {
        var mapped = _sdk.ToSdk(SampleEntities.Redistributable());
        mapped.Version.ShouldBe("2.0.0");
        mapped.Scripts.ShouldAllBe(s => s.Type != ScriptType.Package);
    }

    [Fact]
    public void Sdk_DepotGame_Cover_IsCoverMedia()
    {
        var mapped = _sdk.ToDepotGame(SampleEntities.Game());
        mapped.Cover.ShouldNotBeNull();
        mapped.Cover.Type.ShouldBe(MediaType.Cover);
    }

    [Fact]
    public void Manifest_Game_Version_BaseGame_BaseGameId()
    {
        var game = SampleEntities.Game();
        var mapped = _manifest.ToManifest(game);
        var expectedVersion = game.Versions
            .OrderByDescending(v => v.SortOrder).ThenByDescending(v => v.CreatedOn).First().Version;
        mapped.Version.ShouldBe(expectedVersion);
        mapped.BaseGame.ShouldBe(game.BaseGame!.Title);
        mapped.BaseGameId.ShouldBe(game.BaseGameId!.Value);
    }

    [Fact]
    public void Manifest_Game_BaseGameId_EmptyWhenNull()
    {
        var game = SampleEntities.Game();
        game.BaseGameId = null;
        game.BaseGame = null;
        var mapped = _manifest.ToManifest(game);
        mapped.BaseGameId.ShouldBe(Guid.Empty);
        mapped.BaseGame.ShouldBeNull();
    }

    [Fact]
    public void ChatThread_LastActivityOn_MaxMessage_Then_UpdatedOn()
    {
        var withMessages = SampleEntities.ChatThread(withMessages: true);
        var expectedMax = withMessages.Messages!.Max(m => m.CreatedOn);
        _sdk.ToSdk(withMessages).LastActivityOn.ShouldBe(expectedMax);

        var noMessages = SampleEntities.ChatThread(withMessages: false);
        _sdk.ToSdk(noMessages).LastActivityOn.ShouldBe(noMessages.UpdatedOn);
    }

    [Fact]
    public void Manifest_Tool_Games_Ignored()
    {
        var mapped = _manifest.ToManifest(SampleEntities.Tool());
        mapped.Games.ShouldBeEmpty();
    }

    [Fact]
    public void Sdk_NullCollection_Passthrough()
    {
        var game = SampleEntities.Game();
        game.Scripts = null!;
        // Mapperly is configured (AllowNullPropertyAssignment) to pass null collections through.
        var actual = _sdk.ToSdk(game);
        actual.Scripts.ShouldBeNull();
    }

    [Fact]
    public void Sdk_Game_NullCollections_DoesNotThrow()
    {
        // Game.Collections is non-nullable on the entity, but EF leaves it null when the navigation
        // is not Include()d (as the /api/Games list endpoint does). Regression for the resulting NRE
        // in the Mapperly-generated collection mapping.
        var game = SampleEntities.Game();
        game.Collections = null!;
        var mapped = _sdk.ToSdk(game);
        mapped.Collections.ShouldBeNull();
    }

    [Fact]
    public void Manifest_Game_NullCollections_DoesNotThrow()
    {
        var game = SampleEntities.Game();
        game.Collections = null!;
        var mapped = _manifest.ToManifest(game);
        mapped.Collections.ShouldBeNull();
    }

    [Fact]
    public void Sdk_GameVersion_FlattensArchive()
    {
        var withArchive = SampleEntities.GameVersion(withArchive: true);
        var mapped = _sdk.ToSdk(withArchive);
        mapped.ArchiveId.ShouldBe(withArchive.Archive!.Id);
        mapped.CompressedSize.ShouldBe(withArchive.Archive.CompressedSize);
        mapped.UncompressedSize.ShouldBe(withArchive.Archive.UncompressedSize);

        var noArchive = SampleEntities.GameVersion(withArchive: false);
        _sdk.ToSdk(noArchive).ShouldNotBeNull();
    }
}
