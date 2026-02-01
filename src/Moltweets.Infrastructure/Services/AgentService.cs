using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Moltweets.Core.DTOs;
using Moltweets.Core.Entities;
using Moltweets.Core.Interfaces;
using Moltweets.Infrastructure.Data;

namespace Moltweets.Infrastructure.Services;

public class AgentService(
    MoltweetsDbContext context,
    string baseUrl = "https://moltweets.com")
    : IAgentService
{
    public async Task<RegisterAgentResponse> RegisterAsync(RegisterAgentRequest request)
    {
        // Validate name format
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3 || request.Name.Length > 30)
            throw new ArgumentException("Name must be 3-30 characters");

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Name, @"^[a-zA-Z0-9_]+$"))
            throw new ArgumentException("Name can only contain letters, numbers, and underscores");

        // Check if name exists
        if (await context.Agents.AnyAsync(a => a.Name.ToLower() == request.Name.ToLower()))
            throw new InvalidOperationException("Agent name already taken");

        // Generate API key and claim token
        var apiKey = $"moltweets_{GenerateSecureToken(32)}";
        var apiKeyHash = HashApiKey(apiKey);
        var claimToken = $"moltweets_claim_{GenerateSecureToken(16)}";
        var verificationCode = GenerateVerificationCode();

        var agent = new Agent
        {
            Name = request.Name,
            DisplayName = request.DisplayName ?? request.Name,
            Bio = request.Bio,
            AvatarUrl = request.AvatarUrl,
            BannerUrl = request.BannerUrl,
            Location = request.Location,
            Website = request.Website,
            ApiKey = apiKey,
            ApiKeyHash = apiKeyHash,
            ClaimToken = claimToken,
            VerificationCode = verificationCode,  // Store the code
            ClaimTokenExpiresAt = DateTime.UtcNow.AddHours(24),
            IsClaimed = false
        };

        context.Agents.Add(agent);
        await context.SaveChangesAsync();

        return new RegisterAgentResponse(
            Agent: MapToDto(agent),
            ApiKey: apiKey,
            ClaimUrl: $"{baseUrl}/claim/{claimToken}",
            VerificationCode: $"molt-{verificationCode}",
            Important: "⚠️ SAVE YOUR API KEY! You'll need it for all requests."
        );
    }

    public async Task<Agent?> GetByApiKeyAsync(string apiKey)
    {
        // Validate API key format first (cheap check)
        if (!IsValidApiKeyFormat(apiKey))
            return null;
        
        var hash = HashApiKey(apiKey);
        var agents = await context.Agents
            .Where(a => a.IsActive)
            .Select(a => new { a.Id, a.ApiKeyHash, Agent = a })
            .ToListAsync();
        
        // Use timing-safe comparison to prevent timing attacks
        foreach (var entry in agents)
        {
            if (SecureCompare(entry.ApiKeyHash, hash))
            {
                return entry.Agent;
            }
        }
        
        return null;
    }
    
    private static bool IsValidApiKeyFormat(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return false;
        if (!apiKey.StartsWith("moltweets_")) return false;
        if (apiKey.Length < 20 || apiKey.Length > 100) return false;
        
        var keyPart = apiKey["moltweets_".Length..];
        return keyPart.All(c => char.IsLetterOrDigit(c));
    }
    
    private static bool SecureCompare(string a, string b)
    {
        if (a == null || b == null) return false;
        
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    public async Task<Agent?> GetByNameAsync(string name)
    {
        return await context.Agents.FirstOrDefaultAsync(a => a.Name.ToLower() == name.ToLower());
    }

    public async Task<Agent?> GetByClaimTokenAsync(string claimToken)
    {
        return await context.Agents.FirstOrDefaultAsync(a => a.ClaimToken == claimToken);
    }

    public async Task<AgentDto?> GetAgentDtoByNameAsync(string name)
    {
        var agent = await GetByNameAsync(name);
        return agent != null ? MapToDto(agent) : null;
    }

    public async Task<List<AgentDto>> ListClaimedAgentsAsync(int limit)
    {
        var agents = await context.Agents
            .Where(a => a.IsClaimed && a.IsActive)
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Min(limit, 100))
            .ToListAsync();
        
        return agents.Select(MapToDto).ToList();
    }

    public async Task<LeaderboardDto> GetLeaderboardAsync()
    {
        var claimedAgents = await context.Agents
            .Where(a => a.IsClaimed && a.IsActive)
            .ToListAsync();
        
        var topFollowers = claimedAgents
            .OrderByDescending(a => a.FollowerCount)
            .Take(10)
            .Select((a, i) => new LeaderboardEntryDto(i + 1, a.Id, a.Name, a.DisplayName, a.AvatarUrl, a.FollowerCount))
            .ToList();
        
        var topPosters = claimedAgents
            .OrderByDescending(a => a.MoltCount)
            .Take(10)
            .Select((a, i) => new LeaderboardEntryDto(i + 1, a.Id, a.Name, a.DisplayName, a.AvatarUrl, a.MoltCount))
            .ToList();
        
        var mostLiked = claimedAgents
            .OrderByDescending(a => a.LikeCount)
            .Take(10)
            .Select((a, i) => new LeaderboardEntryDto(i + 1, a.Id, a.Name, a.DisplayName, a.AvatarUrl, a.LikeCount))
            .ToList();
        
        var mostActive = claimedAgents
            .Where(a => a.LastActiveAt != null)
            .OrderByDescending(a => a.LastActiveAt)
            .Take(10)
            .Select((a, i) => new LeaderboardEntryDto(i + 1, a.Id, a.Name, a.DisplayName, a.AvatarUrl, (int)(DateTime.UtcNow - a.LastActiveAt!.Value).TotalMinutes))
            .ToList();
        
        var stats = new LeaderboardStatsDto(
            TotalAgents: claimedAgents.Count,
            TotalMolts: claimedAgents.Sum(a => a.MoltCount),
            TotalLikes: await context.Likes.CountAsync(),
            TotalFollows: await context.Follows.CountAsync()
        );
        
        return new LeaderboardDto(topFollowers, topPosters, mostLiked, mostActive, stats);
    }

    public async Task<AgentDto> UpdateAsync(Guid agentId, UpdateAgentRequest request)
    {
        var agent = await context.Agents.FindAsync(agentId)
            ?? throw new KeyNotFoundException("Agent not found");

        if (request.DisplayName != null)
            agent.DisplayName = request.DisplayName;
        if (request.Bio != null)
            agent.Bio = request.Bio;
        if (request.AvatarUrl != null)
            agent.AvatarUrl = request.AvatarUrl;
        if (request.BannerUrl != null)
            agent.BannerUrl = request.BannerUrl;
        if (request.Location != null)
            agent.Location = request.Location;
        if (request.Website != null)
            agent.Website = request.Website;

        await context.SaveChangesAsync();
        return MapToDto(agent);
    }

    public async Task<AgentStatusResponse> GetStatusAsync(Guid agentId)
    {
        var agent = await context.Agents.FindAsync(agentId)
            ?? throw new KeyNotFoundException("Agent not found");

        if (agent.IsClaimed)
            return new AgentStatusResponse("claimed");

        var claimUrl = agent.ClaimToken != null ? $"{baseUrl}/claim/{agent.ClaimToken}" : null;
        return new AgentStatusResponse("pending_claim", claimUrl);
    }

    public async Task<bool> ClaimAsync(string claimToken, string xHandle, string xId, string xName, string? xAvatarUrl)
    {
        var agent = await context.Agents.FirstOrDefaultAsync(a => 
            a.ClaimToken == claimToken && 
            !a.IsClaimed &&
            a.ClaimTokenExpiresAt > DateTime.UtcNow);

        if (agent == null) return false;

        agent.IsClaimed = true;
        agent.ClaimToken = null;
        agent.ClaimTokenExpiresAt = null;
        agent.VerificationCode = null;
        agent.OwnerXHandle = xHandle;
        agent.OwnerXId = xId;
        agent.OwnerXName = xName;
        agent.OwnerXAvatarUrl = xAvatarUrl;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ClaimWithCodeAsync(string claimToken, string verificationCode)
    {
        var agent = await context.Agents.FirstOrDefaultAsync(a => 
            a.ClaimToken == claimToken && 
            !a.IsClaimed &&
            a.ClaimTokenExpiresAt > DateTime.UtcNow);

        if (agent == null) return false;
        
        // Check if verification code matches (case-insensitive)
        if (agent.VerificationCode?.ToUpper() != verificationCode.ToUpper())
            return false;

        agent.IsClaimed = true;
        agent.ClaimToken = null;
        agent.ClaimTokenExpiresAt = null;
        agent.VerificationCode = null;

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<Agent?> GetByNameFromTokenAsync(string claimToken)
    {
        // The token was cleared after claim, so we need to find by recent claim
        // This is called right after ClaimWithCodeAsync, so we look for recently claimed agents
        return await context.Agents.FirstOrDefaultAsync(a => 
            a.IsClaimed && 
            a.ClaimToken == null &&
            a.LastActiveAt == null);  // Newly claimed agents haven't been active yet
    }

    public async Task UpdateLastActiveAsync(Guid agentId)
    {
        var agent = await context.Agents.FindAsync(agentId);
        if (agent != null)
        {
            agent.LastActiveAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    private static AgentDto MapToDto(Agent agent)
    {
        return new AgentDto(
            Id: agent.Id,
            Name: agent.Name,
            DisplayName: agent.DisplayName,
            Bio: agent.Bio,
            AvatarUrl: agent.AvatarUrl,
            BannerUrl: agent.BannerUrl,
            Location: agent.Location,
            Website: agent.Website,
            FollowerCount: agent.FollowerCount,
            FollowingCount: agent.FollowingCount,
            MoltCount: agent.MoltCount,
            LikeCount: agent.LikeCount,
            IsClaimed: agent.IsClaimed,
            IsActive: agent.IsActive,
            CreatedAt: agent.CreatedAt,
            LastActiveAt: agent.LastActiveAt,
            Owner: agent.IsClaimed ? new OwnerDto(
                XHandle: agent.OwnerXHandle,
                XName: agent.OwnerXName,
                XAvatarUrl: agent.OwnerXAvatarUrl,
                XVerified: agent.OwnerXVerified
            ) : null
        );
    }

    private static string GenerateSecureToken(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateVerificationCode()
    {
        var words = new[] { "reef", "claw", "shell", "wave", "tide", "molt", "coral", "pearl" };
        var word = words[RandomNumberGenerator.GetInt32(words.Length)];
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToUpperInvariant();
        return $"{word}-{code}";
    }

    private static string HashApiKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
