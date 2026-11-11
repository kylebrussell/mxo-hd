namespace HeadlessClient;

public static partial class Scenarios
{
    private static List<byte[]> CombatEntries(HeadlessPlayer player, ushort view)
    {
        var entries = new List<byte[]>();
        byte[] marker = WireFormat.FromHex($"{Le(view)} 01");
        foreach (byte[] section in player.World.ViewSnapshot())
            for (int at = BytePattern.IndexOf(section, marker, 0); at >= 0; at = BytePattern.IndexOf(section, marker, at + 1))
                if (at + 157 <= section.Length && BitConverter.ToUInt32(section, at + 31) == 0 && section[at + 35] == 1)
                    entries.Add(section[(at + 36)..(at + 157)]);
        return entries;
    }
    private static async Task<ushort> WaitForCombatView(HeadlessPlayer player, ushort previous = 0)
    {
        await player.WaitUntilAsync(() => CombatViewOf(player) != 0 && CombatViewOf(player) != previous,
            "a new combat init", TimeSpan.FromSeconds(10));
        return CombatViewOf(player);
    }
    private static async Task WaitForEntry(HeadlessPlayer player, ushort view, int minimumSequence = 1)
    {
        await player.WaitUntilAsync(() => CombatEntries(player, view).Any(e => BitConverter.ToUInt16(e, 32) >= minimumSequence),
            "a complete combat round entry", TimeSpan.FromSeconds(10));
    }
    private static void VerifyParticipantPrelude(HeadlessPlayer player)
    {
        // Independent retail fixture: the state and attribute record consumes exactly 18 bytes. Its next
        // record is master-view creation (1), not 256. Searching for a later init alone missed this corruption.
        byte[] prefix = WireFormat.FromHex("02 00 03 01 0c 00 80 84 00 80 80 80 80 01");
        bool found = false;
        foreach (byte[] section in player.World.ViewSnapshot())
        {
            int at = BytePattern.IndexOf(section, prefix, 0);
            if (at < 0) continue;
            found = true;
            Check.That(at + 18 <= section.Length, "Complete participant prelude");
            Check.That((BitConverter.ToUInt32(section, at + 14) & 0x00100000) != 0, "Participant in-combat flag");
            if (at + 18 != section.Length) // A concurrent queue flush may end this packet at the record boundary.
            {
                Check.That(at + 20 <= section.Length, "No trailing partial view ID after participant prelude");
                Check.Equal((ushort)1, BitConverter.ToUInt16(section, at + 18), "Next view begins at retail's byte boundary");
            }
        }
        Check.That(found, "Participant received retail combat prelude");
    }
    private static void VerifySharedEntry(HeadlessPlayer a, ushort av, HeadlessPlayer b, ushort bv)
    {
        var ae = CombatEntries(a, av); var be = CombatEntries(b, bv);
        Check.That(ae.Any(x => be.Any(y => x.SequenceEqual(y))), "Fighters and observers share the same slot order, animation, offsets and time");
        foreach (var e in be)
        {
            Check.That(e[0] is 1 or 2 && e[1] is 1 or 2 && e[0] != e[1], "Two valid fighter slots");
            Check.Equal(0x24000b8bu, BitConverter.ToUInt32(e, 65), "Animation resource reference");
            Check.That(BitConverter.ToUInt32(e, 61) >= 255, "Named ILDB move id");
            float x = BitConverter.ToSingle(e, 6), z = BitConverter.ToSingle(e, 14);
            Check.That(Math.Abs(Math.Sqrt(x*x + z*z) - 70) < 0.1, "Fighter is 70 units from centre");
            Check.That(Math.Abs(x + BitConverter.ToSingle(e, 18)) < 0.01 && Math.Abs(z + BitConverter.ToSingle(e, 26)) < 0.01,
                "Fighters are on opposite sides, not overlapping");
        }
    }
    private static async Task CombatRoundsAndObservers(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("RoundAlice") { X=24800, Z=-1498, Level=1, Organization=1 },
            new CharacterSeed("RoundBob") { X=24900, Z=-1498, Level=1, Organization=2 },
            new CharacterSeed("RoundCarol") { X=25000, Z=-1498, Level=1, Organization=3 });
        var alice=players[0]; var bob=players[1]; var carol=players[2];
        await carol.WaitForViewNamedAsync("RoundAlice"); await carol.WaitForViewNamedAsync("RoundBob");
        var mob = await alice.WaitForViewNamedAsync("WinterFrost");
        await carol.WaitForViewNamedAsync("WinterFrost");
        alice.SendRpc($"40 {HandleOf(mob)} 01 6b");
        ushort aliceNpc = await WaitForCombatView(alice), carolNpc = await WaitForCombatView(carol), bobNpc = await WaitForCombatView(bob);
        await WaitForEntry(alice,aliceNpc,2); await WaitForEntry(carol,carolNpc,2);
        VerifyParticipantPrelude(alice);
        VerifySharedEntry(alice,aliceNpc,carol,carolNpc);

