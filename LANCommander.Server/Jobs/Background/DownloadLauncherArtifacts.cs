﻿using LANCommander.SDK.Extensions;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Jobs.Background
{
    public class DownloadLauncherArtifacts(
        ILogger<DownloadLauncherArtifacts> logger,
        HttpClient httpClient,
        UpdateService updateService,
        Settings settings) : BaseBackgroundJob(logger)
    {
        public override async Task ExecuteAsync()
        {
            var artifacts = await updateService.GetLauncherArtifactsFromGitHubAsync().ToListAsync();
            var localArtifacts = updateService.GetLauncherArtifactsFromLocalFiles();

            var allowedArtifacts = artifacts.Where(a =>
                settings.Launcher.Architectures.Contains(a.Architecture) &&
                settings.Launcher.Platforms.Contains(a.Platform))
                .ToList();

            foreach (var artifact in allowedArtifacts)
            {
                using (var op = logger?.BeginOperation(" Downloading launcher artifact {ArtifactName} from GitHub", artifact.Name))
                {
                    if (!localArtifacts.Any(a => a.Name.EndsWith(artifact.Name)))
                    {
                        using (var downloadStream = await httpClient.GetStreamAsync(artifact.Url))
                        using (var fs = new FileStream(Path.Combine(settings.Launcher.StoragePath, artifact.Name), FileMode.Create))
                        {
                            await downloadStream.CopyToAsync(fs);
                        }
                    }
                }
            }
        }
    }
}
