using System;
using System.IO;
using System.Linq;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Tests.Helpers;

public class GlobalVariableStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"lc-globals-{Guid.NewGuid():N}");

    private string FilePath => Path.Combine(_directory, GlobalVariableStore.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNone()
    {
        Assert.Empty(GlobalVariableStore.Load(FilePath));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsOrderedByName()
    {
        GlobalVariableStore.Save(
        [
            new GlobalVariable { Name = "LicenseServer", Value = "license.lan:27000", Description = "FlexLM host" },
            new GlobalVariable { Name = "CdKeyPrefix", Value = "line one\nline 'two'" },
        ], FilePath);

        var loaded = GlobalVariableStore.Load(FilePath);

        Assert.Equal(["CdKeyPrefix", "LicenseServer"], loaded.Select(v => v.Name));
        Assert.Equal("line one\nline 'two'", loaded[0].Value);
        Assert.Null(loaded[0].Description);
        Assert.Equal("license.lan:27000", loaded[1].Value);
        Assert.Equal("FlexLM host", loaded[1].Description);
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        GlobalVariableStore.Save([new GlobalVariable { Name = "A", Value = "1" }], FilePath);
        GlobalVariableStore.Save([new GlobalVariable { Name = "B", Value = "2" }], FilePath);

        var loaded = GlobalVariableStore.Load(FilePath);

        Assert.Equal("B", Assert.Single(loaded).Name);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Deserialize_DropsNamelessAndDuplicateEntries_AndNullValuesBecomeEmpty()
    {
        var yaml = """
            - Name: Shared
              Value: first
            - Name: shared
              Value: second
            - Value: orphan
            - Name: Empty
            """;

        var loaded = GlobalVariableStore.Deserialize(yaml);

        Assert.Equal(["Empty", "Shared"], loaded.Select(v => v.Name));
        Assert.Equal(String.Empty, loaded[0].Value);
        Assert.Equal("first", loaded[1].Value);
    }

    [Theory]
    [InlineData("LicenseServer", true)]
    [InlineData("_private", true)]
    [InlineData("Key2", true)]
    [InlineData("2Key", false)]
    [InlineData("has space", false)]
    [InlineData("dash-name", false)]
    [InlineData("", false)]
    public void IsValidName(string name, bool expected)
    {
        Assert.Equal(expected, GlobalVariableStore.IsValidName(name));
    }
}
