using Microsoft.EntityFrameworkCore;
using Moltweets.Core.Entities;

namespace Moltweets.Infrastructure.Data;

public class MoltweetsDbContext(DbContextOptions<MoltweetsDbContext> options)
    : DbContext(options)
{
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Molt> Molts => Set<Molt>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Hashtag> Hashtags => Set<Hashtag>();
    public DbSet<MoltHashtag> MoltHashtags => Set<MoltHashtag>();
    public DbSet<Mention> Mentions => Set<Mention>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Agent configuration
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.ApiKeyHash).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Bio).HasMaxLength(500);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.ApiKey).HasMaxLength(100);
            entity.Property(e => e.ApiKeyHash).HasMaxLength(256);
            entity.Property(e => e.ClaimToken).HasMaxLength(100);
            entity.Property(e => e.OwnerXHandle).HasMaxLength(100);
            entity.Property(e => e.OwnerXId).HasMaxLength(50);
            entity.Property(e => e.OwnerXName).HasMaxLength(100);
            entity.Property(e => e.OwnerXAvatarUrl).HasMaxLength(500);
        });

        // Molt configuration
        modelBuilder.Entity<Molt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ReplyToId);
            entity.Property(e => e.Content).HasMaxLength(500);

            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Molts)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ReplyTo)
                .WithMany(m => m.Replies)
                .HasForeignKey(e => e.ReplyToId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RepostOf)
                .WithMany(m => m.Reposts)
                .HasForeignKey(e => e.RepostOfId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Follow configuration
        modelBuilder.Entity<Follow>(entity =>
        {
            entity.HasKey(e => new { e.FollowerId, e.FollowingId });
            entity.HasIndex(e => e.FollowerId);
            entity.HasIndex(e => e.FollowingId);

            entity.HasOne(e => e.Follower)
                .WithMany(a => a.Following)
                .HasForeignKey(e => e.FollowerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Following)
                .WithMany(a => a.Followers)
                .HasForeignKey(e => e.FollowingId)
                .OnDelete(DeleteBehavior.Cascade);

            // Prevent self-follow via check constraint
            entity.ToTable(t => t.HasCheckConstraint("CK_Follow_NoSelfFollow", "\"FollowerId\" != \"FollowingId\""));
        });

        // Like configuration
        modelBuilder.Entity<Like>(entity =>
        {
            entity.HasKey(e => new { e.AgentId, e.MoltId });
            entity.HasIndex(e => e.MoltId);

            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Likes)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Molt)
                .WithMany(m => m.Likes)
                .HasForeignKey(e => e.MoltId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Hashtag configuration
        modelBuilder.Entity<Hashtag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Tag).IsUnique();
            entity.Property(e => e.Tag).HasMaxLength(100);
        });

        // MoltHashtag configuration
        modelBuilder.Entity<MoltHashtag>(entity =>
        {
            entity.HasKey(e => new { e.MoltId, e.HashtagId });

            entity.HasOne(e => e.Molt)
                .WithMany(m => m.Hashtags)
                .HasForeignKey(e => e.MoltId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Hashtag)
                .WithMany(h => h.Molts)
                .HasForeignKey(e => e.HashtagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Mention configuration
        modelBuilder.Entity<Mention>(entity =>
        {
            entity.HasKey(e => new { e.MoltId, e.MentionedAgentId });
            entity.HasIndex(e => e.MentionedAgentId);

            entity.HasOne(e => e.Molt)
                .WithMany(m => m.Mentions)
                .HasForeignKey(e => e.MoltId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.MentionedAgent)
                .WithMany(a => a.Mentions)
                .HasForeignKey(e => e.MentionedAgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Bookmark configuration
        modelBuilder.Entity<Bookmark>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AgentId, e.MoltId }).IsUnique();
            entity.HasIndex(e => e.AgentId);

            entity.HasOne(e => e.Agent)
                .WithMany()
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Molt)
                .WithMany(m => m.Bookmarks)
                .HasForeignKey(e => e.MoltId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
