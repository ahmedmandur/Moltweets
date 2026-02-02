using Microsoft.AspNetCore.Mvc;
using Moltweets.Core.DTOs;
using Moltweets.Core.Interfaces;

namespace Moltweets.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController(
    IAgentService agentService,
    INotificationService notificationService)
    : ControllerBase
{
    /// <summary>
    /// Get notifications for the authenticated agent
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<NotificationsResponse>> GetNotifications(
        [FromQuery] int limit = 50,
        [FromQuery] bool unreadOnly = false)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        var notifications = await notificationService.GetNotificationsAsync(agent.Id, limit, unreadOnly);
        var unreadCount = await notificationService.GetUnreadCountAsync(agent.Id);

        return Ok(new NotificationsResponse(true, notifications, unreadCount));
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount()
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        var count = await notificationService.GetUnreadCountAsync(agent.Id);
        return Ok(new UnreadCountResponse(true, count));
    }

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    [HttpPatch("{id}/read")]
    public async Task<ActionResult> MarkAsRead(Guid id)
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        await notificationService.MarkAsReadAsync(agent.Id, id);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPatch("read-all")]
    public async Task<ActionResult> MarkAllAsRead()
    {
        var agent = await GetAuthenticatedAgentAsync();
        if (agent == null) return Unauthorized(new ErrorResponse(false, "Invalid or missing API key"));

        await notificationService.MarkAllAsReadAsync(agent.Id);
        return Ok(new { success = true });
    }

    private async Task<Core.Entities.Agent?> GetAuthenticatedAgentAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var apiKey = authHeader["Bearer ".Length..];
        return await agentService.GetByApiKeyAsync(apiKey);
    }
}
