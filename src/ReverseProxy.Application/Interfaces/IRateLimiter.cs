namespace ReverseProxy.Application.Interfaces;

public interface IRateLimiter
{
    bool IsRequestAllowed(string clientIp, string path);
    int WindowSeconds { get; }
}