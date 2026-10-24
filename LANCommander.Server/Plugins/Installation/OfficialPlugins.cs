namespace LANCommander.Server.Plugins.Installation;

/// <summary>
/// Which packages are official LANCommander plugins: those whose ID starts with
/// <see cref="IdPrefix"/> and that the <see cref="Owner"/> organization owns on nuget.org.
/// </summary>
/// <remarks>
/// Only nuget.org's word counts. The prefix isn't reserved there, so anyone can publish under it,
/// and any other feed can claim whatever owners it likes; an uploaded package carries no owner at all.
/// </remarks>
public static class OfficialPlugins
{
    public const string IdPrefix = "LANCommander.Plugins.";
    public const string Owner = "LANCommander";

    /// <summary>Whether a package could be official, before asking nuget.org who owns it.</summary>
    public static bool IsCandidate(string sourceUrl, string packageId) =>
        string.Equals(sourceUrl.Trim(), PluginPackageService.NuGetOrgUrl, StringComparison.OrdinalIgnoreCase)
        && packageId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsOfficial(string sourceUrl, string packageId, IEnumerable<string>? owners) =>
        IsCandidate(sourceUrl, packageId)
        && owners?.Any(owner => string.Equals(owner, Owner, StringComparison.OrdinalIgnoreCase)) == true;
}
