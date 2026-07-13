using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

var startedAtUtc = DateTimeOffset.UtcNow;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    options.UseUtcTimestamp = true;
});

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/api/health", permanent: false));

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "ToolBridge.Api",
    utc = DateTimeOffset.UtcNow
}));

api.MapGet("/status", () =>
{
    using var process = Process.GetCurrentProcess();
    var memory = GC.GetGCMemoryInfo();

    return Results.Ok(new
    {
        service = "ToolBridge.Api",
        environment = app.Environment.EnvironmentName,
        startedAtUtc,
        uptimeSeconds = (long)(DateTimeOffset.UtcNow - startedAtUtc).TotalSeconds,
        runtime = RuntimeInformation.FrameworkDescription,
        os = RuntimeInformation.OSDescription,
        process = new
        {
            id = Environment.ProcessId,
            workingSetBytes = process.WorkingSet64,
            privateMemoryBytes = process.PrivateMemorySize64,
            threadCount = process.Threads.Count
        },
        gc = new
        {
            serverGc = System.Runtime.GCSettings.IsServerGC,
            heapSizeBytes = memory.HeapSizeBytes,
            totalAvailableMemoryBytes = memory.TotalAvailableMemoryBytes
        }
    });
});

api.MapGet("/network", () => Results.Ok(new
{
    hostName = Dns.GetHostName(),
    addresses = GetLocalAddresses()
}));

app.Run();

static IEnumerable<object> GetLocalAddresses()
{
    return NetworkInterface.GetAllNetworkInterfaces()
        .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
        .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses.Select(address => new
        {
            adapter = adapter.Name,
            address = address.Address.ToString(),
            family = address.Address.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6"
        }))
        .Where(address => address.family == "IPv4" && !IPAddress.IsLoopback(IPAddress.Parse(address.address)))
        .OrderBy(address => address.adapter)
        .ThenBy(address => address.address);
}
