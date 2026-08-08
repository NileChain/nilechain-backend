using NileChain.AI;
using NileChain.AI.RAG;
using NileChain.API.Extensions;
using NileChain.Application;
using NileChain.Domain.Identity;
using NileChain.Infrastructure;
using NileChain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddNileChainAI(builder.Configuration);
builder.Services.AddHostedService<NileChain.API.HostedServices.ProactiveMonitorHostedService>();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<NileChain.API.Filters.FluentValidationActionFilter>();
});
builder.Services.AddScoped<NileChain.API.Filters.FluentValidationActionFilter>();
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:4200",
                  "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
var app = builder.Build();

app.UseGlobalExceptionMiddleware();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await IdentitySeeder.SeedAsync(roleManager, userManager);

        var seedOnStartup = string.Equals(
            Environment.GetEnvironmentVariable("SEED_DEMO_ON_STARTUP"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        if (seedDemoOnly || seedOnStartup)
        {
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
            var db = scope.ServiceProvider.GetRequiredService<NileChainDbContext>();
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

app.MapOpenApi();
// Scalar UI omitted: Application Control in some environments blocks Scalar.AspNetCore.dll load.
// CORS before HTTPS redirection so browser preflight from Angular is not stripped by redirects.
app.UseCors("AngularPolicy");
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

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
