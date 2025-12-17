namespace LANCommander.Helpers
{
    public static class FileHelpers
    {
        public static void DeleteIfExists(string? path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
