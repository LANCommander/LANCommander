using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.Services.ScriptDebugging;

/// <summary>
/// Local edits of scripts for games that are not installed yet. A draft is written into the install
/// directory the first time its script runs while the debugger is watching, or when the user applies it.
/// </summary>
/// <remarks>Static and file-based so the debugger can promote a draft from a script-client thread.</remarks>
public static class ScriptDrafts
{
    /// <summary>Where drafts are kept. Defaults to the launcher's config directory; tests point it elsewhere.</summary>
    public static string? RootDirectory { get; set; }

    public static string GetPath(Guid gameId, ScriptKey key) =>
        Path.Combine(
            RootDirectory ?? AppPaths.GetConfigPath("ScriptDebugger", "Drafts"),
            gameId.ToString(),
            key.OwnerId.ToString(),
            ScriptHelper.GetScriptFileName(key.Type));

    public static bool Exists(Guid gameId, ScriptKey key) => File.Exists(GetPath(gameId, key));

    public static async Task SaveAsync(Guid gameId, ScriptKey key, string contents)
    {
        var path = GetPath(gameId, key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await File.WriteAllTextAsync(path, contents);
    }

    public static void Discard(Guid gameId, ScriptKey key)
    {
        var path = GetPath(gameId, key);

        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// Write the draft into <paramref name="scriptPath"/> and remove it. Drafts are edits of the server's
    /// text, which is stored without the <c>#Requires -RunAsAdministrator</c> header the launcher adds on
    /// disk, so the header is restored here; without it the script would not be elevated.
    /// </summary>
    /// <returns>True if there was a draft to promote.</returns>
    public static bool TryPromote(Guid gameId, ScriptKey key, string scriptPath, bool requiresAdmin)
    {
        var draftPath = GetPath(gameId, key);

        if (!File.Exists(draftPath))
            return false;

        var contents = File.ReadAllText(draftPath);

        WriteScriptFile(scriptPath, contents, requiresAdmin);

        File.Delete(draftPath);

        return true;
    }

    /// <summary>Write script text to its install-directory file the way the launcher's installer does.</summary>
    public static void WriteScriptFile(string scriptPath, string contents, bool requiresAdmin)
    {
        contents = ScriptHelper.StripRequiresAdminHeader(contents);

        if (requiresAdmin)
            contents = "#Requires -RunAsAdministrator\r\n\r\n" + contents;

        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);

        File.WriteAllText(scriptPath, contents);
    }
}
