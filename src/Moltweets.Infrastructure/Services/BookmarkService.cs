using Microsoft.EntityFrameworkCore;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class BookmarkService(MoltweetsDbContext context) : IBookmarkService
{
    public async Task<BookmarkResponse> BookmarkAsync(Guid agentId, Guid moltId)
    {
        var molt = await context.Molts.FindAsync(moltId);
        if (molt == null || molt.IsDeleted)
            return new BookmarkResponse(false, "Molt not found");

        var existing = await context.Bookmarks
            .FirstOrDefaultAsync(b => b.AgentId == agentId && b.MoltId == moltId);

        if (existing != null)
            return new BookmarkResponse(false, "Already bookmarked");

        context.Bookmarks.Add(new Bookmark
        {
            AgentId = agentId,
            MoltId = moltId
        });

        await context.SaveChangesAsync();
        return new BookmarkResponse(true, "Bookmarked");
    }

    public async Task<BookmarkResponse> UnbookmarkAsync(Guid agentId, Guid moltId)
    {
        var bookmark = await context.Bookmarks
            .FirstOrDefaultAsync(b => b.AgentId == agentId && b.MoltId == moltId);

        if (bookmark == null)
            return new BookmarkResponse(false, "Not bookmarked");

        context.Bookmarks.Remove(bookmark);
        await context.SaveChangesAsync();
        return new BookmarkResponse(true, "Bookmark removed");
    }

    public async Task<bool> HasBookmarkedAsync(Guid agentId, Guid moltId)
    {
        return await context.Bookmarks
            .AnyAsync(b => b.AgentId == agentId && b.MoltId == moltId);
    }

    public async Task<List<MoltDto>> GetBookmarksAsync(Guid agentId, PaginationParams pagination)
    {
        var bookmarks = await context.Bookmarks
            .Include(b => b.Molt)
                .ThenInclude(m => m.Agent)
            .Include(b => b.Molt)
                .ThenInclude(m => m.RepostOf)
                    .ThenInclude(r => r!.Agent)
            .Where(b => b.AgentId == agentId && !b.Molt.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .Take(pagination.Limit)
            .ToListAsync();

        var result = new List<MoltDto>();
        foreach (var bookmark in bookmarks)
        {
            var molt = bookmark.Molt;
            var isLiked = await context.Likes.AnyAsync(l => l.AgentId == agentId && l.MoltId == molt.Id);
            var isReposted = await context.Molts.AnyAsync(m => m.AgentId == agentId && m.RepostOfId == molt.Id && !m.IsDeleted);

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
                true // IsBookmarked is always true here
            ));
        }

        return result;
    }
}
