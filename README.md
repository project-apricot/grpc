# ApricotFramework.Grpc

[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.Grpc.ErrorDefinitions.svg?label=ApricotFramework.Grpc.ErrorDefinitions)](https://www.nuget.org/packages/ApricotFramework.Grpc.ErrorDefinitions/)
[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.Grpc.Server.svg?label=ApricotFramework.Grpc.Server)](https://www.nuget.org/packages/ApricotFramework.Grpc.Server/)
[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.Grpc.Client.svg?label=ApricotFramework.Grpc.Client)](https://www.nuget.org/packages/ApricotFramework.Grpc.Client/)
[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.Grpc.Client.DiscoveryClient.svg?label=ApricotFramework.Grpc.Client.DiscoveryClient)](https://www.nuget.org/packages/ApricotFramework.Grpc.Client.DiscoveryClient/)
[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.Grpc.Client.Authentication.svg?label=ApricotFramework.Grpc.Client.Authentication)](https://www.nuget.org/packages/ApricotFramework.Grpc.Client.Authentication/)
[![CI](https://github.com/project-apricot/grpc/actions/workflows/ci.yml/badge.svg)](https://github.com/project-apricot/grpc/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](https://github.com/project-apricot/grpc/blob/main/LICENSE)

A gRPC hop that keeps the error. A service throws a classified error; its caller catches the same
error, with the same kind, code and payload, as though the call had been local.

Everything is opt-in and separately installable — errors, discovery, credentials — so a client takes
what it needs and nothing else. Only the server package requires ASP.NET Core.

## Install

```bash
dotnet add package ApricotFramework.Grpc.Server                    # the server
dotnet add package ApricotFramework.Grpc.Client                    # the caller
dotnet add package ApricotFramework.Grpc.Client.DiscoveryClient    # ...addressed by service name
dotnet add package ApricotFramework.Grpc.Client.Authentication     # ...presenting its own token
```

## Usage

```csharp
// Server. AddGrpc stays gRPC's own; this says what happens when a call fails, asking the same
// exception mappers that already answer this service's HTTP requests.
builder.Services.AddGrpc();
builder.Services.AddGrpcErrorHandling();
```

```csharp
// Anywhere in a service method. Nothing catches this; the caller receives it.
throw Err.NotFound("ORDER_NOT_FOUND", $"no order with id '{id}'",
    new Dictionary<string, object?> { ["orderId"] = id }).AsException();
```

```csharp
// Caller. Each line is a capability, and any of them can be left out.
builder.Services.AddGrpcClientsCore(builder.Configuration);
builder.Services.AddGrpcErrorMapping();

builder.Services
    .AddDiscoveredGrpcClient<Orders.OrdersClient>("orders")
    .AddGrpcCallCredentials(credentials => credentials.Scopes = ["orders.read"]);
```

```csharp
try
{
    var order = await client.GetOrderAsync(new GetOrderRequest { Id = id });
}
catch (ErrorDefinitionException failure) when (failure.HasKind(ErrorKinds.NotFound))
{
    // The kind, the code and the payload are the ones the orders service threw.
}
```

> **Note.** Errors travel in a trailer, and a trailer too large for the peer fails the whole response.
> They are therefore trimmed to a byte budget — payloads first, then the errors after the first — with
> a flag on the wire saying so. The server's log always holds the full set.

The wire contract is `apricot.errors.v1`, packed into the contract package as
`protos/apricot/errors/v1/errors.proto` so a service in another language can speak it.

Full documentation at [projectapricot.dev/docs/grpc](https://projectapricot.dev/docs/grpc).
