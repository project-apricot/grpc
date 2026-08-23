using ApricotFramework.Grpc.Client.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Grpc.Client.Tests;

/// <summary>
/// Covers both halves of what the setting does, and that a client which does not ask for it is
/// untouched.
/// </summary>
public class AllowInsecureTests
{
    [Fact]
    public void AllowInsecureTransportIfConfigured_SettingOn_AcceptsACertificateItCannotVerify()
    {
        var primary = Assert.IsType<SocketsHttpHandler>(PrimaryHandler(allowInsecure: true));

        Assert.NotNull(primary.SslOptions.RemoteCertificateValidationCallback);
        Assert.True(primary.SslOptions.RemoteCertificateValidationCallback!.Invoke(this, null, null, default));
    }

    [Fact]
    public void AllowInsecureTransportIfConfigured_SettingOff_LeavesCertificateValidationAlone()
    {
        var primary = Assert.IsType<SocketsHttpHandler>(PrimaryHandler(allowInsecure: false));

        Assert.Null(primary.SslOptions.RemoteCertificateValidationCallback);
    }

    [Fact]
    public void AddGrpcClient_WithoutAsking_LeavesCertificateValidationAlone()
    {
        // Relaxing it globally would stop verifying every outbound call, gRPC or not, so it is opt-in.
        var primary = Assert.IsType<SocketsHttpHandler>(PrimaryHandler(allowInsecure: true, ask: false));

        Assert.Null(primary.SslOptions.RemoteCertificateValidationCallback);
    }

    [Fact]
    public void AllowInsecureTransportIfConfigured_HandlerTheCallerSupplied_IsLeftAlone()
    {
        var supplied = new CapturingHandler();

        var primary = PrimaryHandler(
            allowInsecure: true,
            configure: builder => builder.ConfigurePrimaryHttpMessageHandler(() => supplied));

        Assert.Same(supplied, primary);
    }

    /// <summary>
    /// Builds a client's transport and returns the primary handler it ended up with.
    /// </summary>
    /// <param name="allowInsecure">What the setting says.</param>
    /// <param name="ask">Whether the client asks for insecure transport.</param>
    /// <param name="configure">Anything else to do to the client builder.</param>
    /// <returns>The primary handler.</returns>
    private static HttpMessageHandler PrimaryHandler(
        bool allowInsecure,
        bool ask = true,
        Action<IHttpClientBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddGrpcClientsCore(settings => settings.AllowInsecure = allowInsecure);

        var builder = services.AddGrpcClient<StubClient>(o => o.Address = new Uri("https://localhost:5001"));

        if (ask)
        {
            builder.AllowInsecureTransportIfConfigured();
        }

        configure?.Invoke(builder);

        HttpMessageHandler? primary = null;
        builder.ConfigurePrimaryHttpMessageHandler((handler, _) => primary = handler);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(StubClient));

        Assert.NotNull(primary);

        return primary;
    }
}
