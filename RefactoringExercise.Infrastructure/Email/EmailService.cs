using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RefactoringExercise.Application.Interfaces;

namespace RefactoringExercise.Infrastructure.Email;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendOrderConfirmationAsync(
        string customerEmail,
        int orderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerEmail);

        _logger.LogInformation(
            "Sending confirmation for order {OrderId} to {CustomerEmail} via {SmtpHost}:{SmtpPort} from {FromAddress}.",
            orderId,
            customerEmail,
            _options.SmtpHost,
            _options.SmtpPort,
            _options.FromAddress);

        return Task.CompletedTask;
    }
}
