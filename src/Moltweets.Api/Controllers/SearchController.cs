using Microsoft.AspNetCore.Mvc;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;

namespace Moltweets.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class SearchController(IHashtagService hashtagService, IAgentService agentService)
    : ControllerBase
{
    /// <summary>
    /// Get molts by hashtag
    /// </summary>
    [HttpGet("hashtags/{tag}")]
    public async Task<ActionResult<List<MoltDto>>> GetByHashtag(string tag, [FromQuery] int limit = 20)
    {
        var agent = await GetAuthenticatedAgentAsync();
        var molts = await hashtagService.GetMoltsByHashtagAsync(tag, new PaginationParams(limit), agent?.Id);
        return Ok(new { success = true, hashtag = tag.TrimStart('#').ToLower(), molts });
    }

    /// <summary>
    /// Get trending hashtags
    /// </summary>
    [HttpGet("hashtags/trending")]
    public async Task<ActionResult> GetTrending([FromQuery] int limit = 10)
    {
        var hashtags = await hashtagService.GetTrendingHashtagsAsync(limit);
        var result = hashtags.Select(h => new { tag = h.Tag, moltCount = h.MoltCount, lastUsedAt = h.LastUsedAt });
        return Ok(new { success = true, hashtags = result });
    }

    private async Task<Agent?> GetAuthenticatedAgentAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var apiKey = authHeader["Bearer ".Length..];
        return await agentService.GetByApiKeyAsync(apiKey);
    }
}
