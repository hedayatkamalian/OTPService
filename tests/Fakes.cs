using Microsoft.Extensions.Options;
using Shouldly;

namespace HedKam.Services.Tests;

public class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
}

public class MutableOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
{
    public MutableOptionsMonitor(TOptions currentValue)
    {
        CurrentValue = currentValue;
    }

    public TOptions CurrentValue { get; set; }

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}

public class GateTimeProvider : TimeProvider
{
    private readonly ManualResetEventSlim _reached = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _released = new ManualResetEventSlim(false);
    private int _blockedThreadId;
    private int _blockedReadNumber;
    private int _readCount;
    private TimeSpan _offset = TimeSpan.Zero;

    public void BlockCurrentThreadOnNextRead() => BlockCurrentThreadOnRead(1);

    public void BlockCurrentThreadOnRead(int readNumber)
    {
        _blockedThreadId = Environment.CurrentManagedThreadId;
        _blockedReadNumber = readNumber;
        _readCount = 0;
    }

    public void WaitUntilBlocked() => _reached.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();

    public void Release() => _released.Set();

    public void Advance(TimeSpan delta) => _offset = _offset.Add(delta);

    public override DateTimeOffset GetUtcNow()
    {
        if (Environment.CurrentManagedThreadId == _blockedThreadId && ++_readCount == _blockedReadNumber)
        {
            _blockedThreadId = 0;

            _reached.Set();
            _released.Wait(TimeSpan.FromSeconds(5));
        }

        return DateTimeOffset.UtcNow.Add(_offset);
    }
}
