using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RefactoringExercise.Application.Interfaces;
using RefactoringExercise.Application.Services;
using RefactoringExercise.Infrastructure.Email;
using RefactoringExercise.Infrastructure.Payments;
using RefactoringExercise.Infrastructure.Persistence;
using RefactoringExercise.Infrastructure.Repositories;

namespace RefactoringExercise.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPaymentProcessor, CreditCardPaymentProcessor>();
        services.AddScoped<IPaymentProcessor, PayPalPaymentProcessor>();
        services.AddScoped<IPaymentProcessor, BankTransferPaymentProcessor>();
        services.AddScoped<IOrderService, OrderService>();

        return services;
    }
}
