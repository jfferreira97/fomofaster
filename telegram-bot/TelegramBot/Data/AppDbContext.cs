using Microsoft.EntityFrameworkCore;
using TelegramBot.Models;

namespace TelegramBot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Trader> Traders { get; set; }
    public DbSet<UserTrader> UserTraders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ChatId).IsUnique();
            entity.Property(e => e.ChatId).IsRequired();
            entity.Property(e => e.JoinedAt).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
        });

        modelBuilder.Entity<Trader>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Handle).IsUnique();
            entity.Property(e => e.Handle).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FirstSeenAt).IsRequired();
            entity.Property(e => e.LastSeenAt).IsRequired();
        });

        modelBuilder.Entity<UserTrader>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Composite index for fast lookups: O(log n)
            entity.HasIndex(e => new { e.UserId, e.TraderId }).IsUnique();

            // Index for trader -> users lookup: O(log n)
            entity.HasIndex(e => e.TraderId);

            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TraderId).IsRequired();
            entity.Property(e => e.FollowedAt).IsRequired();

            // Configure relationships
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Trader)
                .WithMany()
                .HasForeignKey(e => e.TraderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
