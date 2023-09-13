using LANCommander.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Data
{
    public class DatabaseContext : IdentityDbContext<User, Role, Guid>
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {
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

            builder.Entity<Game>()
                .HasMany(g => g.Archives)
                .WithOne(g => g.Game)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Scripts)
                .WithOne(s => s.Game)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Keys)
                .WithOne(g => g.Game)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.Actions)
                .WithOne(g => g.Game)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.MultiplayerModes)
                .WithOne(m => m.Game)
                .IsRequired(true)
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

            builder.Entity<User>()
                .HasMany(u => u.GameSaves)
                .WithOne(gs => gs.User)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Game>()
                .HasMany(g => g.GameSaves)
                .WithOne(gs => gs.Game)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Server>()
                .HasOne(s => s.Game)
                .WithMany(g => g.Servers)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Server>()
                .HasMany<ServerConsole>()
                .WithOne(sl => sl.Server)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public DbSet<Game>? Games { get; set; }

        public DbSet<Genre>? Genres { get; set; }

        public DbSet<Category>? Categories { get; set; }

        public DbSet<Tag>? Tags { get; set; }

        public DbSet<Company>? Companies { get; set; }

        public DbSet<Key>? Keys { get; set; }

        public DbSet<GameSave>? GameSaves { get; set; }

        public DbSet<Server>? Servers { get; set; }

        public DbSet<ServerConsole>? ServerConsoles { get; set; }

        public DbSet<DHCPLease>? DHCPLeases { get; set; }
    }
}