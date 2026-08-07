using NileChain.AI;
using NileChain.AI.RAG;
using NileChain.API.Extensions;
using NileChain.Application;
using NileChain.Domain.Identity;
using NileChain.Infrastructure;
using NileChain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

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
        policy.WithOrigins("http://localhost:4200")
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

        if (app.Environment.IsDevelopment()
            && !string.Equals(
                Environment.GetEnvironmentVariable("SKIP_DEMO_SEED"),
                "1",
                StringComparison.OrdinalIgnoreCase))
        {
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("DevelopmentDataSeeder");
            var db = scope.ServiceProvider.GetRequiredService<NileChainDbContext>();
            await DevelopmentDataSeeder.SeedAsync(db, userManager, logger);

            var chromaLogger = loggerFactory.CreateLogger("ChromaKnowledgeSeeder");
            var chroma = scope.ServiceProvider.GetRequiredService<ChromaService>();
            await ChromaKnowledgeSeeder.SeedAsync(chroma, chromaLogger);
        }
        else if (app.Environment.IsDevelopment())
        {
            var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Startup");
            startupLogger.LogWarning("SKIP_DEMO_SEED=1 — skipping DevelopmentDataSeeder / Chroma seed.");
        }
    }
    catch (Exception ex)
    {
        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");
        startupLogger.LogError(
            ex,
            "Startup seeding failed (DB/Chroma). API will still listen; data-dependent endpoints may fail.");
    }
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
