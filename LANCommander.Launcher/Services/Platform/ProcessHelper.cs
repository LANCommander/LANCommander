using System;
using System.Diagnostics;

namespace LANCommander.Launcher.Services.Platform;

/// <summary>
/// Helpers for invoking small CLI tools (pmset, wpctl, pactl, osascript) used by the
/// Linux/macOS platform services.
/// </summary>
internal static class ProcessHelper
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="arguments"/> and returns trimmed
    /// stdout, or null on failure / non-zero exit. Never throws.
    /// </summary>
    public static string? Run(string fileName, string arguments, int timeoutMs = 2000)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            if (!process.Start())
                return null;

            var output = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return null;
            }

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Whether <paramref name="fileName"/> resolves to an executable on PATH.</summary>
    public static bool CommandExists(string fileName)
    {
        // "command -v" is a POSIX shell builtin available on Linux and macOS.
        var result = Run("/bin/sh", $"-c \"command -v {fileName}\"");
        return !string.IsNullOrWhiteSpace(result);
    }
}
