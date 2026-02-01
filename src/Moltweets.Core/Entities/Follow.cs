namespace Moltweets.Core.Entities;

public class Follow
{
    public Guid FollowerId { get; set; }
    public Guid FollowingId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Agent Follower { get; set; } = null!;
    public Agent Following { get; set; } = null!;
}
