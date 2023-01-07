using System.IO;
using System.Text.RegularExpressions;

namespace LANCommander.SDK.Extensions
{
    public static class StringExtensions
    {
        public static string SanitizeFilename(this string filename, string replacement = "")
        {
            var removeInvalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

            return removeInvalidChars.Replace(filename, replacement);
        }
    }
}
