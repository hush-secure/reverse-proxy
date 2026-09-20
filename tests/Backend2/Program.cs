const string ServerName = "Backend2";
const int Port = 5002;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://localhost:{Port}");

var app = builder.Build();

app.Use(async (context, next) =>
{
    var request = context.Request;

    Console.WriteLine("==================================================");
    Console.WriteLine($"[{ServerName}] {DateTime.Now:HH:mm:ss}  {request.Method} {request.Path}{request.QueryString}");
    Console.WriteLine($"[{ServerName}] From: {context.Connection.RemoteIpAddress}");

    foreach (var header in request.Headers)
    {
        Console.WriteLine($"[{ServerName}] Header: {header.Key} = {header.Value}");
    }

    Console.WriteLine("==================================================\n");

    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", server = ServerName }));

app.MapFallback(() => Results.Ok(new { message = $"Hello from {ServerName}", timestamp = DateTime.UtcNow }));

Console.WriteLine($"{ServerName} listening on http://localhost:{Port}");
app.Run();
