using LANCommander.SDK.Enums;
using Riok.Mapperly.Abstractions;

namespace LANCommander.Server.Services.Mappers
{
    /// <summary>
    /// Mapperly mapper for Data.Models.* -> SDK.Models.* (and the handful of reverse maps that
    /// AutoMapper's MappingProfile declared via ReverseMap). Replaces the AutoMapper runtime mapper.
    /// Fully-qualified type names are used deliberately: Mapperly's generator does not handle
    /// namespace aliases correctly, and short names collide between SDK.Models and Data.Models.
    /// </summary>
    [Mapper(AllowNullPropertyAssignment = true)]
    public partial class SdkMapper
    {
        // ---- Simple 1:1 element maps ------------------------------------------------------------
        public partial LANCommander.SDK.Models.Archive ToSdk(LANCommander.Server.Data.Models.Archive source);
        public partial LANCommander.SDK.Models.Company ToSdk(LANCommander.Server.Data.Models.Company source);
        public partial LANCommander.SDK.Models.Collection ToSdk(LANCommander.Server.Data.Models.Collection source);
        public partial LANCommander.SDK.Models.Engine ToSdk(LANCommander.Server.Data.Models.Engine source);
        public partial LANCommander.SDK.Models.GameSave ToSdk(LANCommander.Server.Data.Models.GameSave source);
        public partial LANCommander.SDK.Models.Genre ToSdk(LANCommander.Server.Data.Models.Genre source);
        public partial LANCommander.SDK.Models.Key ToSdk(LANCommander.Server.Data.Models.Key source);
        public partial LANCommander.SDK.Models.Media ToSdk(LANCommander.Server.Data.Models.Media source);
        public partial LANCommander.SDK.Models.MultiplayerMode ToSdk(LANCommander.Server.Data.Models.MultiplayerMode source);
        public partial LANCommander.SDK.Models.Platform ToSdk(LANCommander.Server.Data.Models.Platform source);
        public partial LANCommander.SDK.Models.PlaySession ToSdk(LANCommander.Server.Data.Models.PlaySession source);
        public partial LANCommander.SDK.Models.ServerConsole ToSdk(LANCommander.Server.Data.Models.ServerConsole source);
        public partial LANCommander.SDK.Models.ServerHttpPath ToSdk(LANCommander.Server.Data.Models.ServerHttpPath source);
        public partial LANCommander.SDK.Models.SavePath ToSdk(LANCommander.Server.Data.Models.SavePath source);
        public partial LANCommander.SDK.Models.Script ToSdk(LANCommander.Server.Data.Models.Script source);
        public partial LANCommander.SDK.Models.Tool ToSdk(LANCommander.Server.Data.Models.Tool source);
        public partial LANCommander.SDK.Models.User ToSdk(LANCommander.Server.Data.Models.User source);
        public partial LANCommander.SDK.Models.GameCustomField ToSdk(LANCommander.Server.Data.Models.GameCustomField source);
        public partial LANCommander.SDK.Models.GameExternalId ToSdk(LANCommander.Server.Data.Models.GameExternalId source);
        public partial LANCommander.SDK.Models.AuthenticationProvider ToSdk(LANCommander.Server.Settings.Models.AuthenticationProvider source);

        // Tag has a reverse map in AutoMapper.
        public partial LANCommander.SDK.Models.Tag ToSdk(LANCommander.Server.Data.Models.Tag source);
        public partial LANCommander.Server.Data.Models.Tag ToData(LANCommander.SDK.Models.Tag source);

        // ---- GameVersion (flatten related Archive) ---------------------------------------------
        [MapProperty(new[] { "Archive", "Id" }, new[] { nameof(LANCommander.SDK.Models.GameVersion.ArchiveId) })]
        [MapProperty(new[] { "Archive", "CompressedSize" }, new[] { nameof(LANCommander.SDK.Models.GameVersion.CompressedSize) })]
        [MapProperty(new[] { "Archive", "UncompressedSize" }, new[] { nameof(LANCommander.SDK.Models.GameVersion.UncompressedSize) })]
        public partial LANCommander.SDK.Models.GameVersion ToSdk(LANCommander.Server.Data.Models.GameVersion source);

