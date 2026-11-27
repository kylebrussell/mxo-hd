using System;
using System.Threading;
namespace hds
{

/// <summary>Serializes outgoing preparation/dispatch with incoming comm-reset header publication.</summary>
internal sealed class WorldSendFence
{
    private readonly object sync = new object();
    private long epoch;
    private bool resetPending;

    public long Capture() { lock (sync) return epoch; }
    // Observation only, not serialized execution. Read the epoch on both sides
    // so an entire reset/resume between reads cannot validate an older lifetime.
    public bool IsCurrent(long expected) => Interlocked.Read(ref epoch)==expected &&
        !Volatile.Read(ref resetPending) && Interlocked.Read(ref epoch)==expected;
    public bool TryRun(long expected, Action action)
    {
        lock (sync)
        {
            if (expected != epoch || resetPending) return false;
            action();
            return true;
        }
    }
    /// <summary>Closure-free equivalent of <see cref="TryRun"/> for hot flush paths; pair with <see cref="Exit"/> in finally.</summary>
    public bool TryEnter(long expected, out bool lockTaken)
    {
        lockTaken = false;
        Monitor.Enter(sync, ref lockTaken);
        if (expected != epoch || resetPending)
        {
            Monitor.Exit(sync);
            lockTaken = false;
            return false;
        }
        return true;
    }
    public void PublishIncoming(bool reset, Action publish)
    {
        lock (sync)
        {
            if (reset) { epoch++; resetPending = true; }
            publish();
        }
    }
    /// <summary>Closure-free equivalent of <see cref="PublishIncoming"/> for hot decrypt paths; pair with <see cref="Exit"/> in finally.</summary>
    public void EnterIncoming(bool reset, out bool lockTaken)
    {
        lockTaken = false;
        Monitor.Enter(sync, ref lockTaken);
        if (reset) { epoch++; resetPending = true; }
    }
    public void Exit(bool lockTaken)
    {
        if (lockTaken) Monitor.Exit(sync);
    }
    public bool ApplyReset(long expected, Action reset)
    {
        lock (sync)
        {
            if (expected != epoch || !resetPending) return false;
            reset();
            return true;
        }
    }
    public void Resume(long expected)
    {
        lock (sync) if (expected == epoch) resetPending = false;
    }
}

}
