using System.Buffers.Binary;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class HackerOneCheckBatchMode : NativeQaMode
{
    private const ushort TeamPatcher = 92, EffectiveCode = 391, TrojanHorse = 945, TeamPatcherUpgrade = 1089;
    private static readonly ushort[] Loadout = Profile(2, 17, TeamPatcher, EffectiveCode, TrojanHorse).Order().ToArray();
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (TeamPatcher, TeamPatcherUpgrade, Array.Empty<(ushort, int)>()),
        (EffectiveCode, 0, new (ushort, int)[] { (1027, 12), (1039, 15), (1143, 12) })
    };
    // Team Patcher Upgrade: stock 455f0446826c:26067–26068.
    private const string UpgradeRequest = "80b9410402000100000000000000000000000000000000000000003200000000000000086b0000000000";
    // PR545's stock view-zero Trojan Horse request.
    private const string TrojanRequest = "80b9b103000000000000000000000000000000000000000000000000585cbc0001000000086b0000000000";

    public override string Name => "ability-parity-hacker-passives-and-trojan";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=500,innerStrM=500,alignment=CASE WHEN charId=110 THEN 1 ELSE 2 END WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={CombatFixtureLoadout.CasterX + 200},z={CombatFixtureLoadout.CasterZ} WHERE charId=6114",
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new ushort[] { TeamPatcherUpgrade, TrojanHorse },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "92,391,945", ["NativeCloseableRows"] = 3 },
        readyMessages: new[]
        {
            "Step A. Effective Code (391) shows +12 ranged defense, +15 Pacify duration and +12 melee defense under its own Status source. Unload and reload 391 and confirm all three rows leave and return.",
            "Step B. Team Patcher Upgrade appears once in Actions. Unload/reload 92 and confirm the action leaves and returns once. Use page 2 slot 1 with no hostile selection to verify activation.",
            "Step C. Standing at spawn, clear selection and press page 2 slot 2 for Trojan Horse. QAEnemy is flagged and 2 m east. The fixture fixes the 946 draw to a hit. Observe the pulse removing 20 IS, the caster's recurring 20 IS cost and switch-off on a second press. Keep the outgoing stock 945 request."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(!archive.ContainsKey(TeamPatcherUpgrade), "Team Patcher Upgrade is granted at world readiness");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= 100, "All three level-50 chains fit in memory");
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        await caster.WaitForViewNamedAsync(enemy.Handle);
        await enemy.WaitForViewNamedAsync(caster.Handle);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var proof = new List<object>();
        await CheckPassivesAsync(caster, proof, Passives, 0, remove: false);
        var cycle = new ushort[] { EffectiveCode, TeamPatcher };
        int cursor = await ChangeLoadoutAsync(caster, archive, cycle, load: false);
        await CheckPassivesAsync(caster, proof, Passives, cursor, remove: true);
        cursor = await ChangeLoadoutAsync(caster, archive, cycle.Reverse().ToArray(), load: true);
        await CheckPassivesAsync(caster, proof, Passives, cursor, remove: false);

        byte[] teamPatcher = Convert.FromHexString(UpgradeRequest);
        BinaryPrimitives.WriteUInt32LittleEndian(teamPatcher.AsSpan(4), DuelPackets.Handle(self.ViewId, self.SpawnId));
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(teamPatcher);
        await ObserveAsync(caster, cursor, packet => Bonus(packet, TeamPatcherUpgrade, 564, 7, 0, remove: false),
            "Team Patcher Upgrade applies its self concentration bonus");
        int enemyViews = enemy.World.ViewSnapshot().Count;
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(TrojanRequest);
        await EvidenceRetestWire.CastBarAsync(caster, TrojanHorse, 2);
        await ObserveAsync(caster, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(TrojanHorse, true)),
            "Trojan Horse switches on");
        await enemy.WaitUntilAsync(() => enemy.World.ViewSnapshot().Skip(enemyViews).Any(view =>
            BuffDrainLifetimeFixture.TryInnerStrength(view, out ushort value) && value == 480),
            "Trojan Horse removes 20 IS from the nearby enemy", HeadlessPlayer.DefaultTimeout);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains("[AURA] 945 pulse 946 reaches 1 targets"),
            "One nearby hostile player receives the sourced pulse", HeadlessPlayer.DefaultTimeout);
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(TrojanRequest);
        await ObserveAsync(caster, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(TrojanHorse, false)),
            "Trojan Horse switches off");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Hacker batch request framing");
        return Array.Empty<byte>();
    }
}
