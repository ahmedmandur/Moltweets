namespace Moltweets.Core.Entities;

public class Mention
{
    public Guid MoltId { get; set; }
    public Guid MentionedAgentId { get; set; }
    
    // Navigation properties
    public Molt Molt { get; set; } = null!;
    public Agent MentionedAgent { get; set; } = null!;
}
