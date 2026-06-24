using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using RefactoringExercise.Application.Interfaces;
using RefactoringExercise.Domain.Entities;
using RefactoringExercise.Domain.Enums;
using RefactoringExercise.Infrastructure.Persistence;

namespace RefactoringExercise.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(IDbConnectionFactory connectionFactory, ILogger<OrderRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string orderSql = """
            SELECT Id, TotalAmount, Discount, PaymentMethod, Status, CustomerEmail
            FROM Orders
            WHERE Id = @Id
            """;

        const string itemsSql = """
            SELECT ProductId, Quantity, UnitPrice
            FROM OrderItems
            WHERE OrderId = @OrderId
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        Order? order;
        await using (var command = new SqlCommand(orderSql, connection))
        {
            command.Parameters.AddWithValue("@Id", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            order = MapOrder(reader);
        }

        await using (var command = new SqlCommand(itemsSql, connection))
        {
            command.Parameters.AddWithValue("@OrderId", id);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                order!.Items.Add(MapOrderItem(reader));
            }
        }

        return order;
    }

    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        const string insertOrderSql = """
            INSERT INTO Orders (TotalAmount, Discount, PaymentMethod, Status, CustomerEmail)
            OUTPUT INSERTED.Id
            VALUES (@TotalAmount, @Discount, @PaymentMethod, @Status, @CustomerEmail)
            """;

        const string insertItemSql = """
            INSERT INTO OrderItems (OrderId, ProductId, Quantity, UnitPrice)
            VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)
            """;

        const string updateStockSql = """
            UPDATE Products
            SET StockQuantity = StockQuantity - @Quantity
            WHERE Id = @ProductId AND StockQuantity >= @Quantity
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            foreach (var item in order.Items)
            {
                await using var stockCommand = new SqlCommand(updateStockSql, connection, transaction);
                stockCommand.Parameters.AddWithValue("@ProductId", item.ProductId);
                stockCommand.Parameters.AddWithValue("@Quantity", item.Quantity);

                var stockRowsAffected = await stockCommand
                    .ExecuteNonQueryAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (stockRowsAffected == 0)
                {
                    throw new InvalidOperationException($"Insufficient stock for product {item.ProductId}.");
                }
            }

            int orderId;
            await using (var insertCommand = new SqlCommand(insertOrderSql, connection, transaction))
            {
                insertCommand.Parameters.AddWithValue("@TotalAmount", order.TotalAmount);
                insertCommand.Parameters.AddWithValue("@Discount", order.Discount);
                insertCommand.Parameters.AddWithValue("@PaymentMethod", order.PaymentMethod.ToString());
                insertCommand.Parameters.AddWithValue("@Status", order.Status.ToString());
                insertCommand.Parameters.AddWithValue("@CustomerEmail", order.CustomerEmail);

                var result = await insertCommand
                    .ExecuteScalarAsync(cancellationToken)
                    .ConfigureAwait(false);

                orderId = Convert.ToInt32(result);
            }

            foreach (var item in order.Items)
            {
                await using var itemCommand = new SqlCommand(insertItemSql, connection, transaction);
                itemCommand.Parameters.AddWithValue("@OrderId", orderId);
                itemCommand.Parameters.AddWithValue("@ProductId", item.ProductId);
                itemCommand.Parameters.AddWithValue("@Quantity", item.Quantity);
                itemCommand.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);

                await itemCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            order.Id = orderId;
            _logger.LogInformation("Created order {OrderId} with {ItemCount} items.", orderId, order.Items.Count);

            return order;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Failed to create order in database.");
            throw;
        }
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Orders
            SET TotalAmount = @TotalAmount,
                Discount = @Discount,
                PaymentMethod = @PaymentMethod,
                Status = @Status,
                CustomerEmail = @CustomerEmail
            WHERE Id = @Id
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", order.Id);
        command.Parameters.AddWithValue("@TotalAmount", order.TotalAmount);
        command.Parameters.AddWithValue("@Discount", order.Discount);
        command.Parameters.AddWithValue("@PaymentMethod", order.PaymentMethod.ToString());
        command.Parameters.AddWithValue("@Status", order.Status.ToString());
        command.Parameters.AddWithValue("@CustomerEmail", order.CustomerEmail);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Order {order.Id} was not found.");
        }

        _logger.LogDebug("Updated order {OrderId}.", order.Id);
    }

    private static Order MapOrder(SqlDataReader reader) =>
        new()
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
            Discount = reader.GetDecimal(reader.GetOrdinal("Discount")),
            PaymentMethod = Enum.Parse<PaymentMethod>(reader.GetString(reader.GetOrdinal("PaymentMethod"))),
            Status = Enum.Parse<OrderStatus>(reader.GetString(reader.GetOrdinal("Status"))),
            CustomerEmail = reader.GetString(reader.GetOrdinal("CustomerEmail"))
        };

    private static OrderItem MapOrderItem(SqlDataReader reader) =>
        new()
        {
            ProductId = reader.GetInt32(reader.GetOrdinal("ProductId")),
            Quantity = reader.GetInt32(reader.GetOrdinal("Quantity")),
            UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice"))
        };
}
