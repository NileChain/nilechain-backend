using NileChain.Application.Auth;
using NileChain.Application.Interfaces;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;
using NileChain.Infrastructure.Authentication;
using NileChain.Infrastructure.Email;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Services;
using NileChain.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace NileChain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NileChainDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<NileChainDbContext>()
            .AddDefaultTokenProviders();


        //jwt configure
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()!;

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = jwtOptions.Issuer,

                        ValidAudience = jwtOptions.Audience,

                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwtOptions.Secret)),

                        ClockSkew = TimeSpan.Zero
                    };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<ApplicationUser>>();

                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (string.IsNullOrWhiteSpace(userId))
                        {
                            context.Fail("Invalid token subject.");
                            return;
                        }

                        var user = await userManager.FindByIdAsync(userId);
                        if (user is null)
                        {
                            context.Fail("User not found.");
                            return;
                        }

                        var isLockedOut = await userManager.IsLockedOutAsync(user);
                        if (!AuthTokenValidation.IsAccessAllowed(user, isLockedOut))
                        {
                            context.Fail("User is inactive or locked out.");
                        }
                    }
                };
            });

        services.Configure<CloudinaryOptions>(
                configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<IContractAttachmentService, ContractAttachmentService>();

        services.Configure<EmailOptions>(
                configuration.GetSection(EmailOptions.SectionName));
        services.AddScoped<IEmailService, SmtpEmailService>();

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<IFactoryRepository, FactoryRepository>();
        services.AddScoped<IFulfillmentRepository, FulfillmentRepository>();
        services.AddScoped<IPaymentMilestoneRepository, PaymentMilestoneRepository>();
        services.AddScoped<IEscrowTransactionRepository, EscrowTransactionRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IDisputeRepository, DisputeRepository>();
        services.AddScoped<IChannelMessageRepository, ChannelMessageRepository>();
        services.AddScoped<IContractIntegrityRepository, ContractIntegrityRepository>();
        services.AddScoped<ISigningOtpRepository, SigningOtpRepository>();
        services.AddScoped<IContractSignatureRepository, ContractSignatureRepository>();
        services.AddScoped<IAdminAnalyticsRepository, AdminAnalyticsRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ITemplateRenderer, TemplateRendererService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserAccountDeletionService, UserAccountDeletionService>();

        services.AddHttpClient<IPaymobClient, NileChain.Infrastructure.Paymob.PaymobClient>();

        return services;
    }
}
