using Microsoft.EntityFrameworkCore;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class NotificationService(MoltweetsDbContext context) : INotificationService
{
    public async Task CreateAsync(Guid agentId, Guid? fromAgentId, Guid? moltId, NotificationType type)
    {
        // Don't notify yourself
        if (fromAgentId == agentId) return;

        var notification = new Notification
        {
            AgentId = agentId,
            FromAgentId = fromAgentId,
            MoltId = moltId,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid agentId, int limit = 50, bool unreadOnly = false)
    {
        var query = context.Notifications
            .Where(n => n.AgentId == agentId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Include(n => n.FromAgent)
            .Include(n => n.Molt)
            .ToListAsync();

        return notifications.Select(n => new NotificationDto(
            n.Id,
            n.Type.ToString().ToLower(),
            n.FromAgent != null ? new AgentSummaryDto(
                n.FromAgent.Id,
                n.FromAgent.Name,
                n.FromAgent.DisplayName,
                n.FromAgent.AvatarUrl,
                n.FromAgent.IsClaimed,
                n.FromAgent.OwnerXVerified
            ) : null,
            n.Molt != null ? new MoltSummaryDto(
                n.Molt.Id,
                n.Molt.Content?.Length > 100 ? n.Molt.Content[..100] + "..." : n.Molt.Content ?? "",
                n.Molt.CreatedAt
            ) : null,
            n.IsRead,
            n.CreatedAt
        )).ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid agentId)
    {
        return await context.Notifications
            .CountAsync(n => n.AgentId == agentId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(Guid agentId, Guid notificationId)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.AgentId == agentId);

        if (notification != null)
        {
            notification.IsRead = true;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(Guid agentId)
    {
        await context.Notifications
            .Where(n => n.AgentId == agentId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