        // Someone arriving during a fight gets its creation/init and the next live round.
        var late = await context.AddPlayerAsync(new CharacterSeed("RoundLate") { X=25100,Z=-1498,Level=1 });
        await late.EnterWorldAsync(); await late.WaitForViewNamedAsync("RoundAlice");
        ushort lateNpc = await WaitForCombatView(late); await WaitForEntry(late,lateNpc);
        VerifySharedEntry(alice,aliceNpc,late,lateNpc);
        // Losing the fighter views removes the observer's combat view; returning creates a fresh one.
        static void MoveObserver(HeadlessPlayer player, float x, float z) => player.World.SendViewList(
            WireFormat.FromHex("02 00 01 08").Concat(BitConverter.GetBytes(x)).Concat(BitConverter.GetBytes(495f))
                .Concat(BitConverter.GetBytes(z)).Concat(new byte[] { 0, 0 }).ToArray());
        MoveObserver(late,100000,100000);
        await late.WaitForViewUpdateAsync(1,$"01 01 00 {Le(lateNpc)}");
        MoveObserver(late,25100,-1498);
        lateNpc = await WaitForCombatView(late,lateNpc); await WaitForEntry(late,lateNpc);
        VerifySharedEntry(alice,aliceNpc,late,lateNpc);
        alice.SendRpc("44 00");
        await carol.WaitForViewUpdateAsync(1,$"01 01 00 {Le(carolNpc)}");
        await late.WaitForViewUpdateAsync(1,$"01 01 00 {Le(lateNpc)}");
        Check.Equal(0,carol.Received("45").Count,"NPC observer receives no combat UI rounds");
        Check.Equal(0,carol.Received("47").Count,"NPC observer receives no move choices");

        var bobView=await alice.WaitForViewNamedAsync("RoundBob");
        bob.SendChat("?pvpflag on"); await bob.WaitForViewUpdateAsync(2,OwnPvpFlagOn);
        alice.SendChat("?pvpflag on");
        await alice.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 01 00 00 ?? 01"); // NPC combat bit may still be up.
        alice.SendRpc($"40 {HandleOf(bobView)} 01 6b");
        ushort av=await WaitForCombatView(alice,aliceNpc), bv=await WaitForCombatView(bob,bobNpc), cv=await WaitForCombatView(carol,carolNpc);
        await WaitForEntry(alice,av,2); await WaitForEntry(bob,bv,2); await WaitForEntry(carol,cv,2);
        VerifyParticipantPrelude(alice); VerifyParticipantPrelude(bob);
        VerifySharedEntry(alice,av,bob,bv); VerifySharedEntry(alice,av,carol,cv);

        // Enrage must reject a withdrawal request while exchanges continue, then release that gate on expiry.
        var aliceForBob = await bob.WaitForViewNamedAsync("RoundAlice");
        Cast(bob,588,aliceForBob.ViewId);
        await alice.WaitForRpcAsync(ModPattern(588,93),TimeSpan.FromSeconds(10));
        alice.SendRpc("44 01");
        await Task.Delay(1500);
        Check.That(!context.Server.DebugLog().Contains("RoundAlice is withdrawing"),"Enrage rejects native-style RPC44");
        await alice.WaitForRpcAsync(ModPattern(588,93,true),TimeSpan.FromSeconds(15));
        alice.SendRpc("44 01");
        await alice.WaitUntilAsync(() => context.Server.DebugLog().Contains("RoundAlice withdraws"),
            "Withdrawal succeeds after Enrage expires",TimeSpan.FromSeconds(35));
        await carol.WaitForViewUpdateAsync(1,$"01 01 00 {Le(cv)}");

        // A new fight after withdrawal gets fresh fighter/observer views and live entries, not the old exchange.
        alice.SendRpc($"40 {HandleOf(bobView)} 01 6b");
        ushort nextA=await WaitForCombatView(alice,av), nextB=await WaitForCombatView(bob,bv), nextC=await WaitForCombatView(carol,cv);
        await WaitForEntry(alice,nextA,2); await WaitForEntry(bob,nextB,2); await WaitForEntry(carol,nextC,2);
        VerifyParticipantPrelude(alice); VerifyParticipantPrelude(bob);
        VerifySharedEntry(alice,nextA,bob,nextB); VerifySharedEntry(alice,nextA,carol,nextC);
        cv=nextC;
        // A death retains the combat view for the native defeat presentation, then cleans observers too.
        bob.SendChat("?health 1");
        await bob.WaitForViewUpdateAsync(2,OwnDeath,TimeSpan.FromSeconds(35));
        await carol.WaitForViewUpdateAsync(1,$"01 01 00 {Le(cv)}",TimeSpan.FromSeconds(15));
        Check.Equal(0,carol.Received("45").Count,"PvP observer receives no participant combat UI");
        Check.Equal(0,carol.Received("47").Count,"PvP observer receives no participant move choices");
        Check.That(context.Server.DebugLog().Contains("[COMBAT-VIEW] Round 2"),"Per-round animation records are logged");
    }
}
