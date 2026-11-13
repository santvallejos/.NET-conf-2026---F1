using Microsoft.Extensions.Options;

namespace PitWall.Tests.Jev;

/// <summary>Monitor con valor mutable desde el test (simula el cambio en caliente).</summary>
internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
