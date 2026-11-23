using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerInfectAndBottleneckMode : NativeQaMode
{
    private const int Level = 24, X = 17000, Y = 495, Z = 6400, CentreZ = 6600;
    internal const int ViralResistance = 36;
    private const ushort Infect = 353, Bottleneck = 402, Nuke = 60, Style = CombatStyles.HackerStyle;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Style, Nuke, Infect, Bottleneck }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly (string Handle, uint Id, int X, int Z, int Faction)[] Stands =
    {
        ("QAEnemy", 6114, X, CentreZ, 2), ("QAOther", 6113, X, 7200, 2),
        ("QAFar", 6112, X, 7201, 2), ("QAFriend", 6111, X + 100, CentreZ, 1)
    };

    public override string Name => "ability-parity-hacker-infect-and-bottleneck";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=0,alignment=1,innerStrC=600,innerStrM=800 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", StockAbilityProfile.Profile(2, 17, 8).Order().Select((id, slot) =>
                    $"(6112,{slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }.Concat(Stands.Select(stand => $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            $"x={stand.X},y={Y},z={stand.Z},alignment={stand.Faction},pvpflag=0,healthC=1000,healthM=1000 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Bottleneck, Infect, Nuke }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "353,402", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Full ranked profile at L{Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Clear outdoor NAV. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{CentreZ}), QAOther at z7200 is the 6.00 m edge, QAFar at z7201 is 6.01 m, ally QAFriend is 1 m beside the primary at x17100.",
            "Infect resistance retest: select Hacker Style (page 2 slot 1) and /target QAEnemy. Send the Infect resistance move below for QAFar and leave QAOther on its original 6 m stand. Send the printed QAFar aura-resist action. As soon as it reports OK, press slot 3 once and retain the first complete 353 request. This is the focused retest; the Bottleneck and ordinary edge steps below are optional after it expires.",
            "Expect source 8 stat 157 +36 for 20 s on QAFar only. Infect spends 15 IS (600 to 585), casts for 2 s and starts its 12 s timer. On QAEnemy and QAFar alone: source 353 stat 1157 +114 for 30 s, then matching removals. QAEnemy takes eleven ticks of 10. While source 8 is active QAFar shows [TYPED-RESISTANCE OURS] 353 type2 total36 raw10 final8 and [EFFECTS] 353 ticks on QAFar for 8. After source 8 removes, the same 353 record keeps ticking, now for 10. Expect eleven ticks on QAFar total and no twelfth. No effects on the ally or QAOther. No interlock or [VITALS] max changes.",
            """Infect resistance move: {"actor":"QAFar","action":"move","x":17000,"y":495,"z":7100}. Then {"actor":"QAFar","action":"aura-resist"}. For a full rerun, return QAFar to its original z7201 before Bottleneck.""",
            "Select Hacker Style (page 2 slot 1). Try slots 2 and 3 without a target and on QAFriend. Any sent request is refused reason 24 before cost/cast/reuse. /target QAEnemy. Use slot 2 Bottleneck Field outside combat and retain the first complete 402 frame. Expect 2 s, 23 IS and 30 s reuse.",
            "Only QAEnemy and QAOther receive [MODIFIER] 402: stats 115/877/878 each -125 and 519 -25 for 18 s. No direct HP or Rooted 99 record. Their scale goes to -1.00 without a positive buff and returns to zero with the four removals. CSR's PacketLog can read each opponent view. Ally and QAFar get no 402 modifier/scale.",
            "After expiry move QAOther and QAFar using the printed console requests. Keep QAEnemy selected, use slot 3 Infect Area 1.0 and retain the first complete 353 frame. Expect 15 IS/2 s/12 s reuse and source 353 stat 1157 +114 on QAEnemy and the 5.00 m edge only for 30 s. The shipped base 94 plus this profile's +22% Viral Damage yields eleven viral ticks of 10 per recipient. The record removes before a twelfth tick. QAFar at 5.01 m and the ally get no effect. No max [VITALS] changes or interlock rounds. The archived 450-point live record has an unresolved historical scaling input.",
            "Infect edge: {\"actor\":\"QAOther\",\"action\":\"move\",\"x\":17000,\"y\":495,\"z\":7100}. Infect outside: {\"actor\":\"QAFar\",\"action\":\"move\",\"x\":17000,\"y\":495,\"z\":7101}. Slot 4 is the Code Nuke control."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Selected area QA needs repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Concat(new[] { (X, Z), (X, 7100), (X, 7101) }))
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) <= 100 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z) && NavMesh.HasSightPath(1, X, Y, CentreZ, x, Y, z),
                $"Verified outdoor stand ({x},{Y},{z}) with open sight");

        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loaded.Keys.ToHashSet().SetEquals(Loadout) && Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The exact rank-24 Infect Area/Bottleneck profile fits stock memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        foreach (var helper in context.Helpers.Values) await actor.WaitForViewNamedAsync(helper.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        var resistant = context.Helpers["QAFar"];
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(resistant, ViralResistance);
        var target = await actor.WaitForViewNamedAsync(resistant.Handle);
        uint resistantHandle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        string nuke = StarterHackerViralSightMode.Samples.Single(sample => sample.Ability == Nuke).Capture;
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(resistantHandle))}");
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(nuke, resistantHandle));
        await EvidenceRetestWire.CastBarAsync(actor, Nuke, 4);
        await actor.WaitUntilAsync(() => context.ActualServer!.DebugLog().Contains($"60 type2 total{ViralResistance} raw"),
            "The retained stock Code Nuke control consumes QAFar's live Viral Resistance", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Selected area framing");
        return Array.Empty<byte>();
    }
}
