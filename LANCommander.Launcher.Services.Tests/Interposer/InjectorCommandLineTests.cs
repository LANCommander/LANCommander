using LANCommander.Launcher.Services.Interposer;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Interposer;

public class InjectorCommandLineTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("injector-command-line");

    private string Injector => Path.Combine(_directory.FullName, "Bin", InjectorCommandLine.InjectorFileName);
    private string InjectorDll => Path.Combine(_directory.FullName, "Bin", "LANCommander.Interposer.dll");
    private string Game => Path.Combine(_directory.FullName, "Bin", "Game.exe");

    public InjectorCommandLineTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Injector)!);

        foreach (var file in new[] { Injector, InjectorDll, Game })
            File.WriteAllText(file, "");
    }

    public void Dispose() => _directory.Delete(recursive: true);

    [Theory]
    [InlineData("a b c", new[] { "a", "b", "c" })]
    [InlineData("\"C:\\Program Files\\Game\\game.exe\" -fullscreen", new[] { @"C:\Program Files\Game\game.exe", "-fullscreen" })]
    [InlineData("\"a \\\"quoted\\\" word\"", new[] { "a \"quoted\" word" })]
    [InlineData("\"ends in a slash\\\\\" next", new[] { @"ends in a slash\", "next" })]
    [InlineData(@"C:\path\with\backslashes", new[] { @"C:\path\with\backslashes" })]
    [InlineData("\"\" empty", new[] { "", "empty" })]
    [InlineData("  spaced   out  ", new[] { "spaced", "out" })]
    public void ArgumentsSplitAsTheCRuntimeSplitsThem(string commandLine, string[] expected)
    {
        InjectorCommandLine.Split(commandLine).ShouldBe(expected);
    }

    [Fact]
    public void JoinedArgumentsSplitBackUnchanged()
    {
        string[] arguments = ["plain", "with space", "with \"quotes\"", @"trailing\", @"C:\Program Files\", "", "-x"];

        InjectorCommandLine.Split(InjectorCommandLine.Join(arguments)).ShouldBe(arguments);
    }

    [Fact]
    public void ALaunchCommandLineIsTheGameItsArgumentsAndTheDllBesideTheInjector()
    {
        var parsed = InjectorCommandLine.TryParse(Injector, $"--username Player1 --launch \"{Game}\" -fullscreen +map q3dm17", _directory.FullName);

        parsed.ShouldNotBeNull();
        parsed.Executable.ShouldBe(Game);
        parsed.Arguments.ShouldBe("-fullscreen +map q3dm17");
        parsed.DllPath.ShouldBe(InjectorDll);
        parsed.Username.ShouldBe("Player1");
    }

    [Fact]
    public void TheSeparatorEndsTheGamesArgumentsAndNamesTheDll()
    {
        var dll = Path.Combine(_directory.FullName, "Other.dll");
        File.WriteAllText(dll, "");

        var parsed = InjectorCommandLine.TryParse(Injector, $"--launch Bin\\Game.exe -windowed -- Other.dll", _directory.FullName);

        parsed.ShouldNotBeNull();
        parsed.Executable.ShouldBe(Game);
        parsed.Arguments.ShouldBe("-windowed");
        parsed.DllPath.ShouldBe(dll);
    }

    [Fact]
    public void WithoutASeparatorALastArgumentEndingInDllIsTheDll()
    {
        var parsed = InjectorCommandLine.TryParse(Injector, $"--launch \"{Game}\" -fullscreen \"{InjectorDll}\"", null);

        parsed.ShouldNotBeNull();
        parsed.Arguments.ShouldBe("-fullscreen");
        parsed.DllPath.ShouldBe(InjectorDll);
    }

    [Fact]
    public void ArgumentsThatNeedQuotingKeepThem()
    {
        var parsed = InjectorCommandLine.TryParse(Injector, $"--launch \"{Game}\" -name \"Big Player\" --", null);

        parsed.ShouldNotBeNull();
        parsed.Arguments.ShouldBe("-name \"Big Player\"");
    }

    [Theory]
    [InlineData("Game.exe")]                         // inject mode: into a running process
    [InlineData("--launch")]                         // nothing to launch
    [InlineData("--launch Missing.exe")]             // a game that is not there
    [InlineData("--launch Bin\\Game.exe -- Missing.dll")]
    public void AnythingButALaunchOfAGameThatExistsIsNotParsed(string arguments)
    {
        InjectorCommandLine.TryParse(Injector, arguments, _directory.FullName).ShouldBeNull();
    }

    [Fact]
    public void AProgramThatIsNotTheInjectorIsNotParsed()
    {
        InjectorCommandLine.TryParse(Game, $"--launch \"{Game}\"", null).ShouldBeNull();
    }
}
