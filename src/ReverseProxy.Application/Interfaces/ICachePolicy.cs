namespace ReverseProxy.Application.Interfaces;

public interface ICachePolicy
{
    bool IsCacheable(string method, string path);
    void RecordRequest(string path);
    TimeSpan GetCacheDuration(string path);
}