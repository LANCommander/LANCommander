using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests;

public class RedistributableOptionValuesTests
{
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    [Fact]
    public void ValuesRoundTripPerRedistributable()
    {
        var json = RedistributableOptionValues.With(null, First, new Dictionary<string, string?> { ["A.B"] = "1" });
        json = RedistributableOptionValues.With(json, Second, new Dictionary<string, string?> { ["C"] = "two" });

        RedistributableOptionValues.For(json, First).ShouldBe(new Dictionary<string, string?> { ["A.B"] = "1" });
        RedistributableOptionValues.For(json, Second).ShouldBe(new Dictionary<string, string?> { ["C"] = "two" });
    }

    [Fact]
    public void ClearingARedistributablesValuesDropsIt()
    {
        var json = RedistributableOptionValues.With(null, First, new Dictionary<string, string?> { ["A"] = "1" });

        RedistributableOptionValues.With(json, First, new Dictionary<string, string?>()).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"not-a-guid\":{}}")]
    public void NothingReadableIsNoValues(string? json)
    {
        RedistributableOptionValues.Parse(json).ShouldBeEmpty();
        RedistributableOptionValues.For(json, First).ShouldBeEmpty();
    }
}
