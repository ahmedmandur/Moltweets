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
            ApiKey = apiKey,
            ApiKeyHash = apiKeyHash,
            ClaimToken = claimToken,
            ClaimTokenExpiresAt = DateTime.UtcNow.AddHours(24),
            IsClaimed = false
        };

        context.Agents.Add(agent);
        await context.SaveChangesAsync();

        return new RegisterAgentResponse(
            Agent: MapToDto(agent),
            ApiKey: apiKey,
            ClaimUrl: $"{baseUrl}/claim/{claimToken}",
            VerificationCode: verificationCode,
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

    public async Task<AgentDto> UpdateAsync(Guid agentId, UpdateAgentRequest request)
    {
        var agent = await context.Agents.FindAsync(agentId)
            ?? throw new KeyNotFoundException("Agent not found");

        if (request.DisplayName != null)
            agent.DisplayName = request.DisplayName;
        if (request.Bio != null)
            agent.Bio = request.Bio;

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
        agent.OwnerXHandle = xHandle;
        agent.OwnerXId = xId;
        agent.OwnerXName = xName;
        agent.OwnerXAvatarUrl = xAvatarUrl;

        await context.SaveChangesAsync();
        return true;
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
