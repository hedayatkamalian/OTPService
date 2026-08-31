namespace HedKam.Services.Options;

public class OTPErrors
{
    public string CodeDoesNotExist { get; set; } = nameof(CodeDoesNotExist);
    public string CodeIsInvalid { get; set; } = nameof(CodeIsInvalid);
    public string CodeIsExpired { get; set; } = nameof(CodeIsExpired);
    public string CodeIsUsed { get; set; } = nameof(CodeIsUsed);
    public string MaxAttemptsExceeded { get; set; } = nameof(MaxAttemptsExceeded);
    public string GenerateLimitExceeded { get; set; } = nameof(GenerateLimitExceeded);
}
