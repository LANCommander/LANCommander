using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LANCommander.Launcher.Fixtures;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Notify.NET.Abstractions;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

public class DownloadQueueViewModelTests
{
    [AvaloniaFact]
    public async Task Back_to_back_queue_changes_while_the_cover_downloads_leave_one_entry()
    {
        using var fixture = new QueueFixture();
        var game = new InstallQueueGame(fixture.Game) { Status = InstallStatus.Starting };

        // Adding and then starting an install raises several queue changes in quick succession
        fixture.InstallService.Queue.Add(game);
        fixture.InstallService.Queue.Move(0, 0);
        fixture.InstallService.Queue.Move(0, 0);

        Dispatcher.UIThread.RunJobs();
        fixture.Covers.Release();
        await fixture.SettleAsync();

        Assert.Single(fixture.ViewModel.QueueItems);
        Assert.Equal(1, fixture.Covers.Requests);

        game.Status = InstallStatus.Complete;
        fixture.InstallService.Queue.Move(0, 0);
        await fixture.SettleAsync();

        Assert.True(Assert.Single(fixture.ViewModel.QueueItems).IsCompleted);
        Assert.False(fixture.ViewModel.HasActiveDownload);
        Assert.Null(fixture.ViewModel.CurrentItem);
    }

    [AvaloniaFact]
    public async Task Footer_figures_clear_once_nothing_is_downloading()
    {
        using var fixture = new QueueFixture();
        var game = new InstallQueueGame(fixture.Game) { Status = InstallStatus.Downloading };

        fixture.Covers.Release();
        fixture.InstallService.Queue.Add(game);
        await fixture.SettleAsync();

        fixture.ViewModel.TransferSpeedText = "12 MB/s";
        fixture.ViewModel.TimeRemainingText = "0s remaining";
        fixture.ViewModel.TimeRemainingValue = "0s";
        fixture.ViewModel.CurrentProgress = 1f;

        game.Status = InstallStatus.Complete;
        fixture.InstallService.Queue.Move(0, 0);
        await fixture.SettleAsync();

        Assert.False(fixture.ViewModel.HasActiveDownload);
        Assert.Empty(fixture.ViewModel.TransferSpeedText);
        Assert.Empty(fixture.ViewModel.TimeRemainingText);
        Assert.Empty(fixture.ViewModel.TimeRemainingValue);
        Assert.Equal(0f, fixture.ViewModel.CurrentProgress);
    }

    private sealed class QueueFixture : IDisposable
    {
        private readonly string _mediaPath = Path.Combine(Path.GetTempPath(), $"LANCommander.QueueTests.{Guid.NewGuid():N}");
        private readonly HttpClient _httpClient;
        private readonly AuthenticationSession _session;
        private readonly ServiceProvider _services;

        public CoverHandler Covers { get; } = new();
        public InstallService InstallService { get; }
        public DownloadQueueViewModel ViewModel { get; }

        public Game Game { get; } = new()
        {
            Id = Guid.NewGuid(),
            Title = "Chivalry: Medieval Warfare",
            Media =
            [
                new Media { Id = Guid.NewGuid(), FileId = Guid.NewGuid(), Crc32 = "00000000", Type = MediaType.Cover },
            ],
        };

        public QueueFixture()
        {
            var settings = new Launcher.Settings.Settings();
            settings.Authentication.ServerAddress = new Uri("https://server.test");
            settings.Authentication.Token = new AuthToken
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(
                    new JwtSecurityToken(expires: DateTime.UtcNow.AddHours(1))),
                RefreshToken = "refresh-token",
                Expiration = DateTime.UtcNow.AddHours(1),
            };
            settings.Media.StoragePath = _mediaPath;

            var settingsProvider = new InMemorySettings(settings);
            var tokens = new TokenProvider(settingsProvider);

            _httpClient = new HttpClient(Covers);
            _session = new AuthenticationSession(tokens, settingsProvider, NullLogger<AuthenticationSession>.Instance);

            var requests = new ApiRequestFactory(_httpClient, tokens, settingsProvider, _session);
            var mediaClient = new MediaClient(requests, null!, settingsProvider);
            var eventBus = new PluginEventBus(NullLogger<PluginEventBus>.Instance);
            var gameClient = new GameClient(
                NullLogger<GameClient>.Instance, requests, null!, null!, settingsProvider, null!,
                null!, null!, null!, null!, null!, eventBus, []);

            var toolClient = new ToolClient(NullLogger<ToolClient>.Instance, settingsProvider, requests, null!);

            InstallService = new InstallService(
                NullLogger<InstallService>.Instance, null!, null!, null!, gameClient,
                null!, toolClient, mediaClient, eventBus);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<INotificationService, FixtureNotificationService>();
            services.AddSingleton<ITaskbarProgressService, FixtureTaskbarProgressService>();
            services.AddSingleton<NotificationService>();
            services.AddSingleton<TaskbarProgressService>();
            services.AddSingleton(mediaClient);
            services.AddSingleton(InstallService);
            _services = services.BuildServiceProvider();

            ViewModel = new DownloadQueueViewModel(_services);
            ViewModel.Initialize();
        }

        /// <summary>Pumps the UI thread until queued refreshes and their cover downloads have finished.</summary>
        public async Task SettleAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var idleSince = TimeSpan.Zero;

            while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                Dispatcher.UIThread.RunJobs();

                if (Covers.InFlight == 0)
                {
                    if (idleSince == TimeSpan.Zero)
                        idleSince = stopwatch.Elapsed;
                    else if (stopwatch.Elapsed - idleSince > TimeSpan.FromMilliseconds(100))
                        return;
                }
                else
                {
                    idleSince = TimeSpan.Zero;
                }

                await Task.Delay(10);
            }
        }

        public void Dispose()
        {
            _services.Dispose();
            _session.Dispose();
            _httpClient.Dispose();

            if (Directory.Exists(_mediaPath))
                Directory.Delete(_mediaPath, recursive: true);
        }
    }

    /// <summary>Holds every cover download open until released, then answers 404.</summary>
    private sealed class CoverHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requests;
        private int _inFlight;

        public int Requests => _requests;
        public int InFlight => _inFlight;

        public void Release() => _gate.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            Interlocked.Increment(ref _inFlight);

            try
            {
                await _gate.Task.WaitAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private sealed class InMemorySettings(Launcher.Settings.Settings settings) : ISettingsProvider
    {
        public SDK.Models.Settings CurrentValue => settings;
        public void Update(Action<SDK.Models.Settings> patch) => patch(settings);
    }
}
