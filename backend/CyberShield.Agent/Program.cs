using CyberShield.Agent;
using CyberShield.Agent.Collectors;

var builder = Host.CreateApplicationBuilder(args);

// ── Windows Service support ────────────────────────────────────────────
// Runs as console when launched manually, runs as Windows Service when installed.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "CyberShield Monitoring Agent";
});

// ── Configuration ─────────────────────────────────────────────────────
builder.Configuration
    .AddJsonFile("appsettings.json",                optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json",    optional: true,  reloadOnChange: true)
    .AddEnvironmentVariables("CSAGENT_")
    .AddUserSecrets<MonitoringWorker>(optional: true);

// ── Logging ───────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole(opts =>
{
    opts.FormatterName = "simple";
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ── HTTP Client ───────────────────────────────────────────────────────
var apiBase  = builder.Configuration["Agent:ApiBaseUrl"] ?? "http://localhost:5080";
var agentKey = builder.Configuration["Agent:ApiKey"]     ?? "";

builder.Services.AddHttpClient<TelemetrySender>(client =>
{
    client.BaseAddress = new Uri(apiBase);
    client.Timeout     = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrEmpty(agentKey))
        client.DefaultRequestHeaders.Add("X-Agent-Key", agentKey);
});

// ── Collectors (singleton — PerformanceCounter must be reused) ────────
builder.Services.AddSingleton<SystemHealthCollector>();
builder.Services.AddSingleton<NetworkCollector>();
builder.Services.AddSingleton<SecurityCollector>();
builder.Services.AddSingleton<EventLogCollector>();

// ── Worker ────────────────────────────────────────────────────────────
builder.Services.AddHostedService<MonitoringWorker>();

var host = builder.Build();
host.Run();
