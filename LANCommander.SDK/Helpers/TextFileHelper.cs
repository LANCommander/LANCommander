using System.IO;
using System.Text.RegularExpressions;
using AutoMapper;

namespace LANCommander.SDK.Helpers;

internal static class TextFileHelper
{
    internal static string ReplaceAll(string path, string regexPattern, string substitution, RegexOptions options = RegexOptions.Multiline | RegexOptions.IgnoreCase)
    {
        path = Regex.Replace(path, @"[/|\\]", Path.DirectorySeparatorChar.ToString(), options);
        
        if (!File.Exists(path))
            throw new FileNotFoundException("File not found", path);
        
        var text = File.ReadAllText(path);
        var update = Regex.Replace(text, regexPattern, substitution, options);

        if (text != update)
        {
            File.WriteAllText(path, update);

            return update;
        }

        return text;
    }
}