using CommandLine;

namespace LANCommander.Packager;

public class Options
{
    [Value(0, MetaName = "installer", HelpText = "Path to installer executable")]
    public string? InstallerPath { get; set; }

    [Option('o', "output", HelpText = "Output .lcx file path")]
    public string? OutputPath { get; set; }
}
