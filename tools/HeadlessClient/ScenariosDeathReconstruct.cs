namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task DeathReconstructAndHardlineSelection(ScenarioContext context)
    {
        var player=(await context.AddPlayersInWorldAsync(new CharacterSeed("ReconstructHardline")
            { X=23828,Y=495,Z=13984,Level=30 }))[0];
        player.SendChat("?die");
        await player.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 92 00 00 60");
        player.SendRpc("81 4c");
        await player.WaitForRpcAsync("80 fd 00 00 00 00 01 01");
        player.SendRpc("80 fe 01");
        await player.WaitForRpcAsync("81 07");
        int resetCursor = player.World.RpcSnapshot().Count;
        player.SendRpc("81 08 00 00 00 00");

        var reset = await player.WaitForRpcAsync("81 09");
        var loadingArea = await player.WaitForRpcAsync("06 0e 00 85 03 00 00");
        Check.Equal(0, reset.Index, "LA acknowledgement starts its RPC list");
        Check.Equal(0, loadingArea.Index, "The map starts a new list after the LA acknowledgement");
        Check.That(reset.DatagramSequence != loadingArea.DatagramSequence, "LA acknowledgement and map use separate datagrams");
        Check.That(player.World.RpcSnapshot().Skip(resetCursor).Count(r => r.DatagramSequence == reset.DatagramSequence) == 1,
            "The acknowledgement is the only RPC in the initial Loading Area datagram");
        var balances = await player.WaitForRpcAsync("80 e4");
        Check.Equal(loadingArea.ListCounter, balances.ListCounter, "The map carries the current balances");
        Check.That(!player.World.RpcSnapshot().Any(r => r.ListCounter == loadingArea.ListCounter && BytePattern.Parse("3a").IsMatch(r.Bytes)),
            "Loading Area settings wait for the client readiness request");

        int selfBefore = player.World.CreationSnapshot().Count(v => v.ViewId == 2);
        player.World.SendRpcs(WireFormat.FromHex("6c 01 00 00 00 00 00 00 00"), WireFormat.FromHex("05"));
        await player.WaitForRpcAsync("3a 05 00 11 00 0a 00 50 76 50 53 65 72 76 65 72 00");
        var hardlines = await player.WaitForRpcAsync("6d 01 00 00 00 08 00 01");
        Check.That(BytePattern.Parse("00 98 00 00 00 00").Occurs(hardlines.Bytes), "Mara C is available in the picker");
        Check.Equal(selfBefore, player.World.CreationSnapshot().Count(v => v.ViewId == 2), "The picker has no LA self spawn");

        player.SendRpc("80 ff 01 00 00 00 98 00 00 00");
        await player.WaitForRpcAsync("81 07");
        player.SendRpc("81 08 00 00 00 00");
        await player.WaitForRpcAsync("06 0e 00 01 00 00 00");
        player.SendRpc("05");
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(v => v.ViewId == 2) > selfBefore,
            "post-reconstruction Mara C self spawn", TimeSpan.FromSeconds(15));
        Check.Equal(1L, await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={player.CharId}"),
            "Reconstructed player saved in Richland");
        Check.Equal(0, player.World.Problems.Count, "No packet parser errors across LA entry or exit");
    }

    private static async Task DeathReconstructAndTutorialSelection(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("Reconstruct")
            { X=23828,Y=495,Z=13984,Level=30 }))[0];
        p.SendRpc("81 00"); // only a Loading Area selection can start the tutorial
        p.SendRpc("81 48");
        await p.WaitForRpcAsync("81 49");
        Check.Equal(0,p.Received("81 07").Count,"A city tutorial request is ignored");
        var witness = (await context.AddPlayersInWorldAsync(new CharacterSeed("DeathWitness")
            { X=23928,Y=495,Z=13984,Level=30 }))[0];
        var seen = await witness.WaitForViewNamedAsync(p.Handle);
        p.SendChat("?die");
        await p.WaitForViewUpdateAsync(2,"02 80 80 80 c1 01 00 00 80 80 01 00 00 00 00");
        await p.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 92 00 00 60");
        await p.WaitForViewUpdateAsync(2,"01 28 ?? 40 05 00 00 10");
        await witness.WaitForViewUpdateAsync(seen.ViewId,"01 2a ?? 40 05 00 00 10 00");
        await witness.WaitForViewUpdateAsync(seen.ViewId,"02 80 80 12 01 00 00");
        var late = (await context.AddPlayersInWorldAsync(new CharacterSeed("LateDeathWitness")
            { X=23728,Y=495,Z=13984,Level=30 }))[0];
        var lateSeen = await late.WaitForViewNamedAsync(p.Handle);
        await late.WaitForViewUpdateAsync(lateSeen.ViewId,"01 2a ?? 40 05 00 00 10 00");
        await late.WaitForViewUpdateAsync(lateSeen.ViewId,"02 80 80 12 01 00 00");
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
        await Task.Delay(500); // old death reassertions cannot survive the reset/new-world spawn
        Check.That(!p.World.ViewSnapshot().Skip(viewsAtLa).Any(v=>BytePattern.Parse("02 00 01 28 ?? 40 05 00 00 10").Occurs(v)),
            "No stale corpse movement after reconstruction");
        Check.Equal(0, p.World.Problems.Count,"Native reconstruction sequence has ordered, readable packets");
    }
}
