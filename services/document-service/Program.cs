using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
        policy.WithOrigins("http://localhost:5173", "http://web")
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

builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IMalwareScanner, NoopMalwareScanner>();
builder.Services.AddSingleton<IEventPublisher, NoopEventPublisher>();
builder.Services.AddSingleton<SummaryExportService>();
builder.Services.AddSingleton<SummaryImageExtractor>();

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    // OpenAPI is optional in this starter.
}

app.UseAuthentication();
app.UseAuthorization();
app.UseCors("dev");

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
            EnabledLanes = (int)LaneFlags.LaneA,
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
        PlsDbContext db,
        IFileStorage storage,
        IMalwareScanner scanner,
        IEventPublisher publisher) =>
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
        PlsDbContext db) =>
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

app.MapGet("/api/summaries/{summaryId:guid}", async (Guid summaryId, HttpContext httpContext, PlsDbContext db) =>
    {
        var tenantId = httpContext.User.GetTenantId();
        var userId = httpContext.User.GetUserId();

        var summary = await db.Summaries.FirstOrDefaultAsync(x => x.Id == summaryId && x.TenantId == tenantId && x.UserId == userId);
        if (summary is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new SummaryResponse(summary.Id, summary.DocumentId, summary.Lane, summary.SummaryText, summary.ModelInfoJson, summary.CreatedAt));
    })
    .RequireAuthorization();

app.MapGet("/api/summaries/{summaryId:guid}/download", async (
        Guid summaryId,
        string? format,
        HttpContext httpContext,
        PlsDbContext db,
        SummaryExportService exporter,
        SummaryImageExtractor imageExtractor) =>
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

        var exportData = new SummaryExportData(document.FileName, summary.Lane, summary.SummaryText, summary.CreatedAt);
        var images = imageExtractor.ExtractImages(document.StorageUri, document.ContentType, summary.ModelInfoJson);
        var safeBaseName = MakeSafeFileName(Path.GetFileNameWithoutExtension(document.FileName));
        var chosenFormat = string.IsNullOrWhiteSpace(format) ? "pdf" : format.Trim().ToLowerInvariant();

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

static class SeedData
{
    public static async Task InitializeAsync(PlsDbContext db)
    {
        if (await db.Tenants.AnyAsync())
        {
            return;
        }

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Demo Tenant" });
        db.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            ExternalId = Guid.NewGuid().ToString("n"),
            Email = "demo@pls.local",
            DisplayName = "demo"
        });
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanName = "Starter",
            EnabledLanes = (int)LaneFlags.LaneA,
            TokenQuotaMonthly = 10000,
            ActiveFrom = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();
    }
}

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
