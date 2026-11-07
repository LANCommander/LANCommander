using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LANCommander.Launcher.Services.Protocol;

/// <summary>
/// Makes the operating system open <c>lancommander://</c> links with this launcher. Runs at every start,
/// so the registration follows the launcher when it moves or updates.
/// <list type="bullet">
/// <item>Windows: a per-user URL protocol under <c>HKCU\Software\Classes</c>, no elevation needed.</item>
/// <item>Linux: a hidden desktop entry in the user's applications directory, set as the
/// <c>x-scheme-handler/lancommander</c> default.</item>
/// <item>macOS: nothing to do at run time. The app bundle's <c>CFBundleURLTypes</c> declares the
/// scheme, and the system delivers links to the running app as an open-URL event.</item>
/// </list>
/// </summary>
public static class ProtocolHandlerRegistration
{
    public const string LinuxDesktopFileName = "lancommander-url-handler.desktop";

    private const string MimeType = $"x-scheme-handler/{ProtocolRequest.Scheme}";

    public static void Register(ILogger logger)
    {
        try
        {
            var command = GetLaunchCommand();

            if (command == null)
            {
                logger.LogWarning("Could not determine the launcher's path; {Scheme}:// links won't open it", ProtocolRequest.Scheme);
                return;
            }

            if (OperatingSystem.IsWindows())
                RegisterWindows(command.Value);
            else if (OperatingSystem.IsLinux())
                RegisterLinux(command.Value, logger);
            else
                return;

            logger.LogInformation("Registered {Scheme}:// protocol handler for {Executable}", ProtocolRequest.Scheme, command.Value.Executable);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to register {Scheme}:// protocol handler", ProtocolRequest.Scheme);
        }
    }

    /// <summary>
    /// What starts this launcher: an AppImage is mounted at a new temporary path each run, so its
    /// handler must name the image itself, and a launcher run through <c>dotnet</c> needs its assembly.
    /// </summary>
    private static (string Executable, string? Assembly)? GetLaunchCommand()
    {
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");

        if (OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(appImage) && File.Exists(appImage))
            return (appImage, null);

        var processPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(processPath))
            return null;

        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assembly = typeof(ProtocolHandlerRegistration).Assembly.Location;

            return string.IsNullOrWhiteSpace(assembly) ? null : (processPath, assembly);
        }

        return (processPath, null);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RegisterWindows((string Executable, string? Assembly) command)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProtocolRequest.Scheme}");
        key.SetValue("", "URL:LANCommander Launcher");
        key.SetValue("URL Protocol", "");

        using (var icon = key.CreateSubKey("DefaultIcon"))
            icon.SetValue("", $"\"{command.Executable}\",0");

        var commandLine = command.Assembly == null
            ? $"\"{command.Executable}\" \"%1\""
            : $"\"{command.Executable}\" \"{command.Assembly}\" \"%1\"";

        using var open = key.CreateSubKey(@"shell\open\command");
        open.SetValue("", commandLine);
    }

    private static void RegisterLinux((string Executable, string? Assembly) command, ILogger logger)
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        if (string.IsNullOrWhiteSpace(dataHome))
            dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

        var applications = Path.Combine(dataHome, "applications");
        var path = Path.Combine(applications, LinuxDesktopFileName);

        var exec = string.Join(' ', new[] { command.Executable, command.Assembly }
            .Where(a => a != null)
            .Select(a => QuoteDesktopExecArgument(a!)));

        var entry = BuildLinuxDesktopEntry(exec);

        // Rewriting an unchanged entry would only make the desktop rescan its applications
        if (!File.Exists(path) || File.ReadAllText(path) != entry)
        {
            Directory.CreateDirectory(applications);
            File.WriteAllText(path, entry, new UTF8Encoding(false));

            RunBestEffort("update-desktop-database", logger, applications);
        }

        // Every start, not just when the entry changes: another app may have claimed the scheme since,
        // and a first attempt may have run before xdg-utils was installed
        RunBestEffort("xdg-mime", logger, "default", LinuxDesktopFileName, MimeType);
    }

    internal static string BuildLinuxDesktopEntry(string exec) =>
        "[Desktop Entry]\n" +
        "Type=Application\n" +
        "Name=LANCommander Launcher\n" +
        "Comment=Opens lancommander:// links\n" +
        $"Exec={exec} %u\n" +
        "Icon=lancommander\n" +
        "Terminal=false\n" +
        // The launcher's own menu entry comes from its package; this one exists only to open links
        "NoDisplay=true\n" +
        $"MimeType={MimeType};\n";

    /// <summary>
    /// Quotes an argument for a desktop entry's <c>Exec</c> key: the quoting rule escapes <c>"</c>,
    /// <c>`</c>, <c>$</c> and <c>\</c>, the string-value rule then doubles every backslash, and a literal
    /// <c>%</c> is written <c>%%</c> so it isn't read as a field code.
    /// </summary>
    internal static string QuoteDesktopExecArgument(string argument)
    {
        var quoted = new StringBuilder("\"");

        foreach (var c in argument)
        {
            if (c is '"' or '`' or '$' or '\\')
                quoted.Append('\\');

            quoted.Append(c);
        }

        quoted.Append('"');

        return quoted.ToString().Replace("\\", "\\\\").Replace("%", "%%");
    }

    private static void RunBestEffort(string fileName, ILogger logger, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);

            if (process != null && !process.WaitForExit(5000))
                process.Kill();
        }
        catch (Exception ex)
        {
            // Desktops without xdg-utils still read the entry's MimeType when they next scan
            logger.LogDebug(ex, "Could not run {FileName} while registering the protocol handler", fileName);
        }
    }
}
