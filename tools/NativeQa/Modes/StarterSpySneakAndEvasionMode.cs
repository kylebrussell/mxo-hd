using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterSpySneakAndEvasionMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;                  // 1112
    private const ushort SneakPassive = 151;
    private const ushort SneakGrant = Stealth.SneakGrantAbility;            // 1111
    private const ushort SideRollEscape = CombatSpecials.SideRollEscape;    // 121
    private const ushort UncannySenses = 161;
    private const ushort DisableEvasion = StealthShieldAbilities.DisableEvasion; // 1125
    private const ushort EvadeCombat = EvasionShield.Parent;                // 217
    private const uint ItotToolGoid = StealthShieldFixture.Tool;            // 531
    private const uint EvadeCombatDescriptor = 0x80000000u | ((uint)EvadeCombat << 10);

    private const byte Level = 50, Rank = 50;
    private const int GroundY = 495;
    // QAEnemy stands 2.52 m north of CSR facing +Z (away from CSR), satisfying Sneak InitialSneakTest,
    // 1125 CastOutsideTargetFOVAngle (50 deg), and 8 m SelectionRange.
    private const int CasterX = 24499, CasterZ = -1750;
    private const int EnemyX = 24499, EnemyZ = -1498;

    private static readonly ushort[] Roots = { 2, 17, SpyStyle, SneakPassive, UncannySenses, DisableEvasion };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-starter-spy-sneak-and-evasion";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CasterX, CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={GroundY}," +
                "alignment=1,pvpflag=1,healthC=3000,healthM=3000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot IN (1,{CombatFixtureWeapons.HandSlot})",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{ItotToolGoid},1,1,0)",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={GroundY},z={EnemyZ}," +
                "alignment=2,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES(6114,50,{EvadeCombatDescriptor},50,1,'EvadeCombatAbility')"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        hotbarAbilities: new[] { SpyStyle, SneakGrant, DisableEvasion },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (23/100 memory, max IS 2000), PvP flagged (Org 1), ITOt tool (GOID 531) in bag slot 1. Hostile PvP QAEnemy (Org 2, Evade Combat 217 / EvasionState 624 active) stands 2.52 m north facing away. Page 2 hotbar slots: 1=1112 Spy Style, 2=1111 Sneak, 3=1125 Disable Evasion.",
            "Step A (151 Sneak & 161 Uncanny Senses passive/grant unload & reload): Clear target selection. Send four separate unloads leaf-to-root: '?qaunload 1125' (23->21 mem), '?qaunload 161' (21->20 mem; Sneak Attack Detection stat 577 drops -40), '?qaunload 121' (20->18 mem), '?qaunload 151' (18->9 mem; stats 1027 -5, 1143 -5, 998 -10, 1047 -5 removed and grant 1111 Sneak removed via 80 b3 57 04). Then send four separate reloads root-to-leaf: '?qaload 151' (9->18 mem; stats & grant 1111 restored), '?qaload 121' (18->20), '?qaload 161' (20->21; stat 577 +40 restored), '?qaload 1125' (21->23).",
            "Step B (1125 Disable Evasion): Press slot 1 (1112 Spy Style), then press slot 2 (1111 Sneak) from behind QAEnemy ('[STEALTH] CSR_Tester 1111 on (bits 2, SL 15)'). Select QAEnemy (2.52 m north, EvasionState 624 active) and press slot 3 (1125 Disable Evasion): expect 25 IS spent (2000 -> 1975), 4.0 s cast bar retaining Sneak during cast, 45.0 s own reuse, on landing '[STEALTH] CSR_Tester 1111 off (landed ability1125)' + '[STEALTH-FREE]1125 landed; target suppression8s; caster restealth1.5s', and 1.5 s later automatic restealth '[STEALTH-FREE]1125 restored 1111' + '[STEALTH] CSR_Tester 1111 on (bits 2, SL 15)'."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "151,161,1125",
            ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add("QAEnemy", enemy);
        await enemy.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, enemy);
        await StealthShieldFixture.ActivateTargetAsync(enemy);

        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            $"Legal level-{Level} memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var enemyView = await caster.WaitForViewNamedAsync("QAEnemy");

        // Step A: Leaf-to-root unload of 1125, 161, 121, 151 and root-to-leaf reload.
        int unload151Rpc = caster.World.RpcSnapshot().Count;
        foreach (ushort id in new[] { DisableEvasion, UncannySenses, SideRollEscape, SneakPassive })
        {
            caster.SendChat($"?qaunload {id}");
            await caster.WaitForTextAsync($"Unloaded {id}");
        }
        byte[] unload1111 = ServerPackets.BuildAbilityUnload(SneakGrant);
        Check.That(caster.World.RpcSnapshot().Skip(unload151Rpc).Any(r => r.Bytes.AsSpan().SequenceEqual(unload1111)),
            "Unloading 151 sends 80 b3 unload for grant 1111");

        int reload151Rpc = caster.World.RpcSnapshot().Count;
        foreach (ushort id in new[] { SneakPassive, SideRollEscape, UncannySenses, DisableEvasion })
        {
            caster.SendChat($"?qaload {id}");
            await caster.WaitForTextAsync($"Loaded {id}");
        }
        byte[] load1111 = ServerPackets.BuildAbilityLoad(SneakGrant, Rank, AbilityProgression.HiddenSlot);
        Check.That(caster.World.RpcSnapshot().Skip(reload151Rpc).Any(r => r.Bytes.AsSpan().SequenceEqual(load1111)),
            "Reloading 151 sends 80 b2 load for grant 1111 at rank 50");

        // Step B: Select Spy Style, Sneak behind QAEnemy, cast 1125 Disable Evasion (4 s cast retaining Sneak -> 8 s suppression -> 1.5 s restealth).
        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);
        int sneakLog = server.DebugLog().Length;
        caster.SendRpc(Use(SneakGrant, 0));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[sneakLog..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
            "Sneak 1111 activates behind QAEnemy", TimeSpan.FromSeconds(8));

        int cast1125 = server.DebugLog().Length;
        int rpc1125 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(DisableEvasion, DuelPackets.Handle(enemyView.ViewId, enemyView.SpawnId)));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[cast1125..].Contains("[STEALTH-FREE]1125 landed; target suppression8s; caster restealth1.5s", StringComparison.Ordinal) &&
            server.DebugLog()[cast1125..].Contains("[STEALTH-FREE]1125 restored 1111", StringComparison.Ordinal),
            "Disable Evasion 1125 lands after 4 s, suppresses 624 for 8 s, and restores Sneak 1111 after 1.5 s",
            TimeSpan.FromSeconds(12));
        byte[] reuse1125 = AbilityCooldowns.BuildTimer(DisableEvasion, AbilityCooldowns.OwnTimer, 45, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpc1125).Any(r => r.Bytes.AsSpan().SequenceEqual(reuse1125)),
            "Disable Evasion 1125 starts 45 s own reuse");

        Console.WriteLine("  Starter Spy Sneak (151), Uncanny Senses (161), and Disable Evasion (1125) verified");
        return Array.Empty<byte>();

        static byte[] Use(ushort ability, uint targetHandle) =>
            new AbilityUseRecord(ability, targetHandle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
    }
}
