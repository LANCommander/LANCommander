namespace LANCommander.Server.Extensions
{
    public static class FileExtensions
    {
        public static bool IsBinaryFile(this FileInfo fileInfo, int checkLength = 8000, int requiredConsecutiveNul = 1)
        {
            const char nulChar = '\0';

            int count = 0;

            using (var reader = new StreamReader(fileInfo.FullName))
            {
                for (int i = 0; i < checkLength && !reader.EndOfStream; i++) {
                    if ((char)reader.Read() == nulChar)
                    {
                        count++;

                        if (count >= requiredConsecutiveNul)
                            return true;
                    }
                    else
                    {
                        count = 0;
                    }
                }
            }

            return false;
        }
    }
}
