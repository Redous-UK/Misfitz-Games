using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Effects;

namespace Misfitz_Games.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
    public DbSet<DeviceGroupMember> DeviceGroupMembers => Set<DeviceGroupMember>();
    public DbSet<Effect> Effects => Set<Effect>();
    public DbSet<EffectTarget> EffectTargets => Set<EffectTarget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(x => x.Username)
            .HasMaxLength(32)
            .IsRequired();

        modelBuilder.Entity<Device>()
            .HasIndex(x => new { x.OwnerUserId, x.Name })
            .IsUnique();

        modelBuilder.Entity<Device>()
            .HasIndex(x => new { x.OwnerUserId, x.Provider, x.ExternalDeviceId })
            .IsUnique();

        modelBuilder.Entity<DeviceGroup>()
            .HasIndex(x => new { x.OwnerUserId, x.Name })
            .IsUnique();

        modelBuilder.Entity<DeviceGroupMember>()
            .HasKey(x => new { x.GroupId, x.DeviceId });

        modelBuilder.Entity<Effect>()
            .HasIndex(x => new { x.OwnerUserId, x.Name })
            .IsUnique();

        modelBuilder.Entity<EffectTarget>()
            .HasOne(t => t.Effect)
            .WithMany(e => e.Targets)
            .HasForeignKey(t => t.EffectId);
    }
}