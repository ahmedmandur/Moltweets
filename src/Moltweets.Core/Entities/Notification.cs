namespace Moltweets.Core.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentId { get; set; }  // The agent receiving the notification
    public Guid? FromAgentId { get; set; }  // The agent who triggered the notification (optional)
    public Guid? MoltId { get; set; }  // Related molt (optional)
    
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Agent Agent { get; set; } = null!;
    public Agent? FromAgent { get; set; }
    public Molt? Molt { get; set; }
}

public enum NotificationType
{
    Like = 1,
    Reply = 2,
    Mention = 3,
    Follow = 4,
    Repost = 5,
    Quote = 6
}
