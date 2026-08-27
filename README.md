# HedKam.OTPService

A small, dependency-light one-time-password service for .NET. It issues numeric codes, keeps track of them, and verifies them — with cryptographic randomness, single-use redemption, a per-client guess limit, and per-client issuance throttling built in.

Targets **.NET 10**. The only dependency is `Microsoft.Extensions.Options`.

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
        try
        {
            var otp = otpService.Generate(phoneNumber);

            // otp.Code    -> "4821"        send this to the user
            // otp.Message -> "4821"        or a formatted message, if you configured patterns

            return Ok();
        }
        catch (OTPGenerateLimitException)
        {
            return StatusCode(429);
        }
    }

    public IActionResult Verify(string phoneNumber, string code)
    {
        return otpService.Validate(code, phoneNumber) ? Ok() : Unauthorized();
    }
}
```

There is no handle to round-trip: you verify with the same client name you generated for. Nothing needs to be stored on the client between the two calls.

> **The default is one code per client per 60 seconds.** A second `Generate` inside that window throws `OTPGenerateLimitException`, so handle it — it is the first thing you will hit when testing a resend button. Raise `MaxGeneratePerWindow` if your flow needs more.

`AddOTPService` registers the service as a **singleton**. Do not register `OTPService` yourself as scoped or transient — each instance owns its own store, so a code issued in one request would not be found in the next.

## How it works

- **A client has at most one live code.** Calling `Generate` again for the same client replaces the previous one, which stops working immediately.
- **A code is consumed on first successful verification.** Verifying again fails with `CodeIsUsed`.
- **Wrong guesses are counted.** After `MaxAttempts` failures the code is locked, even for the correct value. Failed guesses do not consume the code.
- **Issuing a new code resets that counter**, which is exactly why `MaxGeneratePerWindow` exists — without it a caller could clear `MaxAttempts` at will. The two limits are only meaningful together: with the defaults, a client gets 5 guesses per minute.
- Client names are trimmed, so `"acme"` and `" acme "` are the same client.

## Configuration

```csharp
builder.Services.AddOTPService(options =>
{
    options.DigitsCount = 6;
    options.ExpireInMinutes = 5;
    options.MaxAttempts = 3;
    options.MaxGeneratePerWindow = 3;
    options.MessagePatterns = [new OTPMessagePattern("sms", "Your code is {code}")];
});
```

| Option | Default | Meaning |
|---|---|---|
| `DigitsCount` | `4` | Length of the generated code. Must be 1–10. |
| `AllowDuplicateDigit` | `true` | Whether a digit may repeat within one code. |
| `AllowZero` | `true` | Whether `0` may appear. With `false` the pool is 1–9. |
| `ExpireInMinutes` | `10` | How long a code stays valid. Must be greater than 0. |
| `MaxAttempts` | `5` | Failed guesses allowed against a code before it is locked. |
| `MaxGeneratePerWindow` | `1` | Codes one client may request per window. |
| `GenerateWindowSeconds` | `60` | Length of that issuance window. |
| `CleanupIntervalSeconds` | `60` | How often expired codes are swept. `0` sweeps on every `Generate`. |
| `MessagePatterns` | empty | Named templates; `{code}` is substituted into `OTPResult.Message`. |
| `Errors` | see below | The message strings returned or thrown on failure. |

Options are validated when the host starts, so a bad configuration fails immediately with a named `OptionsValidationException` instead of silently disabling the service. Enforced rules:

- `DigitsCount` between 1 and 10
- `ExpireInMinutes`, `MaxAttempts`, `MaxGeneratePerWindow`, `GenerateWindowSeconds` all greater than 0
- `CleanupIntervalSeconds` not negative
- `DigitsCount` must fit the available digit pool when `AllowDuplicateDigit` is `false` — asking for 10 unique digits with `AllowZero = false` leaves only 9 to choose from, so it is rejected rather than looping

## Verifying

Three methods run the same checks and differ only in how they report failure:

```csharp
bool ok = otpService.Validate(code, clientName);

otpService.ValidateAndThrow(code, clientName);           // throws OTPValidationException

var result = otpService.ValidateAndReason(code, clientName);
// result.IsValid, result.ErrorMessage
```

Failure reasons come from `OTPServiceOptions.Errors`:

| Property | Raised when |
|---|---|
| `CodeDoesNotExist` | No code is stored for that client — never issued, expired and swept, or the name does not match. |
| `CodeIsInvalid` | The code does not match the client's current code. |
| `CodeIsExpired` | The code is past `ExpireInMinutes`. |
| `CodeIsUsed` | The code was already redeemed. |
| `MaxAttemptsExceeded` | Too many failed guesses against this code. |
| `GenerateLimitExceeded` | Carried by `OTPGenerateLimitException` from `Generate`. |

Each defaults to its own name, so you can replace them with localized text:

```csharp
builder.Services.AddOTPService(options =>
{
    options.Errors.CodeIsInvalid = "کد وارد شده صحیح نیست";
    options.Errors.CodeIsExpired = "کد منقضی شده است";
});
```

## Message patterns

`Generate` takes an optional pattern name, and `OTPResult.Message` comes back rendered:

```csharp
options.MessagePatterns = [new OTPMessagePattern("sms", "Your code is {code}")];

otpService.Generate(phoneNumber, "sms").Message;   // "Your code is 4821"
otpService.Generate(phoneNumber).Message;          // "4821"
otpService.Generate(phoneNumber, "unknown").Message; // "4821" — unknown names fall back to the raw code
```

## Argument handling

`clientName` is yours, so it is validated strictly: `null`, empty, or whitespace throws `ArgumentException`.

`code` comes from an end user, so an empty or whitespace value is treated as a **wrong code**, not a programming error — it returns `CodeIsInvalid` and counts as an attempt. Only a `null` code throws.

## Substituting dependencies

Register your own before `AddOTPService` and it will be used instead:

```csharp
builder.Services.AddSingleton<ICodeGenerator, MyCodeGenerator>();   // e.g. alphanumeric codes
builder.Services.AddSingleton<TimeProvider>(fakeClock);             // e.g. testing expiry
builder.Services.AddOTPService();
```

## Thread safety

`IOTPService` is safe to use concurrently, which is what makes the singleton registration correct. Redemption and attempt counting are atomic: a code racing across several threads is redeemed exactly once, and no failed guess goes uncounted.

## Limitations

Codes are held **in memory, in a single process**. The library does not run across multiple servers and does not survive a restart — a code issued by one instance is unknown to every other. A shared backing store would be needed for that.

Because a client holds only one live code, a user who requests a code on two devices can only finish on the most recent one.

Verification is by client name, so anyone who knows a user's identifier can burn that user's guesses and force them to wait for a new code. This is inherent to identity-based OTP; the expiry window and issuance limit bound it.

Cleanup of expired codes runs inside `Generate`. A service that issues nothing for a long stretch keeps expired entries until the next call.

## Licence

MIT — see [LICENSE](LICENSE).
