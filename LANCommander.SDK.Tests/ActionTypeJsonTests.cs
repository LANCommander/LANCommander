using System.Text.Json;
using LANCommander.SDK.Enums;
using SdkAction = LANCommander.SDK.Models.Action;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.SDK.Tests;

/// <summary>
/// The API and manifests carry an action's type, not the primary flag it replaced, but launchers
/// still read the flag from servers and manifests that predate the type.
/// </summary>
public class ActionTypeJsonTests
{
    // What the server's minimal APIs write, and what the SDK reads them with
    private static readonly JsonSerializerOptions Server = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions Sdk = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void TheApiSendsTheTypeAndNotThePrimaryFlag()
    {
        var json = JsonSerializer.Serialize(new SdkAction { Name = "Setup", Type = ActionType.Config }, Server);

        Assert.Contains("\"type\":2", json);
        Assert.DoesNotContain("isPrimaryAction", json);
    }

    [Fact]
    public void AManifestSentAsJsonCarriesTheTypeAndNotThePrimaryFlag()
    {
        var json = JsonSerializer.Serialize(new ManifestAction { Name = "Play", Type = ActionType.Primary }, Server);

        Assert.Contains("\"type\":1", json);
        Assert.DoesNotContain("isPrimaryAction", json);
    }

    [Theory]
    [InlineData("{\"name\":\"Play\",\"type\":1}", ActionType.Primary)]
    [InlineData("{\"name\":\"Join\",\"type\":3}", ActionType.Join)]
    // A server from before action types
    [InlineData("{\"name\":\"Play\",\"isPrimaryAction\":true}", ActionType.Primary)]
    [InlineData("{\"name\":\"Editor\",\"isPrimaryAction\":false}", ActionType.Secondary)]
    public void ReadsTheTypeOrTheFlagOlderServersSend(string json, ActionType expected) =>
        Assert.Equal(expected, JsonSerializer.Deserialize<SdkAction>(json, Sdk)!.Type);
}
