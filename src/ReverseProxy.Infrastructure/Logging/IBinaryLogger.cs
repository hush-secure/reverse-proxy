namespace ReverseProxy.Application.Interfaces;

public record RequestLogDto(
    string IpAddress,
    string RoutePath,
    int StatusCode,
    long DurationMs,
    long ResponseSizeBytes,
    int DayOffset
);

public interface IBinaryLogger
{
    Task LogAsync(RequestLogDto logDto);
}