#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Updating;

public enum UpdatePackageKind
{
    /// <summary>A zip of the application's directory, or on macOS of its .app bundle.</summary>
    Archive,

    /// <summary>A Linux AppImage: one executable file that replaces the one running.</summary>
    AppImage,
}

/// <summary>Where the running application is installed and what kind of package updates it.</summary>
public sealed record UpdateInstallation(
    string InstallDirectory,
    string Executable,
    UpdatePackageKind PackageKind,
    bool IsBundle)
{
    /// <summary>Set to <c>1</c> or <c>true</c> to allow a debug build to update itself.</summary>
    public const string AllowDebugUpdatesEnvironmentVariable = "LANCOMMANDER_ALLOW_SELF_UPDATE";

    /// <summary>Describes the running process, or returns null where it can't update itself.</summary>
    public static UpdateInstallation? Detect(bool isDebugBuild = false)
    {
        if (isDebugBuild && !IsEnabled(Environment.GetEnvironmentVariable(AllowDebugUpdatesEnvironmentVariable)))
            return null;

        // Android installs through the system's package installer, and containers update by image
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || EnvironmentHelper.IsRunningInContainer())
            return null;

        var executable = Environment.ProcessPath;

        // Running through the dotnet host (dotnet run, dotnet App.dll) leaves nothing of the app's own to replace
        if (String.IsNullOrWhiteSpace(executable)
            || Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return null;

        // An AppImage runs from a read-only mount; the file to replace is the AppImage itself
        var appImage = Environment.GetEnvironmentVariable("APPIMAGE");

        if (OperatingSystem.IsLinux() && !String.IsNullOrWhiteSpace(appImage) && File.Exists(appImage))
            return new UpdateInstallation(Path.GetDirectoryName(Path.GetFullPath(appImage))!, Path.GetFullPath(appImage), UpdatePackageKind.AppImage, false);

        var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!;

        // A macOS app runs from Name.app/Contents/MacOS; updates replace the bundle's contents
        if (OperatingSystem.IsMacOS()
            && Path.GetFileName(executableDirectory) == "MacOS"
            && Path.GetDirectoryName(executableDirectory) is { } contents
            && Path.GetFileName(contents) == "Contents"
            && Path.GetDirectoryName(contents) is { } bundle
            && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return new UpdateInstallation(bundle, executable, UpdatePackageKind.Archive, true);

        return new UpdateInstallation(executableDirectory, executable, UpdatePackageKind.Archive, false);
    }

    /// <summary>Whether this process can write to the install directory without elevating.</summary>
    public bool IsWritable => DirectoryHelper.IsDirectoryWritable(InstallDirectory);

    /// <summary>The platform name used in release asset names: Windows, Linux or macOS.</summary>
    public static string Platform
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return "Windows";

            if (OperatingSystem.IsMacOS())
                return "macOS";

            return "Linux";
        }
    }

    /// <summary>The architecture name used in release asset names: x64, arm64 or x86.</summary>
    public static string Architecture => RuntimeInformation.ProcessArchitecture switch
    {
        System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
        System.Runtime.InteropServices.Architecture.X86 => "x86",
        _ => "x64",
    };

    private static bool IsEnabled(string? value) =>
        value == "1" || String.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
