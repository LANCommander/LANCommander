#if DEBUG
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LANCommander.Launcher.Services.ScriptDebugging;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.ViewModels.ScriptDebugger;
using LANCommander.Launcher.ViewModels.ScriptDebugger.Items;
using LANCommander.Launcher.Views;
using LANCommander.Launcher.Views.ScriptDebugger;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Hosting;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>Windows of their own: chat, the script debugger, the manual viewer and the install dialog.</summary>
public static class WindowFixtures
{
    private static readonly User Me = Person("Pat");
    private static readonly User Sam = Person("Sam");
    private static readonly User Jordan = Person("Jordan");
    private static readonly User Alex = Person("Alex");

    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        new("Chat.Empty", "Chat with no conversations yet", context =>
            new ChatWindow { DataContext = new ChatWindowViewModel(context.Services) }) { Width = 760, Height = 540 },

        new("Chat.Conversation", "A conversation open, another with unread messages", context =>
        {
            var chat = new ChatWindowViewModel(context.Services);

            var lanNight = Thread("LAN night", [Me, Sam, Jordan],
                (Sam, 0, "Server's up, **ONS-Torlan** first"),
                (Sam, 1, "Everyone on patch 3369?"),
                (Me, 3, "Yep, downloading the bonus pack now"),
                (Jordan, 6, "Running late, save me a spot on red"),
                (Me, 7, "Will do. Map rotation is in `#lan-help`"));

            var direct = Thread(null, [Me, Alex],
                (Alex, 30, "Can you check why my install keeps failing?"));

            chat.Threads = new ObservableCollection<ChatThreadViewModel>([lanNight, direct]);
            direct.UnreadCount = 1;
            chat.SelectedThread = lanNight;

            return new ChatWindow { DataContext = chat };
        }) { Width = 760, Height = 540 },

        new("Chat.NewChat", "Picking people for a new conversation", context =>
        {
            var chat = new ChatWindowViewModel(context.Services)
            {
                IsCreatingThread = true,
            };

            foreach (var user in new[] { Sam, Jordan, Alex })
                chat.FilteredUsers.Add(new UserSelectionViewModel(user) { IsSelected = user == Sam });

            return new ChatWindow { DataContext = chat };
        }) { Width = 760, Height = 540 },

        new("ScriptDebugger.NotInstalled", "The script debugger for a game that isn't installed, a breakpoint set on its install script", context =>
        {
            var debugger = ScriptDebugger(context, installed: false);

            debugger.SelectedDocument!.Breakpoints.Toggle(5);
            debugger.SelectedPanelIndex = 3;

            return new ScriptDebuggerWindow { DataContext = debugger };
        }) { Width = 1400, Height = 860 },

        new("ScriptDebugger.Stopped", "Stopped at a breakpoint in an elevated install script", context =>
            new ScriptDebuggerWindow { DataContext = StoppedDebugger(context) }) { Width = 1400, Height = 860 },

        new("ScriptDebugger.CallStack", "Stopped, with the call stack on show", context =>
        {
            var debugger = StoppedDebugger(context);

            debugger.SelectedPanelIndex = 2;

            return new ScriptDebuggerWindow { DataContext = debugger };
        }) { Width = 1400, Height = 860 },

        new("ScriptDebugger.Watch", "Stopped, with watch expressions evaluated", context =>
        {
            var debugger = StoppedDebugger(context);
            var controller = new DebugSessionController();

            foreach (var (expression, value, isError) in new[]
            {
                ("$PlayerAlias", "Pat", false),
                ("(Get-Item $ini).Length", "18422", false),
                ("$Missing.Name", "The variable '$Missing' cannot be retrieved because it has not been set.", true),
            })
            {
                debugger.Watch.Items.Add(new WatchItemViewModel(expression, controller) { Value = value, IsError = isError });
            }

            debugger.SelectedPanelIndex = 1;

            return new ScriptDebuggerWindow { DataContext = debugger };
        }) { Width = 1400, Height = 860 },

        new("Window.ManualViewer", "The manual viewer with no document to show", _ =>
            new ManualViewerWindow
            {
                DataContext = new ManualViewerViewModel("Unreal Tournament 2004 Manual", "manual-not-found.pdf"),
            }) { Width = 900, Height = 700 },

