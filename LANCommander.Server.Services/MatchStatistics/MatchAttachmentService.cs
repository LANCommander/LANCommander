using System.Security.Cryptography;
using LANCommander.Helpers;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// Stores files kept with matches: demos, replays, logs. Each file lives at
/// <c>{storage location}/{match id}/{attachment id}</c>, so the name it was uploaded with never reaches
/// the file system. Large files are sent in chunks against a declared size and checked when complete.
/// </summary>
public sealed class MatchAttachmentService(
    IDbContextFactory<DatabaseContext> contextFactory,
    StorageLocationService storageLocationService,
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<MatchAttachmentService> logger)
{
    private const int MaxFileNameLength = 255;

    private Settings.Models.MatchStatisticsSettings Settings => settingsProvider.CurrentValue.Server.MatchStatistics;

    public long MaxSize => Math.Max(1, Settings.MaxAttachmentSize) * 1024L * 1024L;

    /// <summary>Declares a file whose bytes follow with <see cref="WriteChunkAsync"/> and <see cref="CompleteAsync"/>.</summary>
    /// <exception cref="KeyNotFoundException">The match or reporter doesn't exist.</exception>
    /// <exception cref="MatchAccessDeniedException">The caller isn't a reporter of the match.</exception>
    /// <exception cref="ArgumentException">The size is missing or too large, or the hash isn't SHA-256 hex.</exception>
    public async Task<MatchAttachmentInformation> CreateAsync(Guid matchId, MatchAttachmentCreateRequest request, MatchReporterContext caller, CancellationToken cancellationToken = default)
    {
        if (request.Size <= 0)
            throw new ArgumentException("The file's size is required", nameof(request));

        if (request.Size > MaxSize)
            throw new ArgumentException($"Attachments are at most {Settings.MaxAttachmentSize} MB", nameof(request));

        var sha256 = NormalizeHash(request.Sha256);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        await MatchService.GetMatchForReporterAsync(context, matchId, caller, cancellationToken);

        if (request.ReporterId is { } reporterId)
        {
            var reporter = await context.MatchReporters!.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reporterId && r.MatchId == matchId, cancellationToken)
                ?? throw new KeyNotFoundException($"Reporter {reporterId} isn't part of match {matchId}");

            if (!caller.IsAdministrator && !caller.Owns(reporter.UserId, reporter.PluginId))
                throw new MatchAccessDeniedException("The reporter belongs to someone else");
        }

        var storageLocation = await GetStorageLocationAsync();
        var now = DateTime.UtcNow;

        var attachment = new MatchAttachment
        {
            MatchId = matchId,
            ReporterId = request.ReporterId,
            Kind = request.Kind,
            FileName = CleanFileName(request.FileName),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim()[..Math.Min(255, request.ContentType.Trim().Length)],
            Size = request.Size,
            Sha256 = sha256,
            StartMilliseconds = request.StartMilliseconds,
            EndMilliseconds = request.EndMilliseconds,
            Status = MatchAttachmentStatus.Pending,
            StorageLocationId = storageLocation.Id,
            CreatedOn = now,
            UpdatedOn = now,
            CreatedById = caller.UserId,
            UpdatedById = caller.UserId,
        };

        context.MatchAttachments!.Add(attachment);

        await context.SaveChangesAsync(cancellationToken);

        attachment.StorageLocation = storageLocation;

        return ToInformation(attachment);
    }

    /// <summary>Writes part of a pending attachment's file at <paramref name="offset"/>. Chunks may arrive in any order.</summary>
    /// <exception cref="KeyNotFoundException">The attachment doesn't exist.</exception>
    /// <exception cref="MatchAccessDeniedException">The attachment isn't the caller's.</exception>
    /// <exception cref="InvalidOperationException">The attachment is no longer pending.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The chunk falls outside the declared size.</exception>
    public async Task WriteChunkAsync(Guid attachmentId, long offset, Stream content, MatchReporterContext caller, CancellationToken cancellationToken = default)
    {
        var attachment = await GetOwnedAsync(attachmentId, caller, cancellationToken);

        if (attachment.Status != MatchAttachmentStatus.Pending)
            throw new InvalidOperationException("The attachment's upload has already completed");

        if (offset < 0 || offset >= attachment.Size)
            throw new ArgumentOutOfRangeException(nameof(offset), "The chunk starts outside the file");

        var path = GetPath(attachment);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);

        file.Position = offset;

        // Copied in pieces so a chunk can't write past the declared size
        var buffer = new byte[81920];
        var remaining = attachment.Size - offset;
        int read;

        while ((read = await content.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining + 1)), cancellationToken)) > 0)
        {
            if (read > remaining)
                throw new ArgumentOutOfRangeException(nameof(content), "The chunk runs past the declared size");

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    /// <summary>
    /// Checks a pending attachment's file against its declared size and hash, marking it uploaded or
    /// failed. A failed file is deleted.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The attachment doesn't exist.</exception>
    /// <exception cref="MatchAccessDeniedException">The attachment isn't the caller's.</exception>
    public async Task<MatchAttachmentInformation> CompleteAsync(Guid attachmentId, MatchReporterContext caller, CancellationToken cancellationToken = default)
    {
        var attachment = await GetOwnedAsync(attachmentId, caller, cancellationToken);

        if (attachment.Status != MatchAttachmentStatus.Pending)
            return ToInformation(attachment);

        var path = GetPath(attachment);
        var info = new FileInfo(path);
        var failure = (string?)null;

        if (!info.Exists || info.Length != attachment.Size)
            failure = $"received {(info.Exists ? info.Length : 0)} of {attachment.Size} bytes";
        else if (attachment.Sha256 != null)
        {
            await using var file = File.OpenRead(path);

            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken)).ToLowerInvariant();

            if (hash != attachment.Sha256)
                failure = "the SHA-256 doesn't match";
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.Attach(attachment);

        var now = DateTime.UtcNow;

        if (failure == null)
        {
            attachment.Status = MatchAttachmentStatus.Uploaded;
            attachment.UploadedOn = now;
            attachment.ExpiresOn = Settings.AttachmentRetention > 0 ? now.AddDays(Settings.AttachmentRetention) : null;
        }
        else
        {
            logger.LogWarning("Match attachment {AttachmentId} failed: {Reason}", attachment.Id, failure);

            attachment.Status = MatchAttachmentStatus.Failed;
            FileHelpers.DeleteIfExists(path);
        }

        attachment.UpdatedOn = now;
        attachment.UpdatedById = caller.UserId;

        await context.SaveChangesAsync(cancellationToken);

        return ToInformation(attachment);
    }

    /// <summary>
    /// Stores a whole file at once. A size of zero takes the stream's length when it has one.
    /// </summary>
    /// <exception cref="InvalidDataException">The file doesn't match its declared size or hash.</exception>
    public async Task<MatchAttachmentInformation> AddAsync(Guid matchId, MatchAttachmentCreateRequest request, Stream content, MatchReporterContext caller, CancellationToken cancellationToken = default)
    {
        if (request.Size <= 0 && content.CanSeek)
            request.Size = content.Length - content.Position;

        var created = await CreateAsync(matchId, request, caller, cancellationToken);

        await WriteChunkAsync(created.Id, 0, content, caller, cancellationToken);

        var completed = await CompleteAsync(created.Id, caller, cancellationToken);

        if (completed.Status != MatchAttachmentStatus.Uploaded)
            throw new InvalidDataException("The file doesn't match its declared size or hash");

        return completed;
    }

    /// <summary>The attachment and its file, if it was uploaded. The caller disposes the stream.</summary>
    public async Task<(MatchAttachmentInformation Attachment, Stream Content)?> OpenAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var attachment = await context.MatchAttachments!.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);

        if (attachment is not { Status: MatchAttachmentStatus.Uploaded })
            return null;

        var path = GetPath(attachment);

        if (!File.Exists(path))
            return null;

        return (ToInformation(attachment), File.OpenRead(path));
    }

    public async Task<bool> DeleteAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var attachment = await context.MatchAttachments!.FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);

        if (attachment == null)
            return false;

        FileHelpers.DeleteIfExists(GetPath(attachment));

        context.MatchAttachments!.Remove(attachment);

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>Deletes attachments past their retention and uploads that never completed.</summary>
    public async Task<int> CleanUpAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now.AddHours(-Math.Max(1, Settings.PendingAttachmentTimeout));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var expired = await context.MatchAttachments!
            .Where(a => (a.ExpiresOn != null && a.ExpiresOn < now)
                || (a.Status == MatchAttachmentStatus.Pending && a.CreatedOn < staleBefore)
                || a.Status == MatchAttachmentStatus.Failed)
            .Take(500)
            .ToListAsync(cancellationToken);

        foreach (var attachment in expired)
            FileHelpers.DeleteIfExists(GetPath(attachment));

        context.MatchAttachments!.RemoveRange(expired);

        await context.SaveChangesAsync(cancellationToken);

        return expired.Count;
    }

    /// <summary>How many attachment files a storage location holds, and their total size.</summary>
    public async Task<(int Count, long Bytes)> GetUsageAsync(Guid storageLocationId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var sizes = await context.MatchAttachments!
            .Where(a => a.StorageLocationId == storageLocationId && a.Status != MatchAttachmentStatus.Failed)
            .Select(a => a.Size)
            .ToListAsync(cancellationToken);

        return (sizes.Count, sizes.Sum());
    }

    private async Task<MatchAttachment> GetOwnedAsync(Guid attachmentId, MatchReporterContext caller, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var attachment = await context.MatchAttachments!.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Attachment {attachmentId} doesn't exist");

        if (!caller.IsAdministrator && (caller.UserId == null || attachment.CreatedById != caller.UserId))
            throw new MatchAccessDeniedException("The attachment belongs to someone else");

        return attachment;
    }

    private static string GetPath(MatchAttachment attachment)
    {
        var storageLocation = attachment.StorageLocation
            ?? throw new InvalidOperationException($"The storage location of attachment {attachment.Id} wasn't loaded");

        return AppPaths.ResolveStorageLocationPath(storageLocation.Path, attachment.MatchId.ToString(), attachment.Id.ToString());
    }

    /// <summary>The default match attachment location, made the first time a file is attached.</summary>
    private async Task<StorageLocation> GetStorageLocationAsync()
    {
        var existing = await storageLocationService.DefaultAsync(StorageLocationType.MatchAttachment);

        if (existing != null)
            return existing;

        var added = await storageLocationService.AddMissingAsync(
            l => l.Type == StorageLocationType.MatchAttachment && l.Default,
            new StorageLocation
            {
                // Relative to the data directory, like the other defaults
                Path = "MatchAttachments",
                Type = StorageLocationType.MatchAttachment,
                Default = true,
            });

        return added.Value;
    }

    private static string? NormalizeHash(string? sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
            return null;

        var hash = sha256.Trim().ToLowerInvariant();

        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new ArgumentException("The SHA-256 must be 64 hex characters", nameof(sha256));

        return hash;
    }

    private static string CleanFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName?.Replace('\\', '/') ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(name))
            name = "attachment";

        if (name.Length > MaxFileNameLength)
        {
            var extension = Path.GetExtension(name);
            name = name[..(MaxFileNameLength - extension.Length)] + extension;
        }

        return name;
    }

    private static MatchAttachmentInformation ToInformation(MatchAttachment a) => new()
    {
        Id = a.Id,
        MatchId = a.MatchId,
        ReporterId = a.ReporterId,
        Kind = a.Kind,
        FileName = a.FileName,
        ContentType = a.ContentType,
        Size = a.Size,
        Sha256 = a.Sha256,
        Status = a.Status,
        StartMilliseconds = a.StartMilliseconds,
        EndMilliseconds = a.EndMilliseconds,
        UploadedOn = a.UploadedOn,
        ExpiresOn = a.ExpiresOn,
    };
}
