using System.Diagnostics;

namespace LocalGo.Application.Common;

/// <summary>P-1: per-request timing buckets (save / notify) for the Server-Timing header.</summary>
public sealed class ServerTiming
{
    private static readonly AsyncLocal<ServerTiming?> CurrentSlot = new();
    private long saveTicks;
    private long notifyTicks;

    public static ServerTiming? Current => CurrentSlot.Value;

    public static ServerTiming Begin() => CurrentSlot.Value = new ServerTiming();

    public double SaveMs => TimeSpan.FromTicks(Interlocked.Read(ref saveTicks)).TotalMilliseconds;
    public double NotifyMs => TimeSpan.FromTicks(Interlocked.Read(ref notifyTicks)).TotalMilliseconds;

    public static void AddSave(TimeSpan t) { if (Current is { } c) Interlocked.Add(ref c.saveTicks, t.Ticks); }
    public static void AddNotify(TimeSpan t) { if (Current is { } c) Interlocked.Add(ref c.notifyTicks, t.Ticks); }

    public static long Start() => Stopwatch.GetTimestamp();
    public static TimeSpan Since(long start) => Stopwatch.GetElapsedTime(start);
}
