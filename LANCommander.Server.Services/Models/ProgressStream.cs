namespace LANCommander.Server.Services.Models
{
    /// <summary>
    /// Copies a source stream to a temporary file while reporting download progress.
    /// The returned stream owns the temp file and deletes it on dispose.
    /// </summary>
    public static class ProgressStream
    {
        private const int BufferSize = 1024 * 1024; // 1 MB

        public static async Task<Stream> CopyToTempFileAsync(
            Stream source, long? totalBytes, IProgress<MediaDownloadProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            var tempPath = Path.GetTempFileName();
            long bytesTransferred = 0;

            try
            {
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize))
                {
                    var buffer = new byte[BufferSize];
                    int bytesRead;

                    while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        bytesTransferred += bytesRead;

                        progress?.Report(new MediaDownloadProgress
                        {
                            BytesTransferred = bytesTransferred,
                            TotalBytes = totalBytes,
                            Status = "Downloading..."
                        });
                    }
                }

                return new TempFileStream(tempPath);
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private sealed class TempFileStream : FileStream
        {
            private readonly string _path;

            public TempFileStream(string path)
                : base(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            {
                _path = path;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                try
                {
                    if (File.Exists(_path))
                        File.Delete(_path);
                }
                catch { }
            }
        }
    }
}
