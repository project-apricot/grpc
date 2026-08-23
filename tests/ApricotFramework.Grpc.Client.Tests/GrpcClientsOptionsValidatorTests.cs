using ApricotFramework.Grpc.Client.Extensions;
using Microsoft.Extensions.Configuration;
using ApricotFramework.Grpc.Client.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Tests;

/// <summary>
/// Covers the settings that stop a host starting, and the one that only warns.
/// </summary>
public class GrpcClientsOptionsValidatorTests
{
    [Fact]
    public void Validated_InsecureAllowed_WarnsAboutWhatItCosts()
    {
        var (_, written) = Validated(settings => settings.AllowInsecure = true);

        var warning = Assert.Single(written, record => record.Level == LogLevel.Warning);

        Assert.Contains("AllowInsecure", warning.Message, StringComparison.Ordinal);
        Assert.Contains("plaintext", warning.Message, StringComparison.Ordinal);
        Assert.Contains("certificate", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validated_InsecureNotAllowed_SaysNothing()
    {
        var (_, written) = Validated(_ => { });

        Assert.Empty(written);
    }

    [Fact]
    public void Validated_OptionsRebuiltRepeatedly_WarnsOnlyOnce()
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddProvider(logs));
        services.AddGrpcClientsCore(settings => settings.AllowInsecure = true);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var validators = provider.GetServices<IValidateOptions<GrpcClientsOptions>>().ToList();

        // What a configuration reload, or a caller taking a snapshot per scope, would do.
        for (var rebuild = 0; rebuild < 5; rebuild++)
        {
            foreach (var validator in validators)
            {
                validator.Validate(Microsoft.Extensions.Options.Options.DefaultName, new GrpcClientsOptions { AllowInsecure = true });
            }
        }

        Assert.Single(logs.Records);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validated_DeadlineThatIsNotPositive_StopsTheHost(int seconds)
    {
        var thrown = Assert.Throws<OptionsValidationException>(
            () => Validated(settings => settings.DefaultDeadline = TimeSpan.FromSeconds(seconds)));

        Assert.Contains("deadline must be positive", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddGrpcClientsCore_SectionOfItsOwn_IsBoundFromThere()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Downstream:Grpc:DefaultDeadline"] = "00:00:45",
                ["GrpcClients:DefaultDeadline"] = "00:00:05",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGrpcClientsCore(configuration, "Downstream:Grpc");

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Equal(
            TimeSpan.FromSeconds(45),
            provider.GetRequiredService<IOptions<GrpcClientsOptions>>().Value.DefaultDeadline);
    }

    [Fact]
    public void AddGrpcClientsCore_NoSectionNamed_UsesGrpcClients()
    {
        Assert.Equal("GrpcClients", GrpcClientServiceCollectionExtensions.ConfigurationSectionName);
    }

    /// <summary>
    /// Materializes the settings, which is what runs the validation.
    /// </summary>
    /// <param name="configure">The settings to vet.</param>
    /// <returns>The settings, and whatever was logged while vetting them.</returns>
    private static (GrpcClientsOptions Settings, List<(LogLevel Level, string Message)> Written) Validated(
        Action<GrpcClientsOptions> configure)
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddProvider(logs));
        services.AddGrpcClientsCore(configure);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        return (provider.GetRequiredService<IOptions<GrpcClientsOptions>>().Value, logs.Records);
    }
}
