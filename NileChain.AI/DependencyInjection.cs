using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NileChain.AI.Agents;
using NileChain.AI.Plugins;
using NileChain.AI.RAG;
using NileChain.AI.Services;
using NileChain.AI.Sbg;
using NileChain.AI.Weather;
using NileChain.Application.Interfaces;
using NileChain.Domain.Interfaces;

namespace NileChain.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddNileChainAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SbgOptions>(configuration.GetSection(SbgOptions.SectionName));

        // Overlay env / OpenAI key into Sbg options for local .env workflows.
        services.PostConfigure<SbgOptions>(opts =>
        {
            var configured = FirstNonEmpty(
                opts.BaseUrl,
                configuration["Sbg:BaseUrl"],
                Environment.GetEnvironmentVariable("SBG_BASE_URL"));

            if (!string.IsNullOrWhiteSpace(configured))
                opts.BaseUrl = LlmKernelFactory.NormalizeSbgOrigin(configured);

            if (string.IsNullOrWhiteSpace(opts.ApiKey))
                opts.ApiKey = LlmKernelFactory.ResolveSbgApiKey(configuration) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(opts.ModelId))
                opts.ModelId = LlmKernelFactory.ResolveSbgModel(configuration);
        });

        services.AddHttpClient<SbgStudentChatClient>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        // Scoped: typed HttpClient (SbgStudentChatClient) must not be captured by a singleton.
        services.AddScoped(sp =>
        {
            var sbg = sp.GetService<SbgStudentChatClient>();
            var kernel = LlmKernelFactory.CreateKernel(
                configuration,
                out var unavailableReason,
                out var supportsNativeToolCalling,
                out var providerName,
                sbg);

            return kernel is null
                ? new OpenAiKernelProvider(
                    kernel: null,
                    unavailableReason: unavailableReason,
                    supportsNativeToolCalling: false,
                    providerName: providerName)
                : new OpenAiKernelProvider(
                    kernel,
                    supportsNativeToolCalling: supportsNativeToolCalling,
                    providerName: providerName);
        });

        services.AddHttpClient<ChromaService>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["Chroma:BaseUrl"] ?? "http://localhost:8001");
        });

        services.AddScoped<RagPipeline>();

        services.AddScoped<IRagIndexer, ChromaRagIndexer>();

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
        services.AddScoped<ProactiveMonitorAgent>();
        services.AddScoped<CopilotChatService>();
        services.AddHttpClient<IWeatherRiskClient, OpenMeteoWeatherClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(6);
        });

        services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));

        services.AddScoped<AIOrchestrationService>();
        services.AddScoped<IContractTextReviser, ContractTextReviser>();

        return services;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
