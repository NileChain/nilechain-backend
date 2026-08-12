using NileChain.AI;
using NileChain.AI.RAG;
using NileChain.API.Extensions;
using NileChain.API.Options;
using NileChain.Application;
using NileChain.Domain.Identity;
using NileChain.Infrastructure;
using NileChain.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var seedDemoOnly = args.Any(a =>
    string.Equals(a, "--seed-demo", StringComparison.OrdinalIgnoreCase));

var builder = WebApplication.CreateBuilder(args);

// Local overrides (gitignored): appsettings.{Environment}.local.json
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

// Optional .env next to the API project or backend/ root (gitignored)
LoadDotEnv(Path.Combine(builder.Environment.ContentRootPath, ".env"));
LoadDotEnv(Path.Combine(builder.Environment.ContentRootPath, "..", ".env"));
builder.Configuration.AddEnvironmentVariables();

// PaaS (Heroku/Railway/Render) inject PORT. Prefer it over the aspnet image default :8080.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://*:{port}");

ValidateProductionConfiguration(builder);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust Heroku (and similar) reverse proxies in front of the dyno.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddNileChainAI(builder.Configuration);
builder.Services.AddHostedService<NileChain.API.HostedServices.ProactiveMonitorHostedService>();
builder.Services.Configure<NileChain.API.Options.ContractMatchExpiryOptions>(
    builder.Configuration.GetSection(NileChain.API.Options.ContractMatchExpiryOptions.SectionName));
builder.Services.AddHostedService<NileChain.API.HostedServices.ContractMatchExpiryHostedService>();
builder.Services.AddHostedService<NileChain.API.HostedServices.FarmMarketplaceReminderHostedService>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<NileChain.API.Filters.FluentValidationActionFilter>();
});
builder.Services.AddScoped<NileChain.API.Filters.FluentValidationActionFilter>();
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddScoped<NileChain.API.Health.IDatabasePing, NileChain.API.Health.EfDatabasePing>();
builder.Services.AddHealthChecks()
    .AddCheck<NileChain.API.Health.DatabaseHealthCheck>(
        "database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: ["ready"]);

var corsOrigins = ResolveCorsOrigins(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<NileChain.API.Middleware.AuthRateLimitMiddleware>();
app.UseGlobalExceptionMiddleware();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<NileChainDbContext>();
        var migrateLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup.Migrations");
        await db.Database.MigrateAsync();
        migrateLogger.LogInformation("EF Core migrations applied (or already up to date).");

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentitySeeder");
        await IdentitySeeder.SeedAsync(
            roleManager,
            userManager,
            app.Environment,
            app.Configuration,
            seedLogger);

        var seedOnStartup = string.Equals(
            Environment.GetEnvironmentVariable("SEED_DEMO_ON_STARTUP"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        if (seedDemoOnly || seedOnStartup)
        {
            if (app.Environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "DevelopmentDataSeeder cannot run in Production. Remove --seed-demo / SEED_DEMO_ON_STARTUP.");
            }

            var cs = app.Configuration.GetConnectionString("DefaultConnection") ?? "";
            var host = DescribeSqlHost(cs);
            Console.WriteLine("======== NileChain --seed-demo ========");
            Console.WriteLine($"Connection source: DefaultConnection");
            Console.WriteLine($"SQL host/db: {host}");
            Console.WriteLine("Idempotent: yes (email / [SEED] / [DEMO] markers)");
            Console.WriteLine("Destructive ops: none");
            Console.WriteLine("=======================================");

            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("DevelopmentDataSeeder");
            await DevelopmentDataSeeder.SeedAsync(db, userManager, logger);

            try
            {
                var chromaLogger = loggerFactory.CreateLogger("ChromaKnowledgeSeeder");
                var chroma = scope.ServiceProvider.GetRequiredService<ChromaService>();
                await ChromaKnowledgeSeeder.SeedAsync(chroma, chromaLogger);
            }
            catch (Exception chromaEx)
            {
                logger.LogWarning(chromaEx, "Chroma seed skipped/failed (SQL seed still committed).");
                Console.WriteLine($"[SEED] Chroma skipped/failed: {chromaEx.Message}");
            }

            if (seedDemoOnly)
            {
                Console.WriteLine("[SEED] --seed-demo complete. Exiting without starting HTTP server.");
                return;
            }
        }
        else if (app.Environment.IsDevelopment())
        {
            var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Startup");
            startupLogger.LogInformation(
                "Skipping DevelopmentDataSeeder on startup. Run with --seed-demo or set SEED_DEMO_ON_STARTUP=1.");
        }
    }
    catch (Exception ex)
    {
        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");
        startupLogger.LogError(
            ex,
            "Startup seeding failed (DB/Chroma).");
        if (seedDemoOnly)
        {
            Console.Error.WriteLine($"[SEED] FATAL: {ex}");
            Environment.ExitCode = 1;
            return;
        }
    }
}

if (seedDemoOnly)
{
    return;
}

// Development: always on. Production: only when OpenApi:Enabled=true (graduation demo opt-in).
var openApiEnabled = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("OpenApi:Enabled", false);
if (openApiEnabled)
    app.MapOpenApi();

// Scalar UI omitted: Application Control in some environments blocks Scalar.AspNetCore.dll load.
// CORS before HTTPS redirection so browser preflight from Angular is not stripped by redirects.
app.UseCors("AngularPolicy");

// Local HTTPS (launchSettings). On Heroku TLS terminates at the router; dyno listens on HTTP $PORT.
if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = NileChain.API.Health.HealthResponseWriter.WriteAsync
});

