using HedKam.Services.Exceptions;
using HedKam.Services.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace HedKam.Services.Tests;

public class OTPServiceTests
{
    private const string CLIENT_NAME = "test_client";
    private IOTPService otpService;
    private OTPServiceOptions options = new OTPServiceOptions();
    private Mock<IOptionsMonitor<OTPServiceOptions>> _mockedOptions;

    public OTPServiceTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IOTPService, OTPService>();
        serviceCollection.Configure<OTPServiceOptions>(p => p = options);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        otpService = serviceProvider.GetService<IOTPService>() ?? throw new Exception("service not found");
    }



    [Fact]
    public void OTPService_Must_Generate_OTPResult()
    {
        var result = otpService.Generate(CLIENT_NAME);
        result.Code.ShouldNotBeNullOrEmpty();
        result.TrackId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void OTPService_Must_Validate_With_Correct_Token()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        var result = otpService.Validate(otpResult.Code, otpResult.TrackId, CLIENT_NAME);
        result.ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Not_Validate_With_Incorrect_Data()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        var invalidCodeResult = otpService.Validate(otpResult.Code + "5", otpResult.TrackId, CLIENT_NAME);
        var invalidClientResult = otpService.Validate(otpResult.Code, otpResult.TrackId, CLIENT_NAME + "x");
        var invalidTrackIdResult = otpService.Validate(otpResult.Code, Guid.NewGuid(), CLIENT_NAME);

        invalidCodeResult.ShouldBeFalse();
        invalidClientResult.ShouldBeFalse();
        invalidTrackIdResult.ShouldBeFalse();
    }

    [Fact]
    public void OTPService_Must_Throw_Exception_With_Incorrect_Data()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code + "5", otpResult.TrackId, CLIENT_NAME));
        Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code, otpResult.TrackId, CLIENT_NAME + "x"));
        Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code, Guid.NewGuid(), CLIENT_NAME));
    }

    [Fact]
    public void OTPService_Must_Give_Reason_When_Result_Is_Invalid()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);


        var invalidCodeResult = otpService.ValidateAndReason(otpResult.Code + "5", otpResult.TrackId, CLIENT_NAME);
        var invalidClientResult = otpService.ValidateAndReason(otpResult.Code, otpResult.TrackId, CLIENT_NAME + "x");
        var invalidTrackIdResult = otpService.ValidateAndReason(otpResult.Code, Guid.NewGuid(), CLIENT_NAME);

        invalidCodeResult.IsValid.ShouldBeFalse();
        invalidCodeResult.ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsInvalid);

    }
}
