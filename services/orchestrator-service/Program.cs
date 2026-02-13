using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PLS.BuildingBlocks.Correlation;
using PLS.BuildingBlocks.Data;
using PLS.BuildingBlocks.Entitlements;
using PLS.Contracts;
using PLS.BuildingBlocks.Jobs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PlsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=pls;Username=pls;Password=pls";
    options.UseNpgsql(connectionString);
});

builder.Services.AddHttpClient("summarization", client =>
{
    var baseUrl = builder.Configuration["SummarizationService:BaseUrl"] ?? "http://summarization-service";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHostedService<JobOrchestratorWorker>();

var app = builder.Build();

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    // OpenAPI is optional in this starter.
}

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

sealed class JobOrchestratorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JobOrchestratorWorker> _logger;

    public JobOrchestratorWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, ILogger<JobOrchestratorWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process summary job.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task ProcessNextJobAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

        var job = await db.SummaryJobs
            .OrderBy(x => x.RequestedAt)
            .FirstOrDefaultAsync(x => x.Status == JobStatus.Queued, stoppingToken);

        if (job is null)
        {
            return;
        }

        var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == job.TenantId, stoppingToken);
        if (subscription is null || !LaneEntitlements.Allows(subscription.EnabledLanes, job.Lane))
        {
            JobStateMachine.Fail(job, "Lane not enabled for tenant.");
            await db.SaveChangesAsync(stoppingToken);
            return;
        }

        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == job.DocumentId, stoppingToken);
        if (document is null)
        {
            JobStateMachine.Fail(job, "Document not found.");
            await db.SaveChangesAsync(stoppingToken);
            return;
        }

        JobStateMachine.Start(job);
        document.Status = DocumentStatus.Processing;
        await db.SaveChangesAsync(stoppingToken);

        var client = _httpClientFactory.CreateClient("summarization");
        var request = new SummarizeRequest(job.DocumentId, job.Lane, document.StorageUri, document.ContentType, job.PromptOverride);
        var response = await client.PostAsJsonAsync("/internal/summarize", request, stoppingToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(stoppingToken);
            var message = string.IsNullOrWhiteSpace(errorBody)
                ? $"Summarization failed with status {response.StatusCode}."
                : $"Summarization failed with status {response.StatusCode}: {errorBody}";
            JobStateMachine.Fail(job, message);
            document.Status = DocumentStatus.Failed;
            await db.SaveChangesAsync(stoppingToken);
            return;
        }

        var summaryResult = await response.Content.ReadFromJsonAsync<SummarizeResponse>(cancellationToken: stoppingToken);
        if (summaryResult is null)
        {
            JobStateMachine.Fail(job, "Summarization response was empty.");
            document.Status = DocumentStatus.Failed;
            await db.SaveChangesAsync(stoppingToken);
            return;
        }

        var summary = new Summary
        {
            Id = Guid.NewGuid(),
            TenantId = job.TenantId,
            UserId = job.UserId,
            DocumentId = job.DocumentId,
            JobId = job.Id,
            Lane = job.Lane,
            SummaryText = summaryResult.SummaryText,
            ModelInfoJson = summaryResult.ModelInfoJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Summaries.Add(summary);
        JobStateMachine.Complete(job);
        document.Status = DocumentStatus.Completed;
        await db.SaveChangesAsync(stoppingToken);
    }
}

sealed record SummarizeRequest(Guid DocumentId, SummaryLane Lane, string StorageUri, string ContentType, string? PromptOverride);

sealed record SummarizeResponse(string SummaryText, string? ModelInfoJson);
