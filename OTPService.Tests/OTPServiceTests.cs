using System.Collections.Concurrent;
using HedKam.Services.Exceptions;
using HedKam.Services.Models;
using HedKam.Services.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace HedKam.Services.Tests;

public class OTPServiceTests
{
    private const string CLIENT_NAME = "test_client";
    private IOTPService otpService;
    private OTPServiceOptions options = new OTPServiceOptions();
    private FakeTimeProvider timeProvider = new FakeTimeProvider();

    public OTPServiceTests()
    {
        otpService = CreateService();
    }

    private IOTPService CreateService(Action<OTPServiceOptions>? configure = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TimeProvider>(timeProvider);
        serviceCollection.AddOTPService(p =>
        {
            p.MaxGeneratePerWindow = int.MaxValue;

            configure?.Invoke(p);
        });
        var serviceProvider = serviceCollection.BuildServiceProvider();

        return serviceProvider.GetService<IOTPService>() ?? throw new Exception("service not found");
    }



    [Fact]
    public void OTPService_Must_Generate_OTPResult()
    {
        var result = otpService.Generate(CLIENT_NAME);
        result.Code.ShouldNotBeNullOrEmpty();
        result.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void OTPService_Must_Validate_With_Correct_Token()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        var result = otpService.Validate(otpResult.Code, CLIENT_NAME);
        result.ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Not_Validate_With_Incorrect_Data()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        var invalidCodeResult = otpService.Validate(otpResult.Code + "5", CLIENT_NAME);
        var unknownClientResult = otpService.Validate(otpResult.Code, CLIENT_NAME + "x");

        invalidCodeResult.ShouldBeFalse();
        unknownClientResult.ShouldBeFalse();
    }

