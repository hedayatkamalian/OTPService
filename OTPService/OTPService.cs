using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using HedKam.Services.Exceptions;
using HedKam.Services.Models;
using HedKam.Services.Options;
using Microsoft.Extensions.Options;

namespace HedKam.Services;
public class OTPService : IOTPService
{
    private readonly IOptionsMonitor<OTPServiceOptions> _optionsMonitor;
    private readonly ICodeGenerator _codeGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, OTPItem> _otpItems = new ConcurrentDictionary<string, OTPItem>();
    private readonly object _validationLock = new object();
    private readonly object _cleanupLock = new object();
    private readonly object _generateLock = new object();
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _generateHistory = new ConcurrentDictionary<string, List<DateTimeOffset>>();
    private DateTimeOffset _lastCleanup;

    private OTPServiceOptions Options => _optionsMonitor.CurrentValue;

    public OTPService(IOptionsMonitor<OTPServiceOptions> optionsMonitor, ICodeGenerator codeGenerator, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(codeGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _optionsMonitor = optionsMonitor;
        _codeGenerator = codeGenerator;
        _timeProvider = timeProvider;
        _lastCleanup = timeProvider.GetUtcNow();
    }

    public OTPResult Generate(string clientName, string? patternName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var normalizedClientName = clientName.Trim();

        RemoveExpiredItems();
        EnsureGenerateIsAllowed(normalizedClientName);

        var otp = new OTPItem
        {
            Code = _codeGenerator.Generate(Options.DigitsCount, Options.AllowDuplicateDigit, Options.AllowZero),
            ClientName = normalizedClientName,
            ExpireIn = _timeProvider.GetUtcNow().AddMinutes(Options.ExpireInMinutes)
        };

        lock (_validationLock)
        {
            _otpItems[normalizedClientName] = otp;
        }

        return new OTPResult(otp.Code, CreateMessage(patternName, otp.Code));
    }

    public bool Validate(string code, string clientName)
    {
        return GetValidationError(code, clientName) is null;
    }

    public void ValidateAndThrow(string code, string clientName)
    {
        var error = GetValidationError(code, clientName);

        if (error is not null)
        {
            throw new OTPValidationException(error);
        }
    }

    public OTPValidateResult ValidateAndReason(string code, string clientName)
    {
        var error = GetValidationError(code, clientName);

        return new OTPValidateResult(error is null, error);
    }

    private string? GetValidationError(string code, string clientName)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var otpItem = FindOTPItem(clientName.Trim());

        if (otpItem is null)
        {
            return Options.Errors.CodeDoesNotExist;
        }

        var isCodeMatch = IsCodeMatch(otpItem.Code, code.Trim());
        var isExpired = IsExpired(otpItem);

        return Redeem(clientName.Trim(), otpItem, isCodeMatch, isExpired);
    }

    private void EnsureGenerateIsAllowed(string clientName)
    {
        var now = _timeProvider.GetUtcNow();
        var windowStart = now.AddSeconds(-Options.GenerateWindowSeconds);

        lock (_generateLock)
        {
            var history = _generateHistory.GetOrAdd(clientName, p => new List<DateTimeOffset>());

            history.RemoveAll(p => p < windowStart);

            if (history.Count >= Options.MaxGeneratePerWindow)
            {
                throw new OTPGenerateLimitException(Options.Errors.GenerateLimitExceeded);
            }

            history.Add(now);
        }
    }

    private void RemoveStaleGenerateHistory()
    {
        var windowStart = _timeProvider.GetUtcNow().AddSeconds(-Options.GenerateWindowSeconds);

        lock (_generateLock)
        {
            foreach (var history in _generateHistory)
            {
                history.Value.RemoveAll(p => p < windowStart);

                if (history.Value.Count == 0)
                {
                    _generateHistory.TryRemove(history.Key, out _);
                }
            }
        }
    }

    private OTPItem? FindOTPItem(string clientName)
    {
        _otpItems.TryGetValue(clientName, out var otpItem);

        return otpItem;
    }

    private bool IsCodeMatch(string storedCode, string providedCode)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(storedCode), Encoding.UTF8.GetBytes(providedCode));
    }

    private bool IsExpired(OTPItem otpItem)
    {
        return otpItem.ExpireIn < _timeProvider.GetUtcNow();
    }

    private string? Redeem(string clientName, OTPItem otpItem, bool isCodeMatch, bool isExpired)
    {
        lock (_validationLock)
        {
            if (!_otpItems.TryGetValue(clientName, out var currentItem) || !ReferenceEquals(currentItem, otpItem))
            {
                return Options.Errors.CodeIsInvalid;
            }

            if (isCodeMatch && otpItem.UsedAt is not null)
            {
                return Options.Errors.CodeIsUsed;
            }

            if (otpItem.Attempts >= Options.MaxAttempts)
            {
                return Options.Errors.MaxAttemptsExceeded;
            }

            if (!isCodeMatch)
            {
                otpItem.Attempts++;

                return Options.Errors.CodeIsInvalid;
            }

            if (isExpired)
            {
                return Options.Errors.CodeIsExpired;
            }

            otpItem.UsedAt = _timeProvider.GetUtcNow();

            return null;
        }
    }

    private void RemoveExpiredItems()
    {
        lock (_cleanupLock)
        {
            if (_lastCleanup.AddSeconds(Options.CleanupIntervalSeconds) > _timeProvider.GetUtcNow())
            {
                return;
            }

            _lastCleanup = _timeProvider.GetUtcNow();
        }

        foreach (var otpItem in _otpItems)
        {
            if (IsExpired(otpItem.Value))
            {
                _otpItems.TryRemove(otpItem.Key, out _);
            }
        }

        RemoveStaleGenerateHistory();
    }

    private string CreateMessage(string? patternName, string code)
    {
        if (patternName is null)
        {
            return code;
        }

        var pattern = Options.MessagePatterns.FirstOrDefault(p => p.Name == patternName);

        if (pattern is null)
        {
            return code;
        }

        return pattern.Pattern.Replace("{code}", code);
    }
}
