using System.Buffers.Binary;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static HeadlessClient.EnrageNpcCloseCombatFixture;

namespace NativeQa.Modes;

public sealed class HackerDestroyerFieldsMode : NativeQaMode
{
    private const int CasterLevel = 50, StreetX = 17000, StreetY = 495, StreetZ = 6400, CentreZ = 6700;
    private const ushort Hacker = CombatStyles.HackerStyle, Destroyer = 469, Debuff = 1088, Enrage = 462, Inefficiency = 467;
    // abilities_use_destroyer_at_corrupted.log.txt, lines 198–199. Only the observed live handle is rebound.
    private const string StockDestroyer = "80 b9 40 04 06 00 2e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 20 00 00 00 74 f2 12 00 2a 6b 00 00 00 00 00";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, Destroyer, Enrage, Inefficiency).Order().ToArray();
    private static readonly ushort[] Resistances = { 157, 180, 181, 1033 };
    private static readonly (string Handle, int Character, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, StreetX, CentreZ, 2),
        ("QAOther", 6113, StreetX, CentreZ + 500, 2),
        ("QAFar", 6112, StreetX, CentreZ + 501, 2),
        ("QAFriend", 6111, StreetX + 100, CentreZ, 1)
    };

    public override string Name => "ability-parity-hacker-destroyer-fields";

    public override NativeQaModePlan Plan { get; } = new(Loadout, StreetX, StreetZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{CasterLevel}) WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={StreetX},y={StreetY},z={StreetZ},rotation={MobAggro.YawTowards(StreetX,StreetZ,StreetX,CentreZ)},innerStrC=700,innerStrM=1000 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET x={stand.X},y={StreetY},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Character}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Hacker, Debuff, Enrage, Inefficiency }, testMobFixture: "enrage-armed-blackwood.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "1088,462,467", ["NativeCloseableRows"] = 3, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {CasterLevel}, {Loadout.Sum(AbilityMemory.Cost)}/100 memory. Outdoor NAV CSR ({StreetX},{StreetY},{StreetZ}) faces QAEnemy ({StreetX},{StreetY},{CentreZ}). Ally QAFriend is x17100,z6700; QAOther/QAFar start at z7200/7201. Stock page 2 has Hacker Style, Destroyer, Enrage Foes and Inefficiency Field 2.0.",
            "Select Hacker Style. With no selection, with /target QAFriend, and with QAEnemy at z8001, try slots 2–4. Expect a local refusal or reason 24/34, no cast or IS use. At z7701 try only slot 4 for its 13 m range. Return QAEnemy to z6700 and reselect it.",
            "Send helper moves using {\"actor\":\"QAEnemy\",\"action\":\"move\",\"x\":17000,\"y\":495,\"z\":8001}; change the actor and z for the other positions. CSR stays at the original street stand for all three player casts.",
            "Slot 2 Destroyer grant 1088: 4 s cast, IS 700→660 and 90 s own reuse. On QAEnemy and QAOther only, expect [MODIFIER] source1088 stats157/180/181/1033 -120 for 30 s, then four matched removes per recipient. Radius is 5 m. No vitals or health change.",
            "Move QAOther/QAFar to z7300/7301. Slot 3 Enrage Foes 462: 2 s cast, IS 660→634 and 30 s own reuse. Expect source462 stat93 value 0 for 6 s on QAEnemy and QAOther only (6 m), with the linked state record and matched removal; no 734 immunity. Keep the first full stock 80 b9 ce 01.",
            "Move QAOther/QAFar to z7400/7401. Slot 4 Inefficiency Field 2.0 467: 4 s cast, IS 634→609 and 60 s own reuse. Expect source467 stat117 -30 and stat114 -80 for 30 s on QAEnemy and QAOther (7 m) only, then matched removes. Keep the first full stock 80 b9 d3 01.",
            $"NPC Enrage check after the 30 s reuse. Send QAFriend helper chat ?tp {HelperX} {Y} {HelperZ} 37b b 11, then move CSR via ?tp {CasterX} {Y} {CasterZ} 37b b 11 and strafe once. Stay at that stand, single-click {NpcName} at ({NpcX},{Y},{NpcZ}), then cast slot 3. Expect one [ENRAGE] source=462 seek for char=6111 about 3 s after landing and no second seek after the 6 s state expires. Release with the helper stop action if it entered close combat."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        CheckGeometry(root);
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((StreetX, StreetZ))
            .Concat(new[] { 7300, 7301, 7400, 7401, 7701, 8001 }.Select(z => (StreetX, z))))
        {
            Check.That(NavMesh.Locate(1, x, StreetY, z, out var floor) && !floor.indoors &&
                Math.Abs(floor.floorY + 95 - StreetY) <= 100, $"Outdoor NAV ({x},{StreetY},{z})");
            Check.That(NavMesh.HasSightPath(1, StreetX, StreetY, StreetZ, x, StreetY, z) &&
                NavMesh.HasSightPath(1, StreetX, StreetY, CentreZ, x, StreetY, z), $"Open sight to ({x},{StreetY},{z})");
        }

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var owned = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(CasterLevel) && owned.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                owned.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Full ranked Destroyer profile");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var selected = await caster.WaitForViewNamedAsync("QAEnemy");
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        var grant = new[] { (Destroyer, Debuff, new (ushort Stat, int Value)[] { (995, 5) }) };
        var proof = new List<object>();
        int before = await StockAbilityProfile.ChangeLoadoutAsync(caster, owned, new[] { Destroyer }, load: false);
        await StockAbilityProfile.CheckPassivesAsync(caster, proof, grant, before, remove: true);
        before = await StockAbilityProfile.ChangeLoadoutAsync(caster, owned, new[] { Destroyer }, load: true);
        await StockAbilityProfile.CheckPassivesAsync(caster, proof, grant, before, remove: false);

        var witnesses = context.Helpers.Values.ToDictionary(player => player, player => player.World.RpcSnapshot().Count);
        int cast = caster.World.RpcSnapshot().Count;
        byte[] request = WireFormat.FromHex(StockDestroyer);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), DuelPackets.Handle(selected.ViewId, selected.SpawnId));
        caster.SendRpc(request);
        await EvidenceRetestWire.CastBarAsync(caster, Debuff, 4);
        await EvidenceRetestWire.TimerAsync(caster, cast, Debuff, AbilityCooldowns.OwnTimer, 90);
        await caster.WaitUntilAsync(() => server.DebugLog().Contains("[ABILITY-COST] char=110 ability=1088 paid=40 IS=700->660"),
            "The inherited Destroyer action debits 40 IS", TimeSpan.FromSeconds(6));

        foreach (var (player, cursor) in witnesses.Where(pair => pair.Key.Handle is "QAEnemy" or "QAOther"))
            foreach (ushort stat in Resistances)
                await StockAbilityProfile.ObserveAsync(player, cursor, rpc => StockAbilityProfile.Bonus(rpc, Debuff, stat, -120, 30, false),
                    $"Destroyer reaches {player.Handle} with stat {stat}");
        foreach (var (player, cursor) in witnesses.Where(pair => pair.Key.Handle is "QAFriend" or "QAFar"))
        {
            player.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(player, cursor, rpc => rpc.Opcode == 0x149, "Excluded helper continues processing");
            Check.That(!player.World.RpcSnapshot().Skip(cursor).Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) &&
                record!.SourceId == Debuff), $"Destroyer excludes {player.Handle}");
        }
        foreach (var (player, cursor) in witnesses.Where(pair => pair.Key.Handle is "QAEnemy" or "QAOther"))
            await player.WaitUntilAsync(() => Resistances.All(stat => player.World.RpcSnapshot().Skip(cursor).Any(rpc =>
                    StockAbilityProfile.Bonus(rpc, Debuff, stat, -120, 30, true))),
                $"All four Destroyer modifiers expire for {player.Handle}", TimeSpan.FromSeconds(38));

        Console.WriteLine("DESTROYER BOOTSTRAP PASS: retained 1088 on the primary and exact edge; 462 and 467 stock captures pending.");
        return Array.Empty<byte>();
    }
}
