using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests
{
    public class StringExtensions
    {
        [Theory]
        [InlineData("C:\\Games\\Age of Empires 2", "{InstallDir}", "C:\\Games\\Age of Empires 2")]
        [InlineData("C:\\Games\\Age of Empires 2", "{InstallDir}", "C:\\Games\\Age of Empires 2\\")]
        [InlineData("C:\\Games\\Age of Empires 2\\Data", "{InstallDir}\\Data", "C:\\Games\\Age of Empires 2")]
        [InlineData("C:\\Users\\{UserName}\\Documents", "%MyDocuments%", "C:\\Games")]
        [InlineData("C:\\Users\\{UserName}\\Documents", "%MyDocuments%\\", "C:\\Games")]
        [InlineData("C:\\Users\\{UserName}\\Documents\\My Games", "%MyDocuments%\\My Games", "C:\\Games")]
        [InlineData("C:\\Users\\{UserName}\\AppData\\Roaming", "%APPDATA%", "C:\\Games")]
        [InlineData("C:\\Users\\{UserName}\\AppData\\Local", "%LOCALAPPDATA%", "C:\\Games")]
        public void ExpandEnvironmentVariablesShouldExpand(string expected, string input, string installDirectory)
        {
            expected = expected.Replace("{UserName}", Environment.UserName);

            var result = input.ExpandEnvironmentVariables(installDirectory);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("{InstallDir}", "C:\\Games\\Age of Empires 2", "C:\\Games\\Age of Empires 2")]
        [InlineData("{InstallDir}\\", "C:\\Games\\Age of Empires 2\\", "C:\\Games\\Age of Empires 2")]
        [InlineData("{InstallDir}\\Data", "C:\\Games\\Age of Empires 2\\Data", "C:\\Games\\Age of Empires 2")]
        [InlineData("%MyDocuments%", "C:\\Users\\{UserName}\\Documents", "C:\\Games")]
        [InlineData("%MyDocuments%\\", "C:\\Users\\{UserName}\\Documents\\", "C:\\Games")]
        [InlineData("%MyDocuments%\\My Games", "C:\\Users\\{UserName}\\Documents\\My Games", "C:\\Games")]
        [InlineData("%APPDATA%", "C:\\Users\\{UserName}\\AppData\\Roaming", "C:\\Games")]
        [InlineData("%LOCALAPPDATA%", "C:\\Users\\{UserName}\\AppData\\Local", "C:\\Games")]
        public void DeflateEnvironmentVariablesShouldDeflate(string expected, string input, string installDirectory)
        {
            input = input.Replace("{UserName}", Environment.UserName);

            var result = input.DeflateEnvironmentVariables(installDirectory);

            Assert.Equal(expected, result);
        }
    }
}
