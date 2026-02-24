using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using DocumentService.Exports;
using QuestPDF.Infrastructure;
using PLS.BuildingBlocks.Auth;
using PLS.BuildingBlocks.Correlation;
using PLS.BuildingBlocks.Data;
using PLS.BuildingBlocks.Entitlements;
using PLS.Contracts;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://web",
                "https://pls-web.happydune-0689c206.eastus.azurecontainerapps.io",
                "https://summary.plsplatform.com")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("DevJwt");
        var key = Encoding.UTF8.GetBytes(jwtSection["SigningKey"] ?? "dev-signing-key-change-me");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<PlsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=pls;Username=pls;Password=pls";
    options.UseNpgsql(connectionString, o => o.MigrationsAssembly("DocumentService"));
});

builder.Services.AddHttpClient("summarization", client =>
{
    var baseUrl = builder.Configuration["SummarizationService:BaseUrl"] ?? "http://summarization-service";
    client.BaseAddress = new Uri(baseUrl);
});

var storageProvider = builder.Configuration["Storage:Provider"] ?? "Local";
if (storageProvider.Equals("Blob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
}
else
{
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
}
builder.Services.AddSingleton<IMalwareScanner, NoopMalwareScanner>();
builder.Services.AddSingleton<IEventPublisher, HttpEventPublisher>();
builder.Services.AddSingleton<SummaryExportService>();
builder.Services.AddSingleton<SummaryImageExtractor>();
builder.Services.AddScoped<IAuditLogger, DbAuditLogger>();

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:RunMigrations"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();
    await db.Database.MigrateAsync();
}

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    // OpenAPI is optional in this starter.
}
app.UseCors("dev");

app.UseAuthentication();
app.UseAuthorization();


app.MapPost("/api/auth/dev-login", async (DevLoginRequest request, PlsDbContext db) =>
{
    var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Name == request.TenantName);
    if (tenant is null)
    {
        tenant = new Tenant { Id = Guid.NewGuid(), Name = request.TenantName };
        db.Tenants.Add(tenant);
    }

    var user = await db.Users.FirstOrDefaultAsync(x => x.Email == request.Email && x.TenantId == tenant.Id);
    if (user is null)
    {
        user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = request.Email,
            ExternalId = Guid.NewGuid().ToString("n"),
            DisplayName = request.Email.Split('@')[0]
        };
        db.Users.Add(user);
    }

    var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == tenant.Id);
    if (subscription is null)
    {
        subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            PlanName = "Free",
            EnabledLanes = (int)(LaneFlags.LaneA | LaneFlags.LaneC | LaneFlags.LaneD),
            TokenQuotaMonthly = 10000,
            ActiveFrom = DateTimeOffset.UtcNow
        };
        db.Subscriptions.Add(subscription);
    }

    await db.SaveChangesAsync();

    var jwtSection = app.Configuration.GetSection("DevJwt");
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"] ?? "dev-signing-key-change-me"));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
        new(ClaimsExtensions.TenantIdClaim, tenant.Id.ToString()),
        new(ClaimsExtensions.UserIdClaim, user.Id.ToString()),
        new(ClaimTypes.Email, user.Email)
    };

    var token = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.UtcNow.AddHours(8),
        signingCredentials: credentials);

    var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new DevLoginResponse(tokenValue, tenant.Id, user.Id));
});

