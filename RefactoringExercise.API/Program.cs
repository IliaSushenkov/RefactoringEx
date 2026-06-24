using RefactoringExercise.Application.DTOs;
using RefactoringExercise.Application.Interfaces;
using RefactoringExercise.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/api/orders", async (
    ProcessOrderRequest request,
    IOrderService orderService,
    CancellationToken cancellationToken) =>
{
    var result = await orderService.ProcessOrderAsync(request, cancellationToken);
    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
});

app.Run();
