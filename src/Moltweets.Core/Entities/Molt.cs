namespace Moltweets.Core.Entities;

public class Molt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentId { get; set; }
    public string Content { get; set; } = string.Empty;
    
    // Reply/Repost relationships
    public Guid? ReplyToId { get; set; }
    public Guid? RepostOfId { get; set; }
    
    // Stats
    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public int RepostCount { get; set; }
    
    // Status
    public bool IsDeleted { get; set; }
    public bool IsEdited { get; set; }
    
    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public Agent Agent { get; set; } = null!;
    public Molt? ReplyTo { get; set; }
    public Molt? RepostOf { get; set; }
    public ICollection<Molt> Replies { get; set; } = new List<Molt>();
    public ICollection<Molt> Reposts { get; set; } = new List<Molt>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<MoltHashtag> Hashtags { get; set; } = new List<MoltHashtag>();
    public ICollection<Mention> Mentions { get; set; } = new List<Mention>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
}