app.MapPost("/api/documents/upload", async (
        HttpContext httpContext,
        IFormFile file,
        SummaryLane? lane,
        string? promptOverride,
        string? title,
        string? therapeuticArea,
        string? audience,
        PlsDbContext db,
        IFileStorage storage,
        IMalwareScanner scanner,
        IEventPublisher publisher,
        IAuditLogger auditLogger) =>
    {
        var user = httpContext.User;
        var tenantId = user.GetTenantId();
        var userId = user.GetUserId();

        if (file.Length == 0)
        {
            return Results.BadRequest("File is empty.");
        }

        var scanResult = await scanner.ScanAsync(file);
        if (!scanResult.IsSuccess)
        {
            return Results.BadRequest(scanResult.Error);
        }

        var documentId = Guid.NewGuid();
        var storageUri = await storage.SaveAsync(tenantId, documentId, file);

        var document = new Document
        {
            Id = documentId,
            TenantId = tenantId,
            UserId = userId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            StorageUri = storageUri,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            TherapeuticArea = string.IsNullOrWhiteSpace(therapeuticArea) ? null : therapeuticArea.Trim(),
            Audience = string.IsNullOrWhiteSpace(audience) ? null : audience.Trim(),
            UploadedAt = DateTimeOffset.UtcNow,
            Status = DocumentStatus.Uploaded
        };

        db.Documents.Add(document);

        var jobId = Guid.NewGuid();
        var job = new SummaryJob
        {
            Id = jobId,
            TenantId = tenantId,
            UserId = userId,
            DocumentId = documentId,
            Lane = lane ?? SummaryLane.LaneA,
            Status = JobStatus.Queued,
            RequestedAt = DateTimeOffset.UtcNow,
            PromptOverride = string.IsNullOrWhiteSpace(promptOverride) ? null : promptOverride,
            CorrelationId = httpContext.GetCorrelationId()
        };

        db.SummaryJobs.Add(job);
        await db.SaveChangesAsync();

        await auditLogger.LogAsync(httpContext, "DocumentUploaded", "Document", document.Id.ToString(), new
        {
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            jobId
        });

        await publisher.PublishAsync(new DocumentUploadedEvent(
            document.Id,
            tenantId,
            userId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.StorageUri,
            document.UploadedAt));

        return Results.Ok(new DocumentUploadResponse(documentId, jobId));
    })
    .DisableAntiforgery()
    .RequireAuthorization();

app.MapPost("/api/summaries/{documentId:guid}/generate", async (
        Guid documentId,
        SummaryLane lane,
        HttpContext httpContext,
        PlsDbContext db,
        IAuditLogger auditLogger) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == tenantId && x.UserId == userId);
        if (document is null)
        {
            return Results.NotFound();
        }

        var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId);
        if (subscription is null || !LaneEntitlements.Allows(subscription.EnabledLanes, lane))
        {
            return Results.Forbid();
        }

        var job = new SummaryJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            DocumentId = documentId,
            Lane = lane,
            Status = JobStatus.Queued,
            RequestedAt = DateTimeOffset.UtcNow,
            CorrelationId = httpContext.GetCorrelationId()
        };

        db.SummaryJobs.Add(job);
        await db.SaveChangesAsync();

        await auditLogger.LogAsync(httpContext, "SummaryRequested", "SummaryJob", job.Id.ToString(), new
        {
            documentId,
            lane
        });

        return Results.Ok(new SummaryGenerateResponse(job.Id));
    })
    .DisableAntiforgery()
    .RequireAuthorization();

app.MapGet("/api/jobs/{jobId:guid}", async (Guid jobId, HttpContext httpContext, PlsDbContext db) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var job = await db.SummaryJobs.FirstOrDefaultAsync(x => x.Id == jobId && x.TenantId == tenantId && x.UserId == userId);
        if (job is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new JobStatusResponse(job.Id, job.Status, job.ErrorMessage));
    })
    .RequireAuthorization();

// Returns the latest Lane D stage progress from stage.json written by LaneDStageReporter.
// Returns 204 No Content while no stage has been written yet (job queued or non-Lane-D).
app.MapGet("/api/jobs/{jobId:guid}/stage", async (Guid jobId, HttpContext httpContext, PlsDbContext db) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var job = await db.SummaryJobs.FirstOrDefaultAsync(x => x.Id == jobId && x.TenantId == tenantId && x.UserId == userId);
        if (job is null) return Results.NotFound();

        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == job.DocumentId && x.TenantId == tenantId);
        if (document is null) return Results.NoContent();

        var directory = Path.GetDirectoryName(document.StorageUri);
        if (string.IsNullOrWhiteSpace(directory)) return Results.NoContent();

        var stagePath = Path.Combine(directory, "stage.json");
        if (!File.Exists(stagePath)) return Results.NoContent();

        var content = await File.ReadAllTextAsync(stagePath);
        return Results.Content(content, "application/json");
    })
    .RequireAuthorization();

