using System.IO;
using System.Text;
using LANCommander.SDK.Parsers.Ini;

namespace LANCommander.SDK.Helpers
{
    public static class IniHelper
    {
        public static readonly IniParseOptions DefaultOptions = new();

        public static IniDocument FromString(string iniFileContent)
        {
            return IniParser.Parse(iniFileContent, DefaultOptions);
        }

        public static IniDocument FromString(string iniFileContent, IniParseOptions options)
        {
            return IniParser.Parse(iniFileContent, options);
        }

        public static string ToString(IniDocument document)
        {
            return document.Serialize();
        }

        public static string ToString(IniDocument document, Encoding encoding)
        {
            return document.Serialize();
        }
    }
}
