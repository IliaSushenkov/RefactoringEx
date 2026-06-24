using Microsoft.Extensions.Logging;
using Moq;
using RefactoringExercise.Application.DTOs;
using RefactoringExercise.Application.Interfaces;
using RefactoringExercise.Application.Services;
using RefactoringExercise.Domain.Entities;
using RefactoringExercise.Domain.Enums;

namespace RefactoringExercise.Tests.Services;

public class OrderServiceTests
{
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<OrderService>> _logger = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();

    [Fact]
    public async Task ProcessOrderAsync_ReturnsSuccess()
    {
        var creditCardProcessor = CreateProcessor(PaymentMethod.CreditCard, PaymentResult.Succeeded());
        var service = CreateService(creditCardProcessor);

        SetupProducts(
            new Product { Id = 3, ProductName = "Bread", ProductPrice = 12.50m, StockQuantity = 80 },
            new Product { Id = 7, ProductName = "Butter", ProductPrice = 4.75m, StockQuantity = 40 });

        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order order, CancellationToken _) =>
            {
                order.Id = 1001;
                return order;
            });

        var result = await service.ProcessOrderAsync(CreateRequest(
            email: "anna.k@example.com",
            items:
            [
                new ProcessOrderItem { ProductId = 3, Quantity = 3 },
                new ProcessOrderItem { ProductId = 7, Quantity = 2 }
            ],
            discount: 0m));

        Assert.True(result.IsSuccess);
        Assert.Equal(1001, result.OrderId);
        Assert.Equal(OrderStatus.Paid, result.Status);
        Assert.Equal("Order processed successfully.", result.Message);
        _emailService.Verify(
            e => e.SendOrderConfirmationAsync("anna.k@example.com", 1001, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_WithDiscount()
    {
        decimal capturedAmount = 0;
        var creditCardProcessor = CreateProcessor(
            PaymentMethod.CreditCard,
            PaymentResult.Succeeded(),
            amount => capturedAmount = amount);
        var service = CreateService(creditCardProcessor);

        SetupProducts(new Product { Id = 5, ProductName = "Cheese", ProductPrice = 25m, StockQuantity = 30 });

        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order order, CancellationToken _) =>
            {
                order.Id = 55;
                return order;
            });

        await service.ProcessOrderAsync(CreateRequest(
            email: "buyer@shop.test",
            items: [new ProcessOrderItem { ProductId = 5, Quantity = 4 }],
            discount: 15m));

        Assert.Equal(85m, capturedAmount);
    }

    [Fact]
    public void CalculateTotal_AppliesDiscountOnce_ForMultipleItems()
    {
        var request = new ProcessOrderRequest
        {
            Discount = 25m,
            Items =
            [
                new ProcessOrderItem { ProductId = 10, Quantity = 3 },
                new ProcessOrderItem { ProductId = 20, Quantity = 2 }
            ]
        };

        var products = new Dictionary<int, Product>
        {
            [10] = new() { Id = 10, ProductPrice = 10m },
            [20] = new() { Id = 20, ProductPrice = 6m }
        };

        var total = OrderService.CalculateTotal(request, products);

        Assert.Equal(31.5m, total);
    }

    [Fact]
    public void CalculateTotal_ReturnsFullSubtotal_WhenDiscountIsZero()
    {
        var request = new ProcessOrderRequest
        {
            Discount = 0m,
            Items =
            [
                new ProcessOrderItem { ProductId = 1, Quantity = 2 },
                new ProcessOrderItem { ProductId = 2, Quantity = 1 }
            ]
        };

        var products = new Dictionary<int, Product>
        {
            [1] = new() { Id = 1, ProductPrice = 6.50m },
            [2] = new() { Id = 2, ProductPrice = 3.25m }
        };

        var total = OrderService.CalculateTotal(request, products);

        Assert.Equal(16.25m, total);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenEmailIsInvalid()
    {
        var service = CreateService();

        var result = await service.ProcessOrderAsync(CreateRequest(
            email: "not-valid",
            items: [new ProcessOrderItem { ProductId = 1, Quantity = 1 }]));

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderStatus.Failed, result.Status);
        Assert.Equal("Invalid email address.", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenEmailIsMissing()
    {
        var service = CreateService();

        var result = await service.ProcessOrderAsync(CreateRequest(
            email: "   ",
            items: [new ProcessOrderItem { ProductId = 1, Quantity = 1 }]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Email is required.", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenItemsAreEmpty()
    {
        var service = CreateService();

        var result = await service.ProcessOrderAsync(CreateRequest(items: []));

        Assert.False(result.IsSuccess);
        Assert.Equal("Order cannot be empty.", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenDiscountIsOutOfRange()
    {
        var service = CreateService();

        var result = await service.ProcessOrderAsync(CreateRequest(
            discount: 150m,
            items: [new ProcessOrderItem { ProductId = 1, Quantity = 1 }]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Discount must be between 0 and 100 percent.", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenPaymentMethodIsInvalid()
    {
        var service = CreateService();

        var result = await service.ProcessOrderAsync(new ProcessOrderRequest
        {
            CustomerEmail = "user@mail.com",
            Discount = 5m,
            PaymentMethod = (PaymentMethod)999,
            Items = [new ProcessOrderItem { ProductId = 2, Quantity = 2 }]
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("Unsupported payment method.", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenProductNotFound()
    {
        var service = CreateService();

        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, Product>());

        var result = await service.ProcessOrderAsync(CreateRequest(
            items: [new ProcessOrderItem { ProductId = 404, Quantity = 1 }]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Product 404 was not found", result.Message);
        _orderRepository.Verify(
            r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenStockIsInsufficient()
    {
        var service = CreateService();

        SetupProducts(new Product { Id = 8, ProductName = "Juice", ProductPrice = 7.99m, StockQuantity = 2 });

        var result = await service.ProcessOrderAsync(CreateRequest(
            items: [new ProcessOrderItem { ProductId = 8, Quantity = 6 }]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient stock for product 8", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenPaymentFails()
    {
        var creditCardProcessor = CreateProcessor(
            PaymentMethod.CreditCard,
            PaymentResult.Failed("Insufficient funds."));
        var service = CreateService(creditCardProcessor);

        SetupProducts(new Product { Id = 12, ProductName = "Coffee", ProductPrice = 18m, StockQuantity = 25 });

        var result = await service.ProcessOrderAsync(CreateRequest(
            email: "payer@test.io",
            items: [new ProcessOrderItem { ProductId = 12, Quantity = 2 }]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Insufficient funds.", result.Message);
        _orderRepository.Verify(
            r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenProductLoadFails()
    {
        var service = CreateService();

        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection lost"));

        var result = await service.ProcessOrderAsync(CreateRequest(
            items: [new ProcessOrderItem { ProductId = 1, Quantity = 1 }]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to load products", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsFailure_WhenOrderSaveFails()
    {
        var creditCardProcessor = CreateProcessor(PaymentMethod.CreditCard, PaymentResult.Succeeded());
        var service = CreateService(creditCardProcessor);

        SetupProducts(new Product { Id = 6, ProductName = "Tea", ProductPrice = 9m, StockQuantity = 50 });

        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Write failed"));

        var result = await service.ProcessOrderAsync(CreateRequest(
            items: [new ProcessOrderItem { ProductId = 6, Quantity = 1 }]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to save order.", result.Message);
        _emailService.Verify(
            e => e.SendOrderConfirmationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_ReturnsPartialSuccess_WhenEmailFails()
    {
        var creditCardProcessor = CreateProcessor(PaymentMethod.CreditCard, PaymentResult.Succeeded());
        var service = CreateService(creditCardProcessor);

        SetupProducts(new Product { Id = 9, ProductName = "Honey", ProductPrice = 14m, StockQuantity = 15 });

        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order order, CancellationToken _) =>
            {
                order.Id = 900;
                return order;
            });

        _emailService
            .Setup(e => e.SendOrderConfirmationAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var result = await service.ProcessOrderAsync(CreateRequest(
            email: "honey@buyer.com",
            items: [new ProcessOrderItem { ProductId = 9, Quantity = 1 }]));

        Assert.True(result.IsSuccess);
        Assert.Equal(900, result.OrderId);
        Assert.Equal(OrderStatus.Paid, result.Status);
        Assert.Equal("Order processed, but confirmation email could not be sent", result.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_UsesMatchingPaymentProcessor()
    {
        var creditCardProcessor = CreateProcessor(PaymentMethod.CreditCard, PaymentResult.Succeeded());
        var payPalProcessor = CreateProcessor(
            PaymentMethod.PayPal,
            PaymentResult.Succeeded(),
            _ => { });

        var service = CreateService(creditCardProcessor, payPalProcessor);

        SetupProducts(new Product { Id = 15, ProductName = "Olive Oil", ProductPrice = 22m, StockQuantity = 60 });

        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order order, CancellationToken _) =>
            {
                order.Id = 201;
                return order;
            });

        await service.ProcessOrderAsync(CreateRequest(
            email: "paypal.user@example.com",
            paymentMethod: PaymentMethod.PayPal,
            items: [new ProcessOrderItem { ProductId = 15, Quantity = 2 }]));

        payPalProcessor.Verify(
            p => p.ProcessAsync(44m, It.IsAny<CancellationToken>()),
            Times.Once);
        creditCardProcessor.Verify(
            p => p.ProcessAsync(It.IsAny<decimal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_UsesBankTransferProcessor()
    {
        var bankTransferProcessor = CreateProcessor(PaymentMethod.BankTransfer, PaymentResult.Succeeded());
        var service = CreateService(bankTransferProcessor);

        SetupProducts(new Product { Id = 4, ProductName = "Rice", ProductPrice = 11m, StockQuantity = 100 });

        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order order, CancellationToken _) =>
            {
                order.Id = 77;
                return order;
            });

        var result = await service.ProcessOrderAsync(CreateRequest(
            paymentMethod: PaymentMethod.BankTransfer,
            discount: 5m,
            items: [new ProcessOrderItem { ProductId = 4, Quantity = 10 }]));

        Assert.True(result.IsSuccess);
        bankTransferProcessor.Verify(
            p => p.ProcessAsync(104.5m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_PersistsOrderWithDiscountAndLineItems()
    {
        var creditCardProcessor = CreateProcessor(PaymentMethod.CreditCard, PaymentResult.Succeeded());
        var service = CreateService(creditCardProcessor);

        SetupProducts(
            new Product { Id = 1, ProductName = "Apples", ProductPrice = 5m, StockQuantity = 50 },
            new Product { Id = 2, ProductName = "Pears", ProductPrice = 6m, StockQuantity = 50 });

        Order? capturedOrder = null;
        _orderRepository
            .Setup(r => r.CreateAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((order, _) => capturedOrder = order)
            .ReturnsAsync((Order order, CancellationToken _) =>
            {
                order.Id = 33;
                return order;
            });

        await service.ProcessOrderAsync(CreateRequest(
            discount: 10m,
            items:
            [
                new ProcessOrderItem { ProductId = 1, Quantity = 2 },
                new ProcessOrderItem { ProductId = 2, Quantity = 1 }
            ]));

        Assert.NotNull(capturedOrder);
        Assert.Equal(10m, capturedOrder!.Discount);
        Assert.Equal(14.4m, capturedOrder.TotalAmount);
        Assert.Equal(2, capturedOrder.Items.Count);
        Assert.Equal(5m, capturedOrder.Items[0].UnitPrice);
        Assert.Equal(6m, capturedOrder.Items[1].UnitPrice);
    }

    private static ProcessOrderRequest CreateRequest(
        string email = "customer@example.com",
        decimal discount = 0m,
        PaymentMethod paymentMethod = PaymentMethod.CreditCard,
        List<ProcessOrderItem>? items = null) =>
        new()
        {
            CustomerEmail = email,
            Discount = discount,
            PaymentMethod = paymentMethod,
            Items = items ?? [new ProcessOrderItem { ProductId = 1, Quantity = 1 }]
        };

    private static Mock<IPaymentProcessor> CreateProcessor(
        PaymentMethod method,
        PaymentResult result,
        Action<decimal>? onProcess = null)
    {
        var processor = new Mock<IPaymentProcessor>();
        processor.Setup(p => p.SupportedMethod).Returns(method);
        processor
            .Setup(p => p.ProcessAsync(It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Callback<decimal, CancellationToken>((amount, _) => onProcess?.Invoke(amount))
            .ReturnsAsync(result);
        return processor;
    }

    private void SetupProducts(params Product[] products)
    {
        _productRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(products.ToDictionary(p => p.Id));
    }

    private OrderService CreateService(params Mock<IPaymentProcessor>[] paymentProcessors) =>
        new(
            _orderRepository.Object,
            _productRepository.Object,
            _emailService.Object,
            paymentProcessors.Select(p => p.Object),
            _logger.Object);
}
