using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;
using Octokit;
using Semver;

namespace LANCommander.Server.Services;

public class GitHubService(Logger<GitHubService> logger, IVersionProvider versionProvider)
{
    private const string _owner = "LANCommander";
    private const string _repository = "LANCommander";
    private const string _nightlyWorkflowFile = "LANCommander.Nightly.yml";
    
    private GitHubClient _client;

    public async Task<SemVersion> GetLatestVersionAsync(ReleaseChannel releaseChannel)
    {
        string version = "";
            
        if (releaseChannel == ReleaseChannel.Stable)
        {
            var release = await _client.Repository.Release.GetLatest(_owner, _repository);

            if (release.Prerelease)
            {
                release = (await _client.Repository.Release.GetAll(_owner, _repository))
                    .Where(r => !r.Prerelease)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();
            }

            version = release.TagName;
        }

        if (releaseChannel == ReleaseChannel.Prerelease)
        {
            var release = await _client.Repository.Release.GetLatest(_owner, _repository);
            
            version = release.TagName;
        }
        
        if (releaseChannel == ReleaseChannel.Nightly)
        {
            var workflow = await _client.Actions.Workflows.Get(_owner, _repository, _nightlyWorkflowFile);
            var runs = await _client.Actions.Workflows.Runs.ListByWorkflow(_owner, _repository, workflow.Id);
            
            var latestRun = runs.WorkflowRuns
                .Where(r => r.Conclusion == WorkflowRunConclusion.Success)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();
            
            var artifacts = await _client.Actions.Artifacts.ListWorkflowArtifacts(_owner, _repository, latestRun.Id);

            var versionArtifact = artifacts.Artifacts.FirstOrDefault(a => a.Name.StartsWith("version."));

            if (versionArtifact != null)
                version = versionArtifact.Name.Substring(0, versionArtifact.Name.Length - "version.".Length);
        }

        if (SemVersion.TryParse(version, SemVersionStyles.AllowV, out SemVersion semVersion))
            return semVersion;
        else
            return versionProvider.GetCurrentVersion();
    }

    public async Task<Release?> GetReleaseAsync(SemVersion version)
    {
        return await _client.Repository.Release.Get(_owner, _repository, $"v{version.WithoutMetadata()}");
    }

    public async Task<IEnumerable<Release>> GetReleasesAsync(int count)
    {
        return await _client.Repository.Release.GetAll(_owner, _repository, new ApiOptions
        {
            PageSize = count,
            PageCount = 1,
        });
    }

    public async Task<IEnumerable<Artifact>> GetNightlyArtifactsAsync(string versionOverride = null)
    {
        var currentVersion = versionProvider.GetCurrentVersion();
        
        if (!String.IsNullOrWhiteSpace(versionOverride))
            currentVersion = SemVersion.Parse(versionOverride);
        
        var workflowRunsResponse = await _client.Actions.Workflows.Runs.ListByWorkflow(_owner, _repository, _nightlyWorkflowFile, new WorkflowRunsRequest
        {
            HeadSha = currentVersion.Metadata,
        });

        if (workflowRunsResponse.WorkflowRuns.Any())
        {
            var run = workflowRunsResponse.WorkflowRuns.FirstOrDefault();

            return await GetWorkflowArtifactsAsync(run.Id);
        }
        else
            return Enumerable.Empty<Artifact>();
    }

    public async Task<IEnumerable<Artifact>> GetWorkflowArtifactsAsync(long runId)
    {
        var response = await _client.Actions.Artifacts.ListWorkflowArtifacts(_owner, _repository, runId);
        
        logger.LogInformation($"Searching for workflow artifacts for run #{runId}");
        
        if (response.Artifacts.Any())
            logger.LogInformation($"Found the following artifacts:\n{String.Join("\n\t - ", response.Artifacts.Select(a => a.Name))}");
        else
            logger.LogError($"No artifacts found for run #{runId}");
        
        return response.Artifacts;
    }
}