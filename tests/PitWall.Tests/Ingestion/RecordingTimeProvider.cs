namespace PitWall.Tests.Ingestion;

/// <summary>Registra cada espera pedida (<c>Task.Delay</c> con este proveedor) y la completa sin esperar.</summary>
internal sealed class RecordingTimeProvider : TimeProvider
{
    private readonly List<TimeSpan> _delays = [];

    public IReadOnlyList<TimeSpan> Delays => _delays;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_delays)
        {
            _delays.Add(dueTime);
        }

        ThreadPool.QueueUserWorkItem(_ => callback(state));
        return new CompletedTimer();
    }

    private sealed class CompletedTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
