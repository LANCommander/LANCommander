using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Enums;
using Octokit;
using Semver;

namespace LANCommander.Server.Services.Abstractions;

public interface IGitHubService
{
    Task<SemVersion> GetLatestVersionAsync(ReleaseChannel releaseChannel);
    Task<Release?> GetReleaseAsync(SemVersion version);
    Task<Release?> GetReleaseAsync(string tag);
    Task<IEnumerable<Release>> GetReleasesAsync(int count);
    Task<IEnumerable<Artifact>> GetNightlyArtifactsAsync(string versionOverride = null);
    Task<IEnumerable<Artifact>> GetWorkflowArtifactsAsync(long runId);
}