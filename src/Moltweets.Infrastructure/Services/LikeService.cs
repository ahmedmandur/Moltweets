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
}
