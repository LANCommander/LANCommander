using LANCommander.Data.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Data
{
    public class DatabaseContext : IdentityDbContext<User>
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Company>()
                .HasMany(c => c.PublishedGames)
                .WithOne(g => g.Publisher)
                .IsRequired(false);

            builder.Entity<Company>()
                .HasMany(c => c.DevelopedGames)
                .WithOne(g => g.Developer)
                .IsRequired(false);

            builder.Entity<Game>()
                .HasMany(g => g.Archives)
                .WithOne(g => g.Game)
                .IsRequired(false);
        }

        public DbSet<Game>? Games { get; set; }

        public DbSet<Tag>? Tags { get; set; }

        public DbSet<Company>? Companies { get; set; }
    }
}