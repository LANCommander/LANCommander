using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Data
{
    public class DatabaseContext : DbContext
    {
        private readonly ILoggerFactory LoggerFactory;

        public DatabaseContext() { }

        public DatabaseContext(ILoggerFactory loggerFactory, DbContextOptions options)
            : base(options)
        {
            LoggerFactory = loggerFactory;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LANCommander.db");

            optionsBuilder.UseLoggerFactory(LoggerFactory);
            optionsBuilder.UseSqlite($"Data Source={dbPath};Cache=Shared");
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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
                .Property(g => g.BaseGameId)
                .HasConversion(new GuidToNullConverter());

            builder.Entity<Game>()
                .HasMany(g => g.MultiplayerModes)
                .WithOne(m => m.Game)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Media)
                .WithOne(m => m.Game)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Developers)
                .WithMany(c => c.DevelopedGames)
                .UsingEntity<Dictionary<string, object>>(
                    "GameDeveloper",
                    g => g.HasOne<Company>().WithMany().HasForeignKey("DeveloperId"),
                    g => g.HasOne<Game>().WithMany().HasForeignKey("GameId")
                );

            builder.Entity<Game>()
                .HasMany(g => g.Publishers)
                .WithMany(c => c.PublishedGames)
                .UsingEntity<Dictionary<string, object>>(
                    "GamePublisher",
                    g => g.HasOne<Company>().WithMany().HasForeignKey("PublisherId"),
                    g => g.HasOne<Game>().WithMany().HasForeignKey("GameId")
                );

            builder.Entity<Game>()
                .HasMany(g => g.Redistributables)
                .WithMany(r => r.Games)
                .UsingEntity<Dictionary<string, object>>(
                    "GameRedistributable",
                    gr => gr.HasOne<Redistributable>().WithMany().HasForeignKey("RedistributableId"),
                    gr => gr.HasOne<Game>().WithMany().HasForeignKey("GameId")
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

            #region Collection Relationships
            builder.Entity<Collection>()
                .HasMany(c => c.Games)
                .WithMany(g => g.Collections)
                .UsingEntity<Dictionary<string, object>>(
                    "CollectionGame",
                    cg => cg.HasOne<Game>().WithMany().HasForeignKey("GameId"),
                    cg => cg.HasOne<Collection>().WithMany().HasForeignKey("CollectionId")
                );
            #endregion
        }

        public DbSet<Game>? Games { get; set; }

        public DbSet<Genre>? Genres { get; set; }

        public DbSet<Category>? Categories { get; set; }

        public DbSet<Tag>? Tags { get; set; }

        public DbSet<Company>? Companies { get; set; }

        public DbSet<Redistributable>? Redistributables { get; set; }

        public DbSet<Media>? Media { get; set; }
    }
}