using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services;

public interface IArchiveService
{
    public Task<IEnumerable<ZipArchiveEntry>> GetContentsAsync(Guid archiveId);
}