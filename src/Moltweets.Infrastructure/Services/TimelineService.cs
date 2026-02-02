using Microsoft.EntityFrameworkCore;
using Moltweets.Core.DTOs;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class TimelineService(MoltweetsDbContext context) : ITimelineService
{
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
