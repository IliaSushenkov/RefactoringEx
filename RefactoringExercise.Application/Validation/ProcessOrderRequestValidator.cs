using System.Net.Mail;
using RefactoringExercise.Application.DTOs;

namespace RefactoringExercise.Application.Validation;

public static class ProcessOrderRequestValidator
{
    public const decimal MinDiscount = 0m;
    public const decimal MaxDiscount = 100m;

    public static string? Validate(ProcessOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
            return "Email is required.";

        if (!IsValidEmail(request.CustomerEmail))
            return "Invalid email address.";

        if (request.Items is null || request.Items.Count == 0)
            return "Order cannot be empty.";

        var itemsValidationResult = ValidateItems(request.Items);

        if (itemsValidationResult is not null)
            return itemsValidationResult;

        if (request.Discount < MinDiscount || request.Discount > MaxDiscount)
            return $"Discount must be between {MinDiscount} and {MaxDiscount} percent.";

        if (!Enum.IsDefined(request.PaymentMethod))
            return "Unsupported payment method.";

        return null;
    }

    private static string? ValidateItems(IEnumerable<ProcessOrderItem> items)
    {
        foreach (var item in items)
        {
            if (item.ProductId <= 0)
                return "Product id must be greater than zero.";
            if (item.Quantity <= 0)
                return "Quantity must be greater than zero.";
        }
        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
