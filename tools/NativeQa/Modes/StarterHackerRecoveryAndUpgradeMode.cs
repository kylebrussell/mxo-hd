using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterHackerRecoveryAndUpgradeMode : NativeQaMode
{
    private const int Level = 18, X = 24800, Y = 495, Z = -1498;
    private const ushort Guardian = 51, Upgrade = 1094, Recovery = 70, ResistDamage = 74;
    private const ushort BaseHealth = 500;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Guardian, Recovery, ResistDamage).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private static readonly (ushort Ability, (ushort Id, int Value)[] Stats)[] Passives =
    {
        (Recovery, new (ushort, int)[] { (1057, 8), (1052, 3) }),
        (ResistDamage, new (ushort, int)[] { (1052, 1), (127, 8) })
    };
    private static readonly (ushort Id, int Value)[] UpgradeStats = { (174, 3), (564, 7), (520, 20) };
    // Native 09d164f3543c:3130. Only the selected friendly view/spawn changes.
    private const string UpgradeStock = "80b946040f000d000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-starter-hacker-recovery-and-upgrade";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                $"healthC={BaseHealth},healthM={BaseHealth},innerStrC=700,innerStrM=1000,alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={X+250},y={Y},z={Z},alignment=1,pvpflag=0 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Upgrade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "70,74,1094", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR faces QAFriend 2.5 m east on the outdoor stand. Check Status and PacketLog. Enter submits all chat commands.",
            "Quick Recovery 70: +8% max HP and +3 Sweep Bonus. Guardian Patcher contributes a separate +1%. Enter ?qaunload 70, then ?qaload 70. Max HP should change 545 → 505 → 545 and source 70 should remove and restore both records.",
            "Repeat unload/reload for Resist Damage 74 (+1 Sweep Bonus, +8% Heal and Upgrade Area Radius). The combined Sweep Bonus should fall from +4 to +1 on unload of 70, or +4 to +3 on unload of 74. Check each source in PacketLog even when Status does not list it.",
            "Select QAFriend, then page 2 slot 1 Guardian Patcher Upgrade. Expect 20 IS and 30 s own reuse. Only CSR receives +3 Healing, +7 Concentration and +20 Combat Evasion. Keep QAFriend selected. Enter ?dismissbuff 1094 and confirm all three removals on CSR. The grant should remain available while parent 51 is loaded."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(archive.Keys.ToHashSet().SetEquals(Abilities) && Abilities.All(id => AbilityPrerequisites.Current.Closure(id)
            .All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The stock client has the full ranked tree with the upgrade supplied only by its grant");
        Check.That(Memory <= AbilityMemory.ForLevel(Level), "The prerequisite tree fits at level 18");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var selected = await actor.WaitForViewNamedAsync(friend.Handle);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal((ushort)545, BitConverter.ToUInt16(self.Attributes["MaxHealth"]), "Quick Recovery and Guardian Patcher raise Max HP by nine percent");
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, remove: false), "Guardian publishes its stock grant");
        foreach (var (id, stats) in Passives)
        {
            await RecordsAsync(0, id, stats, remove: false);
            int views = actor.World.ViewSnapshot().Count;
            int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { id }, load: false);
            await RecordsAsync(unloaded, id, stats, remove: true);
            if (id == Recovery) await MaximumAsync(views, 505);
            views = actor.World.ViewSnapshot().Count;
            int reloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { id }, load: true);
            await RecordsAsync(reloaded, id, stats, remove: false);
            if (id == Recovery) await MaximumAsync(views, 545);
        }

        uint handle = DuelPackets.Handle(selected.ViewId, selected.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        int friendAt = friend.World.RpcSnapshot().Count;
        int active = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(UpgradeStock, handle));
        await RecordsAsync(active, Upgrade, UpgradeStats, remove: false);
        await EvidenceRetestWire.TimerAsync(actor, active, Upgrade, AbilityCooldowns.OwnTimer, 30);
        int barrier = friend.World.RpcSnapshot().Count;
        friend.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(friend, barrier, rpc => rpc.Bytes is [0x81, 0x49, ..], "Friendly target receive barrier");
        Check.That(!friend.World.RpcSnapshot().Skip(friendAt).Any(rpc =>
            ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.SourceId == Upgrade),
            "The selected nonmember gets no Guardian Patcher Upgrade effect");
        int removed = actor.World.RpcSnapshot().Count;
        actor.SendChat("?dismissbuff 1094");
        await RecordsAsync(removed, Upgrade, UpgradeStats, remove: true);
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Recovery and upgrade wire framing");
        return Array.Empty<byte>();

        async Task RecordsAsync(int cursor, ushort source, (ushort Id, int Value)[] stats, bool remove)
        {
            foreach (var (stat, value) in stats)
                await StockAbilityProfile.ObserveAsync(actor, cursor, rpc => StockAbilityProfile.Bonus(rpc, source, stat, value, 0, remove),
                    $"{source}/{stat} value {value}, remove={remove}");
        }

        Task MaximumAsync(int cursor, ushort amount) => actor.WaitUntilAsync(() =>
        {
            string value = EvidenceRetestWire.Le(amount);
            var only = BytePattern.Parse($"02 00 02 80 80 01 {value}");
            var both = BytePattern.Parse($"02 00 02 80 80 81 {value} 40 ?? ??");
            return actor.World.ViewSnapshot().Skip(cursor).Any(packet => only.Occurs(packet) || both.Occurs(packet));
        }, $"Maximum health changes to {amount}", HeadlessPlayer.DefaultTimeout);
    }
}
