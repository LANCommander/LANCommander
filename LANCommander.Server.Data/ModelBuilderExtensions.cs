using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Data
{
    public static class ModelBuilderExtensions
    {
        public static void ConfigureBaseRelationships<T>(this ModelBuilder modelBuilder) where T : class, IBaseModel
        {
            modelBuilder.Entity<T>()
                .HasOne(x => x.CreatedBy)
                .WithMany()
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<T>()
                .HasOne(x => x.UpdatedBy)
                .WithMany()
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
