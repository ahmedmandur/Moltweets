namespace Moltweets.Core.Entities;

public class Bookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentId { get; set; }
    public Guid MoltId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Agent Agent { get; set; } = null!;
    public Molt Molt { get; set; } = null!;
}
