using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.SDK.Tests.PowerShell;

public class GlobalVariableRunspaceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"lc-globals-rs-{Guid.NewGuid():N}");

    private string FilePath => Path.Combine(_directory, GlobalVariableStore.FileName);

    public GlobalVariableRunspaceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private Runspace OpenInitialized(params PowerShellVariable[] scriptVariables)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        var builder = new PowerShellRunspaceBuilder(services.BuildServiceProvider(), NullLogger.Instance)
        {
            GlobalVariablesPath = FilePath,
        };

        var runspace = builder.Open();

        builder.Initialize(runspace, _directory, ScriptType.Install, scriptVariables, "logo");

        return runspace;
    }

    private static PSObject[] Invoke(Runspace runspace, string script, out PowerShellErrors errors)
    {
        using var shell = System.Management.Automation.PowerShell.Create();

        shell.Runspace = runspace;
        shell.AddScript(script);

        var results = shell.Invoke().ToArray();

        errors = new PowerShellErrors(shell.Streams.Error.Select(e => e.Exception?.Message ?? e.ToString()).ToArray());

        return results;
    }

    private static object Evaluate(Runspace runspace, string script) =>
        Invoke(runspace, script, out _).SingleOrDefault()?.BaseObject;

    private sealed record PowerShellErrors(string[] Messages);

    [Fact]
    public void VarDrive_ReadsGlobalVariable_LikeEnv()
    {
        GlobalVariableStore.Save([new GlobalVariable { Name = "LicenseServer", Value = "license.lan" }], FilePath);

        using var runspace = OpenInitialized();

        Assert.Equal("license.lan", Evaluate(runspace, "$var:LicenseServer"));
        Assert.Equal("license.lan", Evaluate(runspace, "$var:licenseserver"));
        Assert.Equal("license.lan", Evaluate(runspace, "Get-Content var:LicenseServer"));
        Assert.Equal(true, Evaluate(runspace, "Test-Path var:LicenseServer"));
        Assert.Equal("connect license.lan", Evaluate(runspace, "\"connect $var:LicenseServer\""));
    }

    [Fact]
    public void VarDrive_DoesNotDefinePlainVariables()
    {
        GlobalVariableStore.Save([new GlobalVariable { Name = "LicenseServer", Value = "license.lan" }], FilePath);

        using var runspace = OpenInitialized();

        Assert.Null(Evaluate(runspace, "$LicenseServer"));
    }

    [Fact]
    public void VarDrive_UndefinedName_ReadsAsNull()
    {
        using var runspace = OpenInitialized();

        Assert.Null(Evaluate(runspace, "$var:Missing"));
        Assert.Equal(true, Evaluate(runspace, "$null -eq $var:Missing"));
        Assert.Equal(false, Evaluate(runspace, "Test-Path var:Missing"));
    }

    [Fact]
    public void VarDrive_ListsVariablesAsNameValuePairs()
    {
        GlobalVariableStore.Save(
        [
            new GlobalVariable { Name = "B", Value = "2" },
            new GlobalVariable { Name = "A", Value = "1" },
        ], FilePath);

        using var runspace = OpenInitialized();

        Assert.Equal(["A", "B"], Invoke(runspace, "Get-ChildItem var: | ForEach-Object Name", out _).Select(o => o.BaseObject));
        Assert.Equal("2", Evaluate(runspace, "(Get-ChildItem var: | Where-Object Name -eq 'B').Value"));
    }

    [Fact]
    public void VarDrive_IsReadOnly()
    {
        GlobalVariableStore.Save([new GlobalVariable { Name = "Key", Value = "original" }], FilePath);

        using var runspace = OpenInitialized();

        Invoke(runspace, "$var:Key = 'changed'", out var errors);

        Assert.NotEmpty(errors.Messages);
        Assert.Equal("original", Evaluate(runspace, "$var:Key"));
    }

    [Fact]
    public void VarDrive_DoesNotCollideWithScriptVariables()
    {
        GlobalVariableStore.Save([new GlobalVariable { Name = "InstallDirectory", Value = "global" }], FilePath);

        using var runspace = OpenInitialized(new PowerShellVariable("InstallDirectory", "script", typeof(string)));

        Assert.Equal("script", Evaluate(runspace, "$InstallDirectory"));
        Assert.Equal("global", Evaluate(runspace, "$var:InstallDirectory"));
    }

    [Fact]
    public void VarDrive_WithoutFile_IsEmpty()
    {
        using var runspace = OpenInitialized();

        Assert.Empty(Invoke(runspace, "Get-ChildItem var:", out var errors));
        Assert.Empty(errors.Messages);
        Assert.Equal(_directory, Evaluate(runspace, "$WorkingDirectory"));
    }
}
