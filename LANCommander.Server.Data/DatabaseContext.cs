using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Interceptors;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Settings.Enums;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data
{
    public sealed class DatabaseContext(
        DbContextOptions<DatabaseContext> options) : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>(options)
    {
        public static DatabaseProvider Provider = DatabaseProvider.Unknown;
        public static string ConnectionString = "";

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Check if provider is already configured (e.g., by design-time factory)
            // We check for the presence of database provider extensions
            var hasSqliteExtension = optionsBuilder.Options.Extensions.Any(e => e.GetType().Name.Contains("Sqlite"));
            var hasMySqlExtension = optionsBuilder.Options.Extensions.Any(e => e.GetType().Name.Contains("MySql"));
            var hasNpgsqlExtension = optionsBuilder.Options.Extensions.Any(e => e.GetType().Name.Contains("Npgsql"));
            var isProviderConfigured = hasSqliteExtension || hasMySqlExtension || hasNpgsqlExtension;

            if (!isProviderConfigured && Provider != DatabaseProvider.Unknown && !string.IsNullOrEmpty(ConnectionString))
            {
                optionsBuilder.AddInterceptors(new GameSaveChangesInterceptor());

                optionsBuilder.EnableDetailedErrors();
                optionsBuilder.EnableSensitiveDataLogging();

                switch (Provider)
                {
                    case DatabaseProvider.SQLite:
                        optionsBuilder.UseSqlite(ConnectionString, options => options.MigrationsAssembly("LANCommander.Server.Data.SQLite"));
                        break;

                    case DatabaseProvider.MySQL:
                        optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString), options => options.MigrationsAssembly("LANCommander.Server.Data.MySQL"));
                        break;

                    case DatabaseProvider.PostgreSQL:
                        optionsBuilder.UseNpgsql(ConnectionString, options => options.MigrationsAssembly("LANCommander.Server.Data.PostgreSQL"));
                        break;
                }
            }
            else
            {
                // Provider is already configured, but we still want to add interceptors and settings if not already added
                optionsBuilder.AddInterceptors(new GameSaveChangesInterceptor());
                optionsBuilder.EnableDetailedErrors();
                optionsBuilder.EnableSensitiveDataLogging();
            }

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ConfigureBaseRelationships<Models.Action>();
            builder.ConfigureBaseRelationships<Archive>();
            builder.ConfigureBaseRelationships<Category>();
            builder.ConfigureBaseRelationships<ChatMessage>();
            builder.ConfigureBaseRelationships<ChatThread>();
            builder.ConfigureBaseRelationships<Collection>();
            builder.ConfigureBaseRelationships<Company>();
            builder.ConfigureBaseRelationships<Game>();
            builder.ConfigureBaseRelationships<GameSave>();
            builder.ConfigureBaseRelationships<Genre>();
            builder.ConfigureBaseRelationships<Key>();
            builder.ConfigureBaseRelationships<Library>();
            builder.ConfigureBaseRelationships<Media>();
            builder.ConfigureBaseRelationships<MultiplayerMode>();
            builder.ConfigureBaseRelationships<Platform>();
            builder.ConfigureBaseRelationships<PlaySession>();
            builder.ConfigureBaseRelationships<Redistributable>();
            builder.ConfigureBaseRelationships<Tool>();
            builder.ConfigureBaseRelationships<SavePath>();
            builder.ConfigureBaseRelationships<Script>();
            builder.ConfigureBaseRelationships<Models.Server>();
            builder.ConfigureBaseRelationships<ServerConsole>();
            builder.ConfigureBaseRelationships<ServerHttpPath>();
            builder.ConfigureBaseRelationships<Tag>();
            builder.ConfigureBaseRelationships<Issue>();
            builder.ConfigureBaseRelationships<Page>();
            builder.ConfigureBaseRelationships<Role>();
            builder.ConfigureBaseRelationships<User>();
            builder.ConfigureBaseRelationships<UserCustomField>();

            builder.Entity<Genre>()
                .HasMany(g => g.Games)
                .WithMany(g => g.Genres);

            builder.Entity<Category>()
                .HasMany(c => c.Games)
                .WithMany(g => g.Categories);

            builder.Entity<Category>()
                .HasMany(c => c.Children)
                .WithOne(c => c.Parent)
                .IsRequired(false);

            builder.Entity<ChatMessage>()
                .HasOne(cm => cm.Thread)
                .WithMany(ct => ct.Messages)
                .IsRequired(true);

            builder.Entity<ChatThread>()
                .HasMany(ct => ct.Participants)
                .WithMany(u => u.ChatThreads);

            builder.Entity<Tag>()
                .HasMany(t => t.Games)
                .WithMany(g => g.Tags);

            builder.Entity<Platform>()
                .HasMany(p => p.Games)
                .WithMany(g => g.Platforms);

            builder.Entity<Library>()
                .HasOne(l => l.User)
                .WithOne(u => u.Library)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Library>()
                .HasMany(l => l.Games)
                .WithMany(l => l.Libraries)
                .UsingEntity<Dictionary<string, object>>(
                    "LibraryGame",
                    lg => lg.HasOne<Game>().WithMany().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade),
                    lg => lg.HasOne<Library>().WithMany().HasForeignKey("LibraryId").OnDelete(DeleteBehavior.Cascade)
                );

            #region Game Relationships
            builder.Entity<Game>()
                .HasMany(g => g.Archives)
                .WithOne(g => g.Game)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Scripts)
                .WithOne(s => s.Game)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Keys)
                .WithOne(g => g.Game)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Actions)
                .WithOne(g => g.Game)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.MultiplayerModes)
                .WithOne(m => m.Game)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Media)
                .WithOne(m => m.Game)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.SavePaths)
                .WithOne(p => p.Game)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.PlaySessions)
                .WithOne(ps => ps.Game)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.GameSaves)
                .WithOne(gs => gs.Game)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Game>()
                .HasMany(g => g.Developers)
                .WithMany(c => c.DevelopedGames)
                .UsingEntity<Dictionary<string, object>>(
                    "GameDeveloper",
                    g => g.HasOne<Company>()
                        .WithMany()
                        .HasForeignKey("DeveloperId")
                        .OnDelete(DeleteBehavior.Cascade),
                    g => g.HasOne<Game>()
                        .WithMany()
                        .HasForeignKey("GameId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j => j.HasKey("GameId", "DeveloperId")
                );

            builder.Entity<Game>()
                .HasMany(g => g.Publishers)
                .WithMany(c => c.PublishedGames)
                .UsingEntity<Dictionary<string, object>>(
                    "GamePublisher",
                    g => g.HasOne<Company>()
                        .WithMany()
                        .HasForeignKey("PublisherId")
                        .OnDelete(DeleteBehavior.Cascade),
                    g => g.HasOne<Game>()
                        .WithMany()
                        .HasForeignKey("GameId")
                        .OnDelete(DeleteBehavior.Cascade),
                    j => j.HasKey("GameId", "PublisherId")
                );

            builder.Entity<Game>()
                .HasMany(g => g.Redistributables)
                .WithMany(r => r.Games)
                .UsingEntity<Dictionary<string, object>>(
                    "GameRedistributable",
                    gr => gr.HasOne<Redistributable>().WithMany().HasForeignKey("RedistributableId").OnDelete(DeleteBehavior.Cascade),
                    gr => gr.HasOne<Game>().WithMany().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade)
                );

            builder.Entity<Game>()
                .HasMany(g => g.DependentGames)
                .WithOne(g => g.BaseGame)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Game>()
                .HasOne(g => g.Engine)
                .WithMany(e => e.Games)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            #endregion

            #region Media Relationships
            builder.Entity<Media>()
                .HasOne(m => m.Thumbnail)
                .WithOne(m => m.Parent)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Media>()
                .HasOne(m => m.StorageLocation)
                .WithMany(l => l.Media)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Media>()
                .Navigation(m => m.StorageLocation)
                .AutoInclude();
            #endregion

            #region Archive Relationships
            builder.Entity<Archive>()
                .Navigation(a => a.StorageLocation)
                .AutoInclude();
            #endregion

            #region Game Save Relationships
            builder.Entity<GameSave>()
                .Navigation(gs => gs.StorageLocation)
                .AutoInclude();
            #endregion

            #region User Relationships
            builder.Entity<User>()
                .HasMany(u => u.GameSaves)
                .WithOne(gs => gs.User)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<User>()
                .HasMany(u => u.PlaySessions)
                .WithOne(ps => ps.User)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<User>()
                .HasMany(u => u.Media)
                .WithOne(m => m.User)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<User>()
                .HasMany(u => u.CustomFields)
                .WithOne(cf => cf.User)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<User>(b =>
            {
                b.ToTable("Users");
            });

            builder.Entity<UserRole>(b =>
            {
                b.ToTable("UserRoles");
            });

            builder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId).IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            
            builder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId).IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserClaim>(b =>
            {
                b.ToTable("UserClaims");
            });

            builder.Entity<UserLogin>(b =>
            {
                b.ToTable("UserLogins");
            });

            builder.Entity<UserToken>(b =>
            {
                b.ToTable("UserTokens");
            });

            builder.Entity<Role>(b =>
            {
                b.ToTable("Roles");
            });

            builder.Entity<RoleClaim>(b =>
            {
                b.ToTable("RoleClaims");
            });
            #endregion

            #region Server Relationships
            builder.Entity<Data.Models.Server>()
                .HasOne(s => s.Game)
                .WithMany(g => g.Servers)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Data.Models.Server>()
                .HasMany(s => s.ServerConsoles)
                .WithOne(sl => sl.Server)
                .HasForeignKey(sc => sc.ServerId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Data.Models.Server>()
                .HasMany(s => s.HttpPaths)
                .WithOne(s => s.Server)
                .HasForeignKey(sc => sc.ServerId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Data.Models.Server>()
                .HasMany(s => s.Scripts)
                .WithOne(s => s.Server)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Data.Models.Server>()
                .HasMany(s => s.Actions)
                .WithOne(s => s.Server)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            #endregion

            #region Redistributable Relationships
            builder.Entity<Redistributable>()
                .HasMany(r => r.Archives)
                .WithOne(a => a.Redistributable)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Redistributable>()
                .HasMany(r => r.Scripts)
                .WithOne(s => s.Redistributable)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            #endregion
            
            #region Tool Relationships
            builder.Entity<Tool>()
                .HasMany(t => t.Archives)
                .WithOne(a => a.Tool)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Tool>()
                .HasMany(t => t.Scripts)
                .WithOne(s => s.Tool)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            
            builder.Entity<Tool>()
                .HasMany(t => t.Actions)
                .WithOne(s => s.Tool)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            #endregion

            #region Collection Relationships
            builder.Entity<Collection>()
                .HasMany(c => c.Games)
                .WithMany(g => g.Collections)
                .UsingEntity<Dictionary<string, object>>(
                    "CollectionGame",
                    cg => cg.HasOne<Game>().WithMany().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade),
                    cg => cg.HasOne<Collection>().WithMany().HasForeignKey("CollectionId").OnDelete(DeleteBehavior.Cascade)
                );
            #endregion

            #region Role Relationships
            builder.Entity<Role>()
                .HasMany(r => r.Collections)
                .WithMany(c => c.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RoleCollection",
                    rc => rc.HasOne<Collection>().WithMany().HasForeignKey("CollectionId").OnDelete(DeleteBehavior.Cascade),
                    rc => rc.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade)
                );
            #endregion

            #region Issue Relationships
            builder.Entity<Issue>()
                .HasOne(i => i.Game)
                .WithMany(g => g.Issues)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);
            #endregion

            #region Page Relationships
            builder.Entity<Page>()
                .HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Page>()
                .HasMany(p => p.Games)
                .WithMany(g => g.Pages)
                .UsingEntity<Dictionary<string, object>>(
                    "PageGame",
                    pg => pg.HasOne<Game>().WithMany().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade),
                    pg => pg.HasOne<Page>().WithMany().HasForeignKey("PageId").OnDelete(DeleteBehavior.Cascade)
                );

            builder.Entity<Page>()
                .HasMany(p => p.Redistributables)
                .WithMany(g => g.Pages)
                .UsingEntity<Dictionary<string, object>>(
                    "PageRedistributable",
                    pg => pg.HasOne<Redistributable>().WithMany().HasForeignKey("RedistributableId").OnDelete(DeleteBehavior.Cascade),
                    pg => pg.HasOne<Page>().WithMany().HasForeignKey("PageId").OnDelete(DeleteBehavior.Cascade)
                );

            builder.Entity<Page>()
                .HasMany(p => p.Servers)
                .WithMany(g => g.Pages)
                .UsingEntity<Dictionary<string, object>>(
                    "PageServer",
                    pg => pg.HasOne<Models.Server>().WithMany().HasForeignKey("ServerId").OnDelete(DeleteBehavior.Cascade),
                    pg => pg.HasOne<Page>().WithMany().HasForeignKey("PageId").OnDelete(DeleteBehavior.Cascade)
                );
            #endregion
            
            #region Chat Relationships

            builder.Entity<ChatThreadReadStatus>()
                .HasKey(rs => new { rs.ThreadId, rs.UserId });
            
            builder.Entity<ChatThreadReadStatus>()
                .HasOne(rs => rs.Thread)
                .WithMany()
                .HasForeignKey(rs => rs.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChatThreadReadStatus>()
                .HasOne(rs => rs.User)
                .WithMany()
                .HasForeignKey(rs => rs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ChatThreadReadStatus>()
                .HasOne(rs => rs.LastReadMessage)
                .WithMany()
                .HasForeignKey(rs => rs.LastReadMessageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            #endregion
        }

        public DbSet<Game>? Games { get; set; }

        public DbSet<Genre>? Genres { get; set; }

        public DbSet<Category>? Categories { get; set; }

        public DbSet<Tag>? Tags { get; set; }

        public DbSet<Company>? Companies { get; set; }

        public DbSet<Key>? Keys { get; set; }

        public DbSet<GameSave>? GameSaves { get; set; }

        public DbSet<PlaySession>? PlaySessions { get; set; }

        public DbSet<Data.Models.Server>? Servers { get; set; }

        public DbSet<ServerConsole>? ServerConsoles { get; set; }

        public DbSet<Redistributable>? Redistributables { get; set; }

        public DbSet<Media>? Media { get; set; }
        public DbSet<Issue>? Issues { get; set; }
        public DbSet<Page>? Pages { get; set; }
        public DbSet<StorageLocation>? StorageLocations { get; set; }
        public DbSet<Role>? Roles { get; set; }
        public DbSet<User>? Users { get; set; }
        public DbSet<ChatThread>? ChatThreads { get; set; }
        public DbSet<ChatMessage>? ChatMessages { get; set; }
        public DbSet<ChatThreadReadStatus>? ChatThreadReadStatuses { get; set; }
    }
}