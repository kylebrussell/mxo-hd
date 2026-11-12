namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task DeathReconstructAndTutorialSelection(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("Reconstruct")
            { X=23828,Y=495,Z=13984,Level=30 }))[0];
        p.SendRpc("81 00"); // only a Loading Area selection can start the tutorial
        p.SendRpc("81 48");
        await p.WaitForRpcAsync("81 49");
        Check.Equal(0,p.Received("81 07").Count,"A city tutorial request is ignored");
        p.SendChat("?die");
        await p.WaitForViewUpdateAsync(2,"02 80 80 80 c1 01 00 00 80 80 01 00 00 00 00");
        await p.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 92 00 00 60");
        p.SendRpc("81 4c");
        await p.WaitForRpcAsync("80 fd 00 00 00 00 01 01");
        p.SendRpc("80 fe 01");
        await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08 00 00 00 00");
        await p.WaitForRpcAsync("81 09");
        var la=await p.WaitForRpcAsync("06 0e 00");
        Check.Equal(901u,BitConverter.ToUInt32(la.Bytes,3),"Reconstruct loads the Loading Area");
        int selfViews=p.World.CreationSnapshot().Count(v=>v.ViewId==2);
        int viewsAtLa=p.World.ViewSnapshot().Count;
        // Exact native6:46:41 compound RPC sequence from pr100-death-packetlog.
        p.World.SendRpcs(WireFormat.FromHex("81 00"),WireFormat.FromHex("6c 02 00 00 00 00 00 00 00"),WireFormat.FromHex("05"));
        await p.WaitForRpcAsync("81 07");
        Check.Equal(selfViews,p.World.CreationSnapshot().Count(v=>v.ViewId==2),"Old LA05 cannot spawn the next world's RSI");
        Check.That(!p.World.ViewSnapshot().Skip(viewsAtLa).Any(v=>BytePattern.IndexOf(v,
            WireFormat.FromHex("02 00 02 80 80 80 80 80 08"),0)>=0),"No DeathPenalty update for an absent LA self view");
        p.SendRpc("81 08 00 00 00 00");
        await p.WaitForRpcAsync("81 08");
        var tutorial=await p.WaitForRpcAsync("06 0e 00");
        Check.Equal(800u,BitConverter.ToUInt32(tutorial.Bytes,3),"Native tutorial instance range800..899");
        Check.That(System.Text.Encoding.Latin1.GetString(tutorial.Bytes).Contains("tutorial_v2.metr"),"Tutorial selection loads the tutorial resource");
        p.SendRpc("05");
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Count(v=>v.ViewId==2)>selfViews,"post-reset tutorial self spawn",TimeSpan.FromSeconds(15));
        await p.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[TUTORIAL] Begins"),"tutorial runtime starts",TimeSpan.FromSeconds(15));
        Check.Equal(0L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Tutorial map index saved separately");
        Check.Equal(0, p.World.Problems.Count,"Native reconstruction sequence has ordered, readable packets");
    }
}