        // ---- Server (break Game <-> Server cycle) ----------------------------------------------
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.Server.Game))]
        public partial LANCommander.SDK.Models.Server ToSdk(LANCommander.Server.Data.Models.Server source);

        // ---- Action (PrimaryAction <-> IsPrimaryAction) ----------------------------------------
        [MapProperty(nameof(LANCommander.Server.Data.Models.Action.PrimaryAction), nameof(LANCommander.SDK.Models.Action.IsPrimaryAction))]
        public partial LANCommander.SDK.Models.Action ToSdk(LANCommander.Server.Data.Models.Action source);

        [MapProperty(nameof(LANCommander.SDK.Models.Action.IsPrimaryAction), nameof(LANCommander.Server.Data.Models.Action.PrimaryAction))]
        public partial LANCommander.Server.Data.Models.Action ToData(LANCommander.SDK.Models.Action source);

        // ---- Redistributable (computed Version + filtered Scripts) -----------------------------
        [UserMapping(Default = false)]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Redistributable.Scripts), nameof(LANCommander.SDK.Models.Redistributable.Scripts), Use = nameof(MapNonPackageScripts))]
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.Redistributable.Version))]
        private partial LANCommander.SDK.Models.Redistributable ToSdkCore(LANCommander.Server.Data.Models.Redistributable source);

        [UserMapping(Default = true)]
        public LANCommander.SDK.Models.Redistributable ToSdk(LANCommander.Server.Data.Models.Redistributable source)
        {
            var result = ToSdkCore(source);
            result.Version = MapperFunctions.LatestArchiveVersion(source.Archives);
            return result;
        }

        // ---- Game -> SDK.Game ------------------------------------------------------------------
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.DependentGames), nameof(LANCommander.SDK.Models.Game.DependentGames), Use = nameof(DependentGameIds))]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.Scripts), nameof(LANCommander.SDK.Models.Game.Scripts), Use = nameof(MapNonPackageScripts))]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.Collections), nameof(LANCommander.SDK.Models.Game.Collections), Use = nameof(MapCollections))]
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.Engine), nameof(LANCommander.SDK.Models.Game.Engine), Use = nameof(EngineOrNull))]
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.Game.InLibrary))]
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.Game.InstallDirectory))]
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.Game.Saves))]
        public partial LANCommander.SDK.Models.Game ToSdk(LANCommander.Server.Data.Models.Game source);

        // ---- Game -> SDK.DepotGame -------------------------------------------------------------
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.Media), nameof(LANCommander.SDK.Models.DepotGame.Cover), Use = nameof(CoverMedia))]
        [MapperIgnoreTarget(nameof(LANCommander.SDK.Models.DepotGame.InLibrary))]
        public partial LANCommander.SDK.Models.DepotGame ToDepotGame(LANCommander.Server.Data.Models.Game source);

        // ---- Game -> EntityReference -----------------------------------------------------------
        [MapProperty(nameof(LANCommander.Server.Data.Models.Game.Title), nameof(LANCommander.SDK.Models.EntityReference.Name))]
        public partial LANCommander.SDK.Models.EntityReference ToEntityReference(LANCommander.Server.Data.Models.Game source);

        [MapProperty(nameof(LANCommander.SDK.Models.Game.Title), nameof(LANCommander.SDK.Models.EntityReference.Name))]
        public partial LANCommander.SDK.Models.EntityReference ToEntityReference(LANCommander.SDK.Models.Game source);

        // ---- ChatMessage (hand-written; SDK type has required/init members) --------------------
        public LANCommander.SDK.Models.ChatMessage ToSdk(LANCommander.Server.Data.Models.ChatMessage source)
            => new()
            {
                Id = source.Id,
                UserId = source.CreatedBy.Id,
                UserName = source.CreatedBy.UserName,
                SentOn = source.CreatedOn,
                Content = source.Content,
            };

        public LANCommander.Server.Data.Models.ChatMessage ToData(LANCommander.SDK.Models.ChatMessage source)
            => new()
            {
                Id = source.Id,
                Content = source.Content,
                CreatedBy = new LANCommander.Server.Data.Models.User
                {
                    Id = source.UserId,
                    UserName = source.UserName,
                },
            };

        // ---- ChatThread (hand-written; required/init + computed LastActivityOn) -----------------
        public LANCommander.SDK.Models.ChatThread ToSdk(LANCommander.Server.Data.Models.ChatThread source)
            => new()
            {
                Id = source.Id,
                Name = source.Name,
                LastActivityOn = source.Messages != null && source.Messages.Count > 0
                    ? source.Messages.Max(m => m.CreatedOn)
                    : source.UpdatedOn,
                Participants = source.Participants != null
                    ? source.Participants.Select(ToSdk).ToList()
                    : new List<LANCommander.SDK.Models.User>(),
            };

        public partial LANCommander.Server.Data.Models.ChatThread ToData(LANCommander.SDK.Models.ChatThread source);

        // ---- Queryable projections (preserve EF server-side SELECT) ----------------------------
        // Only pure/expression-translatable targets get projections. Targets whose maps use custom
        // user methods or computed members (Game, Redistributable, Server, ChatThread, ChatMessage)
        // must be materialized then mapped in-memory instead.
        public partial IQueryable<LANCommander.SDK.Models.Archive> ProjectToSdkArchive(IQueryable<LANCommander.Server.Data.Models.Archive> source);
        public partial IQueryable<LANCommander.SDK.Models.GameSave> ProjectToSdkGameSave(IQueryable<LANCommander.Server.Data.Models.GameSave> source);
        public partial IQueryable<LANCommander.SDK.Models.User> ProjectToSdkUser(IQueryable<LANCommander.Server.Data.Models.User> source);
        public partial IQueryable<LANCommander.SDK.Models.Script> ProjectToSdkScript(IQueryable<LANCommander.Server.Data.Models.Script> source);

        // ---- Collection helpers with parity semantics ------------------------------------------
        public IEnumerable<LANCommander.SDK.Models.Archive> ToSdkList(IEnumerable<LANCommander.Server.Data.Models.Archive>? source)
            => source == null ? null! : source.Select(ToSdk);

        // ---- User mappings (reused via Use = ...) ----------------------------------------------
        [UserMapping(Default = false)]
        private IEnumerable<LANCommander.SDK.Models.Script>? MapNonPackageScripts(ICollection<LANCommander.Server.Data.Models.Script>? scripts)
            => scripts == null ? null : scripts.Where(s => s.Type != ScriptType.Package).Select(ToSdk);

        [UserMapping(Default = false)]
        private IEnumerable<Guid>? DependentGameIds(ICollection<LANCommander.Server.Data.Models.Game>? games)
            => games?.Select(g => g.Id);

        // Collections is declared non-nullable on the entity, so Mapperly would otherwise skip the
        // null guard; EF leaves the navigation null when it is not Include()d (e.g. the game list
        // endpoint), which would NRE. Guard it here to match the other unloaded-nav helpers.
        [UserMapping(Default = false)]
        private IEnumerable<LANCommander.SDK.Models.Collection>? MapCollections(ICollection<LANCommander.Server.Data.Models.Collection>? collections)
            => collections?.Select(ToSdk);

        [UserMapping(Default = false)]
        private LANCommander.SDK.Models.Engine? EngineOrNull(LANCommander.Server.Data.Models.Engine? engine)
            => engine == null ? null : ToSdk(engine);

        [UserMapping(Default = false)]
        private LANCommander.SDK.Models.Media? CoverMedia(ICollection<LANCommander.Server.Data.Models.Media>? media)
        {
            var cover = media?.FirstOrDefault(m => m.Type == MediaType.Cover);
            return cover == null ? null : ToSdk(cover);
        }
    }
}
