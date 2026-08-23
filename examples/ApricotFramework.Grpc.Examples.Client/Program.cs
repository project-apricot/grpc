using ApricotFramework.Authentication;
using ApricotFramework.DiscoveryClient.AspNetCore.Extensions;
using ApricotFramework.DiscoveryClient.Impl;
using ApricotFramework.DiscoveryClient.Model;
using ApricotFramework.ErrorDefinitions;
using ApricotFramework.ErrorDefinitions.AspNetCore.Extensions;
using ApricotFramework.Grpc.Client;
using ApricotFramework.Grpc.Examples.Client;
using ApricotFramework.Grpc.Examples.Contracts.Orders;

var builder = WebApplication.CreateBuilder(args);

// Where the orders service lives. A real service reads this from a catalogue file.
builder.Services.AddServiceDefinitionsSource(new StaticServiceDefinitionsSource(new ServiceDefinitions
{
    Services = new Dictionary<string, ServiceDefinition>
    {
        ["orders"] = new()
        {
            Ports = [new ServicePortDefinition { Protocol = ServiceProtocolTypes.Http, Roles = [ServicePortRoles.Grpc], Port = 5401 }],
        },
    },
}));

builder.Services.AddDiscoveryClient(builder.Configuration);
builder.Services.AddSingleton<IClientAuthenticator, ExampleAuthenticator>();

// Turns whatever this service fails with into RFC 9457 problem+json — including an error that came
// back over gRPC, which arrives as an ErrorDefinitionException like any other classified failure.
builder.Services.AddErrorDefinitions();

// Everything gRPC, gathered in this service's own extension rather than here.
builder.Services.AddGrpcClients(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

// The happy path, and the three failures. Nothing here handles an error: the failure the orders
// service classified arrives as an exception and leaves as a problem document, unchanged.
app.MapGet("/orders/{id}", async (string id, IGrpcClientProvider clients) =>
    await clients.Create<Orders.OrdersClient>().GetOrderAsync(new GetOrderRequest { Id = id }));

app.MapPost("/orders", async (PlaceOrderRequest request, IGrpcClientProvider clients) =>
    await clients.Create<Orders.OrdersClient>().PlaceOrderAsync(request));

// The other way to use it: a caller that treats one kind as an answer rather than a failure.
app.MapGet("/orders/{id}/exists", async (string id, IGrpcClientProvider clients) =>
{
    try
    {
        await clients.Create<Orders.OrdersClient>().GetOrderAsync(new GetOrderRequest { Id = id });

        return Results.Ok(new { Id = id, Exists = true });
    }
    catch (ErrorDefinitionException failure) when (failure.HasKind(ErrorKinds.NotFound))
    {
        return Results.Ok(new { Id = id, Exists = false });
    }
});

await app.RunAsync();
