using System.Reflection;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Install;

public class ToolClientInstallProgressTests
{
    /// <summary>
    /// The extraction progress callback in DownloadAndExtractAsync writes straight to the shared
    /// _installProgress instance. That callback runs on a thread pool thread, so a null field there
    /// takes the whole launcher down instead of surfacing as a failed install. The install queue
    /// reaches extraction through ExecuteInstallPlanItemAsync, which never allocated the field.
    /// </summary>
    [Fact]
    public void InstallProgress_IsInitialized_BeforeAnyInstallMethodRuns()
    {
        var client = new ToolClient(null!, null!, null!, null!);

        var field = typeof(ToolClient).GetField("_installProgress", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.NotNull(field.GetValue(client));
    }
}
