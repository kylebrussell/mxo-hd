using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterHackerAreaRecipientsMode : NativeQaMode
{
    private const ushort HackerStyle = 1006, Disruption = 38, FreezeArea = 47, LogicBlast = 57, UiLag = 97;
    private const string HackerCast = "80b9ee0300000000000000000000000000000000000000000000000000000084d75741086b0000000000";
    // PR512 stock UI Lag and preserved Area Disruption capture 5cab2d1841ad:8946.
    private const string LagCast = "80b96100060004000000000000000000000000000000000000000000585cb70001000000086b0000000000";
    private const string DisruptCast = "80b92600000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    // Actual PR540 Freeze Area and PR482 Logic Blast, with only the live target handle substituted.
    private const string FreezeCast = "80b92f00070005000000000000000000000000000000000000000000585cb80001000000086b0000000000";
    private const string BlastCast = "80b93900060005000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private static readonly (string Name, uint Id, int X, int Z, int Org)[] Stands =
    {
        ("QAEnemy", 6114, 25000, -1498, 2), ("QAFriend", 6111, 24880, -1680, 2),
        ("QAFar", 6112, 25400, -1498, 2), ("QAOther", 6113, 24900, -1398, 1)
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, HackerStyle, Disruption, FreezeArea, LogicBlast, UiLag)
        .Order().ToArray();

    public override string Name => "ability-parity-starter-hacker-area-recipients";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,healthC=3000,healthM=3000,innerStrC=500,innerStrM=500,alignment=1,pvpflag=0 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES (6111,400,2147485696,50,1),(6111,401,2147501056,50,1)"
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},x={stand.X},y=495,z={stand.Z},healthC=3000,healthM=3000,alignment={stand.Org},pvpflag=0 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { HackerStyle, UiLag, Disruption, FreezeArea, LogicBlast }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "16383", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "32766"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "38,47,57,97", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay at the stand and select Hacker Style (page 2 slot 1). The helpers are already visible. QAEnemy is 2 m east, QAFriend is a hostile with Self Defense and higher Viral Defense inside 3 m, QAFar is 6 m east, and QAOther is friendly inside 3 m.",
            "The first UI Lag capture already passed. Start this retest with QAFar selected and press slot 4 Freeze Area. QAEnemy receives 6 s Pacify and -30 Viral Resistance for 12 s plus 12 s immunity. QAFriend deflects. QAFar and QAOther stay unaffected.",
            "As soon as Pacify lands, select QAEnemy and press slot 5 Logic Blast. Check that landed damage ends the Freeze Area parent and its two records and preserves the independent 12 s immunity. Then immediately press slot 3 Area Disruption while immunity still holds. QAEnemy should refuse the new Stun. QAFar should get the 3.15 s Stun and 8 s immunity. Keep all observed remove records."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var defender = context.Helpers["QAFriend"];
        await StockAbilityProfile.ObserveAsync(defender, 0,
            frame => ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == 17 &&
                record.TargetId == 183 && record.SignedValue == 47 && record.AddRemoveType == 0,
            "The deflect witness has +47 active Viral Defense");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Legal caster memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var helper in context.Helpers.Values)
            await actor.WaitForViewNamedAsync(helper.Handle);
        actor.SendRpc(HackerCast);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var far = await actor.WaitForViewNamedAsync("QAFar");
        uint farHandle = DuelPackets.Handle(far.ViewId, far.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(farHandle))}");

        (ushort Stat, int Value, float Duration)[] lagModifiers =
            { (1032, -10, 27), (183, -17, 27), (1011, -12, 27), (342, -14, 27) };
        await CastAsync(UiLag, StarterHackerControlTargetMode.WithHandle(LagCast, farHandle), 58,
            new[] { "QAEnemy" }, lagModifiers);
        var near = context.Helpers["QAEnemy"];
        int freezeAt = near.World.RpcSnapshot().Count;
        await CastAsync(FreezeArea, StarterHackerControlTargetMode.WithHandle(FreezeCast, farHandle), 58,
            new[] { "QAEnemy" }, new (ushort, int, float)[] { (85, 0, 6), (157, -30, 12) });

        var nearView = await actor.WaitForViewNamedAsync(near.Handle);
        uint nearHandle = DuelPackets.Handle(nearView.ViewId, nearView.SpawnId);
        int strikeAt = near.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BlastCast, nearHandle));
        var damage = await StockAbilityProfile.ObserveAsync(near, strikeAt, frame => frame.Bytes.Length >= 19 &&
            frame.Bytes[0] == 0x81 && frame.Bytes[1] == 0x67 &&
            BitConverter.ToUInt32(frame.Bytes, 6) == AbilityMessages.DamageTarget &&
            BitConverter.ToUInt32(frame.Bytes, 15) == AbilityMessages.AbilityGameObject(LogicBlast),
            "Ordinary Logic Blast actually damages the pacified opponent");
        var pacifyOff = await StockAbilityProfile.ObserveAsync(near, strikeAt,
            frame => StockAbilityProfile.Bonus(frame, FreezeArea, 85, 0, 6, true), "The hit removes Pacify");
        var resistanceOff = await StockAbilityProfile.ObserveAsync(near, strikeAt,
            frame => StockAbilityProfile.Bonus(frame, FreezeArea, 157, -30, 12, true), "The hit removes the Freeze Area parent vulnerability");
        Check.Equal(damage.ListCounter, pacifyOff.ListCounter, "Pacify clears in the damage response, before natural expiry");
        Check.Equal(damage.ListCounter, resistanceOff.ListCounter, "The same response clears the second parent modifier");
        Check.That(!near.World.RpcSnapshot().Skip(freezeAt).Any(frame =>
            StockAbilityProfile.Bonus(frame, TempMods.ControlImmunityTimerAbility, 681, 0, 12, true)),
            "Premature parent removal preserves control immunity");

        int disruptAt = server.DebugLog().Length;
        await CastAsync(Disruption, Convert.FromHexString(DisruptCast), 43,
            new[] { "QAFar" }, new (ushort, int, float)[] { (102, 0, 3 * 1.05f) });
        Check.That(server.DebugLog()[disruptAt..].Contains("[EFFECTS] 38: control immunity, state refused", StringComparison.Ordinal),
            "QAEnemy's immunity blocks the second control cast");
        await near.WaitUntilAsync(() => near.World.RpcSnapshot().Skip(freezeAt).Any(frame =>
                StockAbilityProfile.Bonus(frame, TempMods.ControlImmunityTimerAbility, 681, 0, 12, true)),
            "The independent immunity timer naturally ends", TimeSpan.FromSeconds(13));
        await near.WaitUntilAsync(() => lagModifiers.All(modifier => near.World.RpcSnapshot().Any(frame =>
            StockAbilityProfile.Bonus(frame, UiLag, modifier.Stat, modifier.Value, modifier.Duration, true))),
            "All four UI Lag modifiers expire on the admitted recipient", TimeSpan.FromSeconds(30));
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Hacker area and deflection framing");
        return Array.Empty<byte>();

        async Task CastAsync(ushort id, byte[] bytes, float reuse, string[] recipients,
            (ushort Stat, int Value, float Duration)[] modifiers)
        {
            int logAt = server.DebugLog().Length;
            int castAt = actor.World.RpcSnapshot().Count;
            var cursors = context.Helpers.ToDictionary(entry => entry.Key, entry => entry.Value.World.RpcSnapshot().Count);
            actor.SkipReceived();
            actor.SendRpc(bytes);
            await EvidenceRetestWire.CastBarAsync(actor, id, 2);
            await EvidenceRetestWire.TimerAsync(actor, castAt, id, AbilityCooldowns.OwnTimer, reuse);
            foreach (string name in recipients)
            {
                foreach (var (stat, value, duration) in modifiers)
                    await StockAbilityProfile.ObserveAsync(context.Helpers[name], cursors[name],
                        frame => StockAbilityProfile.Bonus(frame, id, stat, value, duration, false),
                        $"{id} applies {stat} to {name}");
                if (id == Disruption)
                    await StockAbilityProfile.ObserveAsync(context.Helpers[name], cursors[name],
                        frame => StockAbilityProfile.Bonus(frame, TempMods.ControlImmunityTimerAbility, 681, 0, 8, false),
                        $"Area Disruption starts eight-second immunity on {name}");
                if (id == FreezeArea)
                    await StockAbilityProfile.ObserveAsync(context.Helpers[name], cursors[name],
                        frame => StockAbilityProfile.Bonus(frame, TempMods.ControlImmunityTimerAbility, 681, 0, 12, false),
                        $"Freeze Area starts twelve-second immunity on {name}");
            }

            var deflect = new Regex($@"\[FREECAST-VIRAL\] ability={id} source=CSR_Tester target=QAFriend [^\r\n]+hit=False\b");
            await actor.WaitUntilAsync(() => deflect.IsMatch(server.DebugLog()[logAt..]),
                $"The same area cast deflects on the defended player", HeadlessPlayer.DefaultTimeout);
            foreach (var (name, helper) in context.Helpers)
            {
                if (recipients.Contains(name)) continue;
                int ack = helper.World.RpcSnapshot().Count;
                helper.SendRpc("81 48");
                await StockAbilityProfile.ObserveAsync(helper, ack, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes),
                    $"Recipient exclusion sampled after landing on {name}");
                Check.That(!helper.World.RpcSnapshot().Skip(cursors[name]).Any(frame =>
                    ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == id && record.AddRemoveType == 0),
                    $"{name} receives no modifier from {id}");
            }
        }
    }
}
