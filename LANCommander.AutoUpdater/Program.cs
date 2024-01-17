using System.Diagnostics;
using System.IO.Compression;

var arguments = new Dictionary<string, string>();

if (args.Length % 2 != 0)
    throw new Exception("An invalid number of arguments were supplied!");

for (int i = 0; i < args.Length; i += 2)
{
    arguments[args[i].TrimStart('-')] = args[i + 1];
}

Console.WriteLine("Waiting for LANCommander to stop...");

int delay = 500;
int maxAttempts = 30 * (1000 / delay);

for (int i = 0; i < maxAttempts; i++)
{
    var processes = Process.GetProcessesByName("LANCommander");

    if (processes.Length == 0)
        break;
    else if (i == maxAttempts - 1)
        throw new Exception("Cannot update! LANCommander is still running!");

    await Task.Delay(delay);
}

Console.WriteLine("LANCommander has exited! Updating...");

try
{
    var updateFile = Path.Combine(arguments["Path"], arguments["Version"] + ".zip");

    if (!File.Exists(updateFile))
        throw new ArgumentException("The specified update file does not exist!");

    Console.WriteLine($"Update file found for {arguments["Version"]}. Extracting files...");

    ZipFile.ExtractToDirectory(updateFile, AppDomain.CurrentDomain.BaseDirectory, true);

    Console.WriteLine("Extraction complete!");
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}
finally
{
    Console.WriteLine("Starting LANCommander...");

    Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LANCommander.exe"));

    Console.WriteLine("LANCommander has been started! My job here is done");
}