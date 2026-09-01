using System.Reflection;
using System.Reflection.Emit;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests.Helpers;

public class VersionHelperTests
{
    [Theory]
    [InlineData("2.1.11")]
    [InlineData("2.2.0-rc1")]
    [InlineData("2.1.10-nightly.20260812")]
    public void Resolve_EnvironmentOverride_Wins(string overrideValue)
    {
        var version = VersionHelper.Resolve(overrideValue, StubAssembly("9.9.9"));

        Assert.Equal(overrideValue, version.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    public void Resolve_UnusableOverride_FallsBackToEntryAssembly(string? overrideValue)
    {
        var version = VersionHelper.Resolve(overrideValue, StubAssembly("2.1.11"));

        Assert.Equal("2.1.11", version.ToString());
    }

    [Fact]
    public void Resolve_EntryAssembly_StripsSourceRevisionMetadata()
    {
        var version = VersionHelper.Resolve(null, StubAssembly("2.1.11+f750e411345a97bf00a046942b000cbccf9d5c47"));

        Assert.Equal("2.1.11", version.ToString());
    }

    [Fact]
    public void Resolve_EntryAssembly_PreservesPrereleaseTag()
    {
        var version = VersionHelper.Resolve(null, StubAssembly("2.1.10-nightly.20260812+abc1234"));

        Assert.Equal("2.1.10-nightly.20260812", version.ToString());
    }

    [Fact]
    public void Resolve_NoEntryAssembly_FallsBackToSdkVersion()
    {
        var version = VersionHelper.Resolve(null, null);

        Assert.NotNull(version);
    }

    [Fact]
    public void Resolve_UnparseableEntryAssemblyVersion_FallsBackToSdkVersion()
    {
        var version = VersionHelper.Resolve(null, StubAssembly("garbage"));

        Assert.NotNull(version);
    }

    /// <summary>
    /// Builds an in-memory assembly carrying only an <see cref="AssemblyInformationalVersionAttribute"/>,
    /// standing in for the entry assembly that <see cref="VersionHelper.GetCurrentVersion"/> reads.
    /// </summary>
    private static Assembly StubAssembly(string informationalVersion)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"VersionHelperTests_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);

        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
            [informationalVersion]));

        return assembly;
    }
}
