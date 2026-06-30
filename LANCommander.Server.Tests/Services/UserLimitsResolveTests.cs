using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

public class UserLimitsResolveTests
{
    [Theory]
    [InlineData(50, 50)]  // explicit override wins over role
    [InlineData(0, 0)]    // override of 0 (unlimited) wins
    [InlineData(5, 5)]    // override below role values still wins
    public void UserOverrideTakesPrecedence(int? userOverride, int expected)
    {
        var result = UserService.Resolve(userOverride, new int?[] { 10, 20 });

        result.ShouldBe(expected);
    }

    [Fact]
    public void NoOverrideUsesLowestNonZeroRoleValue()
    {
        var result = UserService.Resolve(null, new int?[] { 20, 5, 50 });

        result.ShouldBe(5);
    }

    [Fact]
    public void NullAndZeroRoleValuesAreIgnoredWhenResolving()
    {
        // null = not configured, 0 = explicit unlimited; both ignored so the only real limit (15) wins.
        var result = UserService.Resolve(null, new int?[] { null, 0, 15 });

        result.ShouldBe(15);
    }

    [Fact]
    public void AllRolesUnsetMeansUnlimited()
    {
        var result = UserService.Resolve(null, new int?[] { null, null });

        result.ShouldBe(0);
    }

    [Fact]
    public void AllRolesExplicitlyUnlimitedMeansUnlimited()
    {
        var result = UserService.Resolve(null, new int?[] { 0, 0 });

        result.ShouldBe(0);
    }

    [Fact]
    public void NoRolesAndNoOverrideMeansUnlimited()
    {
        var result = UserService.Resolve(null, Array.Empty<int?>());

        result.ShouldBe(0);
    }

    [Theory]
    [InlineData(true, true)]   // override true (enabled) wins even though a role disables
    [InlineData(false, false)] // override false (disabled) wins even though roles enable
    public void BoolUserOverrideTakesPrecedence(bool? userOverride, bool expected)
    {
        var result = UserService.Resolve(userOverride, new bool?[] { true, false });

        result.ShouldBe(expected);
    }

    [Fact]
    public void BoolAnyRoleDisabledMeansDisabled()
    {
        // false = explicitly disabled; most restrictive wins so the effective permission is disabled.
        var result = UserService.Resolve((bool?)null, new bool?[] { true, false, null });

        result.ShouldBeFalse();
    }

    [Fact]
    public void BoolAllRolesUnsetOrEnabledMeansEnabled()
    {
        var result = UserService.Resolve((bool?)null, new bool?[] { null, true });

        result.ShouldBeTrue();
    }

    [Fact]
    public void BoolNoRolesAndNoOverrideMeansEnabled()
    {
        var result = UserService.Resolve((bool?)null, Array.Empty<bool?>());

        result.ShouldBeTrue();
    }
}
