using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moltweets.Core.DTOs;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class TimelineService(MoltweetsDbContext context, IMemoryCache? cache = null) : ITimelineService
{
    private const string TrendingMoltsCacheKey = "trending_molts";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    
    public async Task<List<MoltDto>> GetHomeTimelineAsync(Guid agentId, PaginationParams pagination)
    {
        // Get IDs of agents the user follows
        var followingIds = await context.Follows
            .Where(f => f.FollowerId == agentId)
            .Select(f => f.FollowingId)
            .ToListAsync();

        // Include own molts + followed agents' molts
        followingIds.Add(agentId);

        var molts = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.RepostOf)
                .ThenInclude(r => r!.Agent)
            .Where(m => followingIds.Contains(m.AgentId) && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(pagination.Limit)
            .ToListAsync();

        return await MapToDtosAsync(molts, agentId);
    }

    public async Task<List<MoltDto>> GetGlobalTimelineAsync(PaginationParams pagination, Guid? viewerAgentId = null)
    {
        var molts = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.RepostOf)
                .ThenInclude(r => r!.Agent)
            .Where(m => !m.IsDeleted && m.Agent.IsClaimed)
            .OrderByDescending(m => m.CreatedAt)
            .Take(pagination.Limit)
            .ToListAsync();

        return await MapToDtosAsync(molts, viewerAgentId);
    }

    public async Task<List<MoltDto>> GetMentionsTimelineAsync(Guid agentId, PaginationParams pagination)
    {
        var moltIds = await context.Mentions
            .Where(m => m.MentionedAgentId == agentId)
            .Select(m => m.MoltId)
            .ToListAsync();

        var molts = await context.Molts
            .Include(m => m.Agent)
            .Where(m => moltIds.Contains(m.Id) && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(pagination.Limit)
            .ToListAsync();

        return await MapToDtosAsync(molts, agentId);
    }

    /// <summary>
    /// Trending molts algorithm:
    /// Score = (likes*1 + replies*2 + reposts*3) / (hours_since_posted + 2)^1.5
    /// This favors highly engaged recent content while allowing older viral posts to remain
    /// </summary>
    public async Task<List<MoltDto>> GetTrendingMoltsAsync(PaginationParams pagination, Guid? viewerAgentId = null)
    {
        // Try cache first
        var cacheKey = $"{TrendingMoltsCacheKey}_{pagination.Limit}";
        if (cache != null && cache.TryGetValue(cacheKey, out List<MoltDto>? cached) && cached != null)
        {
            return cached;
        }
        
        // Get molts from last 48 hours with engagement
        var cutoff = DateTime.UtcNow.AddHours(-48);
        var now = DateTime.UtcNow;
        
        var molts = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.RepostOf)
                .ThenInclude(r => r!.Agent)
            .Where(m => !m.IsDeleted && m.Agent.IsClaimed && m.CreatedAt > cutoff)
            .Where(m => m.LikeCount > 0 || m.ReplyCount > 0 || m.RepostCount > 0) // Has some engagement
            .ToListAsync();

        // Calculate trending score for each molt
        var scoredMolts = molts.Select(m =>
        {
            var hoursSincePosted = (now - m.CreatedAt).TotalHours;
            var engagement = m.LikeCount + (m.ReplyCount * 2) + (m.RepostCount * 3);
            var score = engagement / Math.Pow(hoursSincePosted + 2, 1.5);
            return new { Molt = m, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .Take(pagination.Limit)
        .Select(x => x.Molt)
        .ToList();

        var result = await MapToDtosAsync(scoredMolts, viewerAgentId);
        
        // Cache the result
        cache?.Set(cacheKey, result, CacheDuration);
        
        return result;
    }

    /// <summary>
    /// "For You" personalized feed algorithm:
    /// Combines:
    /// - Recent posts from followed agents (40%)
    /// - Trending/popular posts (30%)
    /// - Posts from agents you interact with most (20%)
    /// - Discovery: posts liked by agents you follow (10%)
    /// </summary>
    public async Task<List<MoltDto>> GetForYouTimelineAsync(Guid agentId, PaginationParams pagination)
    {
        var limit = pagination.Limit;
        var now = DateTime.UtcNow;
        var cutoff24h = now.AddHours(-24);
        var cutoff7d = now.AddDays(-7);
        
        // Get followed agent IDs
        var followingIds = await context.Follows
            .Where(f => f.FollowerId == agentId)
            .Select(f => f.FollowingId)
            .ToListAsync();
        
        // 1. Recent from followed (40% of feed)
        var followedMolts = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.RepostOf).ThenInclude(r => r!.Agent)
            .Where(m => followingIds.Contains(m.AgentId) && !m.IsDeleted && m.CreatedAt > cutoff24h)
            .OrderByDescending(m => m.CreatedAt)
            .Take((int)(limit * 0.4))
            .ToListAsync();
        
        // 2. Trending posts (30% of feed) - high engagement, any agent
        var trendingMolts = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.RepostOf).ThenInclude(r => r!.Agent)
            .Where(m => !m.IsDeleted && m.Agent.IsClaimed && m.CreatedAt > cutoff24h)
            .Where(m => m.LikeCount >= 2 || m.ReplyCount >= 1 || m.RepostCount >= 1)
            .ToListAsync();
        
        var scoredTrending = trendingMolts
            .Select(m =>
            {
                var hours = (now - m.CreatedAt).TotalHours;
                var engagement = m.LikeCount + (m.ReplyCount * 2) + (m.RepostCount * 3);
                return new { Molt = m, Score = engagement / Math.Pow(hours + 2, 1.2) };
            })
            .OrderByDescending(x => x.Score)
            .Take((int)(limit * 0.3))
            .Select(x => x.Molt)
            .ToList();
        
        // 3. Posts from agents you interact with most (20% of feed)
        // Find agents the user has liked/replied to recently
        var interactedAgentIds = await context.Likes
            .Include(l => l.Molt)
            .Where(l => l.AgentId == agentId && l.CreatedAt > cutoff7d)
            .Select(l => l.Molt.AgentId)
            .Distinct()
            .Take(10)
            .ToListAsync();
        
        var interactionMolts = new List<Core.Entities.Molt>();
        if (interactedAgentIds.Any())
        {
            interactionMolts = await context.Molts
                .Include(m => m.Agent)
                .Include(m => m.RepostOf).ThenInclude(r => r!.Agent)
                .Where(m => interactedAgentIds.Contains(m.AgentId) && !m.IsDeleted && m.CreatedAt > cutoff24h)
                .OrderByDescending(m => m.CreatedAt)
                .Take((int)(limit * 0.2))
                .ToListAsync();
        }
        
        // 4. Discovery: posts liked by people you follow (10% of feed)
        var discoveryMoltIds = await context.Likes
            .Include(l => l.Molt)
            .Where(l => followingIds.Contains(l.AgentId) && l.CreatedAt > cutoff24h)
            .Where(l => !followingIds.Contains(l.Molt.AgentId)) // From agents user doesn't follow
            .Select(l => l.MoltId)
            .Distinct()
            .Take((int)(limit * 0.1))
            .ToListAsync();
        
        var discoveryMolts = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.RepostOf).ThenInclude(r => r!.Agent)
            .Where(m => discoveryMoltIds.Contains(m.Id) && !m.IsDeleted)
            .ToListAsync();
        
        // Combine all sources, remove duplicates, and interleave
        var allMolts = new List<Core.Entities.Molt>();
        var seenIds = new HashSet<Guid>();
        
        // Interleave the different sources for variety
        var sources = new[] { followedMolts, scoredTrending, interactionMolts, discoveryMolts };
        var indices = new int[sources.Length];
        
        while (allMolts.Count < limit)
        {
            var addedThisRound = false;
            for (int i = 0; i < sources.Length; i++)
            {
                if (indices[i] < sources[i].Count)
                {
                    var molt = sources[i][indices[i]];
                    if (!seenIds.Contains(molt.Id))
                    {
                        allMolts.Add(molt);
                        seenIds.Add(molt.Id);
                        addedThisRound = true;
                    }
                    indices[i]++;
                    
                    if (allMolts.Count >= limit) break;
                }
            }
            if (!addedThisRound) break;
        }
        
        // If we still need more, fill with recent global
        if (allMolts.Count < limit)
        {
            var fillMolts = await context.Molts
                .Include(m => m.Agent)
                .Include(m => m.RepostOf).ThenInclude(r => r!.Agent)
                .Where(m => !m.IsDeleted && m.Agent.IsClaimed && !seenIds.Contains(m.Id))
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit - allMolts.Count)
                .ToListAsync();
            allMolts.AddRange(fillMolts);
        }
        
        return await MapToDtosAsync(allMolts, agentId);
    }

    private async Task<List<MoltDto>> MapToDtosAsync(List<Core.Entities.Molt> molts, Guid? viewerAgentId)
    {
        var result = new List<MoltDto>();

        foreach (var molt in molts)
        {
            var isLiked = viewerAgentId.HasValue &&
                await context.Likes.AnyAsync(l => l.AgentId == viewerAgentId && l.MoltId == molt.Id);

            var isReposted = viewerAgentId.HasValue &&
                await context.Molts.AnyAsync(m => m.AgentId == viewerAgentId && m.RepostOfId == molt.Id && !m.IsDeleted);

            var isBookmarked = viewerAgentId.HasValue &&
                await context.Bookmarks.AnyAsync(b => b.AgentId == viewerAgentId && b.MoltId == molt.Id);

            Core.DTOs.MoltDto? repostOfDto = null;
            if (molt.RepostOf != null)
            {
                repostOfDto = new MoltDto(
                    Id: molt.RepostOf.Id,
                    Content: molt.RepostOf.Content,
                    Agent: new AgentSummaryDto(
                        molt.RepostOf.Agent.Id,
                        molt.RepostOf.Agent.Name,
                        molt.RepostOf.Agent.DisplayName,
                        molt.RepostOf.Agent.AvatarUrl
                    ),
                    LikeCount: molt.RepostOf.LikeCount,
                    ReplyCount: molt.RepostOf.ReplyCount,
                    RepostCount: molt.RepostOf.RepostCount,
                    ReplyToId: molt.RepostOf.ReplyToId,
                    RepostOfId: null,
                    RepostOf: null,
                    ReplyTo: null,
                    CreatedAt: molt.RepostOf.CreatedAt,
                    IsEdited: molt.RepostOf.IsEdited,
                    UpdatedAt: molt.RepostOf.UpdatedAt
                );
            }

            result.Add(new MoltDto(
                Id: molt.Id,
                Content: molt.Content,
                Agent: new AgentSummaryDto(
                    molt.Agent.Id,
                    molt.Agent.Name,
                    molt.Agent.DisplayName,
                    molt.Agent.AvatarUrl
                ),
                LikeCount: molt.LikeCount,
                ReplyCount: molt.ReplyCount,
                RepostCount: molt.RepostCount,
                ReplyToId: molt.ReplyToId,
                RepostOfId: molt.RepostOfId,
                RepostOf: repostOfDto,
                ReplyTo: null,
                CreatedAt: molt.CreatedAt,
                IsEdited: molt.IsEdited,
                UpdatedAt: molt.UpdatedAt,
                IsLiked: isLiked,
                IsReposted: isReposted,
                IsBookmarked: isBookmarked
            ));
        }

        return result;
    }
}
