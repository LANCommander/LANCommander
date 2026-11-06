using LANCommander.Helpers;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    /// <summary>
    /// Stores files attached to issues. Each file lives at <c>{storage location}/{issue id}/{attachment id}</c>,
    /// so the name it was uploaded with never reaches the file system.
    /// </summary>
    public sealed class IssueAttachmentService(
        ILogger<IssueAttachmentService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        StorageLocationService storageLocationService) : BaseDatabaseService<IssueAttachment>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        private const int MaxFileNameLength = 255;

        public override async Task<IssueAttachment> AddAsync(IssueAttachment entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Issue);
                await context.UpdateRelationshipAsync(a => a.StorageLocation);
            });
        }

        public override async Task<IssueAttachment> UpdateAsync(IssueAttachment entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(a => a.Issue);
                await context.UpdateRelationshipAsync(a => a.StorageLocation);
            });
        }

        /// <summary>Saves an uploaded file and records it against the issue.</summary>
        public async Task<IssueAttachment> AddAsync(Guid issueId, string fileName, Stream content)
        {
            var storageLocation = await GetStorageLocationAsync();

            var attachment = await AddAsync(new IssueAttachment
            {
                IssueId = issueId,
                FileName = CleanFileName(fileName),
                StorageLocationId = storageLocation.Id,
            });

            attachment.StorageLocation = storageLocation;

            var path = GetPath(attachment);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                await using (var file = File.Create(path))
                    await content.CopyToAsync(file);

                attachment.Size = new FileInfo(path).Length;

                return await UpdateAsync(attachment);
            }
            catch
            {
                FileHelpers.DeleteIfExists(path);
                await base.DeleteAsync(attachment);

                throw;
            }
        }

        public override async Task DeleteAsync(IssueAttachment entity)
        {
            if (entity.StorageLocation == null)
                entity = await GetAsync(entity.Id) ?? entity;

            if (entity.StorageLocation != null)
                FileHelpers.DeleteIfExists(GetPath(entity));

            await base.DeleteAsync(entity);
        }

        public string GetPath(IssueAttachment attachment)
        {
            var storageLocation = attachment.StorageLocation
                ?? throw new InvalidOperationException($"The storage location of attachment {attachment.Id} wasn't loaded");

            return AppPaths.ResolveStorageLocationPath(storageLocation.Path, attachment.IssueId.ToString(), attachment.Id.ToString());
        }

        /// <summary>
        /// The default attachment location. Servers set up before attachments existed get one in the
        /// config directory the first time a file is attached.
        /// </summary>
        private async Task<StorageLocation> GetStorageLocationAsync()
        {
            var existing = await storageLocationService.DefaultAsync(StorageLocationType.IssueAttachment);

            if (existing != null)
                return existing;

            var added = await storageLocationService.AddMissingAsync(
                l => l.Type == StorageLocationType.IssueAttachment && l.Default,
                new StorageLocation
                {
                    // Relative to the data directory, like the default made at startup
                    Path = "Attachments",
                    Type = StorageLocationType.IssueAttachment,
                    Default = true,
                });

            return added.Value;
        }

        /// <summary>Keeps only the name part of what the client sent, trimmed to fit the column.</summary>
        private static string CleanFileName(string fileName)
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
    }
}
