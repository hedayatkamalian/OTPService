namespace HedKam.Services.Options;


public class OTPServiceOptions
{
    private const int DEFAULT_DIGITS_COUNT = 4;
    private const int DEFAULT_EXPIRE_IN_MINUTES = 10;
    private const bool DEFAULT_ALLOW_DUPLICATE_DIGIT = true;
    private const bool DEFAULT_ALLOW_ZERO = true;
    private const int DEFAULT_MAX_ATTEMPTS = 5;
    private const int DEFAULT_CLEANUP_INTERVAL_SECONDS = 60;
    private const int DEFAULT_MAX_GENERATE_PER_WINDOW = 1;
    private const int DEFAULT_GENERATE_WINDOW_SECONDS = 60;

    public int DigitsCount { get; set; } = DEFAULT_DIGITS_COUNT;
    public int ExpireInMinutes { get; set; } = DEFAULT_EXPIRE_IN_MINUTES;
    public bool AllowDuplicateDigit { get; set; } = DEFAULT_ALLOW_DUPLICATE_DIGIT;
    public bool AllowZero { get; set; } = DEFAULT_ALLOW_ZERO;
    public int MaxAttempts { get; set; } = DEFAULT_MAX_ATTEMPTS;
    public int CleanupIntervalSeconds { get; set; } = DEFAULT_CLEANUP_INTERVAL_SECONDS;
    public int MaxGeneratePerWindow { get; set; } = DEFAULT_MAX_GENERATE_PER_WINDOW;
    public int GenerateWindowSeconds { get; set; } = DEFAULT_GENERATE_WINDOW_SECONDS;

    public OTPErrors Errors { get; set; } = new OTPErrors();
    public IEnumerable<OTPMessagePattern> MessagePatterns { get; set; } = [];
}
