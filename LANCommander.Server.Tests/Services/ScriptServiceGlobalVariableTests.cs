using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// The global script variables <see cref="ScriptService"/> keeps (read by scripts as <c>$var:Name</c>)
/// and <c>GET /api/Scripts/Variables</c>, which clients sync them from. Each test works on its own file.
/// </summary>
[Collection("Application")]
public class ScriptServiceGlobalVariableTests : BaseTest, IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"lc-variables-{Guid.NewGuid():N}");
    private readonly ScriptService _service;

    public ScriptServiceGlobalVariableTests(ApplicationFixture fixture) : base(fixture)
    {
        _service = GetService<ScriptService>();
        _service.GlobalVariablesPath = Path.Combine(_directory, GlobalVariableStore.FileName);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);

        return Task.CompletedTask;
    }

    [Fact]
    public void Save_AddsVariable_TrimmingItsName()
    {
        _service.SaveGlobalVariable(new GlobalVariable { Name = "  LicenseServer ", Value = "license.lan", Description = "Shared host" });

        var variable = _service.GetGlobalVariable("licenseserver").ShouldNotBeNull();

        variable.Name.ShouldBe("LicenseServer");
        variable.Value.ShouldBe("license.lan");
        variable.Description.ShouldBe("Shared host");
    }

    [Fact]
    public void Save_WithOriginalName_RenamesInPlace()
    {
        _service.SaveGlobalVariable(new GlobalVariable { Name = "Old", Value = "1" });
        _service.SaveGlobalVariable(new GlobalVariable { Name = "Other", Value = "2" });

        _service.SaveGlobalVariable(new GlobalVariable { Name = "New", Value = "3" }, "Old");

        _service.GetGlobalVariables().Select(v => v.Name).ShouldBe(["New", "Other"]);
        _service.GetGlobalVariable("New")!.Value.ShouldBe("3");
    }

    [Fact]
    public void Save_SameNameAsOriginal_UpdatesValue()
    {
        _service.SaveGlobalVariable(new GlobalVariable { Name = "Key", Value = "1" });

        _service.SaveGlobalVariable(new GlobalVariable { Name = "Key", Value = "2" }, "Key");

        _service.GetGlobalVariables().ShouldHaveSingleItem().Value.ShouldBe("2");
    }

    [Fact]
    public void Save_DuplicateName_Throws()
    {
        _service.SaveGlobalVariable(new GlobalVariable { Name = "Key", Value = "1" });

        Should.Throw<ArgumentException>(() => _service.SaveGlobalVariable(new GlobalVariable { Name = "KEY", Value = "2" }));

        _service.GetGlobalVariables().ShouldHaveSingleItem().Value.ShouldBe("1");
    }

    [Theory]
    [InlineData("", "A name is required.")]
    [InlineData("1st", "Use letters, digits and underscores only, not starting with a digit.")]
    [InlineData("var:Key", "Use letters, digits and underscores only, not starting with a digit.")]
    public void Validate_RejectsUnusableNames(string name, string expected)
    {
        _service.ValidateGlobalVariable(name).ShouldBe(expected);
    }

    [Theory]
    [InlineData("InstallDirectory")]
    [InlineData("Host")]
    public void Validate_AllowsNamesOfOrdinaryVariables(string name)
    {
        // $var:InstallDirectory and $InstallDirectory are different variables
        _service.ValidateGlobalVariable(name).ShouldBeNull();
    }

    [Fact]
    public void Delete_RemovesOnlyThatVariable()
    {
        _service.SaveGlobalVariable(new GlobalVariable { Name = "A", Value = "1" });
        _service.SaveGlobalVariable(new GlobalVariable { Name = "B", Value = "2" });

        _service.DeleteGlobalVariable("a");

        _service.GetGlobalVariables().ShouldHaveSingleItem().Name.ShouldBe("B");
    }

    [Fact]
    public void GetVariablesEndpoint_ReturnsTheVariables()
    {
        _service.SaveGlobalVariable(new GlobalVariable { Name = "LicenseServer", Value = "license.lan" });

        var result = ScriptEndpoints.GetVariables(_service);

        var variable = result.ShouldBeOfType<Ok<IReadOnlyList<GlobalVariable>>>().Value.ShouldHaveSingleItem();
        variable.Name.ShouldBe("LicenseServer");
        variable.Value.ShouldBe("license.lan");
    }
}
