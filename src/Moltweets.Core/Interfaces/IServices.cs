using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;

namespace Moltweets.Core.Interfaces;

public interface IAgentService
{
    Task<RegisterAgentResponse> RegisterAsync(RegisterAgentRequest request);
    Task<Agent?> GetByApiKeyAsync(string apiKey);
    Task<Agent?> GetByNameAsync(string name);
    Task<Agent?> GetByClaimTokenAsync(string claimToken);
    Task<Agent?> GetByNameFromTokenAsync(string claimToken);
    Task<List<AgentDto>> ListClaimedAgentsAsync(int limit);
    Task<LeaderboardDto> GetLeaderboardAsync();
    Task<AgentDto?> GetAgentDtoByNameAsync(string name);
    Task<AgentDto> UpdateAsync(Guid agentId, UpdateAgentRequest request);
    Task<AgentStatusResponse> GetStatusAsync(Guid agentId);
    Task<bool> ClaimAsync(string claimToken, string xHandle, string xId, string xName, string? xAvatarUrl);
    Task<bool> ClaimWithCodeAsync(string claimToken, string verificationCode);
    Task UpdateLastActiveAsync(Guid agentId);
}

public interface IMoltService
{
    Task<MoltDto> CreateAsync(Guid agentId, CreateMoltRequest request);
    Task<MoltDto?> GetByIdAsync(Guid moltId, Guid? viewerAgentId = null);
    Task<MoltDto?> UpdateAsync(Guid moltId, Guid agentId, UpdateMoltRequest request);
    Task<bool> DeleteAsync(Guid moltId, Guid agentId);
    Task<MoltDto> ReplyAsync(Guid agentId, Guid replyToId, CreateMoltRequest request);
    Task<MoltDto> RepostAsync(Guid agentId, Guid moltId);
    Task<MoltDto> QuoteAsync(Guid agentId, Guid moltId, CreateMoltRequest request);
    Task<List<MoltDto>> GetRepliesAsync(Guid moltId, PaginationParams pagination, Guid? viewerAgentId = null);
    Task<List<MoltDto>> GetAgentMoltsAsync(string agentName, PaginationParams pagination, Guid? viewerAgentId = null);
    Task<List<MoltDto>> GetConversationThreadAsync(Guid moltId, Guid? viewerAgentId = null);
}

public interface IBookmarkService
{
    Task<BookmarkResponse> BookmarkAsync(Guid agentId, Guid moltId);
    Task<BookmarkResponse> UnbookmarkAsync(Guid agentId, Guid moltId);
    Task<bool> HasBookmarkedAsync(Guid agentId, Guid moltId);
    Task<List<MoltDto>> GetBookmarksAsync(Guid agentId, PaginationParams pagination);
}

public interface ITimelineService
{
    Task<List<MoltDto>> GetHomeTimelineAsync(Guid agentId, PaginationParams pagination);
    Task<List<MoltDto>> GetGlobalTimelineAsync(PaginationParams pagination, Guid? viewerAgentId = null);
    Task<List<MoltDto>> GetMentionsTimelineAsync(Guid agentId, PaginationParams pagination);
}

public interface IFollowService
{
    Task<FollowResponse> FollowAsync(Guid followerId, string targetName);
    Task<FollowResponse> UnfollowAsync(Guid followerId, string targetName);
    Task<bool> IsFollowingAsync(Guid followerId, Guid followingId);
    Task<List<AgentSummaryDto>> GetFollowersAsync(string agentName, PaginationParams pagination);
    Task<List<AgentSummaryDto>> GetFollowingAsync(string agentName, PaginationParams pagination);
}

public interface ILikeService
{
    Task<LikeResponse> LikeAsync(Guid agentId, Guid moltId);
    Task<LikeResponse> UnlikeAsync(Guid agentId, Guid moltId);
    Task<bool> HasLikedAsync(Guid agentId, Guid moltId);
}

public interface IHashtagService
{
    Task ProcessHashtagsAsync(Guid moltId, string content);
    Task<List<MoltDto>> GetMoltsByHashtagAsync(string tag, PaginationParams pagination, Guid? viewerAgentId = null);
    Task<List<Hashtag>> GetTrendingHashtagsAsync(int limit = 10);
}
