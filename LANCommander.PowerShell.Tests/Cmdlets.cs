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

        [TestMethod]
        [DataRow(640, 480, 640, 360, 16, 9)]
        [DataRow(1024, 768, 1024, 576, 16, 9)]
        [DataRow(1600, 1200, 1600, 900, 16, 9)]
        [DataRow(1920, 1080, 1440, 1080, 4, 3)]
        [DataRow(1366, 1024, 1024, 768, 4, 3)]
        [DataRow(854, 480, 640, 480, 4, 3)]
        public void ConvertAspectRatioShouldReturnCorrectBounds(int x1, int y1, int x2, int y2, int ratioX, int ratioY)
        {
            var aspectRatio = (double)ratioX / (double)ratioY;

            var cmdlet = new ConvertAspectRatioCmdlet()
            {
                AspectRatio = aspectRatio,
                Width = x1,
                Height = y1
            };

            var output = cmdlet.Invoke().OfType<DisplayResolution>().ToList();

            Assert.AreEqual(1, output.Count);

            var bounds = output.First();

            Assert.AreEqual(x2, bounds.Width);
            Assert.AreEqual(y2, bounds.Height);
        }
    }
}
