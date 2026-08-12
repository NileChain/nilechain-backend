using NileChain.Application.Common;
using NileChain.Application.Interfaces;
using NileChain.Application.Services;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace NileChain.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IFarmService, FarmService>();
        services.AddScoped<IFactoryService, FactoryService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IMarketPriceService, MarketPriceService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IContractPdfService, ContractPdfService>();
        services.AddScoped<ICropRequestService, CropRequestService>();
        services.AddScoped<IFulfillmentService, FulfillmentService>();
        services.AddScoped<IPaymentMilestoneService, PaymentMilestoneService>();
        services.AddScoped<IMockEscrowPaymentService, MockEscrowPaymentService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IDisputeService, DisputeService>();
        services.AddScoped<IContractIntegrityService, ContractIntegrityService>();
        services.Configure<Options.PaymentMilestoneOptions>(
            configuration.GetSection(Options.PaymentMilestoneOptions.SectionName));
        services.Configure<Options.DeliveryTermsOptions>(
            configuration.GetSection(Options.DeliveryTermsOptions.SectionName));
        services.Configure<Options.MockPaymentOptions>(
            configuration.GetSection(Options.MockPaymentOptions.SectionName));
        services.Configure<Options.PaymobOptions>(
            configuration.GetSection(Options.PaymobOptions.SectionName));

        return services;
    }
}
