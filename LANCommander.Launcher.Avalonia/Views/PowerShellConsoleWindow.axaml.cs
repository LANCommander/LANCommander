using System;
using Avalonia.Controls;
using LANCommander.Launcher.Avalonia.ViewModels;
using LANCommander.Launcher.Avalonia.Views.Components;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class PowerShellConsoleWindow : Window
{
    public PowerShellConsoleWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets the console control for wiring up debugger events
    /// </summary>
    public ScriptConsoleControl ConsoleControl => Console;
}

