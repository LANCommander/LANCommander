using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// The host's <see cref="IPluginSettings{TSettings}"/>. Register it as an open generic singleton.
/// </summary>
public sealed class PluginSettings<TSettings> : IPluginSettings<TSettings>, IAsyncDisposable, IDisposable
    where TSettings : class, new()
{
    public const string FileName = "Settings.yaml";

    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(1);

    private readonly string _filePath;
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _ioGate = new(1, 1);

    private TSettings? _current;
    private CancellationTokenSource? _saveCts;
    private bool _pending;

    public PluginSettings(PluginLoaderService loader, ILogger<PluginSettings<TSettings>>? logger = null)
        : this(PathFor(loader), logger)
    {
    }

    /// <param name="filePath">Where the settings are kept; tests give a scratch path.</param>
    internal PluginSettings(string filePath, ILogger? logger = null)
    {
        _filePath = filePath;
        _logger = logger ?? NullLogger.Instance;
    }

    public event Action? Changed;

    public TSettings Current
    {
        get
        {
            lock (_lock)
                return _current ??= Load();
        }
    }

    public void Update(Action<TSettings> mutator)
    {
        lock (_lock)
        {
            mutator(_current ??= Load());
            _pending = true;

            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();

            _ = SaveAfterDelayAsync(_saveCts.Token);
        }

        Changed?.Invoke();
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = null;
        }

        await SaveIfPendingAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync().ConfigureAwait(false);

        _ioGate.Dispose();
    }

    /// <summary>For containers disposed synchronously.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private static string PathFor(PluginLoaderService loader)
    {
        var plugin = loader.FindByAssembly(typeof(TSettings).Assembly)
                     ?? throw new InvalidOperationException(
                         $"{typeof(TSettings).FullName} is not declared by a loaded plugin, so there is no plugin to keep its settings for.");

        return Path.Combine(plugin.Manifest.DataDirectory, FileName);
    }

    private TSettings Load()
    {
        if (!File.Exists(_filePath))
            return new TSettings();

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            return deserializer.Deserialize<TSettings>(File.ReadAllText(_filePath)) ?? new TSettings();
        }
        catch (YamlException exception)
        {
            // Defaults rather than a crash; the file is left as it is until the next save, so it can
            // still be fixed by hand
            _logger.LogError(exception, "Plugin settings {Path} could not be read; using defaults", _filePath);

            return new TSettings();
        }
    }

    private async Task SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DebounceDelay, cancellationToken).ConfigureAwait(false);
            await SaveIfPendingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Plugin settings {Path} could not be saved", _filePath);
        }
    }

    private async Task SaveIfPendingAsync(CancellationToken cancellationToken)
    {
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            string yaml;

            lock (_lock)
            {
                if (!_pending || _current is null)
                    return;

                yaml = YamlSerializerFactory.Create().Serialize(_current);
                _pending = false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // Replaced in one step, so a crash mid-write leaves the previous settings
            var temporary = $"{_filePath}.{Guid.NewGuid():N}.tmp";

            try
            {
                await File.WriteAllTextAsync(temporary, yaml, cancellationToken).ConfigureAwait(false);
                File.Move(temporary, _filePath, overwrite: true);
            }
            catch
            {
                // Still unsaved, so the next update or flush tries again
                lock (_lock)
                    _pending = true;

                throw;
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }
}
