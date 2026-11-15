using hds;
namespace HeadlessClient;

/// <summary>QA-only normal owned Invisibility Belt activation; no hidden grant injection.</summary>
public static class GhostStrikeInvisibilityTool
{
    public const uint Goid=460;
    public const byte Slot=40;
    public sealed record Grant(byte Slot,string Inventory,string Archived,string Loaded,string HiddenLoad,string Request,string Modifier,
        float Duration,ushort BeforeIS,IReadOnlyList<ushort> IsUpdates);
    public static ushort[] AbilityIds(byte[] margin)
    {
        if(margin.Length%6!=0)throw new InvalidDataException("Malformed Margin ability rows");
        return Enumerable.Range(0,margin.Length/6).Select(i=>AbilityCatalog.IdOf(BitConverter.ToUInt32(margin,i*6+2))).ToArray();
    }
    public static byte OwnedSlot(byte[] inventory)
    {
        if(inventory.Length%10!=0)throw new InvalidDataException("Malformed Margin inventory rows");
        var slots=Enumerable.Range(0,inventory.Length/10).Where(i=>BitConverter.ToUInt32(inventory,i*10+2)==Goid)
            .Select(i=>BitConverter.ToUInt16(inventory,i*10)).ToArray();
        Check.That(slots.Length==1&&slots[0]==Slot,"Exactly one current owned belt at the declared ordinary bag slot");return Slot;
    }
    public static byte[] Request(byte slot)
    {
        if(slot!=Slot)throw new ArgumentOutOfRangeException(nameof(slot));
        //621ed630 ->621d0970 resolves the item's ReqAbilityID, then621ecf10 with ordinary flag0.
        //Shared native80b9 codec: tool slot37/full RPC, flag38;36 is separate tactic/state, not flag.
        var rpc=new byte[43];rpc[0]=0x80;rpc[1]=0xb9;BitConverter.GetBytes((ushort)1116).CopyTo(rpc,2);
        BitConverter.GetBytes((ushort)2).CopyTo(rpc,4);rpc[36]=1;rpc[37]=slot;rpc[38]=0;return rpc;
    }
    private static ushort[] IsUpdates(HeadlessPlayer p,int cursor=0)
    {
        byte[] prefix=WireFormat.FromHex("02 00 02 80 80 80 10");var result=new List<ushort>();
        foreach(var section in p.World.ViewSnapshot().Skip(cursor))
            for(int at=BytePattern.IndexOf(section,prefix,0);at>=0;at=BytePattern.IndexOf(section,prefix,at+1))
                if(at+prefix.Length+2<=section.Length)result.Add(BitConverter.ToUInt16(section,at+prefix.Length));
        return result.ToArray();
    }
    public static async Task<Grant> ActivateAsync(HeadlessPlayer p)
    {
        var inventory=p.CharacterData[5];byte slot=OwnedSlot(inventory);var archived=p.CharacterData[7];var loaded=p.CharacterData[6];
        Check.That(AbilityIds(archived).Contains((ushort)175)&&AbilityIds(loaded).Contains((ushort)175),"Normal175 is owned and loaded");
        Check.That(!AbilityIds(archived).Contains((ushort)1116)&&!AbilityIds(loaded).Contains((ushort)1116),"Hidden1116 is not manually archived/loaded");
        byte[] hidden=WireFormat.FromHex("80 b2 5c 04 00 00 08 02");
        await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Any(r=>r.Bytes.SequenceEqual(hidden)),"Normal175 hidden1116 grant",TimeSpan.FromSeconds(10));
        var current=IsUpdates(p);ushort before=current.Length>0?current.Last():BitConverter.ToUInt16(p.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes["InnerStrengthAvailable"]);
        int views=p.World.ViewSnapshot().Count;byte[] request=Request(slot);p.SkipReceived();p.SendRpc(WireFormat.ToHex(request));
        var modifier=await p.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 5c 04 00 00 73 00 ?? ?? ?? ?? 00");
        var updates=IsUpdates(p,views);int previous=before,debits=0;
        foreach(ushort value in updates){if(previous>value){Check.Equal(50,previous-value,"Stored belt-grant50IS debit with this fixture's zero117 modifiers");debits++;}previous=value;}
        Check.Equal(1,debits,"One actual belt activation IS debit");float duration=BitConverter.ToSingle(modifier.Bytes,18);
        Check.That(float.IsFinite(duration)&&duration>0,"Actual current grant duration is observed, not assumed130s");
        return new(slot,Convert.ToHexString(inventory),Convert.ToHexString(archived),Convert.ToHexString(loaded),Convert.ToHexString(hidden),Convert.ToHexString(request),modifier.Hex,duration,before,updates);
    }
    public static async Task DeactivateAsync(HeadlessPlayer p)
    {
        p.SkipReceived();p.SendRpc(WireFormat.ToHex(Request(OwnedSlot(p.CharacterData[5]))));
        await p.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 5c 04 00 00 73 00 ?? ?? ?? ?? 01");
    }
}
