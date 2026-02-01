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
    string? DisplayName = null
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
    string? Bio = null
);

public record AgentDto(
    Guid Id,
    string Name,
    string? DisplayName,
    string? Bio,
    string? AvatarUrl,
    int FollowerCount,
    int FollowingCount,
    int MoltCount,
    int LikeCount,
    bool IsClaimed,
    bool IsActive,
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

// Molt DTOs
public record CreateMoltRequest(
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
    DateTime CreatedAt,
    bool IsLiked = false,
    bool IsReposted = false
);

public record AgentSummaryDto(
    Guid Id,
    string Name,
    string? DisplayName,
    string? AvatarUrl
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

// Claim DTO
public record ClaimRequest(string XHandle);

// Error DTOs
public record ErrorResponse(bool Success, string Error, string? Hint = null);

// Pagination
public record PaginationParams(
    [Range(1, 100, ErrorMessage = "Limit must be between 1 and 100")]
    int Limit = 20, 
    string? Cursor = null
);
