namespace RefactoringExercise.Application.Interfaces;

public interface IEmailService
{
    Task SendOrderConfirmationAsync(string customerEmail, int orderId, CancellationToken cancellationToken = default);
}
