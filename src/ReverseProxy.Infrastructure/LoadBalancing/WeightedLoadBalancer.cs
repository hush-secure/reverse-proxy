using Microsoft.Extensions.Configuration;
using ReverseProxy.Application.Interfaces;

namespace ReverseProxy.Infrastructure.LoadBalancing;

public class WeightedLoadBalancer : ILoadBalancer
{
    private readonly List<ServerStatus> _servers = new();
    private int _currentIndex = -1;
    private readonly object _lock = new();

    public WeightedLoadBalancer(IConfiguration config)
    {
        var rawServers = config.GetSection("backends").Get<List<BackendOption>>() ?? new();
        foreach (var server in rawServers)
        {
            _servers.Add(new ServerStatus
            {
                Option = server,
                IsAlive = true,
                ConsecutiveFailures = 0,
                LastCheckedUtc = DateTime.MinValue
            });
        }
    }

    public BackendOption? GetNextServer()
    {
        lock (_lock)
        {
            var activeWeightedList = new List<BackendOption>();
            
            foreach (var server in _servers.Where(s => s.IsAlive))
            {
                int weight = server.Option.Weight > 0 ? server.Option.Weight : 1;
                for (int i = 0; i < weight; i++)
                {
                    activeWeightedList.Add(server.Option);
                }
            }

            if (activeWeightedList.Count == 0) return null;

            _currentIndex = (_currentIndex + 1) % activeWeightedList.Count;
            return activeWeightedList[_currentIndex];
        }
    }

    public IReadOnlyList<ServerStatus> GetServerStatuses()
    {
        lock (_lock)
        {
            return _servers.ToList();
        }
    }

    public void RecordHealthCheckResult(string id, bool isSuccess, int threshold)
    {
        lock (_lock)
        {
            var server = _servers.FirstOrDefault(s => s.Option.Id == id);
            if (server == null) return;

            server.LastCheckedUtc = DateTime.UtcNow;

            if (isSuccess)
            {
                server.ConsecutiveFailures = 0;
                server.IsAlive = true;
            }
            else
            {
                server.ConsecutiveFailures++;
                if (server.ConsecutiveFailures >= threshold)
                {
                    server.IsAlive = false; 
                }
            }
        }
    }
}