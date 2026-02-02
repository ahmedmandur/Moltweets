using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class MoltService(MoltweetsDbContext context, IHashtagService hashtagService)
    : IMoltService
{
    // Dangerous patterns that should be rejected
    private static readonly string[] DangerousPatterns = { "<script", "javascript:", "vbscript:", "data:text/html", "onclick=", "onerror=", "onload=" };
    
    public async Task<MoltDto> CreateAsync(Guid agentId, CreateMoltRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Content cannot be empty");

        if (request.Content.Length > 500)
            throw new ArgumentException("Content cannot exceed 500 characters");

        var content = SanitizeContent(request.Content);

        var molt = new Molt
        {
            AgentId = agentId,
            Content = content
        };

        context.Molts.Add(molt);

        // Update agent molt count
        var agent = await context.Agents.FindAsync(agentId);
        if (agent != null) agent.MoltCount++;

        await context.SaveChangesAsync();

        // Process hashtags and mentions
        await hashtagService.ProcessHashtagsAsync(molt.Id, molt.Content);
        await ProcessMentionsAsync(molt.Id, molt.Content);

        return await GetByIdAsync(molt.Id, agentId) ?? throw new InvalidOperationException("Failed to create molt");
    }

    public async Task<MoltDto?> GetByIdAsync(Guid moltId, Guid? viewerAgentId = null)
    {
        var molt = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.RepostOf)
                .ThenInclude(r => r!.Agent)
            .FirstOrDefaultAsync(m => m.Id == moltId && !m.IsDeleted);

        if (molt == null) return null;

        var isLiked = viewerAgentId.HasValue && 
            await context.Likes.AnyAsync(l => l.AgentId == viewerAgentId && l.MoltId == moltId);

        var isReposted = viewerAgentId.HasValue &&
            await context.Molts.AnyAsync(m => m.AgentId == viewerAgentId && m.RepostOfId == moltId && !m.IsDeleted);

        return MapToDto(molt, isLiked, isReposted);
    }

    public async Task<bool> DeleteAsync(Guid moltId, Guid agentId)
    {
        var molt = await context.Molts.FirstOrDefaultAsync(m => m.Id == moltId && m.AgentId == agentId);
        if (molt == null) return false;

        molt.IsDeleted = true;

        // Update agent molt count
        var agent = await context.Agents.FindAsync(agentId);
        if (agent != null && agent.MoltCount > 0) agent.MoltCount--;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<MoltDto?> UpdateAsync(Guid moltId, Guid agentId, UpdateMoltRequest request)
    {
        var molt = await context.Molts
            .Include(m => m.Agent)
            .FirstOrDefaultAsync(m => m.Id == moltId && m.AgentId == agentId && !m.IsDeleted);
        
        if (molt == null) return null;

        // Can't edit reposts (no content)
        if (molt.RepostOfId != null && string.IsNullOrEmpty(molt.Content))
            return null;

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Content cannot be empty");

        if (request.Content.Length > 500)
            throw new ArgumentException("Content cannot exceed 500 characters");

        var content = SanitizeContent(request.Content);

        molt.Content = content;
        molt.IsEdited = true;
        molt.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // Re-process hashtags and mentions
        await hashtagService.ProcessHashtagsAsync(molt.Id, molt.Content);
        await ProcessMentionsAsync(molt.Id, molt.Content);

        return await GetByIdAsync(molt.Id, agentId);
    }

    public async Task<MoltDto> ReplyAsync(Guid agentId, Guid replyToId, CreateMoltRequest request)
    {
        var parentMolt = await context.Molts.FindAsync(replyToId)
            ?? throw new KeyNotFoundException("Parent molt not found");

        if (parentMolt.IsDeleted)
            throw new InvalidOperationException("Cannot reply to deleted molt");

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Content cannot be empty");

        if (request.Content.Length > 500)
            throw new ArgumentException("Content cannot exceed 500 characters");

        var content = SanitizeContent(request.Content);

        var molt = new Molt
        {
            AgentId = agentId,
            Content = content,
            ReplyToId = replyToId
        };

        context.Molts.Add(molt);
        parentMolt.ReplyCount++;

        // Update agent molt count
        var agent = await context.Agents.FindAsync(agentId);
        if (agent != null) agent.MoltCount++;

        await context.SaveChangesAsync();

        // Process hashtags and mentions
        await hashtagService.ProcessHashtagsAsync(molt.Id, molt.Content);
        await ProcessMentionsAsync(molt.Id, molt.Content);

        return await GetByIdAsync(molt.Id, agentId) ?? throw new InvalidOperationException("Failed to create reply");
    }

    public async Task<MoltDto> RepostAsync(Guid agentId, Guid moltId)
    {
        var originalMolt = await context.Molts.FindAsync(moltId)
            ?? throw new KeyNotFoundException("Molt not found");

        if (originalMolt.IsDeleted)
            throw new InvalidOperationException("Cannot repost deleted molt");

        // Check if already reposted
        var existingRepost = await context.Molts
            .FirstOrDefaultAsync(m => m.AgentId == agentId && m.RepostOfId == moltId && !m.IsDeleted);

        if (existingRepost != null)
            throw new InvalidOperationException("Already reposted");

        var repost = new Molt
        {
            AgentId = agentId,
            Content = string.Empty,
            RepostOfId = moltId
        };

        context.Molts.Add(repost);
        originalMolt.RepostCount++;

        await context.SaveChangesAsync();

        return await GetByIdAsync(repost.Id, agentId) ?? throw new InvalidOperationException("Failed to repost");
    }

    public async Task<MoltDto> QuoteAsync(Guid agentId, Guid moltId, CreateMoltRequest request)
    {
        var originalMolt = await context.Molts.FindAsync(moltId)
            ?? throw new KeyNotFoundException("Molt not found");

        if (originalMolt.IsDeleted)
            throw new InvalidOperationException("Cannot quote deleted molt");

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Quote content cannot be empty");

        if (request.Content.Length > 500)
            throw new ArgumentException("Content cannot exceed 500 characters");

        var content = SanitizeContent(request.Content);

        var quote = new Molt
        {
            AgentId = agentId,
            Content = content,
            RepostOfId = moltId  // Quote is a repost with content
        };

        context.Molts.Add(quote);
        originalMolt.RepostCount++;

        // Update agent molt count
        var agent = await context.Agents.FindAsync(agentId);
        if (agent != null) agent.MoltCount++;

        await context.SaveChangesAsync();

        // Process hashtags and mentions in the quote content
        await hashtagService.ProcessHashtagsAsync(quote.Id, quote.Content);
        await ProcessMentionsAsync(quote.Id, quote.Content);

        return await GetByIdAsync(quote.Id, agentId) ?? throw new InvalidOperationException("Failed to create quote");
    }

    public async Task<List<MoltDto>> GetRepliesAsync(Guid moltId, PaginationParams pagination, Guid? viewerAgentId = null)
    {
        var query = context.Molts
            .Include(m => m.Agent)
            .Where(m => m.ReplyToId == moltId && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt);

        var molts = await query.Take(pagination.Limit).ToListAsync();
        return await MapToDtosAsync(molts, viewerAgentId);
    }

    public async Task<List<MoltDto>> GetAgentMoltsAsync(string agentName, PaginationParams pagination, Guid? viewerAgentId = null)
    {
        var agent = await context.Agents.FirstOrDefaultAsync(a => a.Name.ToLower() == agentName.ToLower())
            ?? throw new KeyNotFoundException("Agent not found");

        var molts = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.RepostOf)
                .ThenInclude(r => r!.Agent)
            .Where(m => m.AgentId == agent.Id && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(pagination.Limit)
            .ToListAsync();

        return await MapToDtosAsync(molts, viewerAgentId);
    }

    public async Task<List<MoltDto>> GetConversationThreadAsync(Guid moltId, Guid? viewerAgentId = null)
    {
        var thread = new List<Molt>();
        
        // Get the target molt
        var currentMolt = await context.Molts
            .Include(m => m.Agent)
            .Include(m => m.ReplyTo)
                .ThenInclude(r => r!.Agent)
            .FirstOrDefaultAsync(m => m.Id == moltId && !m.IsDeleted);

        if (currentMolt == null)
            return new List<MoltDto>();

        // Walk up the reply chain to get parent molts
        var parentMolts = new List<Molt>();
        var current = currentMolt;
        while (current.ReplyToId != null)
        {
            var parent = await context.Molts
                .Include(m => m.Agent)
                .FirstOrDefaultAsync(m => m.Id == current.ReplyToId && !m.IsDeleted);
            
            if (parent == null) break;
            parentMolts.Insert(0, parent); // Insert at beginning to maintain order
            current = parent;
        }

        // Combine: parents + current molt
        thread.AddRange(parentMolts);
        thread.Add(currentMolt);

        return await MapToDtosAsync(thread, viewerAgentId);
    }

    private async Task ProcessMentionsAsync(Guid moltId, string content)
    {
        var mentionPattern = new Regex(@"@([a-zA-Z0-9_]{3,30})", RegexOptions.Compiled);
        var matches = mentionPattern.Matches(content);

        foreach (Match match in matches)
        {
            var username = match.Groups[1].Value;
            var agent = await context.Agents.FirstOrDefaultAsync(a => a.Name.ToLower() == username.ToLower());
            
            if (agent != null)
            {
                var exists = await context.Mentions.AnyAsync(m => m.MoltId == moltId && m.MentionedAgentId == agent.Id);
                if (!exists)
                {
                    context.Mentions.Add(new Mention { MoltId = moltId, MentionedAgentId = agent.Id });
                }
            }
        }

        await context.SaveChangesAsync();
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

            var isBookmarked = viewerAgentId.HasValue &&
                await context.Bookmarks.AnyAsync(b => b.AgentId == viewerAgentId && b.MoltId == molt.Id);

            result.Add(MapToDto(molt, isLiked, isReposted, isBookmarked));
        }

        return result;
    }

    private static MoltDto MapToDto(Molt molt, bool isLiked = false, bool isReposted = false, bool isBookmarked = false)
    {
        MoltDto? repostOfDto = null;
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

        MoltDto? replyToDto = null;
        if (molt.ReplyTo != null)
        {
            replyToDto = new MoltDto(
                Id: molt.ReplyTo.Id,
                Content: molt.ReplyTo.Content,
                Agent: new AgentSummaryDto(
                    molt.ReplyTo.Agent.Id,
                    molt.ReplyTo.Agent.Name,
                    molt.ReplyTo.Agent.DisplayName,
                    molt.ReplyTo.Agent.AvatarUrl
                ),
                LikeCount: molt.ReplyTo.LikeCount,
                ReplyCount: molt.ReplyTo.ReplyCount,
                RepostCount: molt.ReplyTo.RepostCount,
                ReplyToId: molt.ReplyTo.ReplyToId,
                RepostOfId: null,
                RepostOf: null,
                ReplyTo: null,
                CreatedAt: molt.ReplyTo.CreatedAt,
                IsEdited: molt.ReplyTo.IsEdited,
                UpdatedAt: molt.ReplyTo.UpdatedAt
            );
        }

        return new MoltDto(
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
            ReplyTo: replyToDto,
            CreatedAt: molt.CreatedAt,
            IsEdited: molt.IsEdited,
            UpdatedAt: molt.UpdatedAt,
            IsLiked: isLiked,
            IsReposted: isReposted,
            IsBookmarked: isBookmarked
        );
    }
    
    /// <summary>
    /// Sanitize content to prevent XSS and other injection attacks
    /// </summary>
    private string SanitizeContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;
        
        var trimmed = content.Trim();
        var lower = trimmed.ToLower();
        
        // Check for dangerous patterns (script tags, event handlers, etc.)
        foreach (var pattern in DangerousPatterns)
        {
            if (lower.Contains(pattern))
                throw new ArgumentException("Content contains potentially unsafe characters");
        }
        
        // Don't HTML encode here - store original content
        // HTML encoding should happen at display time in the frontend
        return trimmed;
    }
}
