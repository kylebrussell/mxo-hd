using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterHackerAreaRecipientsMode : NativeQaMode
{
    private const ushort HackerStyle = 1006, Disruption = 38, FreezeArea = 47, UiLag = 97;
    private const string HackerCast = "80b9ee0300000000000000000000000000000000000000000000000000000084d75741086b0000000000";
    // PR512 stock UI Lag and preserved Area Disruption capture 5cab2d1841ad:8946.
    private const string LagCast = "80b96100060004000000000000000000000000000000000000000000585cb70001000000086b0000000000";
    private const string DisruptCast = "80b92600000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly (string Name, uint Id, int X, int Z, int Org)[] Stands =
    {
        ("QAEnemy", 6114, 25000, -1498, 2), ("QAFriend", 6111, 24880, -1680, 2),
        ("QAFar", 6112, 25400, -1498, 2), ("QAOther", 6113, 24900, -1398, 1)
    };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, HackerStyle, Disruption, FreezeArea, UiLag)
        .Order().ToArray();

    public override string Name => "ability-parity-starter-hacker-area-recipients";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,healthC=3000,healthM=3000,innerStrC=500,innerStrM=500,alignment=1,pvpflag=0 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES (6111,400,2147485696,50,1),(6111,401,2147501056,50,1)"
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},x={stand.X},y=495,z={stand.Z},healthC=3000,healthM=3000,alignment={stand.Org},pvpflag=0 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { HackerStyle, UiLag, Disruption, FreezeArea }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "16383", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "32766"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "38,47,97", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay at the stand and select Hacker Style (page 2 slot 1). The helpers are already visible. QAEnemy is 2 m east, QAFriend is a hostile with Self Defense and higher Viral Defense inside 3 m, QAFar is 6 m east, and QAOther is friendly inside 3 m.",
            "Select QAFar and keep it selected. Use slot 2 UI Lag. QAEnemy receives four debuffs for 27 s. QAFriend deflects with no debuffs, QAFar is outside the 3 m radius, and QAOther is friendly. Keep the helper deflect line and the target removals.",
            "Use slot 3 Area Disruption. The 8 m effect reaches QAEnemy and QAFar, and QAFriend deflects again. This loadout gives about 3.15 s Stun and 8 s immunity. Wait for both to clear.",
            "Use slot 4 Freeze Area and capture its first stock request. Compare the same 3 m recipient set as UI Lag. QAEnemy should get Pacify and -30 Viral Resistance. The fixture fixes only eligible viral contest draws and keeps the recipient defense differences."
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
        await CastAsync(Disruption, Convert.FromHexString(DisruptCast), 43,
            new[] { "QAEnemy", "QAFar" }, new (ushort, int, float)[] { (102, 0, 3 * 1.05f) });
        var near = context.Helpers["QAEnemy"];
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
