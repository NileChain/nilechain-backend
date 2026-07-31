using NileChain.AI;
using NileChain.API.Extensions;
using NileChain.Application;
using NileChain.Domain.Identity;
using NileChain.Infrastructure;
using NileChain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await IdentitySeeder.SeedAsync(roleManager, userManager);

    if (app.Environment.IsDevelopment())
    {
        var db = scope.ServiceProvider.GetRequiredService<NileChainDbContext>();
        await DevelopmentDataSeeder.SeedAsync(db, userManager);
    }
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("NileChain API")
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
});
app.UseHttpsRedirection();
app.UseCors("AngularPolicy");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
