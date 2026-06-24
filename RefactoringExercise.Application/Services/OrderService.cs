using Microsoft.Extensions.Logging;
using RefactoringExercise.Application.DTOs;
using RefactoringExercise.Application.Interfaces;
using RefactoringExercise.Application.Validation;
using RefactoringExercise.Domain.Entities;
using RefactoringExercise.Domain.Enums;

namespace RefactoringExercise.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IEmailService _emailService;
    private readonly IEnumerable<IPaymentProcessor> _paymentProcessors;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IProductRepository productRepository,
        IEmailService emailService,
        IEnumerable<IPaymentProcessor> paymentProcessors,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _emailService = emailService;
        _paymentProcessors = paymentProcessors;
        _logger = logger;
    }

    public async Task<ProcessOrderResult> ProcessOrderAsync(
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Processing order for customer {CustomerEmail}. Items: {ItemCount}",
            request.CustomerEmail,
            request.Items.Count);

        var validationError = ProcessOrderRequestValidator.Validate(request);
        if (validationError is not null)
        {
            _logger.LogWarning(
                "Order validation failed: {ValidationError}",
                validationError);
            return Failure(OrderStatus.Failed, validationError);
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct();
        IReadOnlyDictionary<int, Product> products;
        try
        {
            products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database failure while loading products.");
            return Failure(OrderStatus.Failed, "Failed to load products");
        }

        foreach (var item in request.Items)
        {
            if (!products.ContainsKey(item.ProductId))
            {
                _logger.LogWarning("Product {ProductId} was not found.", item.ProductId);
                return Failure(OrderStatus.Failed, $"Product {item.ProductId} was not found");
            }
        }

        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];
            if (product.StockQuantity < item.Quantity)
            {
                _logger.LogWarning(
                    "Insufficient stock for product {ProductId}. Requested {Requested}, available {Available}.",
                    item.ProductId,
                    item.Quantity,
                    product.StockQuantity);
                return Failure(OrderStatus.Failed, $"Insufficient stock for product {item.ProductId}");
            }
        }

        var total = CalculateTotal(request, products);

        var paymentProcessor = _paymentProcessors.SingleOrDefault(p => p.SupportedMethod == request.PaymentMethod);
        if (paymentProcessor is null)
        {
            _logger.LogWarning(
                "No payment processor registered for {PaymentMethod}.",
                request.PaymentMethod);
            return Failure(OrderStatus.Failed, "Payment method is not supported");
        }

        var paymentResult = await paymentProcessor.ProcessAsync(total, cancellationToken);
        if (!paymentResult.Success)
        {
            _logger.LogWarning(
                "Payment failed for {CustomerEmail} using {PaymentMethod}: {PaymentError}.",
                request.CustomerEmail,
                request.PaymentMethod,
                paymentResult.ErrorMessage);
            return Failure(OrderStatus.Failed, paymentResult.ErrorMessage ?? "Payment processing failed");
        }

        var order = new Order
        {
            TotalAmount = total,
            Discount = request.Discount,
            PaymentMethod = request.PaymentMethod,
            Status = OrderStatus.Paid,
            CustomerEmail = request.CustomerEmail,
            Items = request.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = products[item.ProductId].ProductPrice
            }).ToList()
        };

        Order createdOrder;
        try
        {
            createdOrder = await _orderRepository.CreateAsync(order, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database failure while saving order for {CustomerEmail}.", request.CustomerEmail);
            return Failure(OrderStatus.Failed, "Failed to save order.");
        }

        try
        {
            await _emailService.SendOrderConfirmationAsync(
                request.CustomerEmail,
                createdOrder.Id,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email failure for order {OrderId}.", createdOrder.Id);
            return new ProcessOrderResult
            {
                IsSuccess = true,
                OrderId = createdOrder.Id,
                Status = OrderStatus.Paid,
                Message = "Order processed, but confirmation email could not be sent"
            };
        }

        _logger.LogInformation(
            "Order processing completed. OrderId: {OrderId}, CustomerEmail: {CustomerEmail}, TotalAmount: {TotalAmount}.",
            createdOrder.Id,
            request.CustomerEmail,
            total);
        return new ProcessOrderResult
        {
            IsSuccess = true,
            OrderId = createdOrder.Id,
            Status = OrderStatus.Paid,
            Message = "Order processed successfully."
        };
    }

    public static decimal CalculateTotal(
        ProcessOrderRequest request,
        IReadOnlyDictionary<int, Product> products)
    {
        var subtotal = request.Items.Sum(item => products[item.ProductId].ProductPrice * item.Quantity);
        return subtotal - subtotal * (request.Discount / 100m);
    }

    private static ProcessOrderResult Failure(OrderStatus status, string message) =>
        new()
        {
            IsSuccess = false,
            Status = status,
            Message = message
        };
}
