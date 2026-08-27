# Known Issues

**None open.** All 23 issues found in the original review have been fixed and verified.

**Verification context:** .NET SDK 10.0.400. The solution builds with **0 warnings** (`TreatWarningsAsErrors` is now on in both projects) and all **67 tests** pass. The suite has grown from 11 tests to 67 across four files.

Two of the follow-up items have since been done as well: per-client issuance throttling, and NuGet packaging. Verification was then changed to be by client name alone — `TrackId` is gone from the public API.

---

## What changed

### Security

| Was | Now |
|---|---|
| `new Random(DateTime.Now.Second)` — 1000 rapid calls produced **one** distinct code | `RandomNumberGenerator.GetInt32`, 1000/1000 distinct, uniform digit distribution |
| A code could be redeemed an unlimited number of times | Consumed on first success (`UsedAt`), replay reported as `CodeIsUsed` |
| No attempt limit — all 10,000 four-digit codes could be tried | `MaxAttempts` (default 5); a full brute-force run now gets **0** accepted |
| Code compared with `==`, leaking the matching prefix through timing | `CryptographicOperations.FixedTimeEquals` |
| `MaxAttempts` was per code, so minting unlimited codes worked around it | `MaxGeneratePerWindow` / `GenerateWindowSeconds` throttle issuance per client |

### Correctness and reliability

| Was | Now |
|---|---|
| `Generate(10, false, false)` looped forever | Rejected by a pool-aware guard; the no-duplicates path draws without replacement |
| `static List` mutated with no synchronisation — `NullReferenceException` under 8 threads | Instance `ConcurrentDictionary`; lookups went from O(n) to O(1) (34 ms → 0.016 ms at 50k items) |
| Store shared by every instance in the process | Per-instance, with `AddOTPService` pinning the singleton lifetime |
| Sweep only ran above 100 items and only removed expired ones; `Count` taken on every `Generate` | Interval-based sweep (5509 ms → 0.03 ms per 1000 calls at 50k items) |
| `Validate` and `ValidateAndReason` gave opposite answers for the same padded client name | One shared validation core behind all three methods |
| `NullReferenceException` on a null `clientName`, and on `Generate(name, pattern)` with no patterns configured | Argument guards, and `MessagePatterns` defaults to `[]` |
| Bad configuration silently disabled the service | `ValidateOnStart()` rejects it at host startup with a named message |

### Design and tests

| Was | Now |
|---|---|
| No DI helper; a `Scoped` registration silently broke validation across requests | `AddOTPService(...)` registers the correct lifetimes via `TryAddSingleton` |
| `CodeGenerator` newed on every property access, impossible to fake | `ICodeGenerator` injected |
| Clock read directly from `DateTimeOffset.UtcNow` | `TimeProvider` injected, so expiry is testable without sleeping |
| `OnChange` subscription leaked; `_options` written unsynchronised | Caching dropped; `Options` reads `CurrentValue` at each use |
| `Configure<OTPServiceOptions>(p => p = options)` — a no-op; tests silently ran on defaults | Single `CreateService(Action<OTPServiceOptions>)` path through `AddOTPService` |
| Five duplicated `[InlineData(8)]` collapsed into one test case by the runner | Explicit loops; the eight `xUnit1025` warnings are gone |
| 13 build warnings ignored, one of them a real production `NullReferenceException` | 0 warnings, `TreatWarningsAsErrors` on |
| `DEFAULT_EXPRIE_IN_MINUTES`, `MessagePattern`, `Lenght` | Spelling corrected; `MessagePattern` → `MessagePatterns` |
| Moq referenced but never used | Removed |
| No package identity, licence, or README | `HedKam.OTPService` 1.0.0 packs with MIT licence, README, and symbols |

---

## Breaking changes

Three public API changes were made. Nothing consumes this library yet, so they were taken now rather than deferred:

1. **`OTPService` constructor** — was `OTPService(IOptionsMonitor<OTPServiceOptions>)`, now takes `(IOptionsMonitor<OTPServiceOptions>, ICodeGenerator, TimeProvider)`. Use `services.AddOTPService()` rather than constructing it directly.
2. **`OTPServiceOptions.MessagePattern` → `MessagePatterns`** — it holds a collection.
3. **`OTPItem.Code` and `OTPItem.ClientName` are `required`** — this is what removed the last two `CS8618` warnings.
4. **`Generate` now throws `OTPGenerateLimitException`** once a client exceeds `MaxGeneratePerWindow` (default 1 per 60 seconds). Callers that issue codes in bulk must either handle it or raise the limit.
5. **The assembly is now `HedKam.OTPService.dll`**, renamed from `OTPService.dll` to match the package id.
6. **`TrackId` is removed.** `OTPResult` no longer carries it and the three validate methods take `(code, clientName)`. A client now has at most one live code — issuing a new one replaces the previous. `Errors.TrackIdDoesNotExist` became `Errors.CodeDoesNotExist`, and `Errors.ClientNameDoesNotMatch` was dropped as it can no longer occur.

---

## Suggested next steps

None of these are defects; they are the natural next increments.

- **`IOTPStore`.** The store is per-instance and in memory, so the library works in one process only and loses everything on restart. Extracting it behind an interface with an in-memory default would let callers plug in Redis or a database, and would make the DI lifetime stop mattering.
- **A scheduled sweep.** Cleanup still only runs when `Generate` is called. A service that issues nothing for a long stretch holds expired items until the next call. A background timer would decouple the two, at the cost of making the service `IDisposable`.
- **XML documentation.** `GenerateDocumentationFile` is off, so the published package ships no IntelliSense for its public API.
- **A CI workflow.** Nothing builds, tests, or packs automatically on push.
