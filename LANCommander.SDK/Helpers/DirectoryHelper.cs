using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LANCommander.SDK.Helpers
{
    public static class DirectoryHelper
    {
        public static void DeleteEmptyDirectories(string directory)
        {
            if (!String.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(directory))
                        DeleteEmptyDirectories(dir);

                    var entries = Directory.EnumerateFileSystemEntries(directory);

                    if (!entries.Any())
                    {
                        try
                        {
                            Directory.Delete(directory, true);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
}
