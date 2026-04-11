using FluentValidation;
using GWT.Application.Interfaces.Services;
using GWT.Application.Services;
using GWT.Application.Validators;
using Microsoft.Extensions.DependencyInjection;

namespace GWT.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFundService, FundService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<INavSyncService, NavSyncService>();

        // Register all FluentValidation validators from this assembly
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        return services;
    }
}
