using System.Text.Json;
using hds;
namespace HeadlessClient;
/// <summary>Retains an optional action already proved in its own phase; never grants an action or reward.</summary>
public sealed class ZionAfter101217OptionalProof
{
    public sealed record Receipt(int Mission,ushort Instance,string Identity,long Started,int Phase,int Goal,int PhaseCursor,int AckIndex,int ObservedRpcCount,string Session,string Checkpoint);
    sealed record Earned(Receipt Proof,object Session,byte[] Ack);
    readonly DisturbingTrendsProgressProof.Acceptance accepted;
    readonly int phase,finalPhase;
    readonly Dictionary<int,Earned> earned=new();
    readonly string sessionLabel=Guid.NewGuid().ToString("N");
    readonly Action<Receipt>? record;
    public ZionAfter101217OptionalProof(DisturbingTrendsProgressProof.Acceptance accepted,int phase,int finalPhase,Action<Receipt>? record=null)
    {this.accepted=accepted;this.phase=phase;this.finalPhase=finalPhase;this.record=record;}
    public bool Done(int goal,bool optional,MissionProgress.SavedState? current,MissionProgress.SavedState? terminal,long completions,IReadOnlyList<byte[]> records,int phaseCursor,int missionCursor,object session)
    {
        bool strict=DisturbingTrendsProgressProof.GoalDone(accepted,phase,finalPhase,goal,optional,current,terminal,completions,records,phaseCursor,missionCursor);
        if(!optional)return strict;
        if(!DisturbingTrendsProgressProof.Owns(accepted,current)||current!.Flags.GetValueOrDefault("failed")||current.Phase<phase||current.Phase>phase+1)
            throw new InvalidDataException("Optional proof owner/phase changed");
        if(earned.TryGetValue(goal,out var old))
        {
            var proof=old.Proof;
            if(!ReferenceEquals(old.Session,session)||proof.PhaseCursor!=phaseCursor||proof.AckIndex<phaseCursor||proof.AckIndex>=records.Count||!records[proof.AckIndex].SequenceEqual(old.Ack))
                throw new InvalidDataException("Optional proof RPC session/cursor changed");
            if(current.Phase==phase)
            {if(!strict)throw new InvalidDataException("Previously proved optional goal disappeared in its own phase");return true;}
            if(!DisturbingTrendsProgressProof.Advanced(accepted,phase,finalPhase,current,terminal,completions,records,missionCursor))
                throw new InvalidDataException("Optional proof requires exact owned next phase");
            return true;
        }
        if(current.Phase!=phase)throw new InvalidDataException("Optional goal advanced without prior same-phase proof");
        if(!strict)return false;
        int ack=Enumerable.Range(phaseCursor,records.Count-phaseCursor).First(i=>BytePattern.Parse($"80 a0 {goal:x2} 06 00 01").IsMatch(records[i]));
        var receipt=new Receipt(accepted.Mission,accepted.Instance,accepted.Identity,accepted.Started,phase,goal,phaseCursor,ack,records.Count,sessionLabel,JsonSerializer.Serialize(current,new JsonSerializerOptions{IncludeFields=true}));
        earned.Add(goal,new(receipt,session,(byte[])records[ack].Clone()));record?.Invoke(receipt);return true;
    }
}
