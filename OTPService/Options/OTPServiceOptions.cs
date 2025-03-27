namespace HedKam.Services.Options;


public class OTPServiceOptions
{
    private const int DEFAULT_DIGITS_COUNT = 4;
    private const int DEFAULT_EXPRIE_IN_MINUTES = 10;
    private const bool DEFAULT_ALLOW_DUPLICATE_DIGIT = true;
    private const bool DEFAULT_ALLOW_ZERO = true;

    public int DigitsCount { get; set; } = DEFAULT_DIGITS_COUNT;
    public int ExpireInMinutes { get; set; } = DEFAULT_EXPRIE_IN_MINUTES;
    public bool AllowDuplicateDigit { get; set; } = DEFAULT_ALLOW_DUPLICATE_DIGIT;
    public bool AllowZero { get; set; } = DEFAULT_ALLOW_ZERO;

    public OTPErrors Errors { get; set; } = new OTPErrors();
    public IEnumerable<OTPMessagePattern> MessagePattern { get; set; }
}