app.MapGet("/api/summaries/{summaryId:guid}", async (Guid summaryId, HttpContext httpContext, PlsDbContext db) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var summary = await db.Summaries.FirstOrDefaultAsync(x => x.Id == summaryId && x.TenantId == tenantId && x.UserId == userId);
        if (summary is null)
        {
            return Results.NotFound();
        }

        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == summary.DocumentId && x.TenantId == tenantId && x.UserId == userId);
        var citations = EvidenceExtractor.Extract(summary.SummaryText, summary.ModelInfoJson, summary.DocumentId, tenantId, db);
        var resolvedSummary = document is null
            ? summary.SummaryText
            : RewriteLocalImageLinks(summary.SummaryText, summary.Id, document.StorageUri);
        return Results.Ok(new SummaryResponse(
            summary.Id,
            summary.DocumentId,
            summary.Lane,
            resolvedSummary,
            summary.ModelInfoJson,
            summary.CreatedAt,
            citations,
            document?.Title,
            document?.TherapeuticArea,
            document?.Audience));
    })
    .RequireAuthorization();

app.MapGet("/api/summaries/{summaryId:guid}/images/{fileName}", async (
        Guid summaryId,
        string fileName,
        HttpContext httpContext,
        PlsDbContext db) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var summary = await db.Summaries.FirstOrDefaultAsync(x => x.Id == summaryId && x.TenantId == tenantId && x.UserId == userId);
        if (summary is null)
        {
            return Results.NotFound();
        }

        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == summary.DocumentId && x.TenantId == tenantId && x.UserId == userId);
        if (document is null)
        {
            return Results.NotFound();
        }

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return Results.BadRequest();
        }

        var directory = Path.GetDirectoryName(document.StorageUri);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Results.NotFound();
        }

        var filePath = Path.Combine(directory, safeName);
        if (!System.IO.File.Exists(filePath))
        {
            return Results.NotFound();
        }

        var contentType = ResolveImageContentType(filePath);
        return Results.File(filePath, contentType);
    })
    .RequireAuthorization();

app.MapGet("/api/summaries/{summaryId:guid}/download", async (
        Guid summaryId,
        string? format,
        HttpContext httpContext,
        PlsDbContext db,
        SummaryExportService exporter,
        SummaryImageExtractor imageExtractor,
        IAuditLogger auditLogger) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var summary = await db.Summaries.FirstOrDefaultAsync(x => x.Id == summaryId && x.TenantId == tenantId && x.UserId == userId);
        if (summary is null)
        {
            return Results.NotFound();
        }

        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == summary.DocumentId && x.TenantId == tenantId && x.UserId == userId);
        if (document is null)
        {
            return Results.NotFound();
        }

        var exportData = new SummaryExportData(
            document.FileName,
            document.Title,
            document.TherapeuticArea,
            document.Audience,
            summary.Lane,
            summary.SummaryText,
            summary.CreatedAt);
        var images = imageExtractor.ExtractImages(document.StorageUri, document.ContentType, summary.ModelInfoJson);
        var safeBaseName = MakeSafeFileName(Path.GetFileNameWithoutExtension(document.FileName));
        var chosenFormat = string.IsNullOrWhiteSpace(format) ? "pdf" : format.Trim().ToLowerInvariant();

        await auditLogger.LogAsync(httpContext, "SummaryDownloaded", "Summary", summary.Id.ToString(), new
        {
            summary.DocumentId,
            summary.Lane,
            format = chosenFormat
        });

        return chosenFormat switch
        {
            "pdf" => Results.File(
                exporter.CreatePdf(exportData, images),
                "application/pdf",
                $"{safeBaseName}-summary-{summary.Lane}.pdf"),
            "docx" => Results.File(
                exporter.CreateDocx(exportData),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"{safeBaseName}-summary-{summary.Lane}.docx"),
            _ => Results.BadRequest("Format must be pdf or docx.")
        };
    })
    .RequireAuthorization();

