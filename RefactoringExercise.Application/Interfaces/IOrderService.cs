using RefactoringExercise.Application.DTOs;

namespace RefactoringExercise.Application.Interfaces;

public interface IOrderService
{
    Task<ProcessOrderResult> ProcessOrderAsync(ProcessOrderRequest request, CancellationToken cancellationToken = default);
}
