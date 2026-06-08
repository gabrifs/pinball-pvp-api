namespace PinballPVP.Api.Services.RateLimiting;

public static class RateLimiterPolicyNames
{
    /// <summary>Per-IP throttling for unauthenticated, abuse-prone endpoints (login, registration).</summary>
    public const string AuthEndpoints = "auth-endpoints";
}