app.MapGet("/api/documents/{documentId:guid}/download", async (
        Guid documentId,
        HttpContext httpContext,
        PlsDbContext db) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == tenantId && x.UserId == userId);
        if (document is null)
        {
            return Results.NotFound();
        }

        if (File.Exists(document.StorageUri))
        {
            var content = await File.ReadAllBytesAsync(document.StorageUri);
            return Results.File(content, document.ContentType, document.FileName, enableRangeProcessing: true);
        }

        if (Uri.TryCreate(document.StorageUri, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return Results.Redirect(document.StorageUri);
        }

        return Results.NotFound();
    })
    .RequireAuthorization();

app.MapGet("/api/prompts/{lane}", (SummaryLane lane, IConfiguration configuration) =>
    {
        var basePath = configuration["PROMPT_TEMPLATE_PATH"]
            ?? Path.Combine(AppContext.BaseDirectory, "prompts");

        var templatePath = lane switch
        {
            SummaryLane.LaneA => Path.Combine(basePath, "lane-a", "summary.md.sbn"),
            SummaryLane.LaneB => Path.Combine(basePath, "lane-b", "summary.md.sbn"),
            SummaryLane.LaneC => Path.Combine(basePath, "lane-c", "summary.md.sbn"),
            SummaryLane.LaneD => Path.Combine(basePath, "lane-d", "agent-plan.md.sbn"),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
        {
            return Results.NotFound();
        }

        var content = File.ReadAllText(templatePath);
        return Results.Text(content, "text/plain");
    })
    .RequireAuthorization();

app.MapGet("/api/summaries", async (
        string? search,
        SummaryLane? lane,
        JobStatus? status,
        HttpContext httpContext,
        PlsDbContext db) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var summariesQuery = from summary in db.Summaries
            join document in db.Documents on summary.DocumentId equals document.Id
            join job in db.SummaryJobs on summary.JobId equals job.Id
            where summary.TenantId == tenantId && summary.UserId == userId
            select new { summary, document, job };

        if (!string.IsNullOrWhiteSpace(search))
        {
            summariesQuery = summariesQuery.Where(x => x.document.FileName.Contains(search));
        }

        if (lane.HasValue)
        {
            summariesQuery = summariesQuery.Where(x => x.summary.Lane == lane.Value);
        }

        if (status.HasValue)
        {
            summariesQuery = summariesQuery.Where(x => x.job.Status == status.Value);
        }

        var items = await summariesQuery
            .OrderByDescending(x => x.summary.CreatedAt)
            .Select(x => new SummaryListItem(
                x.summary.Id,
                x.summary.DocumentId,
                x.summary.Lane,
                x.job.Status,
                x.document.FileName,
                x.summary.CreatedAt))
            .ToListAsync();

        return Results.Ok(items);
    })
    .RequireAuthorization();

app.MapGet("/api/entitlements", async (HttpContext httpContext, PlsDbContext db) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var subscription = await db.Subscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId);
        if (subscription is null)
        {
            return Results.Ok(new EntitlementsResponse(Array.Empty<SummaryLane>()));
        }

        var lanes = LaneEntitlements.ToLanes(subscription.EnabledLanes);
        return Results.Ok(new EntitlementsResponse(lanes));
    })
    .RequireAuthorization();

static string MakeSafeFileName(string name)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        return "summary";
    }

    var safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
    return safe.Trim('-');
}

