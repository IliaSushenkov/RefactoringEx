using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using RefactoringExercise.Application.Interfaces;
using RefactoringExercise.Domain.Entities;
using RefactoringExercise.Infrastructure.Persistence;

namespace RefactoringExercise.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(IDbConnectionFactory connectionFactory, ILogger<ProductRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var products = await GetByIdsAsync([id], cancellationToken).ConfigureAwait(false);
        return products.TryGetValue(id, out var product) ? product : null;
    }

    public async Task<IReadOnlyDictionary<int, Product>> GetByIdsAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, Product>();
        }

        var parameterNames = new string[ids.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            parameterNames[i] = $"@Id{i}";
        }

        var sql = $"""
            SELECT Id, ProductName, ProductPrice, StockQuantity
            FROM Products
            WHERE Id IN ({string.Join(", ", parameterNames)})
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new SqlCommand(sql, connection);
        for (var i = 0; i < ids.Length; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], ids[i]);
        }

        var products = new Dictionary<int, Product>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var product = MapProduct(reader);
            products[product.Id] = product;
        }

        _logger.LogDebug("Loaded {ProductCount} products from database.", products.Count);
        return products;
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Products
            SET ProductName = @ProductName,
                ProductPrice = @ProductPrice,
                StockQuantity = @StockQuantity
            WHERE Id = @Id
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", product.Id);
        command.Parameters.AddWithValue("@ProductName", product.ProductName);
        command.Parameters.AddWithValue("@ProductPrice", product.ProductPrice);
        command.Parameters.AddWithValue("@StockQuantity", product.StockQuantity);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Product {product.Id} was not found.");
        }

        _logger.LogDebug("Updated product {ProductId}.", product.Id);
    }

    private static Product MapProduct(SqlDataReader reader) =>
        new()
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            ProductName = reader.GetString(reader.GetOrdinal("ProductName")),
            ProductPrice = reader.GetDecimal(reader.GetOrdinal("ProductPrice")),
            StockQuantity = reader.GetInt32(reader.GetOrdinal("StockQuantity"))
        };
}
