using HedKam.Services.Models;

namespace HedKam.Services;

public interface IOTPService
{
    OTPResult Generate(string clientName, string? patternName = null);
    bool Validate(string code, string clientName);
    OTPValidateResult ValidateAndReason(string code, string clientName);
    void ValidateAndThrow(string code, string clientName);
}
