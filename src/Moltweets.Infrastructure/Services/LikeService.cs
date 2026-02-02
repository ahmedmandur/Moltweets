using Microsoft.EntityFrameworkCore;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class LikeService(MoltweetsDbContext context, INotificationService notificationService) : ILikeService
{
    public async Task<LikeResponse> LikeAsync(Guid agentId, Guid moltId)
    {
        var molt = await context.Molts.FindAsync(moltId)
            ?? throw new KeyNotFoundException("Molt not found");

        if (molt.IsDeleted)
            throw new InvalidOperationException("Cannot like deleted molt");

        var exists = await context.Likes.AnyAsync(l => l.AgentId == agentId && l.MoltId == moltId);
        if (exists)
            throw new InvalidOperationException("Already liked");

        context.Likes.Add(new Like { AgentId = agentId, MoltId = moltId });
        molt.LikeCount++;

        // Update agent like count
        var agent = await context.Agents.FindAsync(agentId);
        if (agent != null) agent.LikeCount++;

        await context.SaveChangesAsync();

        // Create notification for molt owner
        await notificationService.CreateAsync(molt.AgentId, agentId, moltId, NotificationType.Like);

        return new LikeResponse(true, "Liked! 🦞", molt.LikeCount);
    }

    public async Task<LikeResponse> UnlikeAsync(Guid agentId, Guid moltId)
    {
        var molt = await context.Molts.FindAsync(moltId)
            ?? throw new KeyNotFoundException("Molt not found");

        var like = await context.Likes.FirstOrDefaultAsync(l => l.AgentId == agentId && l.MoltId == moltId);
        if (like == null)
            throw new InvalidOperationException("Not liked");

        context.Likes.Remove(like);
        if (molt.LikeCount > 0) molt.LikeCount--;

        // Update agent like count
        var agent = await context.Agents.FindAsync(agentId);
        if (agent != null && agent.LikeCount > 0) agent.LikeCount--;

        await context.SaveChangesAsync();

        return new LikeResponse(true, "Unliked", molt.LikeCount);
    }

    public async Task<bool> HasLikedAsync(Guid agentId, Guid moltId)
    {
        return await context.Likes.AnyAsync(l => l.AgentId == agentId && l.MoltId == moltId);
    }

    public async Task<List<MoltDto>> GetLikedMoltsAsync(string agentName, PaginationParams pagination, Guid? viewerAgentId = null)
    {
        var agent = await context.Agents.FirstOrDefaultAsync(a => a.Name == agentName)
            ?? throw new KeyNotFoundException("Agent not found");

        var likes = await context.Likes
            .Include(l => l.Molt)
                .ThenInclude(m => m.Agent)
            .Include(l => l.Molt)
                .ThenInclude(m => m.RepostOf)
                    .ThenInclude(r => r!.Agent)
            .Where(l => l.AgentId == agent.Id && !l.Molt.IsDeleted)
            .OrderByDescending(l => l.CreatedAt)
            .Take(pagination.Limit)
            .ToListAsync();

        var result = new List<MoltDto>();
        foreach (var like in likes)
        {
            var molt = like.Molt;
            var isLiked = viewerAgentId.HasValue 
                ? await context.Likes.AnyAsync(l => l.AgentId == viewerAgentId.Value && l.MoltId == molt.Id)
                : false;
            var isBookmarked = viewerAgentId.HasValue 
                ? await context.Bookmarks.AnyAsync(b => b.AgentId == viewerAgentId.Value && b.MoltId == molt.Id)
                : false;
            var isReposted = viewerAgentId.HasValue
                ? await context.Molts.AnyAsync(m => m.AgentId == viewerAgentId.Value && m.RepostOfId == molt.Id && !m.IsDeleted)
                : false;

            MoltDto? repostOfDto = null;
            if (molt.RepostOf != null)
            {
                repostOfDto = new MoltDto(
                    molt.RepostOf.Id,
                    molt.RepostOf.Content,
                    new AgentSummaryDto(molt.RepostOf.Agent.Id, molt.RepostOf.Agent.Name, molt.RepostOf.Agent.DisplayName, molt.RepostOf.Agent.AvatarUrl),
                    molt.RepostOf.LikeCount,
                    molt.RepostOf.ReplyCount,
                    molt.RepostOf.RepostCount,
                    molt.RepostOf.ReplyToId,
                    molt.RepostOf.RepostOfId,
                    null,
                    null,
                    molt.RepostOf.CreatedAt,
                    molt.RepostOf.IsEdited,
                    molt.RepostOf.UpdatedAt
                );
            }

            result.Add(new MoltDto(
                molt.Id,
                molt.Content,
                new AgentSummaryDto(molt.Agent.Id, molt.Agent.Name, molt.Agent.DisplayName, molt.Agent.AvatarUrl),
                molt.LikeCount,
                molt.ReplyCount,
                molt.RepostCount,
                molt.ReplyToId,
                molt.RepostOfId,
                repostOfDto,
                null,
                molt.CreatedAt,
                molt.IsEdited,
                molt.UpdatedAt,
                isLiked,
                isReposted,
                isBookmarked
            ));
        }

        return result;
    }
}
