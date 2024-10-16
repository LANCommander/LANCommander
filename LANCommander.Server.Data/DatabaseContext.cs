using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LANCommander.Server.Data
{
    public class DatabaseContext : DbContext
    {
        public static DatabaseProvider Provider = DatabaseProvider.Unknown;
        public static Dictionary<Guid, Stopwatch> ContextTracker;

        private readonly ILogger Logger;

        public DatabaseContext(DbContextOptions<DatabaseContext> options, ILogger<DatabaseContext> logger)
            : base(options)
        {
            Logger = logger;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(new DatabaseContextConnectionInterceptor(Logger));
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ConfigureBaseRelationships<Models.Action>();
            builder.ConfigureBaseRelationships<Archive>();
            builder.ConfigureBaseRelationships<Category>();
            builder.ConfigureBaseRelationships<Collection>();
            builder.ConfigureBaseRelationships<Company>();
            builder.ConfigureBaseRelationships<Game>();
            builder.ConfigureBaseRelationships<GameSave>();
            builder.ConfigureBaseRelationships<Genre>();
            builder.ConfigureBaseRelationships<Key>();
            builder.ConfigureBaseRelationships<Media>();
            builder.ConfigureBaseRelationships<MultiplayerMode>();
            builder.ConfigureBaseRelationships<Platform>();
            builder.ConfigureBaseRelationships<PlaySession>();
            builder.ConfigureBaseRelationships<Redistributable>();
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

            builder.Entity<Tag>()
                .HasMany(t => t.Games)
                .WithMany(g => g.Tags);

            builder.Entity<Platform>()
                .HasMany(p => p.Games)
                .WithMany(g => g.Platforms);

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
                    g => g.HasOne<Company>().WithMany().HasForeignKey("DeveloperId").OnDelete(DeleteBehavior.Cascade),
                    g => g.HasOne<Game>().WithMany().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade)
                );

            builder.Entity<Game>()
                .HasMany(g => g.Publishers)
                .WithMany(c => c.PublishedGames)
                .UsingEntity<Dictionary<string, object>>(
                    "GamePublisher",
                    g => g.HasOne<Company>().WithMany().HasForeignKey("PublisherId").OnDelete(DeleteBehavior.Cascade),
                    g => g.HasOne<Game>().WithMany().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade)
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
                // Primary key
                b.HasKey(u => u.Id);

                // Indexes for "normalized" username and email, to allow efficient lookups
                b.HasIndex(u => u.NormalizedUserName).HasName("UserNameIndex").IsUnique();
                b.HasIndex(u => u.NormalizedEmail).HasName("EmailIndex");

                // Maps to the AspNetUsers table
                b.ToTable("Users");

                // The relationships between User and other entity types
                // Note that these relationships are configured with no navigation properties

                // Each User can have many UserClaims
                b.HasMany<UserClaim>().WithOne().HasForeignKey(uc => uc.UserId).IsRequired();

                // Each User can have many UserLogins
                b.HasMany<UserLogin>().WithOne().HasForeignKey(ul => ul.UserId).IsRequired();

                // Each User can have many UserTokens
                b.HasMany<UserToken>().WithOne().HasForeignKey(ut => ut.UserId).IsRequired();

                // Each User can have many entries in the UserRole join table
                b.HasMany<UserRole>().WithOne().HasForeignKey(ur => ur.UserId).IsRequired();
            });

            builder.Entity<UserClaim>(b =>
            {
                // Primary key
                b.HasKey(uc => uc.Id);

                // Maps to the AspNetUserClaims table
                b.ToTable("UserClaims");
            });

            builder.Entity<UserLogin>(b =>
            {
                // Composite primary key consisting of the LoginProvider and the key to use
                // with that provider
                b.HasKey(l => new { l.LoginProvider, l.ProviderKey });

                // Maps to the AspNetUserLogins table
                b.ToTable("UserLogins");
            });

            builder.Entity<UserToken>(b =>
            {
                // Composite primary key consisting of the UserId, LoginProvider and Name
                b.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });


                // Maps to the AspNetUserTokens table
                b.ToTable("UserTokens");
            });

            builder.Entity<Role>(b =>
            {
                // Primary key
                b.HasKey(r => r.Id);

                // Index for "normalized" role name to allow efficient lookups
                b.HasIndex(r => r.NormalizedName).HasName("RoleNameIndex").IsUnique();

                // Maps to the AspNetRoles table
                b.ToTable("Roles");

                // A concurrency token for use with the optimistic concurrency checking
                b.Property(r => r.ConcurrencyStamp).IsConcurrencyToken();

                // The relationships between Role and other entity types
                // Note that these relationships are configured with no navigation properties

                // Each Role can have many entries in the UserRole join table
                b.HasMany<UserRole>().WithOne().HasForeignKey(ur => ur.RoleId).IsRequired();

                // Each Role can have many associated RoleClaims
                b.HasMany<RoleClaim>().WithOne().HasForeignKey(rc => rc.RoleId).IsRequired();
            });

            builder.Entity<RoleClaim>(b =>
            {
                // Primary key
                b.HasKey(rc => rc.Id);

                // Maps to the AspNetRoleClaims table
                b.ToTable("RoleClaims");
            });

            builder.Entity<UserRole>(b =>
            {
                // Primary key
                b.HasKey(r => new { r.UserId, r.RoleId });

                // Maps to the AspNetUserRoles table
                b.ToTable("UserRoles");
            });
            #endregion

            #region Server Relationships
            builder.Entity<Data.Models.Server>()
                .HasOne(s => s.Game)
                .WithMany(g => g.Servers)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Data.Models.Server>()
                .HasMany<ServerConsole>()
                .WithOne(sl => sl.Server)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Data.Models.Server>()
                .HasMany<ServerHttpPath>()
                .WithOne(s => s.Server)
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
    }
}