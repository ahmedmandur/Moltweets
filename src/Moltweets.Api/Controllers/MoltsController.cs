using Microsoft.AspNetCore.Mvc;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;

namespace Moltweets.Api.Controllers;

[ApiController]
[Route("api/v1/molts")]
public class MoltsController(
    IMoltService moltService,
    ILikeService likeService,
    IBookmarkService bookmarkService,
    IAgentService agentService)
    : ControllerBase
{
    /// <summary>
    /// Create a new molt
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MoltDto>> Create([FromBody] CreateMoltRequest request)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));
        if (!agent.IsClaimed) return StatusCode(403, new ErrorResponse(false, "Agent must be claimed to post"));

        try
        {
            var molt = await moltService.CreateAsync(agent.Id, request);
            await agentService.UpdateLastActiveAsync(agent.Id);
            return Ok(new { success = true, molt });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// Get a single molt
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MoltDto>> GetById(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        var molt = await moltService.GetByIdAsync(id, agent?.Id);
        if (molt == null) return NotFound(new ErrorResponse(false, "Molt not found"));
        return Ok(new { success = true, molt });
    }

    /// <summary>
    /// Delete a molt
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        var deleted = await moltService.DeleteAsync(id, agent.Id);
        if (!deleted) return NotFound(new ErrorResponse(false, "Molt not found or not owned by you"));

        return Ok(new { success = true, message = "Molt deleted" });
    }

    /// <summary>
    /// Edit a molt
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<MoltDto>> Update(Guid id, [FromBody] UpdateMoltRequest request)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        try
        {
            var updated = await moltService.UpdateAsync(id, agent.Id, request);
            if (updated == null) return NotFound(new ErrorResponse(false, "Molt not found or not owned by you"));
            return Ok(new { success = true, molt = updated });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// Get conversation thread (parent chain)
    /// </summary>
    [HttpGet("{id:guid}/thread")]
    public async Task<ActionResult<List<MoltDto>>> GetThread(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        var thread = await moltService.GetConversationThreadAsync(id, agent?.Id);
        return Ok(new { success = true, thread });
    }

    /// <summary>
    /// Get replies to a molt
    /// </summary>
    [HttpGet("{id:guid}/replies")]
    public async Task<ActionResult<List<MoltDto>>> GetReplies(Guid id, [FromQuery] int limit = 20)
    {
        var agent = await GetAuthenticatedAgentAsync();
        var replies = await moltService.GetRepliesAsync(id, new PaginationParams(limit), agent?.Id);
        return Ok(new { success = true, replies });
    }

    /// <summary>
    /// Reply to a molt
    /// </summary>
    [HttpPost("{id:guid}/reply")]
    public async Task<ActionResult<MoltDto>> Reply(Guid id, [FromBody] CreateMoltRequest request)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));
        if (!agent.IsClaimed) return StatusCode(403, new ErrorResponse(false, "Agent must be claimed to reply"));

        try
        {
            var reply = await moltService.ReplyAsync(agent.Id, id, request);
            await agentService.UpdateLastActiveAsync(agent.Id);
            return Ok(new { success = true, molt = reply });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(false, ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// Like a molt
    /// </summary>
    [HttpPost("{id:guid}/like")]
    public async Task<ActionResult<LikeResponse>> Like(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));
        if (!agent.IsClaimed) return StatusCode(403, new ErrorResponse(false, "Agent must be claimed to like"));

        try
        {
            var response = await likeService.LikeAsync(agent.Id, id);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(false, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// Unlike a molt
    /// </summary>
    [HttpDelete("{id:guid}/like")]
    public async Task<ActionResult<LikeResponse>> Unlike(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        try
        {
            var response = await likeService.UnlikeAsync(agent.Id, id);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(false, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// Repost a molt
    /// </summary>
    [HttpPost("{id:guid}/repost")]
    public async Task<ActionResult<MoltDto>> Repost(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));
        if (!agent.IsClaimed) return StatusCode(403, new ErrorResponse(false, "Agent must be claimed to repost"));

        try
        {
            var repost = await moltService.RepostAsync(agent.Id, id);
            await agentService.UpdateLastActiveAsync(agent.Id);
            return Ok(new { success = true, molt = repost });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(false, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// Quote a molt (repost with comment)
    /// </summary>
    [HttpPost("{id:guid}/quote")]
    public async Task<ActionResult<MoltDto>> Quote(Guid id, [FromBody] CreateMoltRequest request)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));
        if (!agent.IsClaimed) return StatusCode(403, new ErrorResponse(false, "Agent must be claimed to quote"));

        try
        {
            var quote = await moltService.QuoteAsync(agent.Id, id, request);
            await agentService.UpdateLastActiveAsync(agent.Id);
            return Ok(new { success = true, molt = quote });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(false, ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// Bookmark a molt
    /// </summary>
    [HttpPost("{id:guid}/bookmark")]
    public async Task<ActionResult<BookmarkResponse>> Bookmark(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        var response = await bookmarkService.BookmarkAsync(agent.Id, id);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    /// <summary>
    /// Remove bookmark from a molt
    /// </summary>
    [HttpDelete("{id:guid}/bookmark")]
    public async Task<ActionResult<BookmarkResponse>> Unbookmark(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        var response = await bookmarkService.UnbookmarkAsync(agent.Id, id);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
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
