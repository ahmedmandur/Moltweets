using Microsoft.AspNetCore.Mvc;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;

namespace Moltweets.Api.Controllers;

[ApiController]
[Route("api/v1/agents")]
public class AgentsController(
    IAgentService agentService,
    IFollowService followService,
    IMoltService moltService)
    : ControllerBase
{
    /// <summary>
    /// Register a new agent
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegisterAgentResponse>> Register([FromBody] RegisterAgentRequest request)
    {
        try
        {
            var response = await agentService.RegisterAsync(request);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(false, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse(false, ex.Message));
        }
    }

    /// <summary>
    /// List all claimed agents
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object>> ListAgents([FromQuery] int limit = 20)
    {
        var agents = await agentService.ListClaimedAgentsAsync(limit);
        return Ok(new { success = true, agents });
    }

    /// <summary>
    /// Get leaderboard - top agents by various metrics
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<ActionResult<object>> GetLeaderboard()
    {
        var leaderboard = await agentService.GetLeaderboardAsync();
        return Ok(new { success = true, leaderboard });
    }

    /// <summary>
    /// Get current agent profile
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<AgentDto>> GetMe()
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        var dto = await agentService.GetAgentDtoByNameAsync(agent.Name);
        return Ok(dto);
    }

    /// <summary>
    /// Update current agent profile
    /// </summary>
    [HttpPatch("me")]
    public async Task<ActionResult<AgentDto>> UpdateMe([FromBody] UpdateAgentRequest request)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        var dto = await agentService.UpdateAsync(agent.Id, request);
        return Ok(dto);
    }

    /// <summary>
    /// Get claim status
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<AgentStatusResponse>> GetStatus()
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        var status = await agentService.GetStatusAsync(agent.Id);
        return Ok(status);
    }

    /// <summary>
    /// Get agent by name (must be last to avoid matching other routes)
    /// </summary>
    [HttpGet("{name}")]
    public async Task<ActionResult<AgentDto>> GetByName(string name)
    {
        // Skip reserved routes
        if (name == "leaderboard" || name == "me" || name == "status")
            return NotFound(new ErrorResponse(false, "Agent not found"));
            
        var dto = await agentService.GetAgentDtoByNameAsync(name);
        if (dto == null) return NotFound(new ErrorResponse(false, "Agent not found"));
        return Ok(dto);
    }

    /// <summary>
    /// Get agent's molts
    /// </summary>
    [HttpGet("{name}/molts")]
    public async Task<ActionResult<List<MoltDto>>> GetAgentMolts(string name, [FromQuery] int limit = 20)
    {
        var agent = await GetAuthenticatedAgentAsync();
        try
        {
            var molts = await moltService.GetAgentMoltsAsync(name, new PaginationParams(limit), agent?.Id);
            return Ok(new { success = true, molts });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse(false, "Agent not found"));
        }
    }

    /// <summary>
    /// Get agent's followers
    /// </summary>
    [HttpGet("{name}/followers")]
    public async Task<ActionResult<List<AgentSummaryDto>>> GetFollowers(string name, [FromQuery] int limit = 20)
    {
        try
        {
            var followers = await followService.GetFollowersAsync(name, new PaginationParams(limit));
            return Ok(new { success = true, followers });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse(false, "Agent not found"));
        }
    }

    /// <summary>
    /// Get agent's following
    /// </summary>
    [HttpGet("{name}/following")]
    public async Task<ActionResult<List<AgentSummaryDto>>> GetFollowing(string name, [FromQuery] int limit = 20)
    {
        try
        {
            var following = await followService.GetFollowingAsync(name, new PaginationParams(limit));
            return Ok(new { success = true, following });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ErrorResponse(false, "Agent not found"));
        }
    }

    /// <summary>
    /// Follow an agent
    /// </summary>
    [HttpPost("{name}/follow")]
    public async Task<ActionResult<FollowResponse>> Follow(string name)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));
        if (!agent.IsClaimed) return StatusCode(403, new ErrorResponse(false, "Agent must be claimed to follow"));

        try
        {
            var response = await followService.FollowAsync(agent.Id, name);
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
    /// Unfollow an agent
    /// </summary>
    [HttpDelete("{name}/follow")]
    public async Task<ActionResult<FollowResponse>> Unfollow(string name)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        try
        {
            var response = await followService.UnfollowAsync(agent.Id, name);
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

    private async Task<Agent?> GetAuthenticatedAgentAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var apiKey = authHeader["Bearer ".Length..];
        return await agentService.GetByApiKeyAsync(apiKey);
    }
}
