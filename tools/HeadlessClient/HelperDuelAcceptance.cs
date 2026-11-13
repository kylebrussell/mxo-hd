using System.Buffers.Binary;
using hds;
namespace HeadlessClient;

/// <summary>Test-helper transport only: accepts the NEXT ordinary challenge from one expected visible actor.</summary>
public static class HelperDuelAcceptance
{
    public sealed record Armed(string Actor,string Challenger,uint ChallengerHandle,int RpcCursor,int ViewCursor);
    public sealed record Evidence(string Challenger,uint ChallengerHandle,string Challenge,string Reply,string Begin,uint SelfDuelId,uint ChallengerDuelId,int ArmRpcIndex,int BeginRpcIndex);
    public static byte[] Reply(ReadOnlySpan<byte> challenge,uint expectedHandle)
    {
        if(challenge.Length!=5||challenge[0]!=0x51||expectedHandle==0||(expectedHandle&0xffff)<=2||
            (expectedHandle&0xff000000)!=0||BinaryPrimitives.ReadUInt32LittleEndian(challenge[1..])!=expectedHandle)
            throw new ArgumentException("Expected an exact DuelChallenged for the current visible challenger.");
        byte[] reply=new byte[7];reply[0]=0x52;challenge[1..].CopyTo(reply.AsSpan(1));reply[5]=1;return reply;
    }
    public static async Task<Evidence> AcceptNextAsync(HeadlessPlayer actor,string challenger,TimeSpan? timeout=null,Func<Armed,Task>? onArmed=null)
    {
        if(string.IsNullOrWhiteSpace(challenger)||challenger==actor.Handle)throw new ArgumentException("Select a different expected challenger.");
        TimeSpan wait=timeout??TimeSpan.FromSeconds(30);if(wait<TimeSpan.FromSeconds(1)||wait>TimeSpan.FromSeconds(60))throw new ArgumentOutOfRangeException(nameof(timeout));
        var view=await actor.WaitForViewNamedAsync(challenger,wait);uint handle=DuelPackets.Handle(view.ViewId,view.SpawnId);
        int start=actor.World.RpcSnapshot().Count,viewStart=actor.World.ViewSnapshot().Count;
        if(onArmed!=null)await onArmed(new Armed(actor.Handle,challenger,handle,start,viewStart));
        RpcRecord? offer=null;
        await actor.WaitUntilAsync(()=>{offer=actor.World.RpcSnapshot().Skip(start).FirstOrDefault(r=>r.Bytes.Length>0&&r.Bytes[0]==0x51);return offer!=null;},"NEXT duel challenge from "+challenger,wait);
        byte[] reply=Reply(offer!.Bytes,handle);
        var current=actor.World.CreationSnapshot().LastOrDefault(v=>v.ViewId==view.ViewId);
        Check.That(current?.Name==challenger&&current.SpawnId==view.SpawnId,"Challenger view/spawn still matches the received challenge");
        int sentAt=actor.World.RpcSnapshot().Count;actor.SendRpc(reply);RpcRecord? begun=null;int beginIndex=-1;
        await actor.WaitUntilAsync(()=>{var rows=actor.World.RpcSnapshot();for(int i=sentAt;i<rows.Count;i++)if(rows[i].Bytes.Length==5&&rows[i].Bytes[0]==0x55&&BinaryPrimitives.ReadUInt32LittleEndian(rows[i].Bytes.AsSpan(1))==handle){begun=rows[i];beginIndex=i;return true;}return false;},"NEW duel begin after accepted challenge",wait);
        uint self=0,other=0;
        await actor.WaitUntilAsync(()=>{self=Scenarios.ViewUpdateU32(actor,2,"02808080808001",viewStart);other=Scenarios.ViewUpdateU32(actor,view.ViewId,"0280808020",viewStart);return PvpRules.DuelOpponents(self,other);},"NEW paired DuelIDs",wait);
        return new(challenger,handle,Convert.ToHexString(offer.Bytes),Convert.ToHexString(reply),Convert.ToHexString(begun!.Bytes),self,other,start,beginIndex);
    }
}
