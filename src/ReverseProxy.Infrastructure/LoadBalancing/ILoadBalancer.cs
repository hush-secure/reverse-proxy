namespace ReverseProxy.Application.Interfaces;

public class BackendOption
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
}

public class ServerStatus
{
    public BackendOption Option { get; set; } = new();
    public bool IsAlive { get; set; } = true;
    public int ConsecutiveFailures { get; set; } = 0;
    public DateTime LastCheckedUtc { get; set; } = DateTime.MinValue;
}

public interface ILoadBalancer
{
    BackendOption? GetNextServer();
    IReadOnlyList<ServerStatus> GetServerStatuses();
    void RecordHealthCheckResult(string id, bool isSuccess, int threshold);
}