using System.Management.Automation;

namespace LANCommander.SamplePlugin;

/// <summary>
/// A cmdlet contributed by the sample plugin. Once registered via
/// <see cref="SamplePowerShellContributor"/> it is callable from any LANCommander script as
/// <c>Get-SampleGreeting -Name "World"</c>.
/// </summary>
[Cmdlet(VerbsCommon.Get, "SampleGreeting")]
public sealed class SampleGreetingCmdlet : PSCmdlet
{
    [Parameter(Position = 0)]
    public string Name { get; set; } = "World";

    protected override void ProcessRecord()
    {
        WriteObject($"Hello, {Name}, from the LANCommander sample plugin!");
    }
}
