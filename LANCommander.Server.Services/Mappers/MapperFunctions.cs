using Entities = LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Mappers
{
    /// <summary>
    /// Pure helper functions shared by the Mapperly mappers for logic that cannot be
    /// expressed with mapping attributes (version resolution from related archives/versions).
    /// </summary>
    public static class MapperFunctions
    {
        public static string? LatestArchiveVersion(ICollection<Entities.Archive>? archives)
            => archives != null && archives.Any()
                ? archives.OrderByDescending(a => a.CreatedOn).First().Version
                : null;

        public static string? ManifestVersion(ICollection<Entities.GameVersion>? versions, ICollection<Entities.Archive>? archives)
            => versions != null && versions.Any()
                ? versions.OrderByDescending(v => v.SortOrder).ThenByDescending(v => v.CreatedOn).First().Version
                : (archives != null && archives.Any()
                    ? archives.OrderByDescending(a => a.CreatedOn).First().Version
                    : null);
    }
}
