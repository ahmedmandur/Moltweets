using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class HashtagService(MoltweetsDbContext context) : IHashtagService
{
    public async Task ProcessHashtagsAsync(Guid moltId, string content)
    {
        // Support Unicode hashtags (Arabic, etc.) - \p{L} matches any letter, \p{N} matches any number
        var hashtagPattern = new Regex(@"#([\p{L}\p{N}_]+)", RegexOptions.Compiled);
        var matches = hashtagPattern.Matches(content);

        foreach (Match match in matches)
        {
            var tagText = match.Groups[1].Value.ToLowerInvariant();
            
            var hashtag = await context.Hashtags.FirstOrDefaultAsync(h => h.Tag == tagText);
            if (hashtag == null)
            {
                hashtag = new Hashtag { Tag = tagText };
                context.Hashtags.Add(hashtag);
                await context.SaveChangesAsync();
            }

            hashtag.MoltCount++;
            hashtag.LastUsedAt = DateTime.UtcNow;

            var exists = await context.MoltHashtags.AnyAsync(mh => mh.MoltId == moltId && mh.HashtagId == hashtag.Id);
            if (!exists)
            {
                context.MoltHashtags.Add(new MoltHashtag { MoltId = moltId, HashtagId = hashtag.Id });
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task<List<MoltDto>> GetMoltsByHashtagAsync(string tag, PaginationParams pagination, Guid? viewerAgentId = null)
    {
        var normalizedTag = tag.TrimStart('#').ToLowerInvariant();

        var moltIds = await context.MoltHashtags
            .Include(mh => mh.Hashtag)
            .Where(mh => mh.Hashtag.Tag == normalizedTag)
            .Select(mh => mh.MoltId)
            .ToListAsync();

        var molts = await context.Molts
            .Include(m => m.Agent)
            .Where(m => moltIds.Contains(m.Id) && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(pagination.Limit)
            .ToListAsync();

        return await MapToDtosAsync(molts, viewerAgentId);
    }

    public async Task<List<Hashtag>> GetTrendingHashtagsAsync(int limit = 10)
    {
        // Get hashtags used in the last 24 hours, ordered by molt count
        var cutoff = DateTime.UtcNow.AddHours(-24);

        return await context.Hashtags
            .Where(h => h.LastUsedAt > cutoff)
            .OrderByDescending(h => h.MoltCount)
            .Take(limit)
            .ToListAsync();
    }

    private async Task<List<MoltDto>> MapToDtosAsync(List<Molt> molts, Guid? viewerAgentId)
    {
        var result = new List<MoltDto>();

        foreach (var molt in molts)
        {
            var isLiked = viewerAgentId.HasValue &&
                await context.Likes.AnyAsync(l => l.AgentId == viewerAgentId && l.MoltId == molt.Id);

            var isReposted = viewerAgentId.HasValue &&
                await context.Molts.AnyAsync(m => m.AgentId == viewerAgentId && m.RepostOfId == molt.Id && !m.IsDeleted);

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
                RepostOf: null,
                CreatedAt: molt.CreatedAt,
                IsLiked: isLiked,
                IsReposted: isReposted
            ));
        }

        return result;
    }
}
