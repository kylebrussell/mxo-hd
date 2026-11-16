using hds;
namespace HeadlessClient;

// A saved/live observation pair, never a cached moving NPC position or a new authority source.
public static class MachinesCurrentNpcObservation
{
    public static ViewCreation? Read(MissionProgress.SavedState expected, MissionProgress.SavedState current,
        ushort npc, OwnViewLifeSnapshot originalLife, OwnViewLifeSnapshot live, string handle)
    {
        if (!originalLife.Current || !live.Current || !originalLife.OwnViewReady || !live.OwnViewReady ||
            !ReferenceEquals(originalLife.Owner, live.Owner) || originalLife.Life != live.Life)
            throw new InvalidDataException("NPC observation owner/life changed");
        ZionAfter1212RidePlan.SameEntry(expected, current);
        var before = expected.Mobs.Where(m => m.Npc == npc).ToArray();
        var now = current.Mobs.Where(m => m.Npc == npc).ToArray();
        if (before.Length != 1 || now.Length != 1 || !Guid.TryParseExact(before[0].Identity, "N", out _) ||
            before[0].Identity != now[0].Identity || before[0].Phase != now[0].Phase ||
            before[0].Name != now[0].Name || before[0].Level != now[0].Level ||
            before[0].Friendly != now[0].Friendly || before[0].Rsi != now[0].Rsi)
            throw new InvalidDataException("NPC observation accepted identity changed");
        return ZionAfter1212FollowerReadiness.CurrentNpc(live, handle, now[0], current.Instance);
    }
}
