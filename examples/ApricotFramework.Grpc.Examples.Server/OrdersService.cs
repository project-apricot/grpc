using ApricotFramework.ErrorDefinitions;
using ApricotFramework.Grpc.Examples.Contracts.Orders;
using Grpc.Core;

namespace ApricotFramework.Grpc.Examples.Server;

/// <summary>
/// A service that fails in each of the ways the error contract has an answer for.
/// </summary>
public class OrdersService : Orders.OrdersBase
{
    /// <summary>
    /// The one order that exists.
    /// </summary>
    private static readonly Order Known = new() { Id = "A-1", CustomerEmail = "buyer@example.com", Quantity = 2 };

    /// <inheritdoc />
    public override Task<Order> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Id == "boom")
        {
            // Nothing maps this, so the caller is told it was internal and nothing else.
            throw new InvalidOperationException("the order database is unreachable: Password=hunter2");
        }

        if (request.Id != Known.Id)
        {
            throw Err.NotFound("ORDER_NOT_FOUND",
                    $"no order with id '{request.Id}'",
                    new Dictionary<string, object?> { ["orderId"] = request.Id })
                .AsException();
        }

        return Task.FromResult(Known);
    }

    /// <inheritdoc />
    public override Task<Order> PlaceOrder(PlaceOrderRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new ErrorCollector();

        errors.AddIf(
            !request.CustomerEmail.Contains('@', StringComparison.Ordinal),
            Err.Validation("EMAIL_INVALID", "the customer email is not an address", new Dictionary<string, object?> { ["field"] = "customer_email" }));

        errors.AddIf(
            request.Quantity <= 0,
            Err.Validation("QUANTITY_TOO_LOW", "at least one item is needed", new Dictionary<string, object?> { ["field"] = "quantity", ["minimum"] = 1 }));

        errors.ThrowIfAny();

        return Task.FromResult(new Order
        {
            Id = "A-2",
            CustomerEmail = request.CustomerEmail,
            Quantity = request.Quantity,
        });
    }
}
