using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests.PowerShell.Debugging;

public class ScriptIdentityTests
{
    [Theory]
    [InlineData(ScriptType.Install)]
    [InlineData(ScriptType.Uninstall)]
    [InlineData(ScriptType.NameChange)]
    [InlineData(ScriptType.KeyChange)]
    [InlineData(ScriptType.DetectInstall)]
    [InlineData(ScriptType.BeforeStart)]
    [InlineData(ScriptType.AfterStop)]
    [InlineData(ScriptType.RunWrapper)]
    public void TryParseScriptFilePath_RoundTripsGetScriptFilePath(ScriptType type)
    {
        var installDirectory = Path.Combine(Path.GetTempPath(), "Games", "Some Game");
        var id = Guid.NewGuid();

        var path = ScriptHelper.GetScriptFilePath(installDirectory, id, type);

        Assert.True(ScriptHelper.TryParseScriptFilePath(path, out var parsedDirectory, out var parsedId, out var parsedType));
        Assert.Equal(Path.GetFullPath(installDirectory), parsedDirectory);
        Assert.Equal(id, parsedId);
        Assert.Equal(type, parsedType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Games\Foo\Install.ps1")]
    [InlineData(@"C:\Games\Foo\.lancommander\not-a-guid\Install.ps1")]
    [InlineData(@"C:\Games\Foo\.lancommander\5f0e2b5c-9d2b-4a53-8f8a-2f0d7d5a3c11\Readme.txt")]
    [InlineData(@"C:\Games\Foo\elsewhere\5f0e2b5c-9d2b-4a53-8f8a-2f0d7d5a3c11\Install.ps1")]
    public void TryParseScriptFilePath_RejectsOtherPaths(string? path)
    {
        Assert.False(ScriptHelper.TryParseScriptFilePath(path!, out _, out _, out _));
    }

    [Fact]
    public void StripRequiresAdminHeader_RemovesEveryPrependedHeader()
    {
        const string body = "Write-Host 'hi'";
        const string header = "#Requires -RunAsAdministrator\r\n\r\n";

        Assert.Equal(body, ScriptHelper.StripRequiresAdminHeader(header + header + body));
        Assert.Equal(body, ScriptHelper.StripRequiresAdminHeader(body));
    }

    [Fact]
    public void HashContents_IsStableAndSensitiveToChanges()
    {
        Assert.Equal(ScriptHelper.HashContents("abc"), ScriptHelper.HashContents("abc"));
        Assert.NotEqual(ScriptHelper.HashContents("abc"), ScriptHelper.HashContents("abd"));
    }
}
