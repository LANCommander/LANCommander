using LANCommander.SDK.Enums;
using Riok.Mapperly.Abstractions;

namespace LANCommander.Server.Services.Mappers
{
    /// <summary>
    /// Mapperly mapper for Data.Models.* &lt;-&gt; SDK.Models.Manifest.* (all the AutoMapper manifest
    /// maps declared reversible in MappingProfile.CreateManifestMappings). Replaces the AutoMapper
    /// runtime mapper for the manifest export/import surface.
    /// Fully-qualified type names are used deliberately (Mapperly does not handle namespace aliases,
    /// and short names collide between Manifest and Data models).
    /// Reverse (ToData) parameters are nullable so Mapperly reuses these user mappings for nested
    /// collection elements — the SDK manifest types are nullable-oblivious, so their collection
    /// element type is inferred as nullable; a non-nullable parameter would force Mapperly to emit a
    /// separate inline mapper that silently drops [MapProperty] customizations (e.g. IsPrimaryAction).
    /// </summary>
    [Mapper(AllowNullPropertyAssignment = true)]
    public partial class ManifestMapper
    {
        // ---- Simple 1:1 element maps (both directions) -----------------------------------------
        public partial LANCommander.SDK.Models.Manifest.Archive ToManifest(LANCommander.Server.Data.Models.Archive source);
        public partial LANCommander.Server.Data.Models.Archive ToData(LANCommander.SDK.Models.Manifest.Archive? source);

        public partial LANCommander.SDK.Models.Manifest.Collection ToManifest(LANCommander.Server.Data.Models.Collection source);
        public partial LANCommander.Server.Data.Models.Collection ToData(LANCommander.SDK.Models.Manifest.Collection? source);

        public partial LANCommander.SDK.Models.Manifest.Company ToManifest(LANCommander.Server.Data.Models.Company source);
        public partial LANCommander.Server.Data.Models.Company ToData(LANCommander.SDK.Models.Manifest.Company? source);

        public partial LANCommander.SDK.Models.Manifest.Engine ToManifest(LANCommander.Server.Data.Models.Engine source);
        public partial LANCommander.Server.Data.Models.Engine ToData(LANCommander.SDK.Models.Manifest.Engine? source);

        public partial LANCommander.SDK.Models.Manifest.GameCustomField ToManifest(LANCommander.Server.Data.Models.GameCustomField source);
        public partial LANCommander.Server.Data.Models.GameCustomField ToData(LANCommander.SDK.Models.Manifest.GameCustomField? source);

        public partial LANCommander.SDK.Models.Manifest.GameExternalId ToManifest(LANCommander.Server.Data.Models.GameExternalId source);
        public partial LANCommander.Server.Data.Models.GameExternalId ToData(LANCommander.SDK.Models.Manifest.GameExternalId? source);

        public partial LANCommander.SDK.Models.Manifest.Genre ToManifest(LANCommander.Server.Data.Models.Genre source);
        public partial LANCommander.Server.Data.Models.Genre ToData(LANCommander.SDK.Models.Manifest.Genre? source);

        public partial LANCommander.SDK.Models.Manifest.Issue ToManifest(LANCommander.Server.Data.Models.Issue source);
        public partial LANCommander.Server.Data.Models.Issue ToData(LANCommander.SDK.Models.Manifest.Issue? source);

        public partial LANCommander.SDK.Models.Manifest.Key ToManifest(LANCommander.Server.Data.Models.Key source);
        public partial LANCommander.Server.Data.Models.Key ToData(LANCommander.SDK.Models.Manifest.Key? source);

        public partial LANCommander.SDK.Models.Manifest.Media ToManifest(LANCommander.Server.Data.Models.Media source);
        public partial LANCommander.Server.Data.Models.Media ToData(LANCommander.SDK.Models.Manifest.Media? source);

        public partial LANCommander.SDK.Models.Manifest.MultiplayerMode ToManifest(LANCommander.Server.Data.Models.MultiplayerMode source);
        public partial LANCommander.Server.Data.Models.MultiplayerMode ToData(LANCommander.SDK.Models.Manifest.MultiplayerMode? source);

        public partial LANCommander.SDK.Models.Manifest.Platform ToManifest(LANCommander.Server.Data.Models.Platform source);
        public partial LANCommander.Server.Data.Models.Platform ToData(LANCommander.SDK.Models.Manifest.Platform? source);

        public partial LANCommander.SDK.Models.Manifest.PlaySession ToManifest(LANCommander.Server.Data.Models.PlaySession source);
        public partial LANCommander.Server.Data.Models.PlaySession ToData(LANCommander.SDK.Models.Manifest.PlaySession? source);

        public partial LANCommander.SDK.Models.Manifest.SavePath ToManifest(LANCommander.Server.Data.Models.SavePath source);
        public partial LANCommander.Server.Data.Models.SavePath ToData(LANCommander.SDK.Models.Manifest.SavePath? source);

