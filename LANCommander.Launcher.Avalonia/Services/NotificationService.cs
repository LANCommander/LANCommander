using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.Services;

/// <summary>
/// Sends desktop notifications cross-platform:
///   Windows — PowerShell + WinRT XML toast (cover art supported)
///   Linux   — notify-send (cover art supported via -i flag)
///   macOS   — osascript display notification (text only)
/// </summary>
public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void NotifyInstallComplete(string gameTitle, string? coverImagePath, Guid gameId)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SendWindowsToast(gameTitle, $"{gameTitle} is ready to play!", coverImagePath, gameId);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                SendLinuxNotification(gameTitle, $"{gameTitle} is ready to play!", coverImagePath, gameId);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                SendMacNotification(gameTitle, "Installation complete");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send install-complete notification for {Title}", gameTitle);
        }
    }

    public void NotifyInstallFailed(string gameTitle, Guid gameId)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SendWindowsToast(gameTitle, $"{gameTitle} failed to install", null, gameId);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                SendLinuxNotification(gameTitle, $"{gameTitle} failed to install", null, gameId);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                SendMacNotification(gameTitle, "Installation failed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send install-failed notification for {Title}", gameTitle);
        }
    }

    // ── Platform implementations ─────────────────────────────────────────────

    private void SendWindowsToast(string title, string body, string? imagePath, Guid gameId)
    {
        // Build a minimal WinRT toast XML and dispatch it via PowerShell.
        // The launch attribute encodes the game ID as a lancommander:// protocol
        // URL so a click opens this app and navigates straight to the game.
        var launchArg = $"lancommander://game/{gameId}";

        var xml = new StringBuilder();
        xml.Append($"<toast launch=\"{EscapeXml(launchArg)}\">");
        xml.Append("<visual><binding template=\"ToastGeneric\">");
        xml.Append($"<text>{EscapeXml(title)}</text>");
        xml.Append($"<text>{EscapeXml(body)}</text>");
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            xml.Append($"<image placement=\"hero\" src=\"{EscapeXml(imagePath)}\" />");
        xml.Append("</binding></visual>");
        xml.Append("<actions>");
        xml.Append($"<action content=\"View Game\" arguments=\"{EscapeXml(launchArg)}\" activationType=\"protocol\" />");
        xml.Append("</actions>");
        xml.Append("</toast>");

        var ps = new StringBuilder();
        ps.Append("[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; ");
        ps.Append("[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null; ");
        ps.Append($"$xml = New-Object Windows.Data.Xml.Dom.XmlDocument; ");
        ps.Append($"$xml.LoadXml('{xml.ToString().Replace("'", "''")}'); ");
        ps.Append("$toast = [Windows.UI.Notifications.ToastNotification]::new($xml); ");
        ps.Append("[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('LANCommander').Show($toast)");

        RunProcess("powershell", $"-WindowStyle Hidden -NonInteractive -Command \"{ps}\"");
    }

    private void SendLinuxNotification(string title, string body, string? imagePath, Guid gameId)
    {
        // notify-send ≥ 0.7.9 supports --action; clicking it writes the action
        // key to stdout, but since we're fire-and-forget, we rely on the user
        // clicking "View Game" which invokes the app via the default handler
        // (xdg-open lancommander://game/{id}) if a .desktop handler is registered.
        var args = new StringBuilder();
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            args.Append($"-i \"{imagePath}\" ");
        args.Append($"--action=\"view:View Game\" ");
        args.Append($"\"{EscapeShell(title)}\" \"{EscapeShell(body)}\"");

        // Run in background; if action is clicked notify-send prints "view" to
        // stdout — we don't capture that here; xdg-open handles the protocol.
        RunProcess("notify-send", args.ToString());
    }

    private void SendMacNotification(string title, string body)
    {
        RunProcess("osascript",
            $"-e 'display notification \"{EscapeShell(body)}\" with title \"{EscapeShell(title)}\"'");
    }

    private void RunProcess(string executable, string arguments)
    {
        var psi = new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        process?.WaitForExit(5000);
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string EscapeShell(string value) =>
        value.Replace("\"", "\\\"").Replace("'", "\\'");
}
