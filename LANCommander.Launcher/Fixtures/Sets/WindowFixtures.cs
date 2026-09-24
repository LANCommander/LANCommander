#if DEBUG
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.Views;
using LANCommander.Launcher.Views.Components;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>Windows of their own: chat, the script console, the manual viewer and the install dialog.</summary>
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

        new("Console.Output", "The script console while a script runs", _ =>
        {
            var window = Console();

            foreach (var line in ScriptOutput)
                window.ConsoleControl.OnOutput(LogLevel.Information, line);

            return window;
        }) { Width = 1000, Height = 650 },

        new("Console.DebugBreak", "The script console after a script, waiting for commands", _ =>
        {
            var window = Console();

            foreach (var line in ScriptOutput)
                window.ConsoleControl.OnOutput(LogLevel.Information, line);

            // What a debug break prints; the break itself waits on the user forever.
            window.ConsoleControl.OnOutput(LogLevel.Information, "\n--------- DEBUG MODE ---------");
            window.ConsoleControl.OnOutput(LogLevel.Information, "Script execution complete. You can now run commands.");
            window.ConsoleControl.OnOutput(LogLevel.Information, "Type 'exit' to close this window.\n");
            window.ConsoleControl.IsInputEnabled = true;

            return window;
        }) { Width = 1000, Height = 650 },

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

    private static readonly string[] ScriptOutput =
    [
        "Running install script for Unreal Tournament 2004",
        @"Writing C:\Games\Unreal Tournament 2004\System\UT2004.ini",
        "Setting player name to Pat",
        "Registering CD key",
        "Install script finished with exit code 0",
    ];

    private static PowerShellConsoleWindow Console() => new()
    {
        DataContext = new PowerShellConsoleViewModel("Install Scripts - Unreal Tournament 2004", @"C:\Games\Unreal Tournament 2004"),
    };

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
