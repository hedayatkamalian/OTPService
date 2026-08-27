# HedKam.OTPService

A small, dependency-light one-time-password service for .NET. It generates numeric OTP codes, tracks them, and validates them — with cryptographic randomness, single-use redemption, per-code attempt limits, and per-client issuance throttling built in.

## Install

```
dotnet add package HedKam.OTPService
```

## Quick start

```csharp
builder.Services.AddOTPService();
```

```csharp
public class LoginController(IOTPService otpService)
{
    public IActionResult Send(string phoneNumber)
    {
        var otp = otpService.Generate(phoneNumber);

        // otp.Code    -> "4821"
        // otp.Message -> the code, or a formatted message if you configured patterns

        return Ok();
    }

    public IActionResult Verify(string phoneNumber, string code)
    {
        return otpService.Validate(code, phoneNumber) ? Ok() : Unauthorized();
    }
}
```

`AddOTPService` registers the service as a singleton. **Do not register `OTPService` yourself as scoped or transient** — each instance owns its own code store, so a code generated in one request would not be found in the next.

## Configuration

```csharp
builder.Services.AddOTPService(options =>
{
    options.DigitsCount = 6;
    options.ExpireInMinutes = 5;
    options.MaxAttempts = 3;
    options.MessagePatterns = [new OTPMessagePattern("sms", "Your code is {code}")];
});
```

Options are validated at host startup. A bad value throws `OptionsValidationException` with a named message rather than silently disabling the service.

| Option | Default | Meaning |
|---|---|---|
| `DigitsCount` | `4` | Length of the generated code. Must be 1–10. |
| `AllowDuplicateDigit` | `true` | Whether a digit may repeat within one code. |
| `AllowZero` | `true` | Whether `0` may appear. With `false` the pool is 1–9, so unique codes are capped at 9 digits. |
| `ExpireInMinutes` | `10` | How long a code stays valid. Must be greater than 0. |
| `MaxAttempts` | `5` | Failed verification attempts allowed per code before it is locked. |
| `MaxGeneratePerWindow` | `1` | Codes a single client may request per window. |
| `GenerateWindowSeconds` | `60` | Length of that issuance window. |
| `CleanupIntervalSeconds` | `60` | How often expired codes are swept from the store. `0` sweeps on every `Generate`. |
| `MessagePatterns` | empty | Named templates; `{code}` is substituted into `OTPResult.Message`. |
| `Errors` | see below | The message strings returned or thrown on failure. |

## Validating

Three methods run the same checks and differ only in how they report failure:

```csharp
bool ok = otpService.Validate(code, clientName);

otpService.ValidateAndThrow(code, clientName);   // throws OTPValidationException

var result = otpService.ValidateAndReason(code, clientName);
// result.IsValid, result.ErrorMessage
```

Verification is by client name alone — there is no handle to round-trip. The client name you pass to `Validate` must be the same one you passed to `Generate`.

**A client has at most one live code.** Calling `Generate` again for the same client replaces the previous code, which stops working immediately. This also resets that client's failed-attempt counter, which is why `MaxGeneratePerWindow` matters: without it, a caller could reset `MaxAttempts` at will.

A code is **consumed on first successful validation** — a second attempt fails with `CodeIsUsed`. Failed attempts do not consume it, but they count against `MaxAttempts`.

Failure reasons come from `OTPServiceOptions.Errors` and default to their own names, so you can swap them for localized text:

| Property | Raised when |
|---|---|
| `CodeDoesNotExist` | No code is stored for that client — never issued, already swept, or the name does not match. |
| `CodeIsInvalid` | The code does not match the client's current code. |
| `CodeIsExpired` | The code is past `ExpireInMinutes`. |
| `CodeIsUsed` | The code was already redeemed. |
| `MaxAttemptsExceeded` | Too many failed attempts for this code. |
| `GenerateLimitExceeded` | Thrown by `Generate` as `OTPGenerateLimitException`. |

## Rate limiting

`Generate` throttles per client name over a sliding window:

```csharp
try
{
    var otp = otpService.Generate(phoneNumber);
}
catch (OTPGenerateLimitException)
{
    return StatusCode(429);
}
```

`MaxAttempts` protects a single code from being guessed; `MaxGeneratePerWindow` stops a caller from minting unlimited codes to work around it. Both are needed.

To effectively disable throttling, set `MaxGeneratePerWindow` to a large value — it must be greater than 0.

## Customising code generation

Replace the generator by registering your own before `AddOTPService`:

```csharp
builder.Services.AddSingleton<ICodeGenerator, MyCodeGenerator>();
builder.Services.AddOTPService();
```

`TimeProvider` is resolved the same way, which makes expiry testable with a fake clock.

## Limitations

Because a client holds only one live code, a user who requests a code on two devices can only complete the flow on the most recent one.

Codes are held **in memory, in a single process**. The library does not support running across multiple servers or surviving a restart — a code issued by one instance is unknown to every other. If you need that, the store would have to move behind a shared backing service.

Cleanup of expired codes runs inside `Generate`. A service that issues nothing for a long stretch keeps expired entries until the next call.

## Licence

MIT — see [LICENSE](LICENSE).
