namespace HeadlessClient;

public static partial class Scenarios
{
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
        foreach(ushort ability in new ushort[]{325,326,375,416})
        {
            await KillForResurrection(corpse);
            int restored=context.Server.DebugLog().Split("[RESURRECTION] Restored").Length-1;
            Cast(healer,ability,view.ViewId);
            var request=await corpse.WaitForRpcAsync("81 88",TimeSpan.FromSeconds(15));
            Check.Equal((uint)ability,BitConverter.ToUInt32(request.Bytes,2),"Native approval names the cast ability");
            Check.Equal((ushort)54,BitConverter.ToUInt16(request.Bytes,52),"Relative name offset");
            Check.That(System.Text.Encoding.ASCII.GetString(request.Bytes,56,request.Bytes.Length-56)=="RezMedic\0","Approval names its caster");
            Check.Equal(restored,context.Server.DebugLog().Split("[RESURRECTION] Restored").Length-1,"Corpse remains dead before consent");
            corpse.SendRpc(request.Bytes); // Yes: native copies original, bytes6/7 both zero.
            string expected=ability==325?"HP=1 IS=1":"HP=777 IS=333";
            await corpse.WaitUntilAsync(()=>context.Server.DebugLog().Contains("Restored RezFriend with "+ability+" "+expected),
                "Script resurrection resources",TimeSpan.FromSeconds(10));
            await observer.WaitForViewUpdateAsync(watched.ViewId,ability==325?"02 80 80 92 00 01 00 80 04 00 00 00 00":"02 80 80 92 00 09 03 80 04 00 00 00 00");
            corpse.SendRpc(request.Bytes); // Replaying a completed Yes must not apply it again.
            corpse.SendRpc("81 48"); await corpse.WaitForRpcAsync("81 49");
            Check.Equal(restored+1,context.Server.DebugLog().Split("[RESURRECTION] Restored").Length-1,"Consent is single-use");
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
            byte[] reply=(byte[])request.Bytes.Clone();
            if(i==0) reply[7]=1; // No
            if(i==1) reply[6]=1; // native dialog timeout
            if(i==2)
            {
                byte[] forged=(byte[])reply.Clone(); forged[8]^=1;corpse.SendRpc(forged);
                corpse.SendRpc("81 48");await corpse.WaitForRpcAsync("81 49");
                Check.That(!context.Server.DebugLog().Contains("[RESURRECTION] Restored"),"Foreign correlation cannot approve a corpse");
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
            Check.That(!context.Server.DebugLog().Contains("[RESURRECTION] Restored"),"Refusal, timeout and Reconstruct cannot revive");
        }
        Check.Equal(0,corpse.World.Problems.Count+medics.Sum(p=>p.World.Problems.Count),"Approval rejection protocol");
    }
}
