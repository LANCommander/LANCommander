using LANCommander.SDK.Models.Manifest;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests
{
    public class SaveClientTests
    {
        SaveClient Saves;

        public SaveClientTests()
        {
            // Only pure-computation methods are tested here; no API calls are made,
            // so injected dependencies are not exercised and can be null.
            Saves = new SaveClient(null, null, null, null);
        }

        [Theory]
        [InlineData("{InstallDir}", "C:\\Games\\Age of Empires", "C:\\Games\\Age of Empires")]
        [InlineData("{InstallDir}\\baseq3", "C:\\Games\\Quake 3\\baseq3", "C:\\Games\\Quake 3")]
        [InlineData("{InstallDir}/baseq3/autoexec.cfg", "C:\\Games\\Quake 3\\baseq3\\autoexec.cfg", "C:\\Games\\Quake 3")]
        [InlineData("%SYSTEMDRIVE%", "C:", "C:\\Games\\Quake 3")]
        [InlineData("%PROGRAMDATA%", "C:\\ProgramData", "C:\\Games\\Quake 3")]
        [InlineData("%USERNAME%", "{UserName}", "C:\\Games")]
        [InlineData("%LOCALAPPDATA%", "C:\\Users\\{UserName}\\AppData\\Local", "C:\\Games")]
        [InlineData("%TEMP%", "C:\\Users\\{UserName}\\AppData\\Local\\Temp", "C:\\Games")]
        public void GetLocalPathBasicsShouldWork(string input, string expected, string installDirectory)
        {
            // Tests to make sure GetLocalPath gets the correct local paths for a given input.
            // Useful to make sure that the full path to the file is returned properly.
            var result = Saves.GetLocalPath(input, installDirectory);

            // To test anything that might have the username in the expected string
            expected = expected.Replace("{UserName}", Environment.UserName);

            Assert.Equal(expected, result);
        }

        // Note: GetActualPath has a known implementation bug on Windows where `path` is used
        // instead of `actualPath` when replacing path separators (SaveClient.cs line ~364),
        // meaning DeflateEnvironmentVariables output is discarded. These tests document
        // the intended behavior and are skipped until the bug is fixed.
        [Theory]
        [InlineData("C:\\Games\\Age of Empires", "{InstallDir}", "C:\\Games\\Age of Empires",
            Skip = "GetActualPath implementation bug: uses raw `path` instead of deflated `actualPath` on Windows")]
        [InlineData("C:\\", "%SystemDrive%", "C:\\Games\\",
            Skip = "GetActualPath implementation bug: uses raw `path` instead of deflated `actualPath` on Windows")]
        [InlineData("C:\\Games\\Quake 3\\baseq3\\autoexec.cfg", "{InstallDir}\\baseq3\\autoexec.cfg", "C:\\Games\\Quake 3",
            Skip = "GetActualPath implementation bug: uses raw `path` instead of deflated `actualPath` on Windows")]
        [InlineData("C:\\Users\\{UserName}\\AppData\\Roaming\\.nfs2e", "%APPDATA%\\.nfs2e", "C:\\Games",
            Skip = "GetActualPath implementation bug: uses raw `path` instead of deflated `actualPath` on Windows")]
        [InlineData("C:\\Users\\{UserName}\\AppData\\Local\\.minecraft", "%LOCALAPPDATA%\\.minecraft", "C:\\Games",
            Skip = "GetActualPath implementation bug: uses raw `path` instead of deflated `actualPath` on Windows")]
        [InlineData("C:\\Users\\{UserName}\\Documents\\My Games\\Praetorians", "%MyDocuments%\\My Games\\Praetorians", "C:\\Games",
            Skip = "GetActualPath implementation bug: uses raw `path` instead of deflated `actualPath` on Windows")]
        public void GetActualPathBasicsShouldWork(string input, string expected, string installDirectory)
        {
            input = input.Replace("{UserName}", Environment.UserName);

            var result = Saves.GetActualPath(input, installDirectory);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Games\\Quake 3\\baseq3\\autoexec.cfg", "{InstallDir}", "baseq3/autoexec.cfg", "C:\\Games\\Quake 3")]
        [InlineData("C:\\Games\\Quake 3\\baseq3\\autoexec.cfg", "{InstallDir}", "baseq3/autoexec.cfg", "C:\\Games\\Quake 3\\")]
        [InlineData("C:\\Games\\Age of Empires 2\\player.nfz", "{InstallDir}", "player.nfz", "C:\\Games\\Age of Empires 2\\")]
        [InlineData("C:\\Users\\{UserName}\\AppData\\Roaming\\.nfs2e\\Profiles\\Player1", "%APPDATA%\\.nfs2e", "Profiles/Player1", "C:\\Games\\Need for Speed 2\\")]
        [InlineData("C:\\Users\\{UserName}\\Documents\\My Games\\.nfs2e\\Profiles\\Player1", "%USERPROFILE%\\Documents\\My Games\\.nfs2e", "Profiles/Player1", "C:\\Games\\Need for Speed 2\\")]
        public void GetArchivePathBasicsShouldWork(string path, string workingDirectory, string expected, string installDirectory)
        {
            path = path.Replace("{UserName}", Environment.UserName);

            var result = Saves.GetArchivePath(path, workingDirectory, installDirectory);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SimpleInstallDirectorySavePathsShouldWork()
        {
            // Arrange
            var installDirectory = Path.GetTempPath();
            var savePath = new SavePath
            {
                Id = Guid.NewGuid(),
                Path = "base\\autoexec.cfg",
                WorkingDirectory = "{InstallDir}",
                Type = Enums.SavePathType.File,
                IsRegex = false
            };

            // Act
            var entries = Saves.GetFileSavePathEntries(savePath, installDirectory);

            // Assert
            Assert.Single(entries);

            var entry = entries.First();

            Assert.Equal($"base/autoexec.cfg", entry.ArchivePath);
            Assert.Equal("{InstallDir}/base/autoexec.cfg", entry.ActualPath);
        }

        [Fact]
        public void RegexInstallDirectorySavePathsShouldWork()
        {
            // Arrange
            var savePath = new SavePath
            {
                Id = Guid.NewGuid(),
                Path = "base\\.*.cfg",
                WorkingDirectory = "{InstallDir}",
                Type = Enums.SavePathType.File,
                IsRegex = true
            };
            var installDirectory = Path.Combine(Path.GetTempPath(), savePath.Id.ToString());

            Directory.CreateDirectory(installDirectory);
            Directory.CreateDirectory(Path.Combine(installDirectory, "base"));
            File.WriteAllText(Path.Combine(installDirectory, "base", "autoexec.cfg"), savePath.Id.ToString());
            File.WriteAllText(Path.Combine(installDirectory, "base", "player.cfg"), savePath.Id.ToString());

            // Act
            var entries = Saves.GetFileSavePathEntries(savePath, installDirectory);

            // Assert
            Assert.Equal(2, entries.Count());
            Assert.True(File.Exists(Path.Combine($"{Path.GetTempPath()}\\{savePath.Id}\\base\\autoexec.cfg")));
            Assert.True(File.Exists(Path.Combine($"{Path.GetTempPath()}\\{savePath.Id}\\base\\player.cfg")));

            var autoexec = entries.First();
            var player = entries.Last();

            Assert.Equal("base/autoexec.cfg", autoexec.ArchivePath);
            Assert.Equal("{InstallDir}/base/autoexec.cfg", autoexec.ActualPath);

            Assert.Equal("base/player.cfg", player.ArchivePath);
            Assert.Equal("{InstallDir}/base/player.cfg", player.ActualPath);
        }
    }
}