        new("Window.InstallOptions", "The install dialog as its own window", _ =>
        {
            var options = new InstallOptionsViewModel
            {
                GameTitle = FixtureGames.Battlefield1942.Title,
                ConfirmButtonText = "Install",
                BaseDownloadSize = 1_180 * GameActionBarViewModel.MB,
                BaseSpaceRequired = 1_650 * GameActionBarViewModel.MB,
            };

            options.InstallDirectories.Add(@"C:\Games");
            options.InstallDirectories.Add(@"D:\Games");
            options.SelectedInstallDirectory = options.InstallDirectories[0];

            foreach (var addon in GameActionBarViewModel.FixtureAddons.Take(5))
            {
                options.Addons.Add(new InstallAddonItemViewModel(new SDK.Models.Game
                {
                    Id = FixtureGames.IdFor(addon.Title),
                    Title = addon.Title,
                    Type = addon.Type,
                    Archives = [new Archive { CompressedSize = addon.DownloadMb * GameActionBarViewModel.MB, UncompressedSize = addon.DownloadMb * GameActionBarViewModel.MB * 3 / 2 }],
                }, addon.Selected));
            }

            return new InstallOptionsWindow { DataContext = options };
        }) { Width = 460, Height = 600 },
    ];

    private const string InstallDirectory = @"C:\Games\Unreal Tournament 2004";

    private static readonly Guid Ut2004 = FixtureGames.IdFor("Unreal Tournament 2004");
    private static readonly Guid DirectX = FixtureGames.IdFor("DirectX 9.0c");

    private static readonly string InstallScriptPath = ScriptHelper.GetScriptFilePath(InstallDirectory, Ut2004, ScriptType.Install);

    /// <summary>The install script as it sits in an install directory, with the header the launcher adds.</summary>
    private const string InstallScript = "#Requires -RunAsAdministrator\r\n\r\n" + ServerInstallScript;

    /// <summary>The install script as the server stores it: the admin flag is kept separately.</summary>
    private const string ServerInstallScript = """
        Write-Host "Configuring Unreal Tournament 2004"

        $ini = Join-Path $InstallDirectory 'System\UT2004.ini'

        Write-ReplaceContentInFile -Regex '^Name=.+' -Replacement "Name=$PlayerAlias" -FilePath $ini
        New-Item -Path 'HKLM:\SOFTWARE\Unreal Technology\Installed Apps\UT2004' -Force | Out-Null

        $Return = 0
        """;

    /// <summary>The installed game's install script, stopped at line 7 in an elevated process.</summary>
    private static ScriptDebuggerWindowViewModel StoppedDebugger(FixtureContext context)
    {
        var debugger = ScriptDebugger(context, installed: true);

        debugger.SelectedDocument!.Breakpoints.Toggle(7);
        debugger.Owners[0].Scripts[0].IsRunning = true;

        debugger.State = DebugSessionState.Stopped;
        debugger.CurrentLine = 7;
        debugger.StatusText = "Stopped at line 7";
        debugger.RemoteSessionText = "Elevated (PID 4242)";

        debugger.CallStack.Show(
        [
            new CallStackFrameInfo { Index = 0, FunctionName = "<ScriptBlock>", ScriptName = InstallScriptPath, LineNumber = 7 },
            new CallStackFrameInfo { Index = 1, FunctionName = "<ScriptBlock>", ScriptName = null, LineNumber = 1 },
        ]);

        debugger.Variables.Show(
        [
            new VariableInfo { Name = "InstallDirectory", TypeName = "String", Value = "\"C:\\Games\\Unreal Tournament 2004\"", HasChildren = false },
            new VariableInfo { Name = "GameManifest", TypeName = "Game", Value = "[Game]", HasChildren = true, Handle = 0 },
            new VariableInfo { Name = "ini", TypeName = "String", Value = "\"C:\\Games\\Unreal Tournament 2004\\System\\UT2004.ini\"", HasChildren = false },
            new VariableInfo { Name = "ServerAddress", TypeName = "String", Value = "\"http://lancommander.lan:1337\"", HasChildren = false },
        ]);

        debugger.Console.AppendLine(ConsoleOutputKind.System, "> Unreal Tournament 2004: Install");
        debugger.Console.AppendLine(ConsoleOutputKind.System, "[debugger] Install script attached: " + InstallScriptPath);
        debugger.Console.AppendLine(ConsoleOutputKind.Host, "Configuring Unreal Tournament 2004");
        debugger.Console.SetDebuggerStopped(true);

        return debugger;
    }

    /// <summary>
    /// A debugger window over a canned workspace: the game's own scripts, a redistributable's, and an addon's
    /// and a tool's that aren't installed. Nothing is read from disk or the server, and no script runs.
    /// </summary>
    private static ScriptDebuggerWindowViewModel ScriptDebugger(FixtureContext context, bool installed)
    {
        var bonusPack = FixtureGames.IdFor("UT2004 Mega Pack");
        var serverTool = FixtureGames.IdFor("UT2004 Dedicated Server");

        ScriptEntry Entry(Guid owner, ScriptOwnerKind kind, string ownerName, ScriptType type, bool requiresAdmin = false, bool onServer = false)
        {
            var local = installed && !onServer;

            return new()
            {
                Key = new ScriptKey(owner, type),
                OwnerKind = kind,
                OwnerName = ownerName,
                ServerScriptId = FixtureGames.IdFor($"script {owner} {type}"),
                Name = type.ToString(),
                RequiresAdmin = requiresAdmin,
                Source = local ? ScriptSource.Installed : ScriptSource.Server,
                LocalPath = local ? ScriptHelper.GetScriptFilePath(InstallDirectory, owner, type) : null,
            DraftPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lc-fixture-no-drafts", owner.ToString(), type.ToString()),
                ServerContents = type == ScriptType.Install && owner == Ut2004 ? ServerInstallScript : "$Return = 0",
            };
        }

        var workspace = new ScriptWorkspace(
            Ut2004,
            "Unreal Tournament 2004",
            installed ? InstallDirectory : null,
            installed,
            [
                new ScriptOwnerNode(ScriptOwnerKind.Game, Ut2004, "Unreal Tournament 2004", false,
                [
                    Entry(Ut2004, ScriptOwnerKind.Game, "Unreal Tournament 2004", ScriptType.Install, requiresAdmin: true),
                    Entry(Ut2004, ScriptOwnerKind.Game, "Unreal Tournament 2004", ScriptType.Uninstall),
                    Entry(Ut2004, ScriptOwnerKind.Game, "Unreal Tournament 2004", ScriptType.NameChange),
                    Entry(Ut2004, ScriptOwnerKind.Game, "Unreal Tournament 2004", ScriptType.KeyChange),
                ]),
                new ScriptOwnerNode(ScriptOwnerKind.Redistributable, DirectX, "DirectX 9.0c", false,
                [
                    Entry(DirectX, ScriptOwnerKind.Redistributable, "DirectX 9.0c", ScriptType.DetectInstall),
                    Entry(DirectX, ScriptOwnerKind.Redistributable, "DirectX 9.0c", ScriptType.Install, requiresAdmin: true),
                ]),
                new ScriptOwnerNode(ScriptOwnerKind.Game, bonusPack, "Mega Pack", true,
                [
                    Entry(bonusPack, ScriptOwnerKind.Game, "Mega Pack", ScriptType.Install, onServer: true),
                ]),
                new ScriptOwnerNode(ScriptOwnerKind.Tool, serverTool, "Dedicated Server", false,
                [
                    Entry(serverTool, ScriptOwnerKind.Tool, "Dedicated Server", ScriptType.BeforeStart, onServer: true),
                ]),
            ]);

        var debugger = new ScriptDebuggerWindowViewModel(context.Services, Ut2004);

        debugger.ApplyWorkspace(workspace);

        // Installed entries point at files that don't exist here; show the script's text regardless.
        if (installed)
            debugger.SelectedDocument!.Load(InstallScript);

        return debugger;
    }

    private static User Person(string name) => new() { Id = FixtureGames.IdFor("user " + name), UserName = name.ToLowerInvariant(), Alias = name };

    /// <param name="messages">Sender, minutes after 7:30 PM, and the message.</param>
    private static ChatThreadViewModel Thread(string? name, User[] participants, params (User From, int Minute, string Text)[] messages)
    {
        var thread = new ChatThread
        {
            Id = FixtureGames.IdFor("thread " + name + string.Join(",", participants.Select(p => p.UserName))),
            Name = name!,
            Participants = participants.ToList(),
        };

        // Local times, so the clock the window shows doesn't depend on the time zone.
        var start = new DateTime(2026, 9, 20, 19, 30, 0, DateTimeKind.Local);

        // Consecutive messages from one person share a group, as the chat client groups them.
        foreach (var run in messages.Select((m, i) => (m, i)).GroupBy(x => RunIndex(messages, x.i)))
        {
            var from = run.First().m.From;

            thread.MessageGroups.Add(new ChatMessageGroup
            {
                Id = FixtureGames.IdFor($"group {thread.Id} {run.Key}"),
                UserId = from.Id,
                UserName = from.Name,
                Messages = new ObservableCollection<ChatMessage>(run.Select(x => new ChatMessage
                {
                    Id = FixtureGames.IdFor($"message {thread.Id} {x.i}"),
                    UserId = from.Id,
                    UserName = from.Name,
                    SentOn = new DateTimeOffset(start.AddMinutes(x.m.Minute)),
                    Content = x.m.Text,
                })),
            });
        }

        thread.LastActivityOn = new DateTimeOffset(start.AddMinutes(messages[^1].Minute));

        return new ChatThreadViewModel(thread, Me.Id);
    }

    /// <summary>Index of the first message in the run of same-sender messages that <paramref name="index"/> is part of.</summary>
    private static int RunIndex((User From, int Minute, string Text)[] messages, int index)
    {
        while (index > 0 && messages[index - 1].From == messages[index].From)
            index--;

        return index;
    }
}
#endif
