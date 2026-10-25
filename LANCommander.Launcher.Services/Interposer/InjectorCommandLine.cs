using System.Text;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>
/// A game action that runs the Interposer's injector in launch mode -- what the redistributable's
/// Injector load method asks for: <c>LANCommander.Interposer.Injector.exe [options] --launch
/// &lt;game.exe&gt; [game arguments ...] [-- dll]</c>. Read back into the game it launches and the DLL
/// it injects, so the launcher can start the game itself with the player's settings in place. Parsed
/// the way the injector's own <c>wmain</c> does.
/// </summary>
public sealed record InjectorCommandLine(
    string Executable,
    string Arguments,
    string DllPath,
    string? Username,
    string? ComputerName,
    string? FastDlUrl)
{
    public const string InjectorFileName = "LANCommander.Interposer.Injector.exe";

    private static readonly string[] DefaultDllNames = ["interposer.dll", "LANCommander.Interposer.dll"];

    /// <summary>
    /// The game an injector command line launches, or null when <paramref name="executable"/> is not
    /// the injector, it is not in launch mode, or the game or DLL it names does not exist.
    /// </summary>
    /// <param name="executable">The full path of the program the action runs.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="workingDirectory">The directory it runs in, which relative paths resolve against.</param>
    public static InjectorCommandLine? TryParse(string executable, string? arguments, string? workingDirectory)
    {
        if (!string.Equals(Path.GetFileName(executable), InjectorFileName, StringComparison.OrdinalIgnoreCase))
            return null;

        var injectorDirectory = Path.GetDirectoryName(executable) ?? string.Empty;
        var baseDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? injectorDirectory : workingDirectory;

        string? username = null, computerName = null, fastDlUrl = null;
        var args = new List<string>();
        var argv = Split(arguments ?? string.Empty);

        for (var i = 0; i < argv.Count; i++)
        {
            switch (argv[i])
            {
                case "--fastdl-url" when i + 1 < argv.Count:
                    fastDlUrl = argv[++i];
                    break;
                case "--username" when i + 1 < argv.Count:
                    username = argv[++i];
                    break;
                case "--computername" when i + 1 < argv.Count:
                    computerName = argv[++i];
                    break;
                default:
                    args.Add(argv[i]);
                    break;
            }
        }

        // Inject mode -- into a process that is already running -- has nothing to launch
        if (args.Count < 2 || args[0] != "--launch")
            return null;

        var gameArguments = new List<string>();
        string? dllArgument = null;
        var separator = args.IndexOf("--", 2);

        if (separator >= 0)
        {
            gameArguments.AddRange(args.Skip(2).Take(separator - 2));

            if (separator + 1 < args.Count)
                dllArgument = args[separator + 1];
        }
        else if (args.Count >= 3)
        {
            // Without a separator, a last argument ending in .dll is the DLL
            if (args[^1].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                dllArgument = args[^1];
                gameArguments.AddRange(args.Skip(2).Take(args.Count - 3));
            }
            else
            {
                gameArguments.AddRange(args.Skip(2));
            }
        }

        var game = Resolve(args[1], baseDirectory);

        if (game == null || !File.Exists(game))
            return null;

        var dll = dllArgument != null
            ? Resolve(dllArgument, baseDirectory)
            : DefaultDllNames.Select(name => Path.Combine(injectorDirectory, name)).FirstOrDefault(File.Exists);

        if (dll == null || !File.Exists(dll))
            return null;

        return new InjectorCommandLine(game, Join(gameArguments), dll, username, computerName, fastDlUrl);
    }

    private static string? Resolve(string path, string baseDirectory)
    {
        try
        {
            return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(baseDirectory, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Splits a command line's arguments the way the Microsoft C runtime does for <c>argv</c>:
    /// whitespace separates arguments outside quotes; 2n backslashes before a quote are n backslashes
    /// and the quote toggles quoting, 2n+1 are n backslashes and a literal quote; inside quotes, a
    /// doubled quote is a literal one.
    /// </summary>
    internal static List<string> Split(string commandLine)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var inArgument = false;

        for (var i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];

            if (c == '\\')
            {
                var backslashes = 0;

                while (i < commandLine.Length && commandLine[i] == '\\')
                {
                    backslashes++;
                    i++;
                }

                if (i < commandLine.Length && commandLine[i] == '"')
                {
                    current.Append('\\', backslashes / 2);

                    if (backslashes % 2 == 1)
                        current.Append('"');
                    else
                        inQuotes = !inQuotes;
                }
                else
                {
                    current.Append('\\', backslashes);
                    i--;
                }

                inArgument = true;
            }
            else if (c == '"')
            {
                if (inQuotes && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                inArgument = true;
            }
            else if (!inQuotes && (c == ' ' || c == '\t'))
            {
                if (inArgument)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    inArgument = false;
                }
            }
            else
            {
                current.Append(c);
                inArgument = true;
            }
        }

        if (inArgument)
            result.Add(current.ToString());

        return result;
    }

    /// <summary>Joins arguments into a command line that <see cref="Split"/> reads back unchanged.</summary>
    internal static string Join(IEnumerable<string> arguments) => string.Join(' ', arguments.Select(Quote));

    private static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '"']) < 0)
            return argument;

        var quoted = new StringBuilder("\"");

        for (var i = 0; i < argument.Length; i++)
        {
            var backslashes = 0;

            while (i < argument.Length && argument[i] == '\\')
            {
                backslashes++;
                i++;
            }

            if (i == argument.Length)
            {
                // Before the closing quote, every backslash is doubled
                quoted.Append('\\', backslashes * 2);
                break;
            }

            if (argument[i] == '"')
                quoted.Append('\\', backslashes * 2 + 1).Append('"');
            else
                quoted.Append('\\', backslashes).Append(argument[i]);
        }

        return quoted.Append('"').ToString();
    }
}
