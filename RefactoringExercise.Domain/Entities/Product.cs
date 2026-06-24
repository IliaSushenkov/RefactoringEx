namespace RefactoringExercise.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public int StockQuantity { get; set; }
}
