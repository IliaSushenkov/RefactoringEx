using RefactoringExercise.Domain.Enums;

namespace RefactoringExercise.Application.DTOs;

public class ProcessOrderRequest
{
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal Discount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public List<ProcessOrderItem> Items { get; set; } = [];
}
