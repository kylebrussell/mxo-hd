using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterPathogenistAuraSightMode;

namespace NativeQa.Modes;

public sealed class StarterPathogenistAuraResistanceMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498, TargetZ = -1798;
    private const ushort Style = CombatStyles.HackerStyle, Miasma = 61, StaticField = 89, CodeNuke = 60;
    private const ushort ResistInfection = 75, EmptyMind = 8, ViralResistance = 157, StaticPulse = 694, MiasmaPulse = 701;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Miasma, StaticField, CodeNuke, ResistInfection).Order().ToArray();
    private static readonly ushort[] ResistProfile = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();

    public override string Name => "ability-parity-starter-pathogenist-aura-resistance";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X, Z, X, TargetZ)} WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={TargetZ},alignment=2 WHERE charId=6114",
            $"UPDATE characters SET x={X + 300},y={Y},z={TargetZ},alignment=2 WHERE charId=6112",
            "DELETE FROM char_abilities WHERE char_id IN (6112,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", ResistProfile.Select((id, i) => $"(6112,{i},{0x80000000u | ((uint)id << 10)},50,1)")),
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new ushort[] { Style, StaticField, CodeNuke, Miasma },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "60,61,89,694,701", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "QAEnemy is straight ahead at 24800,495,-1798. QAFar is 3 m east of QAEnemy. Both are hostile, on clear ground, with no ambient NPC roster. Use Hacker Style on page 2 slot 1.",
            """Clear selection. Send {"actor":"QAFar","action":"aura-resist"} and wait for OK. Expect [MODIFIER] char=6112 source=8 stat=157 +75 duration=20s. Immediately activate slot 2 Static Field. QAEnemy takes one viral damage per pulse. QAFar absorbs the one point, with [TYPED-RESISTANCE OURS] 694 type2 total75 raw1 final0 while Empty Mind is active.""",
            "After the second Static Field pulse at about 5 s, turn it off. Select QAEnemy and press slot 3 Code Nuke. This is the direct viral control. Check its splash on QAFar and whether Empty Mind was still active when it landed. An exhausted Empty Mind window need not be repeated for this control.",
            "Press slot 4 Miasma, wait through a pulse, then toggle off. Expect [MODIFIER] source=701 stats 1009 and 1010 each -14 for 5 s on both recipients. Empty Mind mitigates health damage. The defense debuffs still apply. Neither recipient loses HP or IS to Miasma. CSR pays 5 IS on activation and 3 per scheduled pulse."
        });

    internal static async Task ApplyResistanceAsync(HeadlessPlayer target, int resistance = 75)
    {
        Check.Equal(6112u, target.CharId, "Only the disposable QAFar target carries the resistance fixture");
        int start = target.World.RpcSnapshot().Count;
        target.SendRpc(StarterHackerControlTargetMode.WithHandle(AwakenedResistanceThresholdMode.EmptyCast, 0));
        await StockAbilityProfile.ObserveAsync(target, start, frame =>
            StockAbilityProfile.Bonus(frame, EmptyMind, ViralResistance, resistance, 20, false), $"Empty Mind adds +{resistance} Viral Resistance");
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The stock profile contains all ranked prerequisites");
        await PvpOpponentsFixture.EnterAsync(actor);
        var neutral = context.Helpers["QAEnemy"];
        var resisted = context.Helpers["QAFar"];
        var primary = await actor.WaitForViewNamedAsync(neutral.Handle);
        await actor.WaitForViewNamedAsync(resisted.Handle);
        uint handle = DuelPackets.Handle(primary.ViewId, primary.SpawnId);
        actor.SendRpc(HackerUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        await ApplyResistanceAsync(resisted);

        int neutralAt = neutral.World.RpcSnapshot().Count;
        int resistedAt = resisted.World.RpcSnapshot().Count;
        int logsAt = server.DebugLog().Length;
        await ToggleAsync(StaticField, Convert.FromHexString(StaticUse), true);
        await neutral.WaitUntilAsync(() => neutral.World.RpcSnapshot().Skip(neutralAt).Count(frame => Pulse(frame, neutral, 1, 0)) >= 2,
            "Static Field hits the unbuffed hostile on the initial and scheduled pulses", HeadlessPlayer.DefaultTimeout);
        await resisted.WaitUntilAsync(() => resisted.World.RpcSnapshot().Skip(resistedAt).Count(frame => Pulse(frame, resisted, 0, 1)) >= 2,
            "Empty Mind absorbs both observed Static Field pulses", HeadlessPlayer.DefaultTimeout);
        await ToggleAsync(StaticField, Convert.FromHexString(StaticUse), false);
        Check.That(!resisted.World.RpcSnapshot().Skip(resistedAt).Any(frame => Pulse(frame, resisted, 1, 0)),
            "Viral resistance absorbs Static Field's one-point hit consistently");
        Check.That(server.DebugLog()[logsAt..].Contains("694 type2 total75 raw1 final0"), "The child consumes the target's Viral Resistance");

        logsAt = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterHackerViralSightMode.Samples[3].Capture, handle));
        await EvidenceRetestWire.CastBarAsync(actor, CodeNuke, 4);
        await actor.WaitUntilAsync(() => server.DebugLog()[logsAt..].Contains("60 type2 total75 raw"), "Code Nuke retains its ordinary resisted splash", HeadlessPlayer.DefaultTimeout);

        neutralAt = neutral.World.RpcSnapshot().Count;
        resistedAt = resisted.World.RpcSnapshot().Count;
        await ToggleAsync(Miasma, StarterHackerControlTargetMode.WithHandle(MiasmaOn, handle), true);
        foreach (var (recipient, cursor) in new[] { (neutral, neutralAt), (resisted, resistedAt) })
            foreach (ushort stat in new ushort[] { 1009, 1010 })
                await StockAbilityProfile.ObserveAsync(recipient, cursor,
                    frame => StockAbilityProfile.Bonus(frame, MiasmaPulse, stat, -14, 5, false),
                    $"The Miasma modifier {stat} lands on {recipient.Handle}");
        await Task.Delay(600); // Separate the two toggle requests from the stock double-click window.
        await ToggleAsync(Miasma, StarterHackerControlTargetMode.WithHandle(MiasmaOff, handle), false);
        Check.Equal(0, actor.World.Problems.Count + neutral.World.Problems.Count + resisted.World.Problems.Count,
            "Pathogenist aura resistance replay framing");
        return Array.Empty<byte>();

        bool Pulse(RpcRecord frame, HeadlessPlayer player, int done, int absorbed) =>
            frame.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, StaticPulse, actor.Handle, player.Handle, done, absorbed));

        async Task ToggleAsync(ushort id, byte[] capture, bool active)
        {
            int start = actor.World.RpcSnapshot().Count;
            actor.SendRpc(capture);
            await StockAbilityProfile.ObserveAsync(actor, start,
                frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(id, active)), $"Aura {id} toggled to {active}");
        }
    }
}
