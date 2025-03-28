

using HedKam.Services.Exceptions;
using HedKam.Services.Models;
using HedKam.Services.Options;
using Microsoft.Extensions.Options;

namespace HedKam.Services;
public class OTPService : IOTPService
{
    private const int MAX_OTP_ITEMS = 100;
    private OTPServiceOptions _options;
    private static List<OTPItem> OTPItems = new List<OTPItem>();
    private CodeGenerator _codeGenerator => new CodeGenerator();

    public OTPService(IOptionsMonitor<OTPServiceOptions> options)
    {
        _options = options.CurrentValue;
        options.OnChange(p => _options = p);
    }

    public OTPResult Generate(string clientName, string? patternName = null)
    {
        RemoveExpiredItems();

        var otp = new OTPItem
        {
            Code = _codeGenerator.Generate(_options.DigitsCount, _options.AllowDuplicateDigit, _options.AllowZero),
            ClientName = clientName.Trim(),
            ExpireIn = DateTimeOffset.UtcNow.AddMinutes(_options.ExpireInMinutes),
            TrackId = Guid.NewGuid()
        };

        OTPItems.Add(otp);

        return new OTPResult(otp.TrackId, otp.Code, CreateMessage(patternName, otp.Code));
    }

    public bool Validate(string code, Guid trackId, string clientName)
    {
        var otpItem = OTPItems.FirstOrDefault(p => p.Code == code.Trim() && p.TrackId == trackId && p.ClientName == clientName);

        if (otpItem is null)
        {
            return false;
        }
        else
        {
            return IsExpired(otpItem) ? false : true;
        }
    }

    public void ValidateAndThrow(string code, Guid trackId, string clientName)
    {
        var otpItem = OTPItems.FirstOrDefault(p => p.TrackId == trackId);

        if (otpItem is null)
        {
            throw new OTPValidationException(_options.Errors.TrackIdDoesNotExist);
        }

        if (otpItem.ClientName != clientName.Trim())
        {
            throw new OTPValidationException(_options.Errors.ClientNameDoesNotMatch);
        }

        if (otpItem.Code != code.Trim())
        {
            throw new OTPValidationException(_options.Errors.CodeIsInvalid);
        }

        if (IsExpired(otpItem))
        {
            throw new OTPValidationException(_options.Errors.CodeIsExpired);
        }
    }

    public OTPValidateResult ValidateAndReason(string code, Guid trackId, string clientName)
    {
        var otpItem = OTPItems.FirstOrDefault(p => p.TrackId == trackId);

        if (otpItem is null)
        {
            return new OTPValidateResult(false, _options.Errors.TrackIdDoesNotExist);
        }

        if (otpItem.ClientName != clientName.Trim())
        {
            return new OTPValidateResult(false, _options.Errors.ClientNameDoesNotMatch);
        }

        if (otpItem.Code != code.Trim())
        {
            return new OTPValidateResult(false, _options.Errors.CodeIsInvalid);
        }

        if (IsExpired(otpItem))
        {
            return new OTPValidateResult(false, _options.Errors.CodeIsExpired);
        }

        return new OTPValidateResult(true, null);
    }

    private bool IsExpired(OTPItem otpItem)
    {
        return otpItem.ExpireIn < DateTimeOffset.UtcNow;
    }

    private void RemoveExpiredItems()
    {
        if (OTPItems.Count > MAX_OTP_ITEMS)
        {
            OTPItems.RemoveAll(p => IsExpired(p));
        }
    }

    private string CreateMessage(string? PatternName, string Code)
    {
        if (PatternName == null)
        {
            return Code;
        }
        else
        {
            var pattern = _options.MessagePattern.FirstOrDefault(p => p.Name == PatternName);
            if (pattern == null)
            {
                return Code;
            }
            else
            {
                return pattern.Pattern.Replace("{code}", Code);
            }
        }
    }
}
