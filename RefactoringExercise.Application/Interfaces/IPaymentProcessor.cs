using RefactoringExercise.Application.DTOs;
using RefactoringExercise.Domain.Enums;

namespace RefactoringExercise.Application.Interfaces;

public interface IPaymentProcessor
{
    PaymentMethod SupportedMethod { get; }
    Task<PaymentResult> ProcessAsync(decimal amount, CancellationToken cancellationToken = default);
}
