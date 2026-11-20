using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class OperativePassiveConsumersMode : NativeQaMode
{
    private const ushort Insight = 113, Multi = 140, Operative = 143;
    private static readonly ushort[] Loadout = Profile(2, 17, Insight, Multi, Operative).Order().ToArray();
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Bonuses =
    {
        (Insight, 0, new (ushort, int)[] { (1048, 10) }),
        (Operative, 1060, new (ushort, int)[] { (526, 50) })
    };

    public override string Name => "ability-parity-operative-passive-consumers";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 17000, 2300, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            "UPDATE characters SET y=495,alignment=2,x=17000,z=2300,healthC=20000,healthM=20000 WHERE charId=110",
            "UPDATE characters SET y=495,alignment=1,x=17090,z=2300,healthC=20000,healthM=20000 WHERE charId IN (6111,6112)",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new ushort[] { Insight, Multi, Operative }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "113,140,143", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Page 2 slots 1–3 identify the loaded passives. Verify Force Combat +10, Sitting Health Regen +50%, and Operative Upgrade 1060 in Actions. Before fighting, use ?health 10000, wait two standing regen ticks, then /sit and measure two sitting ticks. Use /stand, ?qaunload 143, ?health 10000 and /sit again. The current consumer gives +400 per two-second tick while standing or sitting unloaded, and +600 while sitting loaded. Stand and restore 143.",
            "For Multi-Fighting, send ordinary melee requests from QAFriend and then QAFar against CSR_Tester. With both direct opponents live, capture [MULTIFIGHTING OURS] opponents2 loadedTrue penalty0. Press Withdraw and submit stop from both helpers. Wait for all rounds to finish. Use ?qaunload 140 and repeat, expecting loadedFalse penalty-10. Verify 113 and 143 remain active.",
            """Helper actions: {"actor":"QAFriend","action":"melee","target":"CSR_Tester"} and the same for QAFar. To end, use {"actor":"QAFriend","action":"stop"} and the same for QAFar. Enter submits a dev command; keep any selection without pressing Escape. Current PvP link policy and natural combat RNG apply."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (string name in new[] { "QAFriend", "QAFar" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Operative profile fits its memory budget");
        await PvpOpponentsFixture.EnterAsync(actor);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Bonuses, 0, remove: false);

        await RegenAsync(actor, server, sitting: false, expected: 400);
        await RegenAsync(actor, server, sitting: true, expected: 600);
        await StandAsync(actor, server);
        int off = await ChangeLoadoutAsync(actor, rows, new[] { Operative }, load: false);
        await CheckPassivesAsync(actor, proof, Bonuses[1..], off, remove: true);
        await RegenAsync(actor, server, sitting: true, expected: 400);
        await StandAsync(actor, server);
        int on = await ChangeLoadoutAsync(actor, rows, new[] { Operative }, load: true);
        await CheckPassivesAsync(actor, proof, Bonuses[1..], on, remove: false);

        await FightAsync(actor, context, loaded: true, penalty: 0);
        int changed = await ChangeLoadoutAsync(actor, rows, new[] { Multi }, load: false);
        await FightAsync(actor, context, loaded: false, penalty: -10);
        Check.That(!actor.World.RpcSnapshot().Skip(changed).Any(frame =>
            Bonus(frame, Insight, 1048, 10, 0, true) || Bonus(frame, Operative, 526, 50, 0, true)),
            "Unloading Multi-Fighting preserves the two independent passives");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Operative passive consumer framing");
        return Array.Empty<byte>();
    }

    private static async Task RegenAsync(HeadlessPlayer actor, ServerInstance server, bool sitting, int expected)
    {
        if (sitting)
        {
            int logAt = server.DebugLog().Length;
            actor.SendRpc("34 0a"); // Ordinary stock /sit stance.
            await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains($"[EMOTE] {actor.Handle} stance 0a"),
                "Sitting stance changed before measuring recovery", HeadlessPlayer.DefaultTimeout);
        }
        int cursor = actor.World.ViewSnapshot().Count;
        actor.SendChat("?health 10000");
        await actor.WaitUntilAsync(() =>
        {
            ushort[] values = PvpOpponentsFixture.HealthHistory(actor.World.ViewSnapshot(), cursor, 2, self: true);
            return values.Zip(values.Skip(1), (before, after) => after - before).Count(step => step == expected) >= 2;
        }, $"Two {expected}-point ordinary HP updates while sitting={sitting}", TimeSpan.FromSeconds(9));
    }

    private static async Task StandAsync(HeadlessPlayer actor, ServerInstance server)
    {
        int logAt = server.DebugLog().Length;
        actor.SendRpc("33 0a"); // Ordinary stock /stand.
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains($"[EMOTE] {actor.Handle} stance 00"),
            "Standing before normal combat or loadout changes", HeadlessPlayer.DefaultTimeout);
    }

    private static async Task FightAsync(HeadlessPlayer actor, NativeQaBootstrapContext context, bool loaded, int penalty)
    {
        var server = context.ActualServer!;
        int cursor = server.DebugLog().Length;
        foreach (var helper in context.Helpers.Values)
        {
            var target = await helper.WaitForViewNamedAsync(actor.Handle);
            helper.SkipReceived();
            string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
            helper.SendRpc($"40 {handle} 01 6b");
            await helper.WaitForRpcAsync("45");
        }
        await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains(
            $"[MULTIFIGHTING OURS] {actor.Handle} opponents2 loaded{loaded} penalty{penalty} "),
            $"Actual two-opponent PvP input with Multi-Fighting loaded={loaded}", TimeSpan.FromSeconds(15));
        var participants = context.Helpers.Values.Append(actor).ToArray();
        foreach (var participant in participants)
        {
            participant.SkipReceived();
            participant.SendRpc("44 00");
        }
        foreach (var participant in participants)
            await participant.WaitForRpcAsync("46 ?? ?? ?? ?? 00", TimeSpan.FromSeconds(30));
    }
}
