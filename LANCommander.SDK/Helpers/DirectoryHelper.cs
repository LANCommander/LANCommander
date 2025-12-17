using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        
        public static bool IsDirectoryWritable(string path)
        {
            try
            {
                Directory.CreateDirectory(path);

                var probeFile = Path.Combine(path, $".writetest.{Guid.NewGuid():N}.tmp");

                using (var fs = new FileStream(probeFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.WriteByte(0);
                }

                File.Delete(probeFile);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void MoveContents(string source, string destination)
        {
            if (String.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source directory cannot be empty", nameof(source));
            
            if (String.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Destination directory cannot be empty", nameof(destination));

            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException(source);
            
            MoveDirectoryRecursive(source, destination);
        }

        private static void MoveDirectoryRecursive(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                var fileName = Path.GetFileName(file);
                var destinationFile = Path.Combine(destination, fileName);
                
                if (File.Exists(destinationFile))
                    BackupExistingFile(destinationFile);
                
                File.Move(file, destinationFile);
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                var directoryName = Path.GetFileName(directory);
                var destinationDirectory = Path.Combine(destination, directoryName);
                
                MoveDirectoryRecursive(directory, destinationDirectory);
                
                if (Directory.GetFileSystemEntries(directory).Length == 0)
                    Directory.Delete(directory, true);
            }
        }

        private static void BackupExistingFile(string destination)
        {
            var backupFile = destination + ".bak";
            
            if (File.Exists(backupFile))
                BackupExistingFile(backupFile);
            
            File.Move(destination, backupFile);
        }
    }
}
