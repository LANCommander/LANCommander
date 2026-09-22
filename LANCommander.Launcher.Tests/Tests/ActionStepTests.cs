using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.Launcher.ViewModels.Packaging;
using LANCommander.Packaging.Models;
using LANCommander.SDK.Enums;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// The action editor: multiple entry points, their order, and what ends up in the manifest.
/// </summary>
public class ActionStepTests
{
    private const string InstallDirectory = @"C:\Games\Example";

    /// <summary>
    /// A step bound to a package of its own, so the editor can be driven without building the
    /// wizard and every service behind it.
    /// </summary>
    private sealed class TestableActionStep : ActionStepViewModel
    {
        public TestableActionStep(params string[] relativeFiles) : base(null!)
        {
            Definition.InstallDirectory = InstallDirectory;
            Definition.SelectedFiles =
            [
                .. relativeFiles.Select(f => Path.Combine(InstallDirectory, f.Replace('/', Path.DirectorySeparatorChar)))
            ];
        }

        public PackageDefinition Definition { get; } = new();

        protected override PackageDefinition Package => Definition;
    }

    private static async Task<TestableActionStep> EnterAsync(params string[] relativeFiles)
    {
        var step = new TestableActionStep(relativeFiles);

        await step.OnEnterAsync();

        return step;
    }

    [Fact]
    public async Task SeedsOnePrimaryActionFromTheLikeliestExecutable()
    {
        // A single-executable game should need no interaction on this step at all.
        var step = await EnterAsync("game.exe", "unins000.exe", "Redist/vcredist.exe");

        var action = Assert.Single(step.Actions);

        Assert.Equal("Play", action.Name);
        Assert.Equal("game.exe", action.Path);
        Assert.True(action.IsPrimary);
        Assert.Equal("{InstallDir}", action.WorkingDirectory);
    }

    [Fact]
    public async Task HidesInstallersAndRedistributablesUntilAsked()
    {
        var step = await EnterAsync("game.exe", "unins000.exe", "Redist/vcredist.exe");

        Assert.Equal(["game.exe"], step.Executables);

        step.ShowAllExecutables = true;

        Assert.Contains("unins000.exe", step.Executables);
        Assert.Contains("Redist/vcredist.exe", step.Executables);
    }

    [Fact]
    public async Task OffersPathsRelativeToTheInstallDirectoryWithForwardSlashes()
    {
        // The manifest is read on whatever machine installs the game, so the path has to be
        // relative — and forward slashed, because the launcher rewrites those to the local
        // separator and does not do the reverse.
        var step = await EnterAsync(@"Bin\game.exe");

        Assert.Equal(["Bin/game.exe"], step.Executables);
    }

    [Fact]
    public async Task AddingAnActionLeavesTheExistingPrimaryAlone()
    {
        var step = await EnterAsync("game.exe", "server.exe");

        step.AddActionCommand.Execute(null);

        Assert.Equal(2, step.Actions.Count);
        Assert.True(step.Actions[0].IsPrimary);
        Assert.False(step.Actions[1].IsPrimary);
    }

    [Fact]
    public async Task MarkingAnActionPrimaryClearsTheOthers()
    {
        // Drawn as a checkbox but behaves like a radio button: the Play button runs exactly one
        // thing, and it should be the one the user last pointed at.
        var step = await EnterAsync("game.exe", "server.exe");

        step.AddActionCommand.Execute(null);

        step.Actions[1].IsPrimary = true;

        Assert.False(step.Actions[0].IsPrimary);
        Assert.True(step.Actions[1].IsPrimary);
    }

    [Fact]
    public async Task UntickingTheOnlyPrimaryPutsItBack()
    {
        // Leaving none marked still launches something, just not necessarily what was meant.
        var step = await EnterAsync("game.exe");

        step.Actions[0].IsPrimary = false;

        Assert.True(step.Actions[0].IsPrimary);
    }

    [Fact]
    public async Task RemovingThePrimaryPromotesWhatIsLeft()
    {
        var step = await EnterAsync("game.exe", "server.exe");

        step.AddActionCommand.Execute(null);

        var survivor = step.Actions[1];

        step.Actions[0].RemoveCommand.Execute(null);

        Assert.Same(survivor, Assert.Single(step.Actions));
        Assert.True(survivor.IsPrimary);
    }

    [Fact]
    public async Task MovingAnActionReordersIt()
    {
        var step = await EnterAsync("game.exe", "server.exe");

        step.AddActionCommand.Execute(null);

        var second = step.Actions[1];

        second.MoveUpCommand.Execute(null);

        Assert.Same(second, step.Actions[0]);

        second.MoveDownCommand.Execute(null);

        Assert.Same(second, step.Actions[1]);
    }

    [Fact]
    public async Task MovingPastEitherEndDoesNothing()
    {
        var step = await EnterAsync("game.exe");

        step.Actions[0].MoveUpCommand.Execute(null);
        step.Actions[0].MoveDownCommand.Execute(null);

        Assert.Single(step.Actions);
    }

