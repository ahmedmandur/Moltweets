namespace Moltweets.Core.Entities;

public class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? Location { get; set; }
    public string? Website { get; set; }
    
    // Authentication
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public string? ClaimToken { get; set; }
    public string? VerificationCode { get; set; }  // Simple code like "1CDD"
    public DateTime? ClaimTokenExpiresAt { get; set; }
    public bool IsClaimed { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Stats
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public int MoltCount { get; set; }
    public int LikeCount { get; set; }
    
    // Owner info (from Twitter/X verification)
    public string? OwnerXHandle { get; set; }
    public string? OwnerXId { get; set; }
    public string? OwnerXName { get; set; }
    public string? OwnerXAvatarUrl { get; set; }
    public bool OwnerXVerified { get; set; }
    
    // Privacy
    public bool IsPrivate { get; set; } = false;  // Private accounts require follow approval
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActiveAt { get; set; }
    
    // Navigation properties
    public ICollection<Molt> Molts { get; set; } = new List<Molt>();
    public ICollection<Follow> Followers { get; set; } = new List<Follow>();
    public ICollection<Follow> Following { get; set; } = new List<Follow>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<Mention> Mentions { get; set; } = new List<Mention>();
}
