#if DEBUG
using System.Collections.Generic;
using LANCommander.Launcher.ViewModels.Components;

namespace LANCommander.Launcher.ViewModels;

public partial class GameDetailViewModel
{
    /// <summary>
    /// Fills the media carousel and tools card for a visual fixture. The load methods are the only
    /// other things that raise <see cref="HasMedia"/> and <see cref="HasTools"/>.
    /// </summary>
    internal void SeedFixture(IEnumerable<GameMediaItemViewModel> media, IEnumerable<ToolItemViewModel> tools)
    {
        MediaItems.Clear();
        Tools.Clear();

        foreach (var item in media)
            MediaItems.Add(item);

        foreach (var tool in tools)
            Tools.Add(tool);

        OnPropertyChanged(nameof(HasMedia));
        OnPropertyChanged(nameof(HasTools));
    }

    /// <summary>Fills the Issues tab for a visual fixture, in place of <c>LoadIssuesAsync</c>.</summary>
    internal void SeedIssuesForFixture(IEnumerable<IssueItemViewModel> issues)
    {
        Issues.Clear();

        foreach (var issue in issues)
            Issues.Add(issue);

        OnPropertyChanged(nameof(HasIssues));
    }

    /// <summary>Fills the Servers tab for a visual fixture, in place of <c>LoadServersAsync</c>.</summary>
    internal void SeedServersForFixture(IEnumerable<GameServerItemViewModel> servers)
    {
        Servers.Clear();

        foreach (var server in servers)
            Servers.Add(server);

        UpdateServerConnectActions();
        OnPropertyChanged(nameof(HasServers));
    }

    /// <summary>
    /// Shows a fixed size on disk. Should the fixture's install folder happen to exist on this
    /// machine, the measurement already under way no longer matches and so can't replace it.
    /// </summary>
    internal void SetSizeOnDiskForFixture(string text)
    {
        _measuredDirectory = null;
        SizeOnDiskText = text;
    }
}
#endif
