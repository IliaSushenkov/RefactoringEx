using RefactoringExercise.Domain.Enums;

namespace RefactoringExercise.Application.DTOs;

public class ProcessOrderResult
{
    public bool IsSuccess { get; set; }
    public int? OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public string? Message { get; set; }
}