static string RewriteLocalImageLinks(string summaryText, Guid summaryId, string storageUri)
{
    if (string.IsNullOrWhiteSpace(summaryText))
    {
        return summaryText;
    }

    var directory = Path.GetDirectoryName(storageUri);
    if (string.IsNullOrWhiteSpace(directory))
    {
        return summaryText;
    }

    return Regex.Replace(
        summaryText,
        @"!\[(?<alt>[^\]]*)\]\((?<url>[^)]+)\)",
        match =>
        {
            var alt = match.Groups["alt"].Value;
            var url = match.Groups["url"].Value.Trim();

            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) &&
                (absolute.Scheme == "http" || absolute.Scheme == "https"))
            {
                return match.Value;
            }

            var fileName = Path.GetFileName(url);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return match.Value;
            }

            var localPath = Path.Combine(directory, fileName);
            if (!System.IO.File.Exists(localPath))
            {
                return match.Value;
            }

            var resolved = $"/api/summaries/{summaryId}/images/{fileName}";
            return $"![{alt}]({resolved})";
        });
}

static string ResolveImageContentType(string filePath)
{
    var extension = Path.GetExtension(filePath).ToLowerInvariant();
    return extension switch
    {
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}

app.MapGet("/health", () => Results.Ok("ok"));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();
    if (app.Environment.IsDevelopment())
    {
        await db.Database.MigrateAsync();
        await SeedData.InitializeAsync(db);
    }
}

app.Run();

interface IFileStorage
{
    Task<string> SaveAsync(Guid tenantId, Guid documentId, IFormFile file);
}

sealed class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IConfiguration configuration)
    {
        var configured = configuration["LocalStorage:BasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage");
        _basePath = Path.GetFullPath(configured);
    }

    public async Task<string> SaveAsync(Guid tenantId, Guid documentId, IFormFile file)
    {
        var tenantPath = Path.Combine(_basePath, tenantId.ToString());
        Directory.CreateDirectory(tenantPath);

        var filePath = Path.Combine(tenantPath, documentId.ToString());
        await using var stream = File.Create(filePath);
        await file.CopyToAsync(stream);

        return filePath;
    }
}

sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;

    public AzureBlobFileStorage(IConfiguration configuration)
    {
        var connectionString = configuration["Storage:Blob:ConnectionString"];
        var containerName = configuration["Storage:Blob:Container"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(containerName))
        {
            throw new InvalidOperationException("Storage:Blob:ConnectionString and Storage:Blob:Container are required.");
        }

        _container = new BlobContainerClient(connectionString, containerName);
        _container.CreateIfNotExists();
    }

    public async Task<string> SaveAsync(Guid tenantId, Guid documentId, IFormFile file)
    {
        var blobName = $"{tenantId}/{documentId}";
        var blob = _container.GetBlobClient(blobName);
        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, overwrite: true);
        return blob.Uri.ToString();
    }
}

interface IMalwareScanner
{
    Task<PLS.BuildingBlocks.Results.Result> ScanAsync(IFormFile file);
}

sealed class NoopMalwareScanner : IMalwareScanner
{
    public Task<PLS.BuildingBlocks.Results.Result> ScanAsync(IFormFile file)
        => Task.FromResult(PLS.BuildingBlocks.Results.Result.Ok());
}

interface IEventPublisher
{
    Task PublishAsync(DocumentUploadedEvent documentUploadedEvent);
}

sealed class NoopEventPublisher : IEventPublisher
{
    private readonly ILogger<NoopEventPublisher> _logger;

    public NoopEventPublisher(ILogger<NoopEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(DocumentUploadedEvent documentUploadedEvent)
    {
        _logger.LogInformation("DocumentUploaded event queued for {DocumentId}", documentUploadedEvent.DocumentId);
        return Task.CompletedTask;
    }
}

sealed class HttpEventPublisher : IEventPublisher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpEventPublisher> _logger;

    public HttpEventPublisher(IHttpClientFactory httpClientFactory, ILogger<HttpEventPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task PublishAsync(DocumentUploadedEvent documentUploadedEvent)
    {
        var client = _httpClientFactory.CreateClient("summarization");
        var payload = new IndexRequest(
            documentUploadedEvent.DocumentId,
            documentUploadedEvent.TenantId,
            documentUploadedEvent.StorageUri,
            documentUploadedEvent.ContentType);

        try
        {
            var response = await client.PostAsJsonAsync("/internal/index", payload);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Indexing request failed for {DocumentId} with {StatusCode}",
                    documentUploadedEvent.DocumentId,
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Indexing request failed for {DocumentId}", documentUploadedEvent.DocumentId);
        }
    }
}

sealed record IndexRequest(Guid DocumentId, Guid TenantId, string StorageUri, string ContentType);

static class EvidenceExtractor
{
    private const int SnippetLimit = 280;

