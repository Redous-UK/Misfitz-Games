using Microsoft.EntityFrameworkCore;
using Misfitz_Games.Models;
using Misfitz_Games.Models.Effects;
using Misfitz_Games.Models.Games;

namespace Misfitz_Games.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceGroup> DeviceGroups => Set<DeviceGroup>();
    public DbSet<DeviceGroupMember> DeviceGroupMembers => Set<DeviceGroupMember>();
    public DbSet<Effect> Effects => Set<Effect>();
    public DbSet<EffectTarget> EffectTargets => Set<EffectTarget>();
    public DbSet<TuyaAccountLink> TuyaLinks => Set<TuyaAccountLink>();
    public DbSet<TikTokAccountLink> TikTokLinks => Set<TikTokAccountLink>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<UserIdMap> UserIdMaps => Set<UserIdMap>();
    public DbSet<Riddle> Riddles => Set<Riddle>();
    public DbSet<RiddleCatalog> RiddleCatalogs => Set<RiddleCatalog>();
    public DbSet<RoomPlayerScore> RoomPlayerScores => Set<RoomPlayerScore>();

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

        modelBuilder.Entity<TuyaAccountLink>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        modelBuilder.Entity<TikTokAccountLink>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        modelBuilder.Entity<Room>(e =>
        {
            e.HasKey(x => new { x.Id });
            e.HasIndex(x => new { x.OwnerUserId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(64);
            e.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserIdMap>()
            .HasIndex(x => x.UserGuid)
            .IsUnique();

        modelBuilder.Entity<RiddleRound>()
            .HasIndex(x => new { x.RoomId, x.Status });

        modelBuilder.Entity<RiddleCatalog>()
            .HasIndex(x => new { x.Id })
            .IsUnique();

        modelBuilder.Entity<RiddleRound>()
            .HasIndex(x => x.RoomCode);

        modelBuilder.Entity<RiddleSubmission>()
            .HasIndex(x => new { x.RoundId, x.UserId });

        modelBuilder.Entity<RiddlePlayerStats>()
            .HasIndex(x => new { x.RoomId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<RiddleRound>()
            .HasOne(x => x.CatalogRiddle)
            .WithMany()
            .HasForeignKey(x => x.CatalogRiddleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RoomPlayerScore>()
            .HasIndex(x => new { x.RoomId, x.UserId })
            .IsUnique();
    }
}