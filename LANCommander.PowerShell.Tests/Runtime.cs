using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PowerShell.Tests
{
    [TestClass]
    public class RuntimeTests
    {
        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(200)]
        [DataRow(404)]
        public void ExitCodeShouldParse(int errorCode)
        {
            var testScript = $"$Host.SetShouldExit({errorCode})";

            var tempFile = ScriptHelper.SaveTempScript(testScript);

            var script = new PowerShellScript();

            script.UseFile(tempFile);

            var result = script.Execute();

            Assert.AreEqual(errorCode, result);
        }
    }
}
