using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityHackerHostileUpgradesMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498;
    private const ushort TargetBuff = 49;
    private static readonly ushort[] StageOne = Profile(65, 324, 377, 420);
    private static readonly ushort[] ShutdownPrerequisites = Profile(480);
    private static readonly ushort[] Unload = { 318, 420, 422 };
    private static readonly ushort[] Load = ShutdownPrerequisites.Except(StageOne).Order().ToArray();
    private static readonly ushort[] StageTwo = StageOne.Except(Unload).Concat(Load).Order().ToArray();
    private static readonly (ushort Stat, int Value)[] Overheat = { (180, -24), (181, -24) };
    private static readonly (ushort Stat, int Value)[] Shutdown = { (522, -61), (524, -61), (1009, -31), (1010, -31) };
    private static readonly (string Name, int X)[] Helpers = { ("QAFriend", X + 50), ("QAEnemy", X + 400), ("QAFar", X + 2200) };

    public override string Name => "ability-parity-hacker-hostile-upgrades";
    public override NativeQaModePlan Plan { get; } = new(
        StageOne.Concat(StageTwo).Distinct().Order().ToArray(), X, Z,
        Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id IN ({string.Join(",", Load.Select(Descriptor))})" },
        new[]
        {
            $"UPDATE characters SET y={Y},z={Z},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6114)",
            "UPDATE characters SET alignment=1 WHERE charId=110",
            "UPDATE characters SET x=33000+100*(charId-6111),alignment=CASE WHEN charId IN (6112,6114) THEN 2 ELSE 1 END,pvpflag=0 WHERE charId IN (6111,6112,6114)",
            HelperLoadoutSql(6111, StageOne),
            HelperLoadoutSql(6114, Profile(TargetBuff, 110, 217))
        }, DevCommands: true,
        environmentOverrides: new()
        {
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_AGGRO"] = "0",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        readyMessages: new[]
        {
            "After CSR is ready, move QAFriend, QAEnemy and QAFar to their checklist stands via the helper chat action. Use Hacker Style and select QAEnemy. Only the isolated viral contest draw is fixed; callback rolls remain natural.",
            "Cast Overheat 1.0 (65) on QAEnemy: both resistance debuffs -24, duration 33s. Toggle Evade Combat 217 on QAEnemy, confirm it stays on, then arm QAEnemy with Gaussian Blur 49 and use Cancel Upgrade 1.0 (377), Delete Upgrade 1.0 (324), and Delete Upgrade 2.0 (420) with 49 refreshed before each. Capture the removal lines and off record.",
            "Before Combat Shutdown, change to Stage 2 at a Hardline or Load Area: unload 318/420/422 and load the eight listed entries. Use Combat Shutdown (480) on QAEnemy and record -61 both accuracies, -31 both defenses, 25s. See docs/ABILITY-HACKER-HOSTILE-UPGRADES.md."
        },
        extraQaMetadata: new()
        {
            ["InitialMemory"] = 86, ["SecondStageMemory"] = 100,
            ["UnloadForCombatShutdown"] = string.Join(",", Unload), ["LoadForCombatShutdown"] = string.Join(",", Load),
            ["NativeCloseableRows"] = 5, ["NativeCloseableIds"] = "65,324,377,420,480"
        });

    private static ushort[] Profile(params ushort[] ids) => new ushort[] { 2, 17, 1006 }.Concat(ids)
        .Concat(ids.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys)).Distinct().Order().ToArray();
    private static uint Descriptor(ushort id) => 0x80000000u | ((uint)id << 10);
    private static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));
    private static void Cast(HeadlessPlayer caster, ushort id, ushort target) =>
        caster.SendRpc($"80 b9 {Le(id)} {Le(target)}");
    private static string HelperLoadoutSql(int character, ushort[] abilities) =>
        $"DELETE FROM char_abilities WHERE char_id={character}; INSERT INTO char_abilities " +
        "(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES " +
        string.Join(",", abilities.Select((id, slot) => $"({character},{slot},{Descriptor(id)},50,1,'QA hostile upgrade')"));

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        foreach (var (name, _) in Helpers)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.Server == null) return Array.Empty<byte>();

        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var owned = AbilityMemoryUiFixture.Decode(caster.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(loaded.SetEquals(StageOne),
            $"Margin loads Stage 1 only (extra {string.Join(",", loaded.Except(StageOne))}, missing {string.Join(",", StageOne.Except(loaded))})");
        Check.That(StageOne.Concat(StageTwo).All(owned.ContainsKey), "Stock Archive owns every entry in both stages");
        Check.Equal(86, StageOne.Sum(AbilityMemory.Cost), "First profile memory");
        Check.Equal(100, StageTwo.Sum(AbilityMemory.Cost), "Second profile memory");
        await caster.EnterWorldAsync();
        foreach (var (name, x) in Helpers)
        {
            context.Helpers[name].SendChat($"?tp {x} {Y} {Z} 0 0 0");
            var stand = await caster.WaitForViewNamedAsync(name);
            Check.Equal((double)x, BitConverter.ToDouble(stand.Attributes["Position"]), $"Exact {name} stand");
        }
        var target = context.Helpers["QAEnemy"];
        var ally = context.Helpers["QAFriend"];
        var targetView = await caster.WaitForViewNamedAsync(target.Handle);
        var allyView = await ally.WaitForViewNamedAsync(caster.Handle);
        var far = await ally.WaitForViewNamedAsync("QAFar");

        Cast(ally, 65, far.ViewId);
        await ally.WaitForRpcAsync("4d 09", TimeSpan.FromSeconds(10));
        Cast(ally, 324, allyView.ViewId);
        await ally.WaitForRpcAsync("4d 18", TimeSpan.FromSeconds(10));

        int overheatStart = target.World.RpcSnapshot().Count;
        Cast(caster, 65, targetView.ViewId);
        await Effects(target, 65, Overheat, 33, overheatStart, remove: false);

        Cast(target, 217, 2);
        await StockAbilityProfile.ObserveAsync(target, 0, frame => StockAbilityProfile.Bonus(frame, 217, 624, 0, 0, false),
            "QAEnemy enters Evading before Gaussian Blur");

        foreach (ushort dispel in new ushort[] { 377, 324, 420 })
        {
            int start = target.World.RpcSnapshot().Count;
            Cast(target, TargetBuff, 0);
            await TargetBuffChange(target, start, remove: false);
            Cast(caster, dispel, targetView.ViewId);
            byte[] onTarget = AbilityMessages.AboutAbility(AbilityHostile.CounterUpgradeTarget, false, dispel,
                caster.Handle, target.Handle, unchecked((int)AbilityMessages.AbilityGameObject(TargetBuff)));
            await target.WaitForRpcAsync(rpc => rpc.Bytes.AsSpan().SequenceEqual(onTarget), $"Target removal chat from {dispel}");
            await TargetBuffChange(target, start, remove: true);
            if (dispel != 420)
                await target.WaitForRpcAsync("80 bd 01 31 00 04 00 00 00 41 00", TimeSpan.FromSeconds(12));
        }

        await ChangeLoadout(caster, owned, Unload, load: false);
        await ChangeLoadout(caster, owned, Load, load: true);
        int shutdownStart = target.World.RpcSnapshot().Count;
        Cast(caster, 480, targetView.ViewId);
        await Effects(target, 480, Shutdown, 25, shutdownStart, remove: false);
        await Effects(target, 65, Overheat, 33, overheatStart, remove: true);
        await Effects(target, 480, Shutdown, 25, shutdownStart, remove: true);
        Check.That(!caster.World.RpcSnapshot().Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) &&
            bonus!.SourceId is 65 or 480 && bonus.Family == 0x15), "Hostile debuffs were not applied to the caster");
        Console.WriteLine("  Two legal profiles, ranged and allied refusals, three targeted buff strips and both debuff lifetimes verified");
        return Array.Empty<byte>();
    }

    private static Task Effects(HeadlessPlayer actor, ushort ability, (ushort Stat, int Value)[] expected, float seconds,
        int cursor, bool remove) => actor.WaitUntilAsync(() =>
    {
        var records = actor.World.RpcSnapshot().Skip(cursor).ToArray();
        return expected.All(stat => records.Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) &&
            bonus!.SourceId == ability && bonus.TargetId == stat.Stat && bonus.SignedValue == stat.Value &&
            bonus.Duration == seconds && bonus.AddRemoveType == (remove ? 1 : 0)));
    }, $"selected target {ability} exact debuff records (remove={remove})", TimeSpan.FromSeconds(38));

    private static Task TargetBuffChange(HeadlessPlayer target, int cursor, bool remove) => target.WaitUntilAsync(() =>
        target.World.RpcSnapshot().Skip(cursor).Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) &&
            bonus!.SourceId == TargetBuff && bonus.TargetId == 520 && bonus.AddRemoveType == (remove ? 1 : 0)),
        $"target Gaussian Blur change (remove={remove})", HeadlessPlayer.DefaultTimeout);

    private static async Task ChangeLoadout(HeadlessPlayer actor,
        IReadOnlyDictionary<ushort, AbilityMemoryUiFixture.Tuple> owned, ushort[] ids, bool load)
    {
        string operation = load ? "00 00 0a 00" : "0a 00 00 00";
        string entries = string.Join(" ", ids.Select(id => $"00 {Le(owned[id].Slot)} " +
            $"{WireFormat.ToHex(BitConverter.GetBytes((uint)id))} {Le(owned[id].Level)}"));
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc($"80 ae 00 00 00 00 {operation} {Le((ushort)ids.Length)} {entries}");
        var acks = ids.Select(id => BytePattern.Parse($"80 {(load ? "b2" : "b3")} {Le(id)}")).ToArray();
        await actor.WaitUntilAsync(() =>
        {
            var packets = actor.World.RpcSnapshot().Skip(cursor).ToArray();
            return acks.All(ack => packets.Any(rpc => ack.IsMatch(rpc.Bytes)));
        }, $"ordinary 100-memory profile transition, load={load}", HeadlessPlayer.DefaultTimeout);
    }
}
