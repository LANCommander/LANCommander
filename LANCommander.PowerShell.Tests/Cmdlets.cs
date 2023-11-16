using LANCommander.PowerShell.Cmdlets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace LANCommander.PowerShell.Tests
{
    [TestClass]
    public class CmdletTests
    {
        [TestMethod]
        public void ConvertToSerializedBase64ShouldBeDeserializable()
        {
            var testPhrase = "Hello world! This should be deserializable back to its original form.";

            var encodingCmdlet = new ConvertToSerializedBase64Cmdlet()
            {
                Input = testPhrase
            };

            var encodingResults = encodingCmdlet.Invoke().OfType<string>().ToList();

            Assert.AreEqual(1, encodingResults.Count);

            var decodingCmdlet = new ConvertFromSerializedBase64Cmdlet()
            {
                Input = encodingResults.First()
            };

            var decodingResults = decodingCmdlet.Invoke().OfType<string>().ToList();

            Assert.AreEqual(1, encodingResults.Count);
            Assert.AreEqual(testPhrase, decodingResults.First());
        }
    }
}
