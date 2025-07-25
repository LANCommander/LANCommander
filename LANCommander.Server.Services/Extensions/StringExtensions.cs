using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LANCommander.Server.Services.Extensions
{
    public static class StringExtensions
    {
        static readonly Regex WordDelimiters = new Regex(@"[\s—–_]", RegexOptions.Compiled);
        static readonly Regex InvalidChars = new Regex(@"[^a-z0-9\-]", RegexOptions.Compiled);
        static readonly Regex MultipleHyphens = new Regex(@"-{2,}", RegexOptions.Compiled);

        public static string ToUrlSlug(this string value)
        {
            value = value.ToLowerInvariant();

            value = RemoveDiacritics(value);
            value = WordDelimiters.Replace(value, "-");
            value = InvalidChars.Replace(value, "");
            value = MultipleHyphens.Replace(value, "-");

            return value.Trim('-');
        }

        private static string RemoveDiacritics(string stIn)
        {
            string stFormD = stIn.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            for (int ich = 0; ich < stFormD.Length; ich++)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[ich]);

                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(stFormD[ich]);
            }

            return (sb.ToString().Normalize(NormalizationForm.FormC));
        }

        public static string StripPunctuation(this string value)
        {
            return new string(value.Where(c => !char.IsPunctuation(c)).ToArray());
        }

        public static string SanitizeFilename(this string filename, string replacement = "")
        {
            var removeInvalidChars = new Regex($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

            return removeInvalidChars.Replace(filename, replacement);
        }

        public static string ToPath(this string path)
        {
            return Path.Combine(path.Split("/"));
        }
    }
}
