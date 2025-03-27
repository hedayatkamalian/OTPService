using HedKam.Services.Models;

namespace HedKam.Services;

public interface IOTPService
{
    bool Validate(string code, Guid trackId, string clientName);
    OTPResult Generate(string clientName, string? patternName = null);
    OTPValidateResult ValidateAndReason(string code, Guid trackId, string clientName);
    void ValidateAndThrow(string code, Guid trackId, string clientName);
}
