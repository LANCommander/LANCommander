using System.IO;
using System.Text.RegularExpressions;

namespace LANCommander.SDK.Helpers;

internal static class TextFileHelper
{
    internal static bool ReplaceAll(string path, string regexPattern, string substitution, RegexOptions options = RegexOptions.Multiline | RegexOptions.IgnoreCase)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File not found", path);
        
        var text = File.ReadAllText(path);
        var update = Regex.Replace(text, regexPattern, substitution, options);

        if (text != update)
        {
            File.WriteAllText(path, update);
            return true;
        }

        return false;
    }
}