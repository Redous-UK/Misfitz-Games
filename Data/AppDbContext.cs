using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Models;

namespace Misfitz_Games.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(x => x.Username)
            .HasMaxLength(32)
            .IsRequired();
    }
}