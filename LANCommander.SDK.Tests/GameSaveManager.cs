using LANCommander.SDK.Models;

namespace LANCommander.SDK.Tests
{
    public class GameSaveManager
    {
        [Theory]
        [InlineData("{InstallDir}", "C:\\Games\\Age of Empires", "C:\\Games\\Age of Empires")]
        [InlineData("{InstallDir}\\baseq3", "C:\\Games\\Quake 3\\baseq3", "C:\\Games\\Quake 3")]
        [InlineData("{InstallDir}/baseq3/autoexec.cfg", "C:\\Games\\Quake 3\\baseq3\\autoexec.cfg", "C:\\Games\\Quake 3")]
        [InlineData("%SYSTEMDRIVE%", "C:", "C:\\Games\\Quake 3")]
        [InlineData("%PROGRAMDATA%", "C:\\ProgramData", "C:\\Games\\Quake 3")]
        [InlineData("%SYSTEMROOT%", "C:\\Windows", "C:\\Games\\Quake 3")]
        [InlineData("%USERNAME%", "{UserName}", "C:\\Games")]
        [InlineData("%LOCALAPPDATA%", "C:\\Users\\{UserName}\\AppData\\Local", "C:\\Games")]
        [InlineData("%TEMP%", "C:\\Users\\{UserName}\\AppData\\Local\\Temp", "C:\\Games")]
        public void GetLocalPathBasicsShouldWork(string input, string expected, string installDirectory)
        {
            // Tests to make sure GetLocalPath gets the correct local paths for a given input.
            // Useful to make sure that the full path to the file is returned properly.
            var client = new Client("http://localhost:1337");
            var gameSaveManager = new SDK.GameSaveManager(client);

            var result = gameSaveManager.GetLocalPath(input, installDirectory);

            // To test anything that might have the username in the expected string
            expected = expected.Replace("{UserName}", Environment.UserName);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Games\\Age of Empires", "{InstallDir}", "C:\\Games\\Age of Empires")]
        [InlineData("C:\\", "%SystemDrive%\\", "C:\\Games\\")]
        [InlineData("C:\\Games\\Quake 3\\baseq3\\autoexec.cfg", "{InstallDir}\\baseq3\\autoexec.cfg", "C:\\Games\\Quake 3")]

        public void GetArchivePathBasicsShouldWork(string input, string expected, string installDirectory)
        {
            var client = new Client("http://localhost:1337");
            var gameSaveManager = new SDK.GameSaveManager(client);

            var result = gameSaveManager.GetArchivePath(input, installDirectory);

            // To test anything that might have the username in the expected string
            expected = expected.Replace("{UserName}", Environment.UserName);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SimpleInstallDirectorySavePathsShouldWork()
        {
            // Arrange
            var client = new Client("http://localhost:1337");
            var gameSaveManager = new SDK.GameSaveManager(client);
            var installDirectory = Path.GetTempPath();
            var savePath = new SavePath
            {
                Id = Guid.NewGuid(),
                Path = "base\\autoexec.cfg",
                WorkingDirectory = "{InstallDir}",
                Type = "File",
                IsRegex = false
            };

            // Act
            var entries = gameSaveManager.GetFileSavePathEntries(savePath, installDirectory);

            // Assert
            Assert.Single(entries);

            var entry = entries.First();

            Assert.Equal($"{savePath.Id}/base/autoexec.cfg", entry.ArchivePath);
            Assert.Equal("{InstallDir}/base/autoexec.cfg", entry.ActualPath);
        }
    }
}