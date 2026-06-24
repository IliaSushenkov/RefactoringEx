using RefactoringExercise.Domain.Enums;

namespace RefactoringExercise.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Discount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus Status { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = [];
}
