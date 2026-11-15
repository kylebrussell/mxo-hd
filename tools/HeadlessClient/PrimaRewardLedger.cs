using System.Buffers.Binary;

namespace HeadlessClient;

public sealed record PrimaLedgerOperation(string Step,string Kind,int RpcStart,int RpcEnd);
public sealed record PrimaLedgerEvent(int RpcIndex,string Kind,long Amount,uint Total,uint Type,string? Operation,string Raw,
    int? NpcWindowIndex=null,int? AcceptanceIndex=null);
public sealed record PrimaRewardLedgerResult(uint BaselineExperience,uint BaselineInformation,uint Experience,uint Information,
    long FightingExperience,long MissionExperience,long MissionInformation,long LootInformation,IReadOnlyList<PrimaLedgerEvent> Events);

/// <summary>Fixture-only, independent received-record accounting. No runtime builders or DB-derived expected deltas.</summary>
public static class PrimaRewardLedger
{
    public static bool HasCompleteMissionAwards(IReadOnlyList<byte[]> records,int expected)
    {
        if(expected<=0)throw new ArgumentOutOfRangeException(nameof(expected));
        // >= lets duplicates/extras reach the strict decoder and be refused,
        // rather than turning them into a misleading packet-arrival timeout.
        return new byte[]{0xe6,0xe5,0xe4}.All(op=>records.Count(b=>b.Length==10&&b[0]==0x80&&b[1]==op&&
            BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(6,4))==2)>=expected);
    }
    public static bool HasNoCompletionReplay(IReadOnlyList<byte[]> records)=>
        !records.Any(b=>b.Length>=2&&b[0]==0x80&&b[1]==0xa2);

    public static (uint Experience,uint Information) ReadBaseline(IReadOnlyList<byte[]> records)
    {
        int xp=Enumerable.Range(0,records.Count).FirstOrDefault(i=>records[i].Length>=2&&records[i][0]==0x80&&records[i][1]==0xe5,-1);
        int cash=Enumerable.Range(0,records.Count).FirstOrDefault(i=>records[i].Length>=2&&records[i][0]==0x80&&records[i][1]==0xe4,-1);
        if(xp<0||cash<0||records.Take(Math.Max(xp,cash)).Any(b=>b.Length>=2&&
            ((b[0]==0x80&&(b[1]==0xe6||b[1]==0xa2))||(b[0]==0x81&&b[1]==0x19))))
            throw new InvalidDataException("Prima earned ledger: baseline must precede earned events");
        uint Read(byte opcode)
        {
            var b=records.FirstOrDefault(r=>r.Length>=2&&r[0]==0x80&&r[1]==opcode);
            if(b==null||b.Length!=10||BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(6,4))!=0)
                throw new InvalidDataException("Prima earned ledger: missing/malformed initial total");
            return BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(2,4));
        }
        return(Read(0xe5),Read(0xe4));
    }
    public static PrimaRewardLedgerResult Reconcile(IReadOnlyList<byte[]> records,IReadOnlyList<PrimaLedgerOperation> operations,
        uint baselineExperience,uint baselineInformation,IReadOnlyList<uint> missionExperience,IReadOnlyList<uint> missionInformation,IReadOnlySet<int>? restoredBaselines=null)
    {
        void Require(bool condition,string message){if(!condition)throw new InvalidDataException("Prima earned ledger: "+message);}
        bool Op(byte[] b,byte a,byte c)=>b.Length>=2&&b[0]==a&&b[1]==c;
        uint U32(byte[] b,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset,4));
        Require(missionExperience.Count>0&&missionExperience.Count==missionInformation.Count,"literal mission arrays/counts");
        if(restoredBaselines!=null)foreach(int index in restoredBaselines)
            Require(index>=0&&index<records.Count&&records[index].Length==10&&records[index][0]==0x80&&
                (records[index][1]==0xe5||records[index][1]==0xe4)&&U32(records[index],6)==0,"restored boundary must name an exact type0 wallet baseline");
        var baseline=ReadBaseline(records);
        Require(baseline.Experience==baselineExperience&&baseline.Information==baselineInformation,"wire and pre-accept baseline mismatch");
        foreach(var o in operations)Require(o.RpcStart>=0&&o.RpcEnd>=o.RpcStart&&o.RpcEnd<=records.Count,"operation cursor bounds");
        var ordered=operations.OrderBy(o=>o.RpcStart).ToArray();
        for(int i=1;i<ordered.Length;i++)Require(ordered[i-1].RpcEnd<=ordered[i].RpcStart,"overlapping operation cursors");
        PrimaLedgerOperation? At(int index)=>ordered.SingleOrDefault(o=>o.RpcStart<=index&&index<o.RpcEnd);
        var events=new List<PrimaLedgerEvent>();
        var consumedWindows=new HashSet<int>();var consumedAcceptances=new HashSet<int>();
        bool xpStarted=false,infoStarted=false;uint xp=0,info=0;uint? pending=null;uint pendingType=0;
        int missionGains=0,missionCash=0,summaries=0;long fighting=0,missionXp=0,missionInfo=0,lootInfo=0;
        for(int i=0;i<records.Count;i++)
        {
            byte[] b=records[i];var operation=At(i);string? kind=null;long amount=0;uint total=0,type=0;
            int? npcWindowIndex=null,acceptanceIndex=null;
            if(Op(b,0x80,0xe6))
            {
                Require(b.Length==10,"80e6 length");uint gain=U32(b,2);type=U32(b,6);
                Require(xpStarted&&pending==null&&gain>0&&(type==1||type==2),"unexpected/missing/duplicate XP gain or type");
                if(type==1)
                {
                    Require(operation?.Kind is "Defeat" or "DefeatAll","fighting gain outside actual combat operation");
                    fighting+=gain;
                }
                else
                {
                    Require(missionGains<missionExperience.Count&&gain==missionExperience[missionGains],"ordered literal mission XP gain");
                    missionGains++;missionXp+=gain;
                }
                pending=gain;pendingType=type;kind="XP gain";amount=gain;total=xp;
            }
            else if(Op(b,0x80,0xe5))
            {
                Require(b.Length==10,"80e5 length");total=U32(b,2);type=U32(b,6);
                if(!xpStarted)
                {
                    Require(type==0&&pending==null&&total==baselineExperience,"initial wire XP differs from pre-accept baseline");
                    xpStarted=true;kind="XP baseline";
                }
                else if(type==0&&restoredBaselines?.Contains(i)==true)
                {Require(pending==null&&total==xp,"restored XP baseline differs from earned ledger");kind="Restored XP baseline";}
                else
                {
                    Require(pending.HasValue&&type==pendingType&&(long)xp+pending.Value==total,"XP total lacks exactly matching gain/type/arithmetic");
                    amount=pending!.Value;pending=null;kind="XP total";
                }
                xp=total;
            }
            else if(Op(b,0x80,0xe4))
            {
                Require(b.Length==10,"80e4 length");total=U32(b,2);type=U32(b,6);
                if(!infoStarted)
                {
                    Require(type==0&&total==baselineInformation,"initial wire cash differs from pre-accept baseline");
                    infoStarted=true;kind="Cash baseline";
                }
                else if(type==0&&restoredBaselines?.Contains(i)==true)
                {Require(total==info,"restored cash baseline differs from earned ledger");kind="Restored cash baseline";}
                else
                {
                    amount=(long)total-info;Require(amount>0,"unexplained spend/duplicate cash total");
                    if(type==2)
                    {
                        Require(missionCash<missionInformation.Count&&missionCash<missionGains&&amount==missionInformation[missionCash],"ordered literal mission cash credit");
                        missionCash++;missionInfo+=amount;kind="Mission cash";
                    }
                    else
                    {
                        Require(type==0&&operation?.Kind=="Loot","nonmission cash outside current Loot operation");
                        int windowIndex=Enumerable.Range(operation!.RpcStart,i-operation.RpcStart).LastOrDefault(n=>Op(records[n],0x81,0x19),-1);
                        var window=windowIndex<0?null:records[windowIndex];
                        Require(window!=null&&window.Length==26&&window.AsSpan(2,6).SequenceEqual(new byte[]{0,0,0,0,0x10,0})&&
                            window.AsSpan(12,10).SequenceEqual(new byte[]{0,0,0,0,1,0,0,0,0,8})&&U32(window,22)!=0,"missing/malformed current NPC loot window");
                        Require(U32(window!,8)==amount,"cash credit differs from actual NPC window amount");
                        Require(!consumedWindows.Contains(windowIndex),"NPC loot window already credited");
                        int nextWindow=Enumerable.Range(i+1,operation.RpcEnd-i-1).FirstOrDefault(n=>Op(records[n],0x81,0x19),operation.RpcEnd);
                        var accepted=Enumerable.Range(i+1,nextWindow-i-1).Where(n=>Op(records[n],0x81,0x20)).ToArray();
                        Require(accepted.Length==1&&records[accepted[0]].AsSpan().SequenceEqual(Convert.FromHexString("81200C0000000000000000000000")),
                            "one current acceptance required before next window/end");
                        Require(consumedAcceptances.Add(accepted[0]),"NPC loot acceptance already consumed");
                        consumedWindows.Add(windowIndex);npcWindowIndex=windowIndex;acceptanceIndex=accepted[0];
                        lootInfo+=amount;kind="NPC loot cash";
                    }
                }
                info=total;
            }
            else if(Op(b,0x80,0xe7))Require(false,"unexpected cash delta path in this profile");
            else if(Op(b,0x80,0xa2))
            {
                Require(b.Length==26&&b[2]==0&&summaries<missionExperience.Count,"success summary length/status/count");
                Require(U32(b,15)==missionExperience[summaries]&&U32(b,11)==missionInformation[summaries],"independent literal mission summary amounts/order");
                // The current server queues the summary before its gain/total
                // records. Compare ordered families at completion, not by an
                // invented summary-after-wallet ordering.
                summaries++;kind="Mission summary";
            }
            if(kind!=null)events.Add(new(i,kind,amount,total,type,operation?.Step,Convert.ToHexString(b),npcWindowIndex,acceptanceIndex));
        }
        Require(xpStarted&&infoStarted&&pending==null,"missing baseline/dangling XP gain");
        Require(summaries==missionExperience.Count&&missionGains==summaries&&missionCash==summaries,"incomplete mission award ledger");
        Require((long)baselineExperience+fighting+missionXp==xp&&(long)baselineInformation+lootInfo+missionInfo==info,"final wire totals do not equal earned events");
        return new(baselineExperience,baselineInformation,xp,info,fighting,missionXp,missionInfo,lootInfo,events);
    }
}
