using System.Threading.Channels;

using PitWall.Domain;

namespace PitWall.Replay;

/// <summary>Suscripción a los <see cref="RaceState"/> del replay; al liberarla se completa su canal.</summary>
public sealed class ReplaySubscription : IDisposable
{
    private readonly Action _unsubscribe;
    private int _disposed;

    internal ReplaySubscription(ChannelReader<RaceState> reader, Action unsubscribe)
    {
        Reader = reader;
        _unsubscribe = unsubscribe;
    }

    public ChannelReader<RaceState> Reader { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _unsubscribe();
        }
    }
}
