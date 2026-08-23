using ApricotFramework.Grpc.Client.Authentication.Extensions;
using ApricotFramework.Grpc.Client.Authentication.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Authentication.Tests;

/// <summary>
/// Covers the header names gRPC would refuse, caught at startup rather than on the first call.
/// </summary>
public class GrpcCredentialsOptionsValidatorTests
{
    [Theory]
    [InlineData("", "must be named")]
    [InlineData("   ", "must be named")]
    [InlineData("x service auth", "not a gRPC metadata key")]
    [InlineData("x-auth-bin", "binary value")]
    public void Validated_HeaderGrpcWouldNotAccept_StopsTheHost(string header, string reason)
    {
        var thrown = Assert.Throws<OptionsValidationException>(() => Validated(header));

        Assert.Contains(reason, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validated_MixedCaseHeader_IsAccepted()
    {
        // gRPC lowercases the name itself, so case is not a misconfiguration.
        Assert.Equal("X-Service-Authorization", Validated("X-Service-Authorization").AuthorizationHeader);
    }

    [Fact]
    public void Validated_NoHeaderGiven_IsAuthorization()
    {
        var services = new ServiceCollection();
        services.ConfigureGrpcCallCredentials(_ => { });

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Equal(
            GrpcCredentialsOptions.DefaultAuthorizationHeader,
            provider.GetRequiredService<IOptions<GrpcCredentialsOptions>>().Value.AuthorizationHeader);
    }

    /// <summary>
    /// Materializes the settings, which is what runs the validation.
    /// </summary>
    /// <param name="header">The header to vet.</param>
    /// <returns>The settings.</returns>
    private static GrpcCredentialsOptions Validated(string header)
    {
        var services = new ServiceCollection();
        services.ConfigureGrpcCallCredentials(options => options.AuthorizationHeader = header);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        return provider.GetRequiredService<IOptions<GrpcCredentialsOptions>>().Value;
    }
}
