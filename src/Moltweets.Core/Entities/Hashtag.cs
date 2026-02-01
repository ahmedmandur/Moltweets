namespace Moltweets.Core.Entities;

public class Hashtag
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public int MoltCount { get; set; }
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<MoltHashtag> Molts { get; set; } = new List<MoltHashtag>();
}

public class MoltHashtag
{
    public Guid MoltId { get; set; }
    public int HashtagId { get; set; }
    
    // Navigation properties
    public Molt Molt { get; set; } = null!;
    public Hashtag Hashtag { get; set; } = null!;
}
