using System.Text;
using GWT.Api.Middleware;
using GWT.Application;
using GWT.Application.Interfaces.Services;
using GWT.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog Structured Logging ─────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ── Services ───────────────────────────────────────────────────────────────
var services = builder.Services;

// Clean Architecture layers
services.AddApplication();
services.AddInfrastructure(builder.Configuration);

// Controllers
services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Swagger / OpenAPI
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Global Wealth Tracker API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── JWT Authentication ─────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero
        };
    });

services.AddAuthorization();

// ── Rate Limiting ──────────────────────────────────────────────────────────
services.AddRateLimiter(opts =>
{
    // Global: 200 requests per minute per IP (mirrors original express-rate-limit config)
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── CORS ───────────────────────────────────────────────────────────────────
// Config may return an empty array (appsettings.json default) — fall back to localhost
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                         ?.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
if (allowedOrigins is null || allowedOrigins.Length == 0)
    allowedOrigins = ["http://localhost:5173"];

services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

// ── Application Pipeline ───────────────────────────────────────────────────
var app = builder.Build();

// Security headers — remove X-Powered-By equivalent (Server header) and add hardening headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GWT API v1"));
}
else
{
    // HSTS in production (per security checklist)
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Log.Information("GWT API starting on {Environment}", app.Environment.EnvironmentName);

// ── Step 1: Run DB migrations synchronously before accepting any requests ─────────────
// This must happen before StartAsync() so EF Core's model is always in sync with the
// schema when the first search/query arrives. Migrations are idempotent and run in
// < 1 second on subsequent restarts when no pending migrations exist.
try
{
    using var migrationScope = app.Services.CreateScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<GWT.Infrastructure.Data.GwtDbContext>();
    await db.Database.MigrateAsync();
    Log.Information("Database migrations applied successfully.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Database migration failed — cannot serve requests with a mismatched schema.");
    throw;
}

// Start accepting HTTP requests after schema is confirmed correct.
await app.StartAsync();

_ = Task.Run(async () =>
{
    // ── Step 2: Fetch all external data in parallel (no DB writes yet) ────────────
    List<GWT.Application.DTOs.Funds.AmfiFundRawDto>?  amfiFunds  = null;
    List<GWT.Application.DTOs.Funds.NasdaqSymbolDto>? nasdaqEtfs = null;

    // Each fetch uses its own scope so typed HttpClients are properly scoped.
    var amfiTask = Task.Run(async () =>
    {
        try
        {
            using var s = app.Services.CreateScope();
            var amfi = s.ServiceProvider.GetRequiredService<IAmfiService>();
            amfiFunds = await amfi.FetchAllNavsAsync();
            Log.Information("AMFI warm-up complete: {Count} Growth plan entries cached.", amfiFunds.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AMFI warm-up failed — India search will fall back to on-demand fetch.");
        }
    });

    var yahooTask = Task.Run(async () =>
    {
        try
        {
            // IYahooFinanceService is singleton — resolve directly from root, not via scope.
            var yahoo = app.Services.GetRequiredService<IYahooFinanceService>();
            await yahoo.WarmUpAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Yahoo Finance warm-up failed — crumb will be fetched on first request.");
        }
    });

    var nasdaqTask = Task.Run(async () =>
    {
        try
        {
            using var s = app.Services.CreateScope();
            var nasdaq = s.ServiceProvider.GetRequiredService<GWT.Application.Interfaces.Services.INasdaqService>();
            nasdaqEtfs = await nasdaq.GetAllEtfsAsync();
            Log.Information("NASDAQ ETF catalogue fetched: {Count} ETFs.", nasdaqEtfs.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "NASDAQ ETF fetch failed — global search will cache results on demand.");
        }
    });

    await Task.WhenAll(amfiTask, yahooTask, nasdaqTask);

    // ── Step 3: Bulk-import India + Global catalogues into fund_meta (in parallel) ──
    // Each import uses its own DI scope (and therefore its own DbContext instance)
    // to avoid concurrent access on a single non-thread-safe DbContext.

    // ── Step 3: One-time seed imports (each runs in its own scope / DbContext) ─────
    // A fund count threshold distinguishes a seeded DB from an empty or near-empty one.
    // If the table already has enough records the import is skipped entirely,
    // so subsequent restarts are fast and don't perform redundant upserts.

    var importAmfi = Task.Run(async () =>
    {
        if (amfiFunds is not { Count: > 0 }) return;
        try
        {
            using var s  = app.Services.CreateScope();
            var db       = s.ServiceProvider.GetRequiredService<GWT.Infrastructure.Data.GwtDbContext>();
            var existing = await db.Set<GWT.Domain.Entities.FundMeta>()
                                   .CountAsync(f => f.Region == GWT.Domain.Enums.Region.INDIA);

            if (existing > 5000)
            {
                Log.Information("India funds already seeded ({Count} records) — skipping bulk import.", existing);
                return;
            }

            var repo     = s.ServiceProvider.GetRequiredService<GWT.Application.Interfaces.Repositories.IFundMetaRepository>();
            var entities = amfiFunds.Select(f => new GWT.Domain.Entities.FundMeta
            {
                Id         = $"IN-{f.SchemeCode}",
                Region     = GWT.Domain.Enums.Region.INDIA,
                Name       = f.SchemeName,
                Amc        = f.Amc,
                Ticker     = $"AMFI-{f.SchemeCode}",
                SchemeCode = f.SchemeCode,
                Isin       = f.Isin,
                Timezone   = "Asia/Kolkata",
                LatestNav  = f.Nav,
                NavDate    = f.NavDate,
                UpdatedAt  = DateTime.UtcNow,
            });
            await repo.BulkUpsertFundsAsync(entities);
            Log.Information("AMFI one-time seed complete: {Count} India funds inserted into fund_meta.", amfiFunds.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AMFI one-time seed failed — India funds will be inserted on demand.");
        }
    });

    var importNasdaq = Task.Run(async () =>
    {
        if (nasdaqEtfs is not { Count: > 0 }) return;
        try
        {
            using var s  = app.Services.CreateScope();
            var db       = s.ServiceProvider.GetRequiredService<GWT.Infrastructure.Data.GwtDbContext>();
            var existing = await db.Set<GWT.Domain.Entities.FundMeta>()
                                   .CountAsync(f => f.Region == GWT.Domain.Enums.Region.GLOBAL);

            if (existing > 500)
            {
                Log.Information("Global ETFs already seeded ({Count} records) — skipping bulk import.", existing);
                return;
            }

            var repo     = s.ServiceProvider.GetRequiredService<GWT.Application.Interfaces.Repositories.IFundMetaRepository>();
            var entities = nasdaqEtfs.Select(e => new GWT.Domain.Entities.FundMeta
            {
                Id        = $"US-{e.Symbol}",
                Region    = GWT.Domain.Enums.Region.GLOBAL,
                Name      = e.Name,
                Amc       = e.Exchange,
                Ticker    = e.Symbol,
                Timezone  = "America/New_York",  // NASDAQ/NYSE — Eastern Time
                UpdatedAt = DateTime.UtcNow,
                // LatestNav / NavDate are null at import time.
                // The daily NavSyncService fetches live prices via GetBulkQuotesAsync
                // for ALL global funds in fund_meta (including these catalogue entries).
            });
            await repo.BulkUpsertFundsAsync(entities);
            Log.Information("NASDAQ one-time seed complete: {Count} global ETFs inserted into fund_meta.", nasdaqEtfs.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "NASDAQ one-time seed failed — global ETFs will be cached on demand.");
        }
    });

    await Task.WhenAll(importAmfi, importNasdaq);
});

await app.WaitForShutdownAsync();
