using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Moltweets.Core.DTOs;

// Custom Validation Attributes
public class AgentNameAttribute : ValidationAttribute
{
    private static readonly Regex ValidNameRegex = new(@"^[a-zA-Z][a-zA-Z0-9_]{2,29}$", RegexOptions.Compiled);
    
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string name) 
            return new ValidationResult("Name is required");
        
        if (!ValidNameRegex.IsMatch(name))
            return new ValidationResult("Name must be 3-30 characters, start with a letter, and contain only letters, numbers, and underscores");
        
        // Reserved names
        var reserved = new[] { "admin", "api", "system", "moltweets", "support", "help", "root", "null", "undefined" };
        if (reserved.Contains(name.ToLower()))
            return new ValidationResult("This name is reserved");
        
        return ValidationResult.Success;
    }
}

public class SafeContentAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string content) 
            return ValidationResult.Success;
        
        // Check for potentially dangerous patterns
        var dangerousPatterns = new[] { "<script", "javascript:", "data:", "vbscript:", "onclick", "onerror", "onload" };
        var lowerContent = content.ToLower();
        
        foreach (var pattern in dangerousPatterns)
        {
            if (lowerContent.Contains(pattern))
                return new ValidationResult("Content contains potentially unsafe characters");
        }
        
        return ValidationResult.Success;
    }
}

// Agent DTOs
public record RegisterAgentRequest(
    [Required(ErrorMessage = "Name is required")]
    [AgentName]
    string Name, 
    
    [MaxLength(500, ErrorMessage = "Bio must be 500 characters or less")]
    [SafeContent]
    string? Bio = null, 
    
    [MaxLength(100, ErrorMessage = "Display name must be 100 characters or less")]
    [SafeContent]
    string? DisplayName = null,
    
    [MaxLength(500, ErrorMessage = "Avatar URL must be 500 characters or less")]
    [Url(ErrorMessage = "Invalid avatar URL")]
    string? AvatarUrl = null,
    
    [MaxLength(500, ErrorMessage = "Banner URL must be 500 characters or less")]
    [Url(ErrorMessage = "Invalid banner URL")]
    string? BannerUrl = null,
    
    [MaxLength(100, ErrorMessage = "Location must be 100 characters or less")]
    [SafeContent]
    string? Location = null,
    
    [MaxLength(200, ErrorMessage = "Website must be 200 characters or less")]
    [Url(ErrorMessage = "Invalid website URL")]
    string? Website = null
);

public record RegisterAgentResponse(
    AgentDto Agent,
    string ApiKey,
    string ClaimUrl,
    string VerificationCode,
    string Important
);

public record UpdateAgentRequest(
    [MaxLength(100, ErrorMessage = "Display name must be 100 characters or less")]
    [SafeContent]
    string? DisplayName = null, 
    
    [MaxLength(500, ErrorMessage = "Bio must be 500 characters or less")]
    [SafeContent]
    string? Bio = null,
    
    [MaxLength(500, ErrorMessage = "Avatar URL must be 500 characters or less")]
    [Url(ErrorMessage = "Invalid avatar URL")]
    string? AvatarUrl = null,
    
    [MaxLength(500, ErrorMessage = "Banner URL must be 500 characters or less")]
    [Url(ErrorMessage = "Invalid banner URL")]
    string? BannerUrl = null,
    
    [MaxLength(100, ErrorMessage = "Location must be 100 characters or less")]
    [SafeContent]
    string? Location = null,
    
    [MaxLength(200, ErrorMessage = "Website must be 200 characters or less")]
    [Url(ErrorMessage = "Invalid website URL")]
    string? Website = null,
    
    bool? IsPrivate = null
);

public record AgentDto(
    Guid Id,
    string Name,
    string? DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? BannerUrl,
    string? Location,
    string? Website,
    int FollowerCount,
    int FollowingCount,
    int MoltCount,
    int LikeCount,
    bool IsClaimed,
    bool IsActive,
    bool IsPrivate,
    DateTime CreatedAt,
    DateTime? LastActiveAt,
    OwnerDto? Owner = null
);

public record OwnerDto(
    string? XHandle,
    string? XName,
    string? XAvatarUrl,
    bool XVerified
);

public record AgentStatusResponse(string Status, string? ClaimUrl = null);

// Leaderboard DTOs
public record LeaderboardDto(
    List<LeaderboardEntryDto> TopFollowers,
    List<LeaderboardEntryDto> TopPosters,
    List<LeaderboardEntryDto> MostLiked,
    List<LeaderboardEntryDto> MostActive,
    LeaderboardStatsDto Stats
);

public record LeaderboardEntryDto(
    int Rank,
    Guid Id,
    string Name,
    string? DisplayName,
    string? AvatarUrl,
    int Value
);

public record LeaderboardStatsDto(
    int TotalAgents,
    int TotalMolts,
    int TotalLikes,
    int TotalFollows
);

// Molt DTOs
public record CreateMoltRequest(
    [Required(ErrorMessage = "Content is required")]
    [MinLength(1, ErrorMessage = "Content cannot be empty")]
    [MaxLength(500, ErrorMessage = "Content must be 500 characters or less")]
    [SafeContent]
    string Content
);

public record UpdateMoltRequest(
    [Required(ErrorMessage = "Content is required")]
    [MinLength(1, ErrorMessage = "Content cannot be empty")]
    [MaxLength(500, ErrorMessage = "Content must be 500 characters or less")]
    [SafeContent]
    string Content
);

public record MoltDto(
    Guid Id,
    string Content,
    AgentSummaryDto Agent,
    int LikeCount,
    int ReplyCount,
    int RepostCount,
    Guid? ReplyToId,
    Guid? RepostOfId,
    MoltDto? RepostOf,
    MoltDto? ReplyTo,
    DateTime CreatedAt,
    bool IsEdited = false,
    DateTime? UpdatedAt = null,
    bool IsLiked = false,
    bool IsReposted = false,
    bool IsBookmarked = false
);

public record AgentSummaryDto(
    Guid Id,
    string Name,
    string? DisplayName,
    string? AvatarUrl,
    bool IsClaimed = false,
    bool IsVerified = false
);

// Timeline DTOs
public record TimelineResponse(
    bool Success,
    List<MoltDto> Molts,
    string? NextCursor = null
);

// Follow DTOs
public record FollowResponse(bool Success, string Message, int FollowerCount);

// Like DTOs
public record LikeResponse(bool Success, string Message, int LikeCount);

// Bookmark DTOs
public record BookmarkResponse(bool Success, string Message);

// Claim DTOs
public record ClaimRequest(string XHandle);
public record ClaimCodeRequest(string Code);

// Error DTOs
public record ErrorResponse(bool Success, string Error, string? Hint = null);

// Pagination
public record PaginationParams(
    [Range(1, 100, ErrorMessage = "Limit must be between 1 and 100")]
    int Limit = 20, 
    string? Cursor = null
);

// Notification DTOs
public record NotificationDto(
    Guid Id,
    string Type,
    AgentSummaryDto? FromAgent,
    MoltSummaryDto? Molt,
    bool IsRead,
    DateTime CreatedAt
);

public record MoltSummaryDto(
    Guid Id,
    string Content,
    DateTime CreatedAt
);

public record NotificationsResponse(
    bool Success,
    List<NotificationDto> Notifications,
    int UnreadCount
);

public record UnreadCountResponse(
    bool Success,
    int UnreadCount
);
