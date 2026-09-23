using Azure.AI.Projects;
using Azure.Identity;
using FoundryAgentJob.Jobs;
using FoundryAgentJob.Options;
using Microsoft.Extensions.Options;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services
    .AddOptions<FoundryOptions>()
    .Bind(builder.Configuration.GetSection(FoundryOptions.SectionName))
    .Validate(
        o => Uri.TryCreate(o.ProjectEndpoint, UriKind.Absolute, out _),
        "Foundry:ProjectEndpoint is missing or invalid. Set it in appsettings.json or via the Foundry__ProjectEndpoint environment variable.")
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.AgentName) && !o.AgentName.Contains('<'),
        "Foundry:AgentName is missing. Set it in appsettings.json or via the Foundry__AgentName environment variable.")
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
{
    FoundryOptions foundryOptions = sp.GetRequiredService<IOptions<FoundryOptions>>().Value;

    return new AIProjectClient(
        endpoint: new Uri(foundryOptions.ProjectEndpoint),
        tokenProvider: new DefaultAzureCredential());
});

int jobIntervalSeconds = builder.Configuration
    .GetSection(FoundryOptions.SectionName)
    .GetValue(nameof(FoundryOptions.JobIntervalSeconds), 30);

builder.Services.AddQuartz(q =>
{
    q.ScheduleJob<AgentJob>(trigger => trigger
        .WithIdentity("agent-job-trigger")
        .StartNow()
        .WithSimpleSchedule(x => x
            .WithInterval(TimeSpan.FromSeconds(jobIntervalSeconds))
            .RepeatForever()));
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

var app = builder.Build();

app.MapGet("/", () => "FoundryAgentJob is running.");

app.Run();
