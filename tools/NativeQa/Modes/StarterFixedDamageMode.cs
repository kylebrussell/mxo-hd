using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterFixedDamageMode : NativeQaMode
{
    private const byte PlayerLevel = 11;
    private const int TrainingRank = 10;
    private const ushort Tinkering = 36, HinderingShot = 130, CoderStyle = 1008, Handguns = 129;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Tinkering, HinderingShot, CoderStyle, Handguns }
        .Concat(new[] { Tinkering, HinderingShot, CoderStyle, Handguns }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-starter-fixed-damage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={PlayerLevel},exp={PlayerProgression.ExperienceForLevel(PlayerLevel)},y=495,healthC=1000,healthM=1000,innerStrC=200,innerStrM=200,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END WHERE charId IN (110,6114)",
            $"UPDATE char_abilities SET level={TrainingRank} WHERE char_id IN (110,6114)",
            "UPDATE characters SET x=25200,z=-1498 WHERE charId=6114",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}; " +
                $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{CombatFixtureWeapons.Handgun},{CombatFixtureWeapons.HandSlot},1,0)"
        }, DevCommands: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        readyMessages: new[]
        {
            "Level 11, trained rank 10, 36/41 memory. Select QAEnemy at 4 m, activate Coder Style and use normal Tinkering (36). Capture the ordinary 6 s cast, viral fixed base 4, and 30 s reuse.",
            "Select Handguns with the equipped pistol and use ordinary Hindering Shot (130) against QAEnemy while out of interlock. Capture the 2 s cast, ballistic fixed base 9, 8 s own reuse and four timed records at the selected target.",
            "Only Tinkering's eligible viral contest draw is fixed in this isolated two-cast sample. Keep the outgoing stock ability packets. Hindering Shot stock admission and the full Tinkering area and Hindering movement/choreography checks remain open."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "36,130", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(loaded.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout)
            && loaded.All(row => row.Level == TrainingRank), "Margin provides both starter chains at the owned rank");
        Check.Equal(36, Loadout.Sum(AbilityMemory.Cost), "Both level-11 prerequisite chains use 36 of 41 memory");
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(CombatFixtureWeapons.Handgun, BitConverter.ToUInt32(self.Attributes["EquippedItemID"]), "Stock login has a handgun in hand");
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        await enemy.WaitForViewNamedAsync(caster.Handle);

        await SelectStyle(caster, self, CoderStyle, 172);
        await Land(caster, enemy, target, Tinkering, 4, 6, 30);
        await SelectStyle(caster, self, Handguns, 171);

        int cursor = enemy.World.RpcSnapshot().Count;
        await Land(caster, enemy, target, HinderingShot, 9, 2, 8);
        var effects = new (ushort Stat, int Value)[] { (115, -50), (877, -50), (878, -50), (519, -10) };
        foreach (bool remove in new[] { false, true })
            await enemy.WaitUntilAsync(() => effects.All(expected => enemy.World.RpcSnapshot().Skip(cursor).Any(rpc =>
                ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.SourceId == HinderingShot &&
                bonus.TargetId == expected.Stat && bonus.SignedValue == expected.Value && bonus.Duration == 8 &&
                bonus.AddRemoveType == (remove ? 1 : 0))),
                $"Hindering Shot four eight-second modifiers on the selected target (remove={remove})", TimeSpan.FromSeconds(11));
        Console.WriteLine("  Level-11 full ability requests delivered viral 4 and ballistic 9; Hindering Shot retained all four add/off records");
        return Array.Empty<byte>();
    }

    private static void Use(HeadlessPlayer actor, ushort id, ViewCreation target) =>
        actor.SendRpc(new AbilityUseRecord(id, DuelPackets.Handle(target.ViewId, target.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());

    private static async Task SelectStyle(HeadlessPlayer actor, ViewCreation self, ushort id, ushort stat)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        Use(actor, id, self);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(rpc =>
            ManageBonusRecord.TryDecode(rpc.Bytes, out var modifier) && modifier!.SourceId == id &&
            modifier.TargetId == stat && modifier.AddRemoveType == 0), $"selected combat style {id}", HeadlessPlayer.DefaultTimeout);
    }

    private static async Task Land(HeadlessPlayer caster, HeadlessPlayer enemy, ViewCreation view,
        ushort id, int expected, float castSeconds, int reuse)
    {
        int cursor = caster.World.RpcSnapshot().Count;
        int targetCursor = enemy.World.RpcSnapshot().Count;
        Use(caster, id, view);
        byte[] damage = AbilityMessages.Damage(false, id, caster.Handle, enemy.Handle, expected, 0);
        await enemy.WaitUntilAsync(() => enemy.World.RpcSnapshot().Skip(targetCursor).Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(damage)),
            $"ordinary selected-player hit {id} for {expected}", TimeSpan.FromSeconds(12));
        byte[] timer = AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, reuse, true);
        string idHex = WireFormat.ToHex(BitConverter.GetBytes(id));
        var bar = BytePattern.Parse($"80 ac {idHex}");
        var records = caster.World.RpcSnapshot().Skip(cursor).ToArray();
        Check.That(records.Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(timer)), $"own reuse {id}: {reuse}s");
        Check.That(records.Any(rpc => bar.IsMatch(rpc.Bytes) && BitConverter.ToSingle(rpc.Bytes, rpc.Bytes.Length - 4) == castSeconds),
            $"cast bar {id}: {castSeconds}s");
    }
}
