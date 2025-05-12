using System.IO;
using System.Text;
using MadMilkman.Ini;

namespace LANCommander.SDK.Helpers
{
    public static class IniHelper
    {
        public static readonly IniOptions DefaultIniOptions = new()
        {
        };

        public static IniFile FromString(string iniFileContent)
        {
            return FromString(iniFileContent, DefaultIniOptions);
        }

        public static IniFile FromString(string iniFileContent, IniOptions options)
        {
            IniFile file = new(options);

            using (var stream = new MemoryStream(options.Encoding.GetBytes(iniFileContent)))
                file.Load(stream);

            return file;
        }

        public static string ToString(IniFile file, IniOptions options)
        {
            return ToString(file, options.Encoding);
        }

        public static string ToString(IniFile file, Encoding encoding)
        {
            string iniFileContent;
            using (var stream = new MemoryStream())
            using (var reader = new StreamReader(stream, encoding))
            {
                file.Save(stream);
                stream.Flush();
                stream.Seek(0, SeekOrigin.Begin);

                iniFileContent = reader.ReadToEnd();
            }

            return iniFileContent;
        }
    }
}
