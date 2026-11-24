using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class TransmitCodeThrownMode : NativeQaMode
{
    private const ushort Passive = 593, Knife = 146, Style = 1112;
    private const int Level = 50, X = 17000, Y = 495, Z = 2300, EnemyX = 17200;
    private static readonly ushort[] ActorProfile = DisablingShotMode.Closure(2, 17, Style, Knife).Order().ToArray();
    private static readonly ushort[] TargetProfile = TransmitCodeFixture.DefenderAbilities;
    private static readonly ushort[] ResistanceStats = { 1146, 1148 };

    public override string Name => "ability-parity-transmit-code-thrown-dot";
    public override NativeQaModePlan Plan { get; } = new(ActorProfile, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        "UPDATE characters SET rotation=64 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},rotation=192 WHERE charId=6114",
        "DELETE FROM inventory WHERE charId IN (110,6114) AND slot BETWEEN 105 AND 107",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", TargetProfile.Select((id, slot) => $"(6114,{400 + slot},{AbilityMessages.AbilityGameObject(id)},{Level},{(id == Passive ? 0 : 1)})"))
    }, DevCommands: true, FixedCallbackRolls: false, hotbarAbilities: new[] { Style, Knife },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_POISON_KNIFE_QA"] = "players"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "593", ["NativeCloseableRows"] = 1,
            ["AttackerMemory"] = ActorProfile.Sum(AbilityMemory.Cost), ["DefenderMemory"] = TargetProfile.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            "CSR starts on outdoor HOME facing QAEnemy at 2 m. No teleport or NPC selection needed. Both level-50 profiles have their full parents. Page 2 slots 1 Spy Style and 2 Poison Knife. /target QAEnemy. Remain outside interlock.",
            "Select Spy Style, then press Poison Knife once. Expect 15 IS, 2 s, one 146/1157 ThrownDOT lease for 25 s, zero direct damage and an unresisted tick of 2. The existing disposable draw aid pins only the named QA pair's 146 contest.",
            """After the first unresisted tick send {"actor":"QAEnemy","action":"transmit-code","loaded":true}. Expect [MODIFIER] char=6114 source=593 stat=1146 and 1148 each +10. On the same DOT, [RESISTANCE-BONUS OURS] type7 points22 and [TYPED-RESISTANCE OURS] 146 type7 total22 raw2 final1. The helper switches its owned Archive row through the ordinary loadout request.""",
            """After at least one reduced tick send {"actor":"QAEnemy","action":"transmit-code","loaded":false}. Watch both records remove and the next ticks return to 2, then the original 146/1157 removal at expiry. Do not cast it again. If the 25 s lease expires while entering the helper commands, start again after reuse with 593 unloaded.""",
            "Seeded health, legal profiles, disabled ambient aggro and the pair-scoped hit aid are fixture setup. Tick damage, normal Archive handlers and timings are unmodified. This extends the existing viral proof to the thrown stat; the original bonus coefficient remains unknown."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Z, 64, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(EnemyX, Y, Z, 192, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, EnemyX, Y, Z), "Clear outdoor Knife lane");
        var server = context.ActualServer!;
        var target = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(target.Handle, target);
        await PvpOpponentsFixture.EnterAsync(target);
        NativeQaReadiness.RequireNoAmbientAggro(server, target);
        VerifyProfile(target, TargetProfile.Where(id => id != Passive).ToArray(), Passive);
        Check.That(TargetProfile.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Owned Transmit Code fits when loaded");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        VerifyProfile(actor, ActorProfile);
        var view = await actor.WaitForViewNamedAsync(target.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await ObserveAsync(actor, 0, frame => Bonus(frame, Style, 1034, 29, 0, false), "Rank 50 Spy Style");
        int sourceAt = actor.World.RpcSnapshot().Count;
        int at = target.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterSpyCombatAdmissionMode.PoisonCast, handle));
        await EvidenceRetestWire.CastBarAsync(actor, Knife, 2);
        await EvidenceRetestWire.TimerAsync(actor, sourceAt, Knife, AbilityCooldowns.OwnTimer, 20);
        await ObserveAsync(target, at, frame => Bonus(frame, Knife, 1157, 21, 25, false), "One normal ThrownDOT lease");
        await TickAsync(at, 2, 0);
        int logAt = server.DebugLog().Length;
        at = target.World.RpcSnapshot().Count;
        await SetPassiveAsync(target, true);
        await TickAsync(at, 1, 1);
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("146 type7 total22 raw2 final1", StringComparison.Ordinal),
            "Live thrown resistance points feed the next tick", HeadlessPlayer.DefaultTimeout);
        at = target.World.RpcSnapshot().Count;
        await SetPassiveAsync(target, false);
        await TickAsync(at, 2, 0);
        await target.WaitUntilAsync(() => target.World.RpcSnapshot().Skip(at).Any(frame => Bonus(frame, Knife, 1157, 21, 25, true)),
            "Original ThrownDOT lease ends after the loadout comparison", TimeSpan.FromSeconds(30));
        Console.WriteLine("TRANSMIT CODE THROWN PASS: same Poison Knife DOT ticks 2/0, 1/1, 2/0 across owned loadout changes.");
        return Array.Empty<byte>();

        Task<RpcRecord> TickAsync(int cursor, int damage, int resisted) => ObserveAsync(target, cursor, frame =>
            TransmitCodeFixture.Damage(frame.Bytes, Knife, actor.Handle, target.Handle, out int done, out int held) &&
            done == damage && held == resisted, $"Stock recipient ThrownDOT tick {damage}/{resisted}");
    }

    internal static async Task SetPassiveAsync(HeadlessPlayer helper, bool load)
    {
        if (helper.Handle != "QAEnemy" || helper.CharId != 6114)
            throw new InvalidOperationException("Use QAEnemy for the Transmit Code loadout control.");
        var owned = AbilityMemoryUiFixture.Decode(helper.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        int cursor = await ChangeLoadoutAsync(helper, owned, new[] { Passive }, load);
        foreach (ushort stat in ResistanceStats)
            await ObserveAsync(helper, cursor, frame => Bonus(frame, Passive, stat, 10, 0, !load),
                $"QAEnemy Transmit Code {stat}, loaded={load}");
    }

    private static void VerifyProfile(HeadlessPlayer actor, ushort[] loaded, params ushort[] archived)
    {
        int memory = loaded.Concat(archived).Sum(AbilityMemory.Cost);
        Check.That(memory <= AbilityMemory.ForLevel(Level), $"Legal Archive for {actor.Handle}");
        var active = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(active.Keys.ToHashSet().SetEquals(loaded) && archived.All(id => owned.ContainsKey(id) && !active.ContainsKey(id)) &&
            loaded.Concat(archived).All(id => AbilityPrerequisites.Current.Closure(id)
                .All(parent => active.TryGetValue(parent.Key, out var row) && row.Level >= parent.Value)), $"Exact ranked profile for {actor.Handle}");
    }
}
