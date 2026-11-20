using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class SpyMasterDisciplinesMode : NativeQaMode
{
    private const ushort WeakPoints = 164, WickedAim = 278, StealthCountermeasures = 293;
    private const ushort MasterKnifeThrower = 338, MasterAssassin = 389;
    private const ushort MasterKnifeThrowerUpgrade = 1121, MasterAssassinUpgrade = 1123;

    private static readonly ushort[] ReloadOrder = { WeakPoints, WickedAim, MasterKnifeThrower, MasterAssassin };
    private static readonly ushort[] UnloadOrder = ReloadOrder.Reverse().ToArray();
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, StealthCountermeasures }
        .Concat(ReloadOrder)
        .Concat(ReloadOrder.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Concat(AbilityPrerequisites.Current.Closure(StealthCountermeasures).Keys)
        .Distinct().Order().ToArray();

    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (WeakPoints, 0, new (ushort, int)[] { (1028, 10), (1145, 10), (1027, 10) }),
        (WickedAim, 0, new (ushort, int)[] { (1028, 15), (1145, 15), (1040, 10), (1039, 11) }),
        (MasterKnifeThrower, MasterKnifeThrowerUpgrade, new (ushort, int)[] { (1145, 5) }),
        (MasterAssassin, MasterAssassinUpgrade, new (ushort, int)[] { (1028, 5) })
    };

    private static readonly (ushort Stat, int Value)[] MasterKnifeStats = { (1034, 6), (1032, 18), (520, 10) };
    private static readonly (ushort Stat, int Value)[] MasterAssassinStats = { (1034, 10), (1032, 7), (209, 8) };
    private const string SelfCastRequest = "80b925040000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-spy-master-disciplines";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        },
        DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { StealthCountermeasures, MasterKnifeThrowerUpgrade, MasterAssassinUpgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "164,278,293,338,389",
            ["NativeCloseableRows"] = 1,
            ["BehaviorTestRows"] = "290,338,389",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(50)
        },
        readyMessages: new[]
        {
            "Step A (5 Spy-Master rows: 389 Master Assassin [closeable], 338 Master Knife Thrower, 164 Weak Points, 278 Wicked Aim, 293 Stealth Countermeasures). On login inspect Status Bonuses (Thrown Dmg Bonus +30 from 164:+10, 278:+15, 389:+5; Thrown Acc Bonus +30 from 164:+10, 278:+15, 338:+5; Ballistic Def Bonus +10 from 164; Root Dur +10 & Pacify Dur +11 from 278) and verify 1121 and 1123 appear once in Actions.",
            "Step B (Unload/Reload cycle). Unload leaf-to-root: ?qaunload 389, 338, 278, 164. Reload root-to-leaf: ?qaload 164, 278, 338, 389 and confirm exact login Status and both upgrades (1121, 1123) return once.",
            "Step C (Actives on page 2). Clear selection. Press slot 1 (293 Stealth Countermeasures: 140 IS, 15s reuse, Stat 204 +80 Stealth Detection for 30 s). Press slot 2 (1121 Master Knife Thrower Upgrade: +6 Thrown Dmg, +18 Thrown Acc, +10 Combat Evasion, 20 IS, 30s reuse). Press slot 3 (1123 Master Assassin Upgrade: replaces 1121 with +10 Thrown Dmg, +7 Thrown Acc, +8 Stealth, 30s reuse), then press slot 3 again at +3-5s for clean toggle-off during reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        foreach (ushort grant in new[] { MasterKnifeThrowerUpgrade, MasterAssassinUpgrade })
            Check.That(!archive.ContainsKey(grant), $"Grant {grant} is absent from owned Archive");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Spy master batch fits level-50 memory");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);

        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Passives, 0, remove: false);

        int cursor = await ChangeLoadoutAsync(actor, archive, UnloadOrder, load: false);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: true);

        cursor = await ChangeLoadoutAsync(actor, archive, ReloadOrder, load: true);
        await CheckPassivesAsync(actor, proof, Passives, cursor, remove: false);

        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildSelfRequest(StealthCountermeasures));
        await EvidenceRetestWire.TimerAsync(actor, cursor, StealthCountermeasures, AbilityCooldowns.OwnTimer, 15);
        await ObserveAsync(actor, cursor, packet => Bonus(packet, StealthCountermeasures, 204, 80, 30, remove: false),
            "Stealth Countermeasures 293 applies +80 Stealth Detection for 30 s");

        cursor = await ActivateUpgradeAsync(actor, MasterKnifeThrowerUpgrade, MasterKnifeStats);
        cursor = await ActivateUpgradeAsync(actor, MasterAssassinUpgrade, MasterAssassinStats);
        foreach (var (stat, value) in MasterKnifeStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, MasterKnifeThrowerUpgrade, stat, value, 0, remove: true),
                $"Master Assassin Upgrade removes Master Knife Thrower stat {stat}");

        await Task.Delay(650);
        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildSelfRequest(MasterAssassinUpgrade));
        byte[] off = AbilityBuffs.BuildExclusive(MasterAssassinUpgrade, false);
        await ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(off),
            "Master Assassin Upgrade toggles off during reuse");
        foreach (var (stat, value) in MasterAssassinStats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, MasterAssassinUpgrade, stat, value, 0, remove: true),
                $"Master Assassin Upgrade removes stat {stat} on toggle-off");

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "spy-master-disciplines.json"), JsonSerializer.Serialize(proof));
        Check.Equal(0, actor.World.Problems.Count, "Spy master disciplines batch framing");
        return Array.Empty<byte>();
    }

    private static async Task<int> ActivateUpgradeAsync(HeadlessPlayer actor, ushort ability, (ushort Stat, int Value)[] stats)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(BuildSelfRequest(ability));
        await EvidenceRetestWire.TimerAsync(actor, cursor, ability, AbilityCooldowns.OwnTimer, 30);
        foreach (var (stat, value) in stats)
            await ObserveAsync(actor, cursor, packet => Bonus(packet, ability, stat, value, 0, remove: false),
                $"Upgrade {ability} publishes stat {stat}={value}");
        return cursor;
    }

    private static byte[] BuildSelfRequest(ushort ability)
    {
        byte[] request = Convert.FromHexString(SelfCastRequest);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        return request;
    }
}
