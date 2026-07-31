using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using NileChain.AI.Agents;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;
using NileChain.AI.Services;

namespace NileChain.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddNileChainAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(sp =>
        {
            var key = Environment.GetEnvironmentVariable("OPENAI_KEY")
                      ?? configuration["OpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(key))
            {
                return new OpenAiKernelProvider(
                    kernel: null,
                    unavailableReason: "AI service is unavailable. Set OPENAI_KEY or OpenAI:ApiKey.");
            }

            var kernel = Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    configuration["OpenAI:Model"] ?? "gpt-4o",
                    key)
                .Build();

            return new OpenAiKernelProvider(kernel);
        });

        services.AddHttpClient<ChromaService>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["Chroma:BaseUrl"] ?? "http://localhost:8001");
        });

        services.AddScoped<RagPipeline>();

        services.AddScoped<MatchingPlugin>();
        services.AddScoped<RiskPlugin>();
        services.AddScoped<ContractPlugin>();

        services.AddScoped<MatchingAgent>();
        services.AddScoped<RiskAgent>();
        services.AddScoped<ContractAgent>();
        // Lazy so matching (/agent/run) does not resolve ContractAgent.
        services.AddScoped(sp =>
            new Lazy<ContractAgent>(() => sp.GetRequiredService<ContractAgent>()));
        services.AddScoped<OrchestratorAgent>();

        services.AddScoped<AIOrchestrationService>();

        return services;
    }
}
