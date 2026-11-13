using System;
namespace hds
{

/// <summary>Serializes outgoing preparation/dispatch with incoming comm-reset header publication.</summary>
internal sealed class WorldSendFence
{
    private readonly object sync = new object();
    private long epoch;
    private bool resetPending;

    public long Capture() { lock (sync) return epoch; }
    public bool TryRun(long expected, Action action)
    {
        lock (sync)
        {
            if (expected != epoch || resetPending) return false;
            action();
            return true;
        }
    }
    public void PublishIncoming(bool reset, Action publish)
    {
        lock (sync)
        {
            if (reset) { epoch++; resetPending = true; }
            publish();
        }
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
