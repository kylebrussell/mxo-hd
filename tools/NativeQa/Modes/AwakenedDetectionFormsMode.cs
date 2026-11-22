using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedDetectionFormsMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 2300;
    private const ushort Detection = ConcealmentDetection.Ability;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Detection }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z, int Org, string Form, ushort State, bool Revealed)[] Helpers =
    {
        ("QAEnemy", 6114, 17120, 2300, 2, "imparted", Stealth.InvisibilityImpartedAbility, true),
        ("QAOther", 6113, 17000, 3500, 2, "invis", Stealth.InvisibilityGrantAbility, true),
        ("QAFar", 6112, 17500, 2300, 2, "shadow", Stealth.MasterShadowAbility, false),
        ("QAFriend", 6111, 16880, 2300, 1, "imparted", Stealth.InvisibilityImpartedAbility, false)
    };

    public override string Name => "ability-parity-awakened-detection-forms";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM inventory WHERE charId=110 AND (slot=8 OR goid=2388)",
        "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,2388,8,1,0)"
    }, new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},innerStrC=200,innerStrM=200 WHERE charId=110",
        $"UPDATE characters SET rotation={MobAggro.YawTowards(X,Z,Helpers[0].X,Helpers[0].Z)} WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
    }.Concat(Helpers.Select(helper =>
        $"UPDATE characters SET x={helper.X},y={Y},z={helper.Z},alignment={helper.Org} WHERE charId={helper.Id}")).ToArray(),
        DevCommands: true, testMobFixture: "martial-player-controls.csv",
        hotbarInventory: new AbilitySweepHotbar.InventoryShortcut[] { new(8, 2388, "Concealment Countermeasure Tool") },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "298,629,1116,1118", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "298" },
        readyMessages: new[]
        {
            $"Level {Level}, full prerequisites, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR (17000,495,2300) faces QAEnemy (17120,495,2300) on outdoor NAV floor 400. QAOther (17000,495,3500), QAFar (17500,495,2300) and allied QAFriend (16880,495,2300) are all on the same open floor within 12 m. Keep selection clear.",
            """Use the helper chat request for each helper. Send {"actor":"QAEnemy","action":"chat","text":"?stealth imparted"} to QAEnemy and QAFriend. Send ?stealth invis to QAOther and ?stealth shadow to QAFar. Wait for state 629 on both imparted helpers, 1116 on QAOther and 1118 on QAFar.""",
            "Press page 2 slot 1 Countermeasure (bag slot 8). Expect stock 298 (80 b9 2a 01), [MODIFIER] 298/204 +5 and 298/638 +72 at login, IS 200->195 and 10 s own reuse. Only QAEnemy 629 and QAOther 1116 end, each with counter FX 2800059d. [DETECTION] uses one roll and reveals both. QAFriend 629 and QAFar 1118 are unchanged. No [PVP] round or maximum [VITALS] change."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Helpers.Select(helper => (helper.X, helper.Z)).Prepend((X, Z)))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Detection stand ({x},{Y},{z}) is on outdoor NAV");
            Check.That(AbilityTargets.WithinArea(X, Y, Z, x, Y, z, 12) && NavMesh.HasSightPath(1, X, Y, Z, x, Y, z),
                $"Detection stand ({x},{Y},{z}) is inside the source radius in clear sight");
        }

        var server = context.ActualServer!;
        foreach (var stand in Helpers)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var ranks = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && ranks.GetValueOrDefault(Detection) == Level &&
            Loadout.All(id => ranks.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                ranks.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "All learned Detection prerequisites fit");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, Detection, 638, 72, 0, false),
            "Detection's loaded contest total");
        foreach (var helper in context.Helpers.Values)
            await actor.WaitForViewNamedAsync(helper.Handle);

        var cursors = new Dictionary<string, int>();
        foreach (var stand in Helpers)
        {
            var helper = context.Helpers[stand.Name];
            int cursor = helper.World.RpcSnapshot().Count;
            helper.SendChat($"?stealth {stand.Form}");
            await StockAbilityProfile.ObserveAsync(helper, cursor, frame => Switch(frame, stand.State, true),
                $"{stand.Name} enters {stand.Form}");
            cursors.Add(stand.Name, helper.World.RpcSnapshot().Count);
        }

        int rpcAt = actor.World.RpcSnapshot().Count;
        int viewAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(AwakenedDetectionBoundaryMode.Countermeasure);
        await EvidenceRetestWire.TimerAsync(actor, rpcAt, Detection, AbilityCooldowns.OwnTimer, 10);
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
            .Contains((ushort)195), "Countermeasure spends exactly five IS", HeadlessPlayer.DefaultTimeout);
        foreach (var stand in Helpers)
        {
            var helper = context.Helpers[stand.Name];
            int cursor = cursors[stand.Name];
            if (stand.Revealed)
            {
                await StockAbilityProfile.ObserveAsync(helper, cursor, frame => Switch(frame, stand.State, false),
                    $"Detection ends {stand.Form} for {stand.Name}");
                await helper.WaitForViewUpdateAsync(2, "02 80 80 80 80 30 9d 05 00 28 ??");
                continue;
            }

            helper.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(helper, cursor, frame => frame.Bytes is [0x81, 0x49, ..],
                $"{stand.Name} stays connected through the reveal");
            Check.That(!helper.World.RpcSnapshot().Skip(cursor).Any(frame => Switch(frame, stand.State, false)),
                $"{stand.Name} keeps {stand.Form}");
        }
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Detection framing");
        return Array.Empty<byte>();

        static bool Switch(RpcRecord frame, ushort state, bool on) =>
            frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(state, on));
    }
}
