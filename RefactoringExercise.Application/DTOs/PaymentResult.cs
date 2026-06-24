namespace RefactoringExercise.Application.DTOs;

public sealed class PaymentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static PaymentResult Succeeded() => new() { Success = true };

    public static PaymentResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
