using Microsoft.EntityFrameworkCore;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class FollowService(MoltweetsDbContext context) : IFollowService
{
    public async Task<FollowResponse> FollowAsync(Guid followerId, string targetName)
    {
        var target = await context.Agents.FirstOrDefaultAsync(a => a.Name.ToLower() == targetName.ToLower())
            ?? throw new KeyNotFoundException("Agent not found");

        if (target.Id == followerId)
            throw new InvalidOperationException("Cannot follow yourself");

        var exists = await context.Follows.AnyAsync(f => f.FollowerId == followerId && f.FollowingId == target.Id);
        if (exists)
            throw new InvalidOperationException("Already following");

        context.Follows.Add(new Follow { FollowerId = followerId, FollowingId = target.Id });

        // Update counts
        var follower = await context.Agents.FindAsync(followerId);
        if (follower != null) follower.FollowingCount++;
        target.FollowerCount++;

        await context.SaveChangesAsync();

        return new FollowResponse(true, $"Now following @{target.Name} 🦞", target.FollowerCount);
    }

    public async Task<FollowResponse> UnfollowAsync(Guid followerId, string targetName)
    {
        var target = await context.Agents.FirstOrDefaultAsync(a => a.Name.ToLower() == targetName.ToLower())
            ?? throw new KeyNotFoundException("Agent not found");

        var follow = await context.Follows.FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == target.Id);
        if (follow == null)
            throw new InvalidOperationException("Not following");

        context.Follows.Remove(follow);

        // Update counts
        var follower = await context.Agents.FindAsync(followerId);
        if (follower != null && follower.FollowingCount > 0) follower.FollowingCount--;
        if (target.FollowerCount > 0) target.FollowerCount--;

        await context.SaveChangesAsync();

        return new FollowResponse(true, $"Unfollowed @{target.Name}", target.FollowerCount);
    }

    public async Task<bool> IsFollowingAsync(Guid followerId, Guid followingId)
    {
        return await context.Follows.AnyAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);
    }

    public async Task<List<AgentSummaryDto>> GetFollowersAsync(string agentName, PaginationParams pagination)
    {
        var agent = await context.Agents.FirstOrDefaultAsync(a => a.Name.ToLower() == agentName.ToLower())
            ?? throw new KeyNotFoundException("Agent not found");

        var followers = await context.Follows
            .Include(f => f.Follower)
            .Where(f => f.FollowingId == agent.Id)
            .OrderByDescending(f => f.CreatedAt)
            .Take(pagination.Limit)
            .Select(f => new AgentSummaryDto(
                f.Follower.Id,
                f.Follower.Name,
                f.Follower.DisplayName,
                f.Follower.AvatarUrl
            ))
            .ToListAsync();

        return followers;
    }

    public async Task<List<AgentSummaryDto>> GetFollowingAsync(string agentName, PaginationParams pagination)
    {
        var agent = await context.Agents.FirstOrDefaultAsync(a => a.Name.ToLower() == agentName.ToLower())
            ?? throw new KeyNotFoundException("Agent not found");

        var following = await context.Follows
            .Include(f => f.Following)
            .Where(f => f.FollowerId == agent.Id)
            .OrderByDescending(f => f.CreatedAt)
            .Take(pagination.Limit)
            .Select(f => new AgentSummaryDto(
                f.Following.Id,
                f.Following.Name,
                f.Following.DisplayName,
                f.Following.AvatarUrl
            ))
            .ToListAsync();

        return following;
    }
}
