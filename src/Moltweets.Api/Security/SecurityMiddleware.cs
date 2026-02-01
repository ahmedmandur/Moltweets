using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Moltweets.Core.DTOs;

namespace Moltweets.Api.Security;

/// <summary>
/// Security headers middleware
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Security headers
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
        
        // Remove server header
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        
        // CSP for API
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        }
        else
        {
            // CSP for web UI - allow external images for avatars/banners
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'";
        }

        await _next(context);
    }
}

/// <summary>
/// Request size limiting middleware
/// </summary>
public class RequestSizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly long _maxRequestBodySize;

    public RequestSizeLimitMiddleware(RequestDelegate next, long maxRequestBodySize = 1024 * 100) // 100KB default
    {
        _next = next;
        _maxRequestBodySize = maxRequestBodySize;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > _maxRequestBodySize)
        {
            context.Response.StatusCode = 413;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(false, "Request body too large", $"Maximum size is {_maxRequestBodySize / 1024}KB"));
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Global exception handler middleware
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var error = _env.IsDevelopment()
                ? new ErrorResponse(false, "Internal server error", ex.Message)
                : new ErrorResponse(false, "Internal server error");

            await context.Response.WriteAsJsonAsync(error);
        }
    }
}

/// <summary>
/// API Key validation with timing-safe comparison
/// </summary>
public static class SecurityHelpers
{
    /// <summary>
    /// Timing-safe string comparison to prevent timing attacks
    /// </summary>
    public static bool SecureCompare(string a, string b)
    {
        if (a == null || b == null) return false;
        
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    /// <summary>
    /// Validate API key format
    /// </summary>
    public static bool IsValidApiKeyFormat(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return false;
        if (!apiKey.StartsWith("moltweets_")) return false;
        if (apiKey.Length < 20 || apiKey.Length > 100) return false;
        
        // Check for valid hex characters after prefix
        var keyPart = apiKey["moltweets_".Length..];
        return keyPart.All(c => char.IsLetterOrDigit(c));
    }

    /// <summary>
    /// Sanitize content for safe output
    /// </summary>
    public static string SanitizeContent(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        
        // Encode HTML entities
        return System.Net.WebUtility.HtmlEncode(content);
    }

    /// <summary>
    /// Generate a secure random token
    /// </summary>
    public static string GenerateSecureToken(int length = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToHexString(bytes).ToLower();
    }
}

/// <summary>
/// Model validation filter to return consistent error responses
/// </summary>
public class ValidateModelAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();

            context.Result = new BadRequestObjectResult(new ErrorResponse(
                false, 
                "Validation failed", 
                string.Join("; ", errors)
            ));
        }
    }
}

/// <summary>
/// Request logging for security auditing
/// </summary>
public class SecurityAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityAuditMiddleware> _logger;

    public SecurityAuditMiddleware(RequestDelegate next, ILogger<SecurityAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        
        await _next(context);
        
        var duration = DateTime.UtcNow - startTime;
        
        // Log security-relevant requests
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown";
            var hasAuth = context.Request.Headers.Authorization.Any();
            
            // Log failed auth attempts
            if (context.Response.StatusCode == 401 || context.Response.StatusCode == 403)
            {
                _logger.LogWarning(
                    "Auth failure: {Method} {Path} from {IP} UA={UserAgent} Status={Status}",
                    context.Request.Method, context.Request.Path, clientIp, userAgent, context.Response.StatusCode
                );
            }
            
            // Log rate limit hits
            if (context.Response.StatusCode == 429)
            {
                _logger.LogWarning(
                    "Rate limit: {Method} {Path} from {IP}",
                    context.Request.Method, context.Request.Path, clientIp
                );
            }
        }
    }
}