app.Run();

static void ValidateProductionConfiguration(WebApplicationBuilder builder)
{
    if (!builder.Environment.IsProduction())
        return;

    var connection = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connection)
        || connection.Contains("__SET_IN_LOCAL_CONFIG__", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection must be set in Production (e.g. ConnectionStrings__DefaultConnection).");
    }

    var jwtSecret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(jwtSecret)
        || string.Equals(jwtSecret, "__SET_IN_LOCAL_CONFIG__", StringComparison.Ordinal)
        || jwtSecret.Contains("THIS_IS_DEVELOPMENT_SECRET_KEY", StringComparison.OrdinalIgnoreCase)
        || jwtSecret.Contains("CHANGE_IT", StringComparison.OrdinalIgnoreCase)
        || jwtSecret.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt:Secret must be set to a strong non-development secret in Production (e.g. Jwt__Secret).");
    }
}

static string[] ResolveCorsOrigins(IConfiguration configuration)
{
    var origins = new List<string>();
    var section = configuration.GetSection($"{CorsOptions.SectionName}:Origins");

    // Prefer scalar Cors__Origins when set. appsettings.json keeps Cors:Origins:0/:1 children,
    // so GetChildren() alone would ignore Heroku's Cors__Origins string and leave only localhost.
    if (!string.IsNullOrWhiteSpace(section.Value))
    {
        origins.AddRange(
            section.Value.Split(
                [',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
    else
    {
        foreach (var child in section.GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child.Value))
                origins.Add(child.Value.Trim());
        }
    }

    var frontendBase = configuration["App:FrontendBaseUrl"];
    if (!string.IsNullOrWhiteSpace(frontendBase))
        origins.Add(frontendBase);

    var distinct = origins
        .Select(o => o.Trim().TrimEnd('/'))
        .Where(o => o.Length > 0 && !o.Equals("__SET_IN_LOCAL_CONFIG__", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (distinct.Length > 0)
        return distinct;

    return
    [
        "http://localhost:4200",
        "http://127.0.0.1:4200"
    ];
}

static string DescribeSqlHost(string connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return "(missing DefaultConnection)";

    string? server = null;
    string? database = null;
    foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var idx = part.IndexOf('=');
        if (idx <= 0) continue;
        var key = part[..idx].Trim();
        var value = part[(idx + 1)..].Trim();
        if (key.Equals("Server", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Data Source", StringComparison.OrdinalIgnoreCase))
            server = value;
        if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
            database = value;
    }

    return $"Server={server ?? "?"}; Database={database ?? "?"}";
}

static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
        return;

    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#') || !line.Contains('='))
            continue;

        var idx = line.IndexOf('=');
        var key = line[..idx].Trim();
        var value = line[(idx + 1)..].Trim().Trim('"');
        if (key.Length == 0)
            continue;

        // Do not overwrite variables already set in the process/environment.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}
