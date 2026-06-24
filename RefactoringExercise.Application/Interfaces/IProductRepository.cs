using RefactoringExercise.Domain.Entities;

namespace RefactoringExercise.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, Product>> GetByIdsAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
}
