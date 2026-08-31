namespace HedKam.Services.Exceptions;

public class OTPValidationException : Exception
{
    public OTPValidationException(string message) : base(message) { }
}