        public partial LANCommander.SDK.Models.Manifest.Script ToManifest(LANCommander.Server.Data.Models.Script source);
        public partial LANCommander.Server.Data.Models.Script ToData(LANCommander.SDK.Models.Manifest.Script? source);

        public partial LANCommander.SDK.Models.Manifest.ServerConsole ToManifest(LANCommander.Server.Data.Models.ServerConsole source);
        public partial LANCommander.Server.Data.Models.ServerConsole ToData(LANCommander.SDK.Models.Manifest.ServerConsole? source);

        public partial LANCommander.SDK.Models.Manifest.ServerHttpPath ToManifest(LANCommander.Server.Data.Models.ServerHttpPath source);
        public partial LANCommander.Server.Data.Models.ServerHttpPath ToData(LANCommander.SDK.Models.Manifest.ServerHttpPath? source);

        public partial LANCommander.SDK.Models.Manifest.Tag ToManifest(LANCommander.Server.Data.Models.Tag source);
        public partial LANCommander.Server.Data.Models.Tag ToData(LANCommander.SDK.Models.Manifest.Tag? source);

        // ---- GameSave <-> Manifest.Save --------------------------------------------------------
        public partial LANCommander.SDK.Models.Manifest.Save ToManifest(LANCommander.Server.Data.Models.GameSave source);
        public partial LANCommander.Server.Data.Models.GameSave ToData(LANCommander.SDK.Models.Manifest.Save? source);

        // ---- Action <-> Manifest.Action (PrimaryAction <-> IsPrimaryAction) --------------------
        [MapProperty(nameof(LANCommander.Server.Data.Models.Action.PrimaryAction), nameof(LANCommander.SDK.Models.Manifest.Action.IsPrimaryAction))]
        public partial LANCommander.SDK.Models.Manifest.Action ToManifest(LANCommander.Server.Data.Models.Action source);

        [MapProperty(nameof(LANCommander.SDK.Models.Manifest.Action.IsPrimaryAction), nameof(LANCommander.Server.Data.Models.Action.PrimaryAction))]
        public partial LANCommander.Server.Data.Models.Action ToData(LANCommander.SDK.Models.Manifest.Action? source);

        // ---- Server <-> Manifest.Server (Game flattened to Title) ------------------------------
        [MapProperty(nameof(LANCommander.Server.Data.Models.Server.Game), nameof(LANCommander.SDK.Models.Manifest.Server.Game), Use = nameof(GameToTitle))]
        public partial LANCommander.SDK.Models.Manifest.Server ToManifest(LANCommander.Server.Data.Models.Server source);

        [MapperIgnoreTarget(nameof(LANCommander.Server.Data.Models.Server.Game))]
        public partial LANCommander.Server.Data.Models.Server ToData(LANCommander.SDK.Models.Manifest.Server? source);

