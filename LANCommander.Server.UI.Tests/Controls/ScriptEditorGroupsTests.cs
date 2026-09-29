using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

/// <summary>The script editor lists scripts by owner: the one being edited first, then its game's redistributables, then the system's.</summary>
public class ScriptEditorGroupsTests
{
    private static readonly Game Game = new() { Id = Guid.NewGuid(), Title = "Natural Selection 2" };
    private static readonly Redistributable DirectX = new() { Id = Guid.NewGuid(), Name = "DirectX" };
    private static readonly Redistributable Vcredist = new() { Id = Guid.NewGuid(), Name = "Visual C++" };

    private static Script GameScript(string name, ScriptType type) =>
        new() { Id = Guid.NewGuid(), Name = name, Type = type, GameId = Game.Id, Game = Game };

    private static Script RedistScript(Redistributable redistributable, string name, ScriptType type) =>
        new() { Id = Guid.NewGuid(), Name = name, Type = type, RedistributableId = redistributable.Id, Redistributable = redistributable };

    private static Script SystemScript(string name, ScriptType type) =>
        new() { Id = Guid.NewGuid(), Name = name, Type = type };

    [Fact]
    public void GroupsByOwner_CurrentFirst_SystemLast()
    {
        var install = GameScript("Install", ScriptType.Install);

        var groups = ScriptEditorGroups.Build(
        [
            SystemScript("User registration", ScriptType.UserRegistration),
            RedistScript(Vcredist, "Install", ScriptType.Install),
            install,
            RedistScript(DirectX, "Install", ScriptType.Install),
            GameScript("Uninstall", ScriptType.Uninstall),
        ], install);

        Assert.Equal(["Natural Selection 2", "DirectX", "Visual C++", "System"], groups.Select(g => g.Name));
        Assert.Equal(["GAME", "REDIST", "REDIST", null], groups.Select(g => g.KindLabel));
        Assert.Equal(["Install", "Uninstall"], groups[0].Scripts.Select(s => s.Name));
    }

    [Fact]
    public void ARedistributableScript_PutsItsOwnerFirst()
    {
        var current = RedistScript(DirectX, "Install", ScriptType.Install);

        var groups = ScriptEditorGroups.Build([GameScript("Install", ScriptType.Install), current], current);

        Assert.Equal(ScriptOwnerKind.Redistributable, groups[0].Kind);
        Assert.Equal("DirectX", groups[0].Name);
    }

    [Fact]
    public void ScriptsWithEmptyOwnerIds_AreSystemScripts()
    {
        var script = new Script { Name = "Login", Type = ScriptType.UserLogin, GameId = Guid.Empty, RedistributableId = Guid.Empty };

        Assert.Equal(ScriptOwnerKind.System, ScriptEditorGroups.KindOf(script));
        Assert.Equal("system", ScriptEditorGroups.KeyOf(script));
    }

    [Theory]
    [InlineData(ScriptType.Install, "Install")]
    [InlineData(ScriptType.DetectInstall, "Detect")]
    [InlineData(ScriptType.NameChange, "Name")]
    [InlineData(ScriptType.KeyChange, "Key")]
    public void TypeLabel_IsShortEnoughForARow(ScriptType type, string expected) =>
        Assert.Equal(expected, ScriptEditorGroups.TypeLabel(type));
}
