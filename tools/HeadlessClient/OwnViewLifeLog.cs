namespace HeadlessClient;

public sealed record OwnViewLifeEvent(long Life, int Section, byte[] Bytes);
public sealed record OwnViewLifeSnapshot(long Life, bool OwnViewReady, int OwnBirthSection,
    IReadOnlyList<OwnViewLifeEvent> Events)
{
    internal OwnViewLifeLog? Owner { get; init; }
    public bool Current => Owner?.IsCurrent(Life) == true;
}

/// <summary>Observation only; never changes transport, ACKs, reset handling or existing view logs.</summary>
public sealed class OwnViewLifeLog
{
    private long life;
    private bool closed;
    private bool resetSeen;
    private byte? ownSpawn;
    private int ownBirthSection = -1;
    private readonly List<OwnViewLifeEvent> events = new();

    private void NewLife()
    {
        Interlocked.Increment(ref life);
        ownSpawn = null;
        ownBirthSection = -1;
        events.Clear();
    }

    public void Observe(bool reset, byte[]? section, int sectionIndex, IReadOnlyList<ViewCreation> parsed)
    {
        if (closed) return;
        if (reset && !resetSeen) NewLife();
        resetSeen = reset;
        if (reset || section == null) return;
        if (BytePattern.IndexOf(section, new byte[] { 1, 0, 1, 1, 0, 2, 0 }, 0) >= 0) NewLife();
        foreach (var own in parsed.Where(v => v.ViewId == 2 && v.Goid == 12))
        {
            if (ownSpawn.HasValue && ownSpawn.Value != own.SpawnId) NewLife();
            if (!ownSpawn.HasValue) ownBirthSection = sectionIndex;
            ownSpawn = own.SpawnId;
        }
        events.Add(new OwnViewLifeEvent(life, sectionIndex, section));
    }

    internal bool IsCurrent(long expected) => !Volatile.Read(ref closed) && Interlocked.Read(ref life) == expected;
    public void EndLife() { Volatile.Write(ref closed, true); NewLife(); }
    public OwnViewLifeSnapshot Snapshot() => new(life, !closed && ownSpawn.HasValue, ownBirthSection, events.ToArray()) { Owner=this };
}
