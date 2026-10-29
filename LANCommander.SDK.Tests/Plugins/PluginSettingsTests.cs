using LANCommander.SDK.Plugins;

namespace LANCommander.SDK.Tests.Plugins;

public sealed class PluginSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lancommander-plugin-settings-tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "PluginData", "com.example.test", PluginSettings<TestSettings>.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void WithNoFileTheDefaultsAreUsedAndNothingIsWritten()
    {
        using var settings = new PluginSettings<TestSettings>(FilePath);

        Assert.Equal(1234, settings.Current.Port);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task ChangesAreWrittenAsPascalCaseYamlAndReadBack()
    {
        await using (var settings = new PluginSettings<TestSettings>(FilePath))
        {
            var changed = 0;
            settings.Changed += () => changed++;

            settings.Update(s =>
            {
                s.Enabled = true;
                s.Port = 4321;
                s.Names.Add("alice");
            });

            await settings.FlushAsync();

            Assert.Equal(1, changed);
        }

        var yaml = File.ReadAllText(FilePath);

        Assert.Contains("Enabled: true", yaml);
        Assert.Contains("Port: 4321", yaml);
        Assert.Contains("- alice", yaml);

        using var reloaded = new PluginSettings<TestSettings>(FilePath);

        Assert.True(reloaded.Current.Enabled);
        Assert.Equal(4321, reloaded.Current.Port);
        Assert.Equal(["alice"], reloaded.Current.Names);
    }

    [Fact]
    public async Task ABurstOfChangesIsWrittenOnceTheDebounceElapses()
    {
        using var settings = new PluginSettings<TestSettings>(FilePath);

        for (var port = 1; port <= 5; port++)
            settings.Update(s => s.Port = port);

        Assert.False(File.Exists(FilePath));

        await WaitForAsync(() => File.Exists(FilePath) && File.ReadAllText(FilePath).Contains("Port: 5"));
    }

    [Fact]
    public void PropertiesThePluginNoLongerHasAreIgnored()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "Port: 99\nRemovedLongAgo: something\n");

        using var settings = new PluginSettings<TestSettings>(FilePath);

        Assert.Equal(99, settings.Current.Port);
    }

    [Fact]
    public void AFileThatCantBeReadGivesTheDefaultsAndIsLeftAlone()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "Port: [unclosed\n");

        using var settings = new PluginSettings<TestSettings>(FilePath);

        Assert.Equal(1234, settings.Current.Port);
        Assert.Equal("Port: [unclosed\n", File.ReadAllText(FilePath));
    }

    [Fact]
    public void ATypeNoPluginDeclaresHasNoSettingsFile()
        => Assert.Throws<InvalidOperationException>(() => new PluginSettings<TestSettings>(new PluginLoaderService()));

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The settings were not written.");

            await Task.Delay(50);
        }
    }

    public sealed class TestSettings
    {
        public bool Enabled { get; set; }

        public int Port { get; set; } = 1234;

        public List<string> Names { get; set; } = [];
    }
}
