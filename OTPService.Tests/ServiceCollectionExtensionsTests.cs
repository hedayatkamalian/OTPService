using HedKam.Services.Models;
using HedKam.Services.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HedKam.Services.Tests;

public class ServiceCollectionExtensionsTests
{
    private const string CLIENT_NAME = "test_client";

    private static void RunStartupValidation(IServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void AddOTPService_Must_Register_The_Service_As_A_Singleton()
    {
        var serviceProvider = new ServiceCollection().AddOTPService().BuildServiceProvider();

        var firstResolve = serviceProvider.GetRequiredService<IOTPService>();
        var secondResolve = serviceProvider.GetRequiredService<IOTPService>();

        firstResolve.ShouldBeSameAs(secondResolve);
    }

    [Fact]
    public void AddOTPService_Must_Keep_Codes_Valid_Across_Scopes()
    {
        var serviceProvider = new ServiceCollection().AddOTPService().BuildServiceProvider();

        OTPResult otpResult;

        using (var firstScope = serviceProvider.CreateScope())
        {
            otpResult = firstScope.ServiceProvider.GetRequiredService<IOTPService>().Generate(CLIENT_NAME);
        }

        using var secondScope = serviceProvider.CreateScope();

        secondScope.ServiceProvider.GetRequiredService<IOTPService>()
            .Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();
    }

    [Fact]
    public void AddOTPService_Must_Apply_The_Configured_Options()
    {
        const int digitsCount = 8;

        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => p.DigitsCount = digitsCount)
            .BuildServiceProvider();

        serviceProvider.GetRequiredService<IOTPService>().Generate(CLIENT_NAME).Code.Length.ShouldBe(digitsCount);
    }

    [Fact]
    public void AddOTPService_Must_Work_Without_Configuration()
    {
        var serviceProvider = new ServiceCollection().AddOTPService().BuildServiceProvider();

        var otpService = serviceProvider.GetRequiredService<IOTPService>();
        var otpResult = otpService.Generate(CLIENT_NAME);

        otpResult.Code.Length.ShouldBe(new OTPServiceOptions().DigitsCount);
        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();
    }

    [Fact]
    public void AddOTPService_Must_Not_Register_The_Service_Twice()
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService()
            .AddOTPService()
            .BuildServiceProvider();

        serviceProvider.GetServices<IOTPService>().Count().ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void AddOTPService_Must_Reject_An_Out_Of_Range_DigitsCount(int digitsCount)
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => p.DigitsCount = digitsCount)
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => RunStartupValidation(serviceProvider));
    }

    [Fact]
    public void AddOTPService_Must_Reject_A_DigitsCount_Larger_Than_The_Digit_Pool()
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => { p.DigitsCount = 10; p.AllowDuplicateDigit = false; p.AllowZero = false; })
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => RunStartupValidation(serviceProvider));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddOTPService_Must_Reject_A_Non_Positive_ExpireInMinutes(int expireInMinutes)
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => p.ExpireInMinutes = expireInMinutes)
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => RunStartupValidation(serviceProvider));
    }

    [Fact]
    public void AddOTPService_Must_Reject_A_Non_Positive_MaxAttempts()
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => p.MaxAttempts = 0)
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => RunStartupValidation(serviceProvider));
    }

    [Fact]
    public void AddOTPService_Must_Allow_A_Zero_CleanupIntervalSeconds()
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => p.CleanupIntervalSeconds = 0)
            .BuildServiceProvider();

        Should.NotThrow(() => RunStartupValidation(serviceProvider));
    }

    [Fact]
    public void AddOTPService_Must_Reject_A_Negative_CleanupIntervalSeconds()
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => p.CleanupIntervalSeconds = -1)
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => RunStartupValidation(serviceProvider));
    }

    [Fact]
    public void AddOTPService_Must_Reject_A_Non_Positive_MaxGeneratePerWindow()
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => p.MaxGeneratePerWindow = 0)
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => RunStartupValidation(serviceProvider));
    }

    [Fact]
    public void AddOTPService_Must_Reject_A_Non_Positive_GenerateWindowSeconds()
    {
        var serviceProvider = new ServiceCollection()
            .AddOTPService(p => p.GenerateWindowSeconds = 0)
            .BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => RunStartupValidation(serviceProvider));
    }
}
