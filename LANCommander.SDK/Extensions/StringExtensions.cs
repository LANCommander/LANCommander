using System.IO;
using System.Text.RegularExpressions;

namespace LANCommander.SDK.Extensions
{
    public static class StringExtensions
    {
        public static string SanitizeFilename(this string filename, string replacement = "")
        {
            var colonInTitle = new Regex(@"(\w)(: )(\w)");
            var removeInvalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

            filename = colonInTitle.Replace(filename, "$1 - $3");
            filename = removeInvalidChars.Replace(filename, replacement);

            return filename;
        }
    }
}