    [Fact]
    public void OTPService_Must_Throw_Exception_With_Incorrect_Data()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code + "5", CLIENT_NAME));
        Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code, CLIENT_NAME + "x"));
    }

    [Fact]
    public void OTPService_Must_Give_Reason_When_Result_Is_Invalid()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);


        var invalidCodeResult = otpService.ValidateAndReason(otpResult.Code + "5", CLIENT_NAME);
        var unknownClientResult = otpService.ValidateAndReason(otpResult.Code, CLIENT_NAME + "x");

        invalidCodeResult.IsValid.ShouldBeFalse();
        invalidCodeResult.ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsInvalid);

        unknownClientResult.IsValid.ShouldBeFalse();
        unknownClientResult.ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeDoesNotExist);
    }

    [Fact]
    public void OTPService_Must_Not_Validate_The_Same_Code_Twice()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();
        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeFalse();
    }

    [Fact]
    public void OTPService_Must_Give_Used_Reason_When_Code_Is_Already_Validated()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        otpService.ValidateAndReason(otpResult.Code, CLIENT_NAME).IsValid.ShouldBeTrue();

        var usedResult = otpService.ValidateAndReason(otpResult.Code, CLIENT_NAME);

        usedResult.IsValid.ShouldBeFalse();
        usedResult.ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsUsed);
    }

    [Fact]
    public void OTPService_Must_Throw_Exception_When_Code_Is_Already_Validated()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        otpService.ValidateAndThrow(otpResult.Code, CLIENT_NAME);

        Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code, CLIENT_NAME));
    }

    [Fact]
    public void OTPService_Must_Consume_Code_For_Every_Validate_Method()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();

        otpService.ValidateAndReason(otpResult.Code, CLIENT_NAME).IsValid.ShouldBeFalse();
        Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code, CLIENT_NAME));
    }

    [Fact]
    public void OTPService_Must_Not_Consume_Code_When_Validation_Fails()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        otpService.Validate(otpResult.Code + "5", CLIENT_NAME).ShouldBeFalse();
        otpService.ValidateAndReason(otpResult.Code, CLIENT_NAME + "x").IsValid.ShouldBeFalse();
        Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code + "5", CLIENT_NAME));

        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Redeem_Code_Once_When_Validated_Concurrently()
    {
        const int threadCount = 8;
        const int trialCount = 50;

        for (var trial = 0; trial < trialCount; trial++)
        {
            var otpResult = otpService.Generate(CLIENT_NAME);
            var succeededCount = 0;

            using var gate = new ManualResetEventSlim(false);

            var threads = Enumerable.Range(0, threadCount).Select(p => new Thread(() =>
            {
                gate.Wait();

                if (otpService.Validate(otpResult.Code, CLIENT_NAME))
                {
                    Interlocked.Increment(ref succeededCount);
                }
            })).ToList();

            threads.ForEach(p => p.Start());
            gate.Set();
            threads.ForEach(p => p.Join());

            succeededCount.ShouldBe(1);
        }
    }

    [Fact]
    public void OTPService_Must_Not_Validate_After_Max_Attempts()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        for (var attempt = 0; attempt < options.MaxAttempts; attempt++)
        {
            otpService.Validate(otpResult.Code + "5", CLIENT_NAME).ShouldBeFalse();
        }

        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeFalse();
    }

    [Fact]
    public void OTPService_Must_Give_Max_Attempts_Reason_After_Max_Attempts()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        for (var attempt = 0; attempt < options.MaxAttempts; attempt++)
        {
            otpService.ValidateAndReason(otpResult.Code + "5", CLIENT_NAME).IsValid.ShouldBeFalse();
        }

        var exceededResult = otpService.ValidateAndReason(otpResult.Code, CLIENT_NAME);

        exceededResult.IsValid.ShouldBeFalse();
        exceededResult.ErrorMessage.ShouldBeEquivalentTo(options.Errors.MaxAttemptsExceeded);
    }

    [Fact]
    public void OTPService_Must_Throw_Exception_After_Max_Attempts()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        for (var attempt = 0; attempt < options.MaxAttempts; attempt++)
        {
            Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code + "5", CLIENT_NAME));
        }

        var exception = Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code, CLIENT_NAME));

        exception.Message.ShouldBeEquivalentTo(options.Errors.MaxAttemptsExceeded);
    }

    [Fact]
    public void OTPService_Must_Validate_Correct_Code_Before_Max_Attempts()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        for (var attempt = 0; attempt < options.MaxAttempts - 1; attempt++)
        {
            otpService.Validate(otpResult.Code + "5", CLIENT_NAME).ShouldBeFalse();
        }

        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Count_Failed_Attempts_From_Every_Validate_Method()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        for (var attempt = 0; attempt < options.MaxAttempts; attempt++)
        {
            if (attempt % 3 == 0)
            {
                otpService.Validate(otpResult.Code + "5", CLIENT_NAME);
            }
            else if (attempt % 3 == 1)
            {
                otpService.ValidateAndReason(otpResult.Code + "5", CLIENT_NAME);
            }
            else
            {
                Should.Throw<OTPValidationException>(() => otpService.ValidateAndThrow(otpResult.Code + "5", CLIENT_NAME));
            }
        }

        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeFalse();
    }

    [Fact]
    public void OTPService_Must_Count_Every_Failed_Attempt_When_Validated_Concurrently()
    {
        const int trialCount = 50;

        for (var trial = 0; trial < trialCount; trial++)
        {
            var otpResult = otpService.Generate(CLIENT_NAME);

            using var gate = new ManualResetEventSlim(false);

            var threads = Enumerable.Range(0, options.MaxAttempts).Select(p => new Thread(() =>
            {
                gate.Wait();

                otpService.Validate(otpResult.Code + "5", CLIENT_NAME);
            })).ToList();

            threads.ForEach(p => p.Start());
            gate.Set();
            threads.ForEach(p => p.Join());

            otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeFalse();
        }
    }

    [Fact]
    public void OTPService_Must_Generate_Concurrently_Without_Losing_Items()
    {
        const int generateCount = 500;

        var otpResults = new ConcurrentBag<(string ClientName, string Code)>();

        Parallel.For(0, generateCount, new ParallelOptions { MaxDegreeOfParallelism = 8 }, p =>
        {
            var clientName = CLIENT_NAME + p;

            otpResults.Add((clientName, otpService.Generate(clientName).Code));
        });

        otpResults.Count.ShouldBe(generateCount);

        otpResults.ToList().ForEach(p => otpService.Validate(p.Code, p.ClientName).ShouldBeTrue());
    }

    [Fact]
    public void OTPService_Must_Validate_While_Generating_Concurrently()
    {
        const int generateCount = 500;

        var otpResults = Enumerable.Range(0, generateCount)
            .Select(p => (ClientName: CLIENT_NAME + p, otpService.Generate(CLIENT_NAME + p).Code))
            .ToList();

        Parallel.ForEach(otpResults, new ParallelOptions { MaxDegreeOfParallelism = 8 }, p =>
        {
            otpService.Generate("noise" + p.ClientName);

            otpService.Validate(p.Code, p.ClientName).ShouldBeTrue();
        });
    }

    [Fact]
    public void OTPService_Must_Not_Share_Codes_Between_Instances()
    {
        var otherService = CreateService();

        var otpResult = otpService.Generate(CLIENT_NAME);

        otherService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeFalse();
        otherService.ValidateAndReason(otpResult.Code, CLIENT_NAME).ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeDoesNotExist);

        otpService.Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Remove_Expired_Items_From_The_Store()
    {
        var service = CreateService(p => { p.ExpireInMinutes = 5; p.CleanupIntervalSeconds = 0; });

        var otpResult = service.Generate(CLIENT_NAME);

        timeProvider.Advance(TimeSpan.FromMinutes(6));

        service.ValidateAndReason(otpResult.Code, CLIENT_NAME)
            .ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsExpired);

        service.Generate("someone_else");

        service.ValidateAndReason(otpResult.Code, CLIENT_NAME)
            .ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeDoesNotExist);
    }

    [Fact]
    public void OTPService_Must_Not_Remove_Expired_Items_Before_The_Cleanup_Interval()
    {
        var service = CreateService(p => { p.ExpireInMinutes = 5; p.CleanupIntervalSeconds = 3600; });

        var otpResult = service.Generate(CLIENT_NAME);

        timeProvider.Advance(TimeSpan.FromMinutes(6));

        service.Generate("someone_else");

        service.ValidateAndReason(otpResult.Code, CLIENT_NAME)
            .ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsExpired);
    }

    [Fact]
    public void OTPService_Must_Keep_Unexpired_Items_When_Cleaning_Up()
    {
        const int generateCount = 200;

        var service = CreateService(p => p.CleanupIntervalSeconds = 0);

        var otpResult = service.Generate(CLIENT_NAME);

        for (var i = 0; i < generateCount; i++)
        {
            service.Generate(CLIENT_NAME + i);
        }

        service.Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Return_Code_When_No_Message_Pattern_Is_Configured()
    {
        var otpResult = otpService.Generate(CLIENT_NAME, "sms");

        otpResult.Message.ShouldBeEquivalentTo(otpResult.Code);
    }

    [Fact]
    public void OTPService_Must_Apply_The_Message_Pattern()
    {
        var service = CreateService(p => p.MessagePatterns = [new OTPMessagePattern("sms", "Your code is {code}")]);

        var otpResult = service.Generate(CLIENT_NAME, "sms");

        otpResult.Message.ShouldBeEquivalentTo($"Your code is {otpResult.Code}");
    }

    [Fact]
    public void OTPService_Must_Return_Code_When_Message_Pattern_Is_Not_Found()
    {
        var service = CreateService(p => p.MessagePatterns = [new OTPMessagePattern("sms", "Your code is {code}")]);

        var otpResult = service.Generate(CLIENT_NAME, "email");

        otpResult.Message.ShouldBeEquivalentTo(otpResult.Code);
    }

    [Fact]
    public void OTPService_Must_Throw_Argument_Exception_For_An_Invalid_Client_Name()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        Should.Throw<ArgumentException>(() => otpService.Generate(null!));
        Should.Throw<ArgumentException>(() => otpService.Generate("   "));
        Should.Throw<ArgumentException>(() => otpService.Validate(otpResult.Code, null!));
        Should.Throw<ArgumentException>(() => otpService.ValidateAndReason(otpResult.Code, "   "));
        Should.Throw<ArgumentException>(() => otpService.ValidateAndThrow(otpResult.Code, null!));
    }

    [Fact]
    public void OTPService_Must_Throw_Argument_Exception_For_A_Null_Code()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        Should.Throw<ArgumentNullException>(() => otpService.Validate(null!, CLIENT_NAME));
        Should.Throw<ArgumentNullException>(() => otpService.ValidateAndReason(null!, CLIENT_NAME));
        Should.Throw<ArgumentNullException>(() => otpService.ValidateAndThrow(null!, CLIENT_NAME));
    }

    [Fact]
    public void OTPService_Must_Treat_An_Empty_Code_As_Invalid_Rather_Than_An_Error()
    {
        var otpResult = otpService.Generate(CLIENT_NAME);

        otpService.ValidateAndReason(string.Empty, CLIENT_NAME)
            .ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsInvalid);
    }

    [Fact]
    public void OTPService_Must_Validate_A_Code_Just_Before_It_Expires()
    {
        const int expireInMinutes = 5;

        var service = CreateService(p => p.ExpireInMinutes = expireInMinutes);

        var otpResult = service.Generate(CLIENT_NAME);

        timeProvider.Advance(TimeSpan.FromMinutes(expireInMinutes).Subtract(TimeSpan.FromSeconds(1)));

        service.Validate(otpResult.Code, CLIENT_NAME).ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Not_Validate_A_Code_Just_After_It_Expires()
    {
        const int expireInMinutes = 5;

        var service = CreateService(p => p.ExpireInMinutes = expireInMinutes);

        var otpResult = service.Generate(CLIENT_NAME);

        timeProvider.Advance(TimeSpan.FromMinutes(expireInMinutes).Add(TimeSpan.FromSeconds(1)));

        service.ValidateAndReason(otpResult.Code, CLIENT_NAME)
            .ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsExpired);
    }

    [Fact]
    public void OTPService_Must_Follow_Options_Changed_At_Runtime()
    {
        var optionsMonitor = new MutableOptionsMonitor<OTPServiceOptions>(
            new OTPServiceOptions { DigitsCount = 4, MaxGeneratePerWindow = int.MaxValue });
        var service = new OTPService(optionsMonitor, new CodeGenerator(), timeProvider);

        service.Generate(CLIENT_NAME).Code.Length.ShouldBe(4);

        optionsMonitor.CurrentValue = new OTPServiceOptions { DigitsCount = 8, MaxGeneratePerWindow = int.MaxValue };

        service.Generate(CLIENT_NAME).Code.Length.ShouldBe(8);
    }

    [Fact]
    public void OTPService_Must_Throw_When_The_Generate_Limit_Is_Exceeded()
    {
        const int maxGeneratePerWindow = 3;

        var service = CreateService(p => p.MaxGeneratePerWindow = maxGeneratePerWindow);

        for (var i = 0; i < maxGeneratePerWindow; i++)
        {
            service.Generate(CLIENT_NAME);
        }

        var exception = Should.Throw<OTPGenerateLimitException>(() => service.Generate(CLIENT_NAME));

        exception.Message.ShouldBeEquivalentTo(options.Errors.GenerateLimitExceeded);
    }

    [Fact]
    public void OTPService_Must_Allow_Generate_Again_After_The_Window_Passes()
    {
        const int maxGeneratePerWindow = 3;
        const int generateWindowSeconds = 60;

        var service = CreateService(p =>
        {
            p.MaxGeneratePerWindow = maxGeneratePerWindow;
            p.GenerateWindowSeconds = generateWindowSeconds;
        });

        for (var i = 0; i < maxGeneratePerWindow; i++)
        {
            service.Generate(CLIENT_NAME);
        }

        Should.Throw<OTPGenerateLimitException>(() => service.Generate(CLIENT_NAME));

        timeProvider.Advance(TimeSpan.FromSeconds(generateWindowSeconds + 1));

        Should.NotThrow(() => service.Generate(CLIENT_NAME));
    }

    [Fact]
    public void OTPService_Must_Limit_Generate_Per_Client()
    {
        const int maxGeneratePerWindow = 3;

        var service = CreateService(p => p.MaxGeneratePerWindow = maxGeneratePerWindow);

        for (var i = 0; i < maxGeneratePerWindow; i++)
        {
            service.Generate(CLIENT_NAME);
        }

        Should.Throw<OTPGenerateLimitException>(() => service.Generate(CLIENT_NAME));
        Should.NotThrow(() => service.Generate(CLIENT_NAME + "_other"));
    }

    [Fact]
    public void OTPService_Must_Not_Let_A_Padded_Client_Name_Bypass_The_Generate_Limit()
    {
        const int maxGeneratePerWindow = 2;

        var service = CreateService(p => p.MaxGeneratePerWindow = maxGeneratePerWindow);

        service.Generate(CLIENT_NAME);
        service.Generate("  " + CLIENT_NAME + "  ");

        Should.Throw<OTPGenerateLimitException>(() => service.Generate(CLIENT_NAME));
    }

    [Fact]
    public void OTPService_Must_Not_Exceed_The_Generate_Limit_When_Called_Concurrently()
    {
        const int maxGeneratePerWindow = 5;
        const int threadCount = 20;

        var service = CreateService(p => p.MaxGeneratePerWindow = maxGeneratePerWindow);
        var succeededCount = 0;

        using var gate = new ManualResetEventSlim(false);

        var threads = Enumerable.Range(0, threadCount).Select(p => new Thread(() =>
        {
            gate.Wait();

            try
            {
                service.Generate(CLIENT_NAME);

                Interlocked.Increment(ref succeededCount);
            }
            catch (OTPGenerateLimitException)
            {
            }
        })).ToList();

        threads.ForEach(p => p.Start());
        gate.Set();
        threads.ForEach(p => p.Join());

        succeededCount.ShouldBe(maxGeneratePerWindow);
    }

    [Fact]
    public void OTPService_Must_Invalidate_The_Previous_Code_When_A_New_One_Is_Issued()
    {
        var service = CreateService(p => p.DigitsCount = 8);

        var firstResult = service.Generate(CLIENT_NAME);
        var secondResult = service.Generate(CLIENT_NAME);

        service.ValidateAndReason(firstResult.Code, CLIENT_NAME)
            .ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsInvalid);

        service.Validate(secondResult.Code, CLIENT_NAME).ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Keep_One_Live_Code_Per_Client()
    {
        var service = CreateService(p => p.DigitsCount = 8);

        var firstClientResult = service.Generate(CLIENT_NAME);
        var secondClientResult = service.Generate(CLIENT_NAME + "_other");

        service.Validate(firstClientResult.Code, CLIENT_NAME).ShouldBeTrue();
        service.Validate(secondClientResult.Code, CLIENT_NAME + "_other").ShouldBeTrue();
    }

    [Fact]
    public void OTPService_Must_Not_Accept_A_Code_Belonging_To_Another_Client()
    {
        var service = CreateService(p => p.DigitsCount = 8);

        var otpResult = service.Generate(CLIENT_NAME);

        service.Generate(CLIENT_NAME + "_other");

        service.ValidateAndReason(otpResult.Code, CLIENT_NAME + "_other")
            .ErrorMessage.ShouldBeEquivalentTo(options.Errors.CodeIsInvalid);
    }
}
