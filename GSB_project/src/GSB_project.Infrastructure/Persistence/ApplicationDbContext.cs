using System;
using System.Collections.Generic;
using GSB_project.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GSB_project.Infrastructure.Persistence;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Participant> Participants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.ToTable("Participant");

            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasDefaultValue("");
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
