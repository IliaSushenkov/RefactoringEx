using Microsoft.Extensions.Logging;
using RefactoringExercise.Application.DTOs;
using RefactoringExercise.Application.Interfaces;
using RefactoringExercise.Domain.Enums;

namespace RefactoringExercise.Infrastructure.Payments;

public class BankTransferPaymentProcessor : IPaymentProcessor
{
    private readonly ILogger<BankTransferPaymentProcessor> _logger;

    public BankTransferPaymentProcessor(ILogger<BankTransferPaymentProcessor> logger)
    {
        _logger = logger;
    }

    public PaymentMethod SupportedMethod => PaymentMethod.BankTransfer;

    public Task<PaymentResult> ProcessAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            _logger.LogWarning("Bank transfer payment rejected for non-positive amount {Amount}.", amount);
            return Task.FromResult(PaymentResult.Failed("Bank transfer payment amount must be greater than zero."));
        }

        _logger.LogInformation("Bank transfer payment processed for amount {Amount}.", amount);
        return Task.FromResult(PaymentResult.Succeeded());
    }
}