        // ---- Tool <-> Manifest.Tool (Games ignored both directions) ----------------------------
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.Manifest.Tool.Games))]
        public partial LANCommander.SDK.Models.Manifest.Tool ToManifest(LANCommander.Server.Data.Models.Tool source);

        [MapperIgnoreTarget(nameof(LANCommander.Server.Data.Models.Tool.Games))]
        public partial LANCommander.Server.Data.Models.Tool ToData(LANCommander.SDK.Models.Manifest.Tool? source);

        // ---- Redistributable <-> Manifest.Redistributable (computed Version, filtered Scripts) -
        [UserMapping(Default = false)]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Redistributable.Scripts), nameof(LANCommander.SDK.Models.Manifest.Redistributable.Scripts), Use = nameof(MapNonPackageScripts))]
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.Manifest.Redistributable.Version))]
        private partial LANCommander.SDK.Models.Manifest.Redistributable ToManifestCore(LANCommander.Server.Data.Models.Redistributable source);

        [UserMapping(Default = true)]
        public LANCommander.SDK.Models.Manifest.Redistributable ToManifest(LANCommander.Server.Data.Models.Redistributable source)
        {
            var result = ToManifestCore(source);
            result.Version = MapperFunctions.LatestArchiveVersion(source.Archives);
            return result;
        }

        public partial LANCommander.Server.Data.Models.Redistributable ToData(LANCommander.SDK.Models.Manifest.Redistributable? source);

        // ---- Game <-> Manifest.Game ------------------------------------------------------------
        [UserMapping(Default = false)]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.BaseGame), nameof(LANCommander.SDK.Models.Manifest.Game.BaseGame), Use = nameof(GameToTitle))]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.Scripts), nameof(LANCommander.SDK.Models.Manifest.Game.Scripts), Use = nameof(MapNonPackageScripts))]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.Collections), nameof(LANCommander.SDK.Models.Manifest.Game.Collections), Use = nameof(MapCollections))]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.Engine), nameof(LANCommander.SDK.Models.Manifest.Game.Engine), Use = nameof(EngineOrNull))]
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.Manifest.Game.Version))]
        private partial LANCommander.SDK.Models.Manifest.Game ToManifestCore(LANCommander.Server.Data.Models.Game source);

        [UserMapping(Default = true)]
        public LANCommander.SDK.Models.Manifest.Game ToManifest(LANCommander.Server.Data.Models.Game source)
        {
            var result = ToManifestCore(source);
            result.Version = MapperFunctions.ManifestVersion(source.Versions, source.Archives);
            return result;
        }

        [MapperIgnoreTarget(nameof(LANCommander.Server.Data.Models.Game.BaseGameId))]
        [MapperIgnoreTarget(nameof(LANCommander.Server.Data.Models.Game.BaseGame))]
        public partial LANCommander.Server.Data.Models.Game ToData(LANCommander.SDK.Models.Manifest.Game? source);

        // ---- Queryable projections (preserve EF server-side SELECT) ----------------------------
        // Only pure/expression-translatable exporter targets get projections. Server (Game->Title
        // flatten via user method), Redistributable (computed Version) and Game (computed Version,
        // filtered Scripts) must be materialized then mapped in-memory instead.
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Tool> ProjectToManifestTool(IQueryable<LANCommander.Server.Data.Models.Tool> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Tag> ProjectToManifestTag(IQueryable<LANCommander.Server.Data.Models.Tag> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.ServerHttpPath> ProjectToManifestServerHttpPath(IQueryable<LANCommander.Server.Data.Models.ServerHttpPath> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.ServerConsole> ProjectToManifestServerConsole(IQueryable<LANCommander.Server.Data.Models.ServerConsole> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.SavePath> ProjectToManifestSavePath(IQueryable<LANCommander.Server.Data.Models.SavePath> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Company> ProjectToManifestCompany(IQueryable<LANCommander.Server.Data.Models.Company> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.PlaySession> ProjectToManifestPlaySession(IQueryable<LANCommander.Server.Data.Models.PlaySession> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Platform> ProjectToManifestPlatform(IQueryable<LANCommander.Server.Data.Models.Platform> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.MultiplayerMode> ProjectToManifestMultiplayerMode(IQueryable<LANCommander.Server.Data.Models.MultiplayerMode> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Media> ProjectToManifestMedia(IQueryable<LANCommander.Server.Data.Models.Media> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Key> ProjectToManifestKey(IQueryable<LANCommander.Server.Data.Models.Key> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Genre> ProjectToManifestGenre(IQueryable<LANCommander.Server.Data.Models.Genre> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Engine> ProjectToManifestEngine(IQueryable<LANCommander.Server.Data.Models.Engine> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.GameCustomField> ProjectToManifestGameCustomField(IQueryable<LANCommander.Server.Data.Models.GameCustomField> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Collection> ProjectToManifestCollection(IQueryable<LANCommander.Server.Data.Models.Collection> source);
        public partial IQueryable<LANCommander.SDK.Models.Manifest.Action> ProjectToManifestAction(IQueryable<LANCommander.Server.Data.Models.Action> source);

        // ---- User mappings shared across the mapper --------------------------------------------
        [UserMapping(Default = true)]
        private static string? UserToName(LANCommander.Server.Data.Models.User? user) => user?.UserName;

        [UserMapping(Default = true)]
        private LANCommander.Server.Data.Models.User? NameToUser(string? name) => null;

        [UserMapping(Default = true)]
        private string? GameToTitle(LANCommander.Server.Data.Models.Game? game) => game?.Title;

        [UserMapping(Default = false)]
        private ICollection<LANCommander.SDK.Models.Manifest.Script>? MapNonPackageScripts(ICollection<LANCommander.Server.Data.Models.Script>? scripts)
            => scripts == null ? null : scripts.Where(s => s.Type != ScriptType.Package).Select(ToManifest).ToList();

        // Collections is non-nullable on the entity, so Mapperly would skip the null guard; EF leaves
        // it null when not Include()d (e.g. addon games reached through Game.Addons recursion), which
        // would NRE. Guard it here to match the other unloaded-nav helpers.
        [UserMapping(Default = false)]
        private ICollection<LANCommander.SDK.Models.Manifest.Collection>? MapCollections(ICollection<LANCommander.Server.Data.Models.Collection>? collections)
            => collections == null ? null : collections.Select(ToManifest).ToList();

        [UserMapping(Default = false)]
        private LANCommander.SDK.Models.Manifest.Engine? EngineOrNull(LANCommander.Server.Data.Models.Engine? engine)
            => engine == null ? null : ToManifest(engine);
    }
}
