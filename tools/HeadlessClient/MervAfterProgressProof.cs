using hds;
namespace HeadlessClient;
/// <summary>Pure QA predicates for one normal accepted mission, never a mutation or gameplay policy.</summary>
public static class MervAfterProgressProof
{
    public sealed record Acceptance(int Mission,ushort Instance,string Identity,long Started);
    public sealed record InventoryRow(long Character,int Slot,uint Goid,int Count);
    public static Acceptance Capture(MissionProgress.SavedState state)
    {
        if(state.Mission<=0||state.Instance==0||state.Started<=0||!Guid.TryParseExact(state.Identity,"N",out _))
            throw new InvalidDataException("No exact accepted mission token");
        return new(state.Mission,state.Instance,state.Identity,state.Started);
    }
    public static bool Owns(Acceptance scope,MissionProgress.SavedState? state)=>state!=null&&
        state.Mission==scope.Mission&&state.Instance==scope.Instance&&state.Identity==scope.Identity&&state.Started==scope.Started;
    static bool Ack(IReadOnlyList<byte[]> records,int cursor,int goal)=>cursor>=0&&cursor<=records.Count&&goal>0&&goal<=255&&
        records.Skip(cursor).Any(b=>BytePattern.Parse($"80 a0 {goal:x2} 06 00 01").IsMatch(b));
    public static bool Advanced(Acceptance scope,int phase,int finalPhase,MissionProgress.SavedState? current,
        MissionProgress.SavedState? receipt,long completions,IReadOnlyList<byte[]> records,int missionCursor)
    {
        if(phase<0||finalPhase<phase)return false;
        if(phase<finalPhase)return Owns(scope,current)&&!current!.Flags.GetValueOrDefault("failed")&&current.Phase==phase+1;
        return current==null&&completions==1&&Owns(scope,receipt)&&!receipt!.Flags.GetValueOrDefault("failed")&&receipt.Phase==phase&&
            missionCursor>=0&&missionCursor<=records.Count&&records.Skip(missionCursor).Any(b=>b.Length==26&&b[0]==0x80&&b[1]==0xa2&&b[2]==0);
    }
    public static bool GoalDone(Acceptance scope,int phase,int finalPhase,int goal,bool optional,
        MissionProgress.SavedState? current,MissionProgress.SavedState? receipt,long completions,
        IReadOnlyList<byte[]> records,int phaseCursor,int missionCursor)
    {
        if(phase<0||phase>finalPhase||!Ack(records,phaseCursor,goal))return false;
        if(Owns(scope,current)&&current!.Phase==phase)return !current.Flags.GetValueOrDefault("failed")&&current.Done.Contains(goal);
        // Optional actions must be observed in their own phase before a final mandatory action can advance it.
        if(optional)return false;
        if(!Advanced(scope,phase,finalPhase,current,receipt,completions,records,missionCursor))return false;
        return current!=null?current.EverDone.Contains(goal):receipt!.Done.Contains(goal);
    }
    // Mission-item insertion omits inventory.count; schema default0 is the nonstackable row's stored value.
    // One owned item is proved by one row, not an invented count=1 stack field.
    public static InventoryRow RequireOwnedItem(IReadOnlyList<InventoryRow> rows,long character,uint goid,int slot,int storedCount=0)
    {
        if(rows.Count!=1||character<=0||slot<1||slot>96||rows[0].Character!=character||rows[0].Goid!=goid||rows[0].Slot!=slot||rows[0].Count!=storedCount)
            throw new InvalidDataException("Actual owned inventory must contain one exact character/item/slot/count");
        return rows[0];
    }
}
