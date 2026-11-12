namespace HeadlessClient;

public static partial class Scenarios
{
    // Captured7.6005 native reply: fixed0x36 bytes, no name/correlation echo; byte11 was ca.
    private static byte[] NativeResurrectionReply(ushort ability)
    {
        byte[] reply=new byte[54];reply[0]=0x81;reply[1]=0x88;
        BitConverter.GetBytes((uint)ability).CopyTo(reply,2);reply[11]=0xca;
        return reply;
    }

    private const string ResurrectionLine = "81 67 ?? ?? ?? ?? 3e 28 00 c6";

    private static async Task KillForResurrection(HeadlessPlayer target)
    {
        target.SendChat("?die");
        await target.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 92 00 00 60");
    }

    private static async Task AbilityResurrectionResources(ScenarioContext context)
    {
        var healer=await context.AddPlayerAsync(new CharacterSeed("RezMedic") { Level=50,InnerStrength=2000,MaxInnerStrength=2000 });
        var corpse=await context.AddPlayerAsync(new CharacterSeed("RezFriend") { X=17120,Level=50,Health=777,MaxHealth=777,InnerStrength=1,MaxInnerStrength=333 });
        var observer=await context.AddPlayerAsync(new CharacterSeed("RezWitness") { X=17220,Level=50 });
        foreach(var p in new[]{healer,corpse,observer}) await p.EnterWorldAsync();
        var view=await healer.WaitForViewNamedAsync(corpse.Handle);
        var watched=await observer.WaitForViewNamedAsync(corpse.Handle);
        var offerIds=new HashSet<uint>();
        foreach(ushort ability in new ushort[]{325,326,375,416,438})
        {
            await KillForResurrection(corpse);
            int restored=corpse.Received(ResurrectionLine).Count;
            Cast(healer,ability,view.ViewId);
            var request=await corpse.WaitForRpcAsync("81 88",TimeSpan.FromSeconds(15));
            Check.Equal((uint)ability,BitConverter.ToUInt32(request.Bytes,2),"Native approval names the cast ability");
            uint offerId=BitConverter.ToUInt32(request.Bytes,8);
            Check.That(offerId!=0 && offerIds.Add(offerId),"Each native offer has a new nonzero offer field");
            Check.Equal((ushort)54,BitConverter.ToUInt16(request.Bytes,52),"Relative name offset");
            Check.That(System.Text.Encoding.ASCII.GetString(request.Bytes,56,request.Bytes.Length-56)=="RezMedic\0","Approval names its caster");
            Check.Equal(restored,corpse.Received(ResurrectionLine).Count,"Corpse remains dead before consent");
            int viewsBefore=corpse.World.ViewSnapshot().Count;
            corpse.SendRpc(NativeResurrectionReply(ability)); // Native Yes: rebuilt fixed prefix, flags6/7 zero.
            await corpse.WaitForRpcAsync(ResurrectionLine+" 00 "+Le(ability)+" 00 00");
            var own=BytePattern.Parse("02 00 02 80 80 80 d1 00 "+(ability==325?"01 00 01 00":"4d 01 09 03")+" 80 80 01 00 00 00 00");
            await corpse.WaitUntilAsync(()=>corpse.World.ViewSnapshot().Skip(viewsBefore).Any(own.Occurs),
                "New own-view IsDead0, script HP/IS and unflagged conditions",TimeSpan.FromSeconds(10));
            await observer.WaitForViewUpdateAsync(watched.ViewId,ability==325?"02 80 80 92 00 01 00 80 04 00 00 00 00":"02 80 80 92 00 09 03 80 04 00 00 00 00");
            corpse.SendRpc(NativeResurrectionReply(ability)); // No outstanding offer remains for this duplicate Yes.
            corpse.SendRpc("81 48"); await corpse.WaitForRpcAsync("81 49");
            Check.Equal(restored+1,corpse.Received(ResurrectionLine).Count,"Consent is single-use");
        }
        Check.Equal(0,healer.World.Problems.Count+corpse.World.Problems.Count+observer.World.Problems.Count,"Resurrection protocol");
    }

    private static async Task AbilityResurrectionApprovalGuards(ScenarioContext context)
    {
        var corpse=await context.AddPlayerAsync(new CharacterSeed("RezRefuser") { X=17120,Level=50 });
        var medics=new List<HeadlessPlayer>();
        for(int i=0;i<5;i++) medics.Add(await context.AddPlayerAsync(new CharacterSeed("RezMedic"+i) { Level=50 }));
        await corpse.EnterWorldAsync();foreach(var p in medics) await p.EnterWorldAsync();
        await KillForResurrection(corpse);
        for(int i=0;i<5;i++)
        {
            var healer=medics[i];var view=await healer.WaitForViewNamedAsync(corpse.Handle);
            Cast(healer,416,view.ViewId);
            var request=await corpse.WaitForRpcAsync("81 88",TimeSpan.FromSeconds(15));
            Check.That(BitConverter.ToUInt32(request.Bytes,8)!=0,"Rejected/expired offers also retain the nonzero dialog field");
            byte[] reply=NativeResurrectionReply(416);
            if(i==0) reply[7]=1; // No
            if(i==1) reply[6]=1; // native dialog timeout
            if(i==2)
            {
                byte[] forged=NativeResurrectionReply(375);corpse.SendRpc(forged);
                corpse.SendRpc("81 48");await corpse.WaitForRpcAsync("81 49");
                Check.That(corpse.Received(ResurrectionLine).Count==0,"A different ability cannot approve the pending offer");
                reply[7]=1;
            }
            if(i==3)
            {
                healer.World.SendViewList(WireFormat.FromHex("02 00 01 08").Concat(BitConverter.GetBytes(21020f))
                    .Concat(BitConverter.GetBytes(495f)).Concat(BitConverter.GetBytes(2693f)).Concat(new byte[]{0,0}).ToArray());
                healer.SendRpc("81 48");await healer.WaitForRpcAsync("81 49");
            }
            if(i==4)
            {
                corpse.SendRpc("81 4c");await corpse.WaitForRpcAsync("80 fd 00 00 00 00 01 01");
            }
            corpse.SendRpc(reply);
            corpse.SendRpc("81 48");await corpse.WaitForRpcAsync("81 49");
            Check.That(corpse.Received(ResurrectionLine).Count==0,"Refusal, timeout and Reconstruct cannot revive");
        }
        Check.Equal(0,corpse.World.Problems.Count+medics.Sum(p=>p.World.Problems.Count),"Approval rejection protocol");
    }
}
