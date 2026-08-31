using HedKam.Services;
using HedKam.Services.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class OTPServiceCollectionExtensions
{
    private const int MIN_DIGITS_COUNT = 1;
    private const int MAX_DIGITS_COUNT = 10;

    public static IServiceCollection AddOTPService(this IServiceCollection services, Action<OTPServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<OTPServiceOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder
            .Validate(p => p.DigitsCount >= MIN_DIGITS_COUNT && p.DigitsCount <= MAX_DIGITS_COUNT,
                $"{nameof(OTPServiceOptions.DigitsCount)} must be between {MIN_DIGITS_COUNT} and {MAX_DIGITS_COUNT}")
            .Validate(p => p.AllowDuplicateDigit || p.DigitsCount <= (p.AllowZero ? 10 : 9),
                $"{nameof(OTPServiceOptions.DigitsCount)} is larger than the pool of available digits when {nameof(OTPServiceOptions.AllowDuplicateDigit)} is false")
            .Validate(p => p.ExpireInMinutes > 0,
                $"{nameof(OTPServiceOptions.ExpireInMinutes)} must be greater than 0")
            .Validate(p => p.MaxAttempts > 0,
                $"{nameof(OTPServiceOptions.MaxAttempts)} must be greater than 0")
            .Validate(p => p.CleanupIntervalSeconds >= 0,
                $"{nameof(OTPServiceOptions.CleanupIntervalSeconds)} must not be negative")
            .Validate(p => p.MaxGeneratePerWindow > 0,
                $"{nameof(OTPServiceOptions.MaxGeneratePerWindow)} must be greater than 0")
            .Validate(p => p.GenerateWindowSeconds > 0,
                $"{nameof(OTPServiceOptions.GenerateWindowSeconds)} must be greater than 0")
            .Validate(p => p.MessagePatterns is not null,
                $"{nameof(OTPServiceOptions.MessagePatterns)} must not be null")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICodeGenerator, CodeGenerator>();
        services.TryAddSingleton<IOTPService, OTPService>();

        return services;
    }
}
