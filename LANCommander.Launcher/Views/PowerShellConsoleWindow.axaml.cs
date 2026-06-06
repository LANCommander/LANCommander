using System;
using Avalonia.Controls;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views.Components;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Views;

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

