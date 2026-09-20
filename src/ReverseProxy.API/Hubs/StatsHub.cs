using Microsoft.AspNetCore.SignalR;

namespace src.ReverseProxy.API.Hubs;

public class StatsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}