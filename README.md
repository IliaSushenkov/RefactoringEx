RefactoringExercise

Overview

This project is a refactoring of the provided legacy OrderProcessor implementation.

The original code was responsible for many different concerns at once:

* database access
* order calculations
* payment processing
* email notifications
* validation
* error handling

The goal of this refactoring was not to redesign the system from scratch, but to improve maintainability, testability and security while keeping the original business flow intact.

The solution targets .NET 10 and uses a simple layered architecture.


What I changed

Separated responsibilities

The original OrderProcessor class acted as a God Object and handled the entire workflow.

The logic has been split into dedicated components:

* OrderService orchestrates the order processing flow
* repositories handle database access
* payment processors handle payment-specific behavior
* email service handles notifications
* validation is performed before processing begins

This makes individual parts easier to understand, test and maintain.

Removed SQL injection vulnerabilities

The original implementation built SQL statements using string concatenation.

All database operations now use parameterized SQL commands.

Removed hardcoded configuration

Database connection strings and email settings have been moved to configuration files and are injected through IOptions<T>.

Added dependency injection

All infrastructure dependencies are injected through interfaces, making the application easier to test and extend.

Added transactions

Creating an order, saving order items and updating stock levels are performed inside a single database transaction.

This ensures the database remains consistent if an error occurs during processing.

Added logging

Structured logging has been added throughout the order processing flow to simplify troubleshooting and monitoring.

Added automated tests

Unit tests cover the main business scenarios including:

* successful order processing
* payment failures
* invalid requests
* insufficient stock
* discount calculations

Project Structure

Project	Responsibility
RefactoringExercise.Domain	Domain entities and enums
RefactoringExercise.Application	Business logic, DTOs, validation and contracts
RefactoringExercise.Infrastructure	Database access, payment processing, email services and dependency registration
RefactoringExercise.API	HTTP API entry point
RefactoringExercise.Tests	Unit tests

How Order Processing Works

When an order is submitted:

1. The request is validated.
2. All requested products are loaded from the database in a single query.
3. Product availability is checked.
4. The order total is calculated.
5. The requested discount is applied.
6. The appropriate payment processor is selected.
7. Payment is processed.
8. The order is stored together with its order items.
9. Stock levels are updated.
10. A confirmation email is sent.
11. A result is returned to the client.

Payment Processing

The payment logic uses a simple Strategy Pattern implementation.

Each payment method has its own processor:

* CreditCardPaymentProcessor
* PayPalPaymentProcessor
* BankTransferPaymentProcessor

This removes payment-specific conditional logic from OrderService and makes it easier to add new payment methods in the future.

API Example

POST /api/orders

{
  "customerEmail": "customer@example.com",
  "discount": 10,
  "paymentMethod": 0,
  "items": [
    {
      "productId": 1,
      "quantity": 2
    },
    {
      "productId": 2,
      "quantity": 1
    }
  ]
}

Payment methods:

Value	Method
0	Credit Card
1	PayPal
2	Bank Transfer

Database Schema

CREATE TABLE Products (
    Id INT PRIMARY KEY,
    ProductName NVARCHAR(200) NOT NULL,
    ProductPrice DECIMAL(18,2) NOT NULL,
    StockQuantity INT NOT NULL
);
CREATE TABLE Orders (
    Id INT PRIMARY KEY IDENTITY(1,1),
    TotalAmount DECIMAL(18,2) NOT NULL,
    Discount DECIMAL(18,2) NOT NULL,
    PaymentMethod NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    CustomerEmail NVARCHAR(256) NOT NULL
);
CREATE TABLE OrderItems (
    OrderId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL
);

Future Improvements

If this were a production system rather than a refactoring exercise, there are several areas I would consider improving further:

Stronger identifiers

Currently product and order identifiers use integer keys.

For distributed systems I would consider using GUIDs or UUIDs to avoid predictable identifiers and simplify data generation across multiple services.

Richer domain model

The current implementation intentionally keeps the domain simple.

A production system could introduce dedicated value objects for concepts such as:

* Money
* EmailAddress
* OrderItem

This would improve domain consistency and validation.

Real payment integrations

Payment processors currently simulate external payment providers.

In a real system they would communicate with external APIs and include retry policies, timeout handling and resilience mechanisms.

Email delivery

The email service currently focuses on the application workflow.

A production implementation would include:

* SMTP integration
* retry policies
* delivery tracking
* background processing

Observability

Additional improvements could include:

* distributed tracing
* metrics collection
* health checks
* centralized log aggregation

Running the Application

dotnet build
dotnet test
dotnet run --project RefactoringExercise.API