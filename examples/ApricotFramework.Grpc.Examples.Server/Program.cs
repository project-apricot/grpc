using ApricotFramework.Grpc.Examples.Server;

var builder = WebApplication.CreateBuilder(args);

// Gathered in this service's own extension rather than here.
builder.Services.AddGrpcServer();

var app = builder.Build();

app.MapGrpcService<OrdersService>();
app.MapGrpcReflectionService();

app.MapGet("/", () => "The orders example. Call it with the client example, or with grpcurl.");

await app.RunAsync();