    public static IReadOnlyList<EvidenceSection> Extract(
        string summaryText,
        string? modelInfoJson,
        Guid documentId,
        Guid tenantId,
        PlsDbContext db)
    {
        if (string.IsNullOrWhiteSpace(modelInfoJson))
        {
            return Array.Empty<EvidenceSection>();
        }

        var chunkIds = ExtractChunkIds(summaryText);
        if (chunkIds.Count == 0)
        {
            return Array.Empty<EvidenceSection>();
        }

        List<EvidenceItem> items;
        try
        {
            var chunkMeta = ExtractChunkMeta(modelInfoJson);

            var chunkTextLookup = db.DocumentChunks
                .Where(x => x.TenantId == tenantId && x.DocumentId == documentId && chunkIds.Contains(x.ChunkId))
                .Select(x => new { x.ChunkId, x.Text })
                .ToList()
                .ToDictionary(x => x.ChunkId, x => x.Text, StringComparer.OrdinalIgnoreCase);

            items = new List<EvidenceItem>();
            for (var i = 0; i < chunkIds.Count; i++)
            {
                var chunkId = chunkIds[i];
                chunkTextLookup.TryGetValue(chunkId, out var text);
                chunkMeta.TryGetValue(chunkId, out var meta);
                var snippet = Truncate(text);
                var sourceUrl = BuildSourceUrl(documentId, meta.Page);
                items.Add(new EvidenceItem(chunkId, snippet, meta.Page, meta.Section, i + 1, sourceUrl));
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<EvidenceSection>();
        }

        return items.Count == 0
            ? Array.Empty<EvidenceSection>()
            : new[] { new EvidenceSection("Retrieved context", items) };
    }

    private static string? Truncate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (text.Length <= SnippetLimit)
        {
            return text;
        }

        return $"{text[..SnippetLimit]}...";
    }

    private static string BuildSourceUrl(Guid documentId, int? page)
    {
        var baseUrl = $"/api/documents/{documentId}/download";
        if (page.HasValue && page.Value > 0)
        {
            return $"{baseUrl}#page={page.Value}";
        }

        return baseUrl;
    }

    private static List<string> ExtractChunkIds(string summaryText)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            summaryText ?? string.Empty,
            @"\[\s*chunk\s*(?<id>[0-9a-f]{64})\s*\]",
            match => $"[{match.Groups["id"].Value}]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                     normalized,
                     @"\[(?<id>[0-9a-f]{64})\]",
                     System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var id = match.Groups["id"].Value;
            if (seen.Add(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static Dictionary<string, (int? Page, string? Section)> ExtractChunkMeta(string? modelInfoJson)
    {
        var chunkMeta = new Dictionary<string, (int? Page, string? Section)>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(modelInfoJson))
        {
            return chunkMeta;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(modelInfoJson);
            if (!doc.RootElement.TryGetProperty("retrieval", out var retrieval))
            {
                return chunkMeta;
            }

            if (!retrieval.TryGetProperty("chunks", out var chunksElement)
                || chunksElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return chunkMeta;
            }

            foreach (var chunk in chunksElement.EnumerateArray())
            {
                if (!chunk.TryGetProperty("chunkId", out var idElement))
                {
                    continue;
                }

                var chunkId = idElement.GetString();
                if (string.IsNullOrWhiteSpace(chunkId))
                {
                    continue;
                }

                var page = chunk.TryGetProperty("page", out var pageElement) && pageElement.TryGetInt32(out var pageValue)
                    ? pageValue
                    : (int?)null;
                var section = chunk.TryGetProperty("section", out var sectionElement)
                    ? sectionElement.GetString()
                    : null;

                chunkMeta[chunkId] = (page, section);
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return chunkMeta;
        }

        return chunkMeta;
    }
}