    [Fact]
    public async Task WritesEveryActionToTheManifestInListOrder()
    {
        var step = await EnterAsync("game.exe", "mp.exe");

        step.AddActionCommand.Execute(null);

        step.Actions[1].Name = "Multiplayer";
        step.Actions[1].Path = "mp.exe";
        step.Actions[1].Arguments = "+connect";
        step.Actions[1].IsPrimary = true;

        // Sort order follows the list, so moving a row is all it takes to reorder the launcher's
        // menu; it is not a field the user has to maintain.
        step.Actions[1].MoveUpCommand.Execute(null);

        await step.OnLeaveAsync();

        // ICollection on the manifest, so index it through a list.
        var actions = step.Definition.Manifest.Actions.ToList();

        Assert.Equal(2, actions.Count);

        Assert.Equal("Multiplayer", actions[0].Name);
        Assert.Equal(0, actions[0].SortOrder);
        Assert.True(actions[0].IsPrimaryAction);
        Assert.Equal("+connect", actions[0].Arguments);

        Assert.Equal("Play", actions[1].Name);
        Assert.Equal(1, actions[1].SortOrder);
        Assert.False(actions[1].IsPrimaryAction);
    }

    [Fact]
    public async Task ManifestPathsAreForwardSlashedEvenWhenTypedWithBackslashes()
    {
        var step = await EnterAsync(@"Bin\game.exe");

        step.Actions[0].Path = @"Bin\game.exe";

        await step.OnLeaveAsync();

        Assert.Equal("Bin/game.exe", Assert.Single(step.Definition.Manifest.Actions).Path);
    }

    [Fact]
    public async Task ActionsAreNotRestrictedToAPlatform()
    {
        // RuntimePlatform.None is what the runtime reads as "no restriction". A package built
        // from one install has nothing to say about other platforms, and narrowing it here
        // would make the action unrunnable everywhere else.
        var step = await EnterAsync("game.exe");

        await step.OnLeaveAsync();

        Assert.Equal(RuntimePlatform.None, Assert.Single(step.Definition.Manifest.Actions).Platforms);
    }

    [Fact]
    public async Task TestIsOnlyOfferedOnceAnActionHasAPath()
    {
        var step = await EnterAsync("game.exe");

        Assert.True(step.Actions[0].TestCommand.CanExecute(null));

        step.AddActionCommand.Execute(null);

        Assert.False(step.Actions[1].TestCommand.CanExecute(null));

        step.Actions[1].Path = "game.exe";

        Assert.True(step.Actions[1].TestCommand.CanExecute(null));
    }

    [Fact]
    public async Task TestReportsAPathThatIsNotOnDisk()
    {
        // The install directory here is fictional, so this exercises the guard rather than
        // launching anything.
        var step = await EnterAsync("game.exe");

        step.Actions[0].TestCommand.Execute(null);

        Assert.Contains("Could not find", step.TestStatus);
    }

    [Fact]
    public async Task BlocksTheStepUntilEveryActionHasANameAndAPath()
    {
        var step = await EnterAsync("game.exe");

        Assert.True(step.CanGoNext);

        step.AddActionCommand.Execute(null);

        // The new row has a name but no path yet.
        Assert.False(step.CanGoNext);

        step.Actions[1].Path = "game.exe";

        Assert.True(step.CanGoNext);

        step.Actions[1].Name = "   ";

        Assert.False(step.CanGoNext);
    }

    [Fact]
    public async Task BlocksTheStepWhenEveryActionHasBeenRemoved()
    {
        var step = await EnterAsync("game.exe");

        step.Actions[0].RemoveCommand.Execute(null);

        Assert.Empty(step.Actions);
        Assert.False(step.CanGoNext);
    }

    [Fact]
    public async Task FlagsAPathThatIsNotInThePackageWithoutBlocking()
    {
        // The case this catches: going back, changing the file selection, and leaving an action
        // aimed at a file that is no longer included. It is a warning rather than a blocker
        // because a path can legitimately point at something a script installs.
        var step = await EnterAsync("game.exe");

        step.Actions[0].Path = "missing.exe";

        Assert.True(step.Actions[0].IsPathMissing);
        Assert.True(step.CanGoNext);
        Assert.Contains("not in the package", step.Summary);
    }

    [Fact]
    public async Task ReEnteringKeepsTheActionsTheUserAlreadyEdited()
    {
        var step = await EnterAsync("game.exe", "mp.exe");

        step.AddActionCommand.Execute(null);

        step.Actions[1].Name = "Multiplayer";
        step.Actions[1].Path = "mp.exe";

        await step.OnEnterAsync();

        Assert.Equal(2, step.Actions.Count);
        Assert.Equal("Multiplayer", step.Actions[1].Name);
    }

    [Fact]
    public async Task ResetClearsTheEditorForANewPackage()
    {
        // The wizard and its steps live on the shell and are reused, so a second package must
        // not inherit the first one's actions.
        var step = await EnterAsync("game.exe");

        step.AddActionCommand.Execute(null);

        step.Reset();

        Assert.Empty(step.Actions);
        Assert.Empty(step.Executables);
        Assert.False(step.CanGoNext);
    }
}
