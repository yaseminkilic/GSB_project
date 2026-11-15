using GSB_project.Application.Common.Interfaces;
using GSB_project.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GSB_project.Infrastructure.Persistence
{
    public partial class ApplicationDbContext : IApplicationDbContext
    {
        // Scaffold komutu (scaffold-db.ps1) çalıştırıldığında üretilen 
        // "public partial class ApplicationDbContext : DbContext" sınıfı 
        // otomatik olarak bu dosya ile birleşir.


        public DbSet<Event> Events => Set<Event>();

        public DbSet<Registration> Registrations => Set<Registration>();

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("Event");
                entity.Property(e => e.Name).HasMaxLength(150);
            });
            modelBuilder.Entity<Registration>().ToTable("Registration");
        }
    }
}
