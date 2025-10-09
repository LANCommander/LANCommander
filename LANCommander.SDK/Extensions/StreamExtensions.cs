using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Extensions;

public static class StreamExtensions
{
    public static async Task CopyToAsync(
        this Stream source,
        Stream destination,
        Action<long, long> progressCallback = null,
        CancellationToken cancellationToken = default,
        int bufferSize = 1024 * 1024,
        int reportIntervalBytes = 64 * 1024)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            long transferred = 0;
            long nextReportAt = reportIntervalBytes;

            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken);

                if (read == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                transferred += read;

                if (transferred >= nextReportAt)
                {
                    progressCallback?.Invoke(transferred, source.Length);

                    long multiples = transferred / nextReportAt;
                    nextReportAt = (multiples + 1) * reportIntervalBytes;
                }
            }

            progressCallback?.Invoke(transferred, source.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}