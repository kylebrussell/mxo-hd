using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AbilityParityGunmanProcSweepMode : AbilitySweepMode
{
    public override string Name => "ability-parity-gunman-proc-sweep";

    public AbilityParityGunmanProcSweepMode() : base(new Sample[]
    {
        new(147, "Rifles", "self with rifle", "0", "Rifles style", "while selected", 0),
        new(253, "Wounding Shot", "Melee Dummy outside combat in Rifles with an IWRf weapon", "record opener hit and each DOT tick",
            "2 s cast and 30% stun roll on landing; record stun and immunity independently", "4 s stun / 4 s immunity on proc", 20),
        new(256, "Three Round Burst", "Staggered Melee Dummy in Rifles interlock", "record landed hit",
            "40% stun roll on landing; observe move 11367 and immunity", "4 s stun / 4 s immunity on proc", 20),
        new(158, "Submachine Guns", "self with SMG", "0", "Submachine Guns style", "while selected", 0),
        new(247, "Bullet Spray", "Melee Dummy outside combat in SMG Style", "record landed primary hit",
            "record Enraged if the 15% roll procs", "8 s on proc", 30),
        new(248, "Suppression Fire", "Melee Dummy outside combat in SMG Style", "record landed primary hit",
            "record Rooted and any break; leave cone admission unclaimed in this isolated fixture", "12 s GOB input", 35),
        new(250, "Controlled Burst", "Melee Dummy in SMG interlock", "record landed hit",
            "observe move 21082 and Enraged if the 40% roll procs", "8 s on proc", 30),
        new(496, "Full Auto Redux", "Dazed Melee Dummy in SMG interlock", "record landed hit",
            "check both accuracy penalties on the actual recipient", "8 s", 45)
    }, new ushort[] { 2, 17 }, firearms: true, isolatedMelee: true, staggerFixture: true, additionalInstructions: new[]
    {
        "Retest 248 first, before any damage opens a retaliatory fight. Equip the single SMG with ?combatdepth fixture-weapon smg, select style 158 and face Melee Dummy. With the HUD clear and no family timer, activate 248 before interlock.",
        "For 496, enter SMG interlock and reselect 158 if necessary. With the dummy selected use ?combatdepth daze 30; confirm Dazed 199 before activating 496. Capture the request and both Melee and Ballistic Accuracy penalties on the target.",
        "Wait for the family reuse timer shared with 496 to clear. Equip the rifle and use Close Combat on Melee Dummy. Reselect Rifles 147 if Close Combat switched to Self Defense. With the dummy selected and interlock active, use ?combatdepth stagger 30; confirm Staggered 201 before activating 256. Capture the request and resolved round.",
        "The hand weapon and Free Attack dropdown are separate selections. The prior swap put rifle 692 in bag slot 8, then moved SMG 698 into the hand. Stock explicitly selected the displaced rifle with 3f 08 00 10 00 00 00. Select the SMG explicitly when testing ranged Free Attack."
    }) { }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var readiness = await base.AfterStartAsync(context);
        if (context.StockProbe is not { } caster)
            return readiness;

        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Expected the disposable gun inventory.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable gun server.");
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        var view = WireFormat.ToHex(BitConverter.GetBytes(dummy.ViewId));
        uint spawn = dummy.SpawnId;
        var handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, spawn)));
        caster.SendRpc($"81 51 {view} {spawn:x2}");

        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, database, "smg", hds.CombatFixtureWeapons.Smg);
        await Style(158);
        Check.That(!server.DebugLog().Contains("[COMBAT] Close combat requested", StringComparison.Ordinal),
            "Suppression Fire starts before any interlock");
        await Cast(248);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains($"[ABILITY] 248 lands on view {dummy.ViewId} for ") &&
            server.DebugLog().Contains($"[EFFECTS] 248 on Melee Dummy: State {hds.TempMods.RootedAbility}"),
            "Suppression Fire lands and roots Melee Dummy from the ordinary non-interlock cast", TimeSpan.FromSeconds(15));

        await Interlock(158);
        await Condition("daze", "884/199", 2);
        await Cast(496);
        await caster.WaitUntilAsync(() =>
        {
            var effects = server.DebugLog().Split('\n').FirstOrDefault(line =>
                line.Contains("[EFFECTS] 496 on Melee Dummy:", StringComparison.Ordinal));
            return effects is not null && effects.Contains("Stat 522 -15 8 s", StringComparison.Ordinal) &&
                effects.Contains("Stat 524 -15 8 s", StringComparison.Ordinal);
        }, "Full Auto Redux applies both sourced eight-second accuracy penalties to Melee Dummy",
            TimeSpan.FromSeconds(18));
        // The two interlock specials share group 689. Wait for its stock timer-off record.
        await caster.WaitForRpcAsync("80 bd 01 b1 02 00 00 00 a0 40 00");

        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, database, "rifle", hds.CombatFixtureWeapons.Rifle);
        await Interlock(147);
        await Condition("stagger", "883/201", 8);
        await Cast(256);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains("ThreeRoundBurstAbility is the next move", StringComparison.Ordinal),
            "Three Round Burst reaches the interlock executor", TimeSpan.FromSeconds(18));

        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, database, "smg", hds.CombatFixtureWeapons.Smg);
        var rifleSlot = await database.LongAsync(
            "SELECT slot FROM inventory WHERE charId=110 AND goid=692 AND slot BETWEEN 1 AND 96");
        Check.That(rifleSlot is >= 1 and <= 96, "The replaced rifle is in the actor’s bag");
        await Choice((ushort)rifleSlot, hds.CombatFixtureWeapons.Rifle);
        await Choice(hds.CombatFixtureWeapons.HandSlot, hds.CombatFixtureWeapons.Smg);

        return readiness;

        async Task Interlock(ushort style)
        {
            caster.SkipReceived();
            caster.SendRpc($"40 {handle} 01 6b");
            await caster.WaitForRpcAsync("47");
            await Style(style);
        }

        async Task Style(ushort style)
        {
            var id = WireFormat.ToHex(BitConverter.GetBytes(style));
            caster.SkipReceived();
            caster.SendRpc($"80 b9 {id} {view}");
            await caster.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {id} 00 00");
        }

        async Task Condition(string command, string effect, uint bit)
        {
            const uint inCombat = hds.CombatInterlock.InCombatFlag;
            var header = WireFormat.FromHex($"{view} 04 80 80 80 80 80 80 02");
            var required = inCombat | bit;
            int cursor = caster.World.ViewSnapshot().Count;
            caster.SendRpc($"81 51 {view} {spawn:x2}");
            caster.SendChat($"?combatdepth {command} 30");
            await caster.WaitForTextAsync($"applied {effect} to Melee Dummy for 30 seconds");
            await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(cursor).Any(record =>
            {
                int offset = record.AsSpan().IndexOf(header);
                return offset >= 0 && offset + header.Length + 4 <= record.Length &&
                    (BitConverter.ToUInt32(record, offset + header.Length) & required) == required;
            }), $"Melee Dummy presents required interlock condition 0x{bit:x2}", HeadlessPlayer.DefaultTimeout);
        }

        async Task Cast(ushort ability)
        {
            var id = WireFormat.ToHex(BitConverter.GetBytes(ability));
            caster.SkipReceived();
            caster.SendRpc($"80 b9 {id} {view}");
            await caster.WaitForRpcAsync($"80 ac {id} 00 00 00 00 00 00 00 00 00 00 00 00 80 40");
        }

        async Task Choice(ushort slot, uint goid)
        {
            var id = WireFormat.ToHex(BitConverter.GetBytes(slot));
            int cursor = server.DebugLog().Length;
            caster.SendRpc($"3f {id} 10 00 00 00");
            await caster.WaitUntilAsync(() => server.DebugLog()[Math.Min(cursor, server.DebugLog().Length)..]
                .Contains($"[COMBAT] Free Fire weapon: slot {slot}, kind 16, goid {goid}"),
                $"Free Attack choice resolves weapon {goid} in slot {slot}", HeadlessPlayer.DefaultTimeout);
            Check.Equal((long)hds.CombatFixtureWeapons.Smg, await database.LongAsync(
                "SELECT goid FROM inventory WHERE charId=110 AND slot=105"), "Free Attack selection leaves the SMG equipped");
        }
    }
}
