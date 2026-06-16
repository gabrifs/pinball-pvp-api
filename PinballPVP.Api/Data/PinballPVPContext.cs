using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Models;

namespace PinballPVP.Api.Data;

public class PinballPVPContext : DbContext
{
    public PinballPVPContext(DbContextOptions<PinballPVPContext> options) : base(options) {}

    public DbSet<User> Users { get; set; }

    public DbSet<SoloMatch> SoloMatches { get; set; }

    public DbSet<VersusMatch> VersusMatches { get; set; }

    public DbSet<PlayerRecord> PlayerRecords { get; set; }

    public DbSet<AllTimeBestRecord> AllTimeBestRecords { get; set; }

    public DbSet<YearlyLeaderboardEntry> YearlyLeaderboardEntries { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<PendingVersusMatch> PendingVersusMatches { get; set; }

    public DbSet<PasswordRecoveryCode> PasswordRecoveryCodes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User <--One-to-One--> Player Record
        // PlayerRecord: Set UserID as the Primary Key
        modelBuilder.Entity<PlayerRecord>()
            .HasKey(playerRecord => playerRecord.UserId);

        // User: Configure relationship, each User has one PlayerRecord, one PlayerRecord belongs to one User
        modelBuilder.Entity<User>()
            .HasOne(user => user.PlayerRecord)
            .WithOne(playerRecord => playerRecord.User)
            .HasForeignKey<PlayerRecord>(playerRecord => playerRecord.UserId);

        // Indexes to support ORDER BY … LIMIT 100 on all-time leaderboard and rank queries.
        // PostgreSQL can scan a B-tree index in either direction, so a plain index covers both
        // ASC and DESC sorts without needing separate descending indexes.
        modelBuilder.Entity<PlayerRecord>().HasIndex(r => r.SoloHighscore);
        modelBuilder.Entity<PlayerRecord>().HasIndex(r => r.SoloWins);
        modelBuilder.Entity<PlayerRecord>().HasIndex(r => r.VersusHighscore);
        modelBuilder.Entity<PlayerRecord>().HasIndex(r => r.VersusWins);

        // User <--One-to-One--> All-Time Best Record
        // AllTimeBestRecord: Set UserID as the Primary Key
        modelBuilder.Entity<AllTimeBestRecord>()
            .HasKey(bestRecord => bestRecord.UserId);

        modelBuilder.Entity<User>()
            .HasOne(user => user.AllTimeBestRecord)
            .WithOne(bestRecord => bestRecord.User)
            .HasForeignKey<AllTimeBestRecord>(bestRecord => bestRecord.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Yearly Leaderboard Entries: nullable UserId so historical entries survive account deletion
        modelBuilder.Entity<YearlyLeaderboardEntry>()
            .HasOne(entry => entry.User)
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<YearlyLeaderboardEntry>()
            .HasIndex(entry => new { entry.Year, entry.Category, entry.Rank })
            .IsUnique();

        // Solo Matches
        // Configure One User to Many Solo Matches relationship and UserID as Foreign Key
        modelBuilder.Entity<SoloMatch>()
            .HasOne(match => match.User)
            .WithMany()
            .HasForeignKey(match => match.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Versus Matches
        // Configure WinnerId and LoserId as a foreign keys, and configure Match.WinnerId and Match.LoserId to point to User.Id
        // Match -> Winner
        modelBuilder.Entity<VersusMatch>()
            .HasOne(match => match.Winner)
            .WithMany()
            .HasForeignKey(match => match.WinnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Match -> Loser
        modelBuilder.Entity<VersusMatch>()
            .HasOne(match => match.Loser)
            .WithMany()
            .HasForeignKey(match => match.LoserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Pending Versus Matches
        modelBuilder.Entity<PendingVersusMatch>()
            .HasOne(p => p.Reporter)
            .WithMany()
            .HasForeignKey(p => p.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PendingVersusMatch>()
            .HasOne(p => p.Winner)
            .WithMany()
            .HasForeignKey(p => p.WinnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PendingVersusMatch>()
            .HasOne(p => p.Loser)
            .WithMany()
            .HasForeignKey(p => p.LoserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PendingVersusMatch>()
            .HasIndex(p => new { p.MinPlayerId, p.MaxPlayerId })
            .IsUnique();

        // Refresh Tokens
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.TokenHash)
            .IsUnique();

        // Password Recovery Codes
        modelBuilder.Entity<PasswordRecoveryCode>()
            .HasOne(rc => rc.User)
            .WithMany()
            .HasForeignKey(rc => rc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PasswordRecoveryCode>()
            .HasIndex(rc => new { rc.UserId, rc.Used });

        base.OnModelCreating(modelBuilder);
    }
}