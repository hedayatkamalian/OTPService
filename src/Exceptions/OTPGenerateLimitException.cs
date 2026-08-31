namespace HedKam.Services.Exceptions;

public class OTPGenerateLimitException : Exception
{
    public OTPGenerateLimitException(string message) : base(message) { }
}
