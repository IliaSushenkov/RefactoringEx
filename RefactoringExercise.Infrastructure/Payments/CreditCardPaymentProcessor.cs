using Microsoft.Extensions.Logging;
using RefactoringExercise.Application.DTOs;
using RefactoringExercise.Application.Interfaces;
using RefactoringExercise.Domain.Enums;

namespace RefactoringExercise.Infrastructure.Payments;

public class CreditCardPaymentProcessor : IPaymentProcessor
{
    private readonly ILogger<CreditCardPaymentProcessor> _logger;

    public CreditCardPaymentProcessor(ILogger<CreditCardPaymentProcessor> logger)
    {
        _logger = logger;
    }

    public PaymentMethod SupportedMethod => PaymentMethod.CreditCard;

    public Task<PaymentResult> ProcessAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            _logger.LogWarning(
                "Credit card payment less than or equal to 0: {Amount}.",
                amount);
            return Task.FromResult(PaymentResult.Failed("Credit card must be greater than 0"));
        }

        _logger.LogInformation(
            "Credit card payment processed for amount {Amount}.",
            amount);
        return Task.FromResult(PaymentResult.Succeeded());
    }
}
