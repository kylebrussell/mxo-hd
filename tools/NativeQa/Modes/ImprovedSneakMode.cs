using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ImprovedSneakMode : NativeQaMode
{
    internal const ushort BaseAbility = 151, Mastery = 186, Sneak = 1111, Countermeasures = 293;
    internal const int Level = 25, SneakRank = 25, MasteryRank = 1;
    private const int X = 17000, Y = 495, CasterZ = 6400, ObserverZ = 8000, RevealZ = 6650;
    private static readonly ushort[] Loadout = WithPrerequisites(2, 17, 155, Mastery);
    private static readonly ushort[] ObserverLoadout = WithPrerequisites(2, 17, Countermeasures);
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-improved-sneak";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET x={X},y={Y},z={CasterZ},rotation=0,alignment=1,pvpflag=0,level={Level}," +
                $"exp={PlayerProgression.ExperienceForLevel(Level)},innerStrC=400,innerStrM=400 WHERE charId=110",
            $"UPDATE char_abilities SET level={SneakRank} WHERE char_id=110",
            $"UPDATE char_abilities SET level={MasteryRank} WHERE char_id=110 AND ability_id={0x80000000u | ((uint)Mastery << 10)}",
            $"UPDATE characters SET x={X},y={Y},z={ObserverZ},rotation=128,alignment=2,pvpflag=0,level=50," +
                $"exp={PlayerProgression.ExperienceForLevel(50)},innerStrC=400,innerStrM=400 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", ObserverLoadout.Select((id, i) => $"(6114,{400 + i},{0x80000000u | ((uint)id << 10)},50,1)"))
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.SpyStyle, Sneak, Mastery }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "186", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory,
            ["NativeCondition"] = "The stock activation must emit a direct 186 request." },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory. Sneak151 rank {SneakRank}, Improved Sneak186 rank {MasteryRank}. " +
                $"CSR ({X},{Y},{CasterZ}) faces north. QAEnemy ({X},{Y},{ObserverZ}) faces south, 16 m away. Stay at the spawn stand. Page 2 Spy, Sneak, Improved Sneak.",
            "Press slot 1 for Spy Style. Open Actions > Abilities and look for Improved Sneak. If found, try its entry. If it is missing or sends nothing, try page 2 slot 3. " +
                "Save the complete first stock request and identify whether it asks for 186 or 1111. A 1111 request is the ordinary Sneak control. A missing or substituted request leaves direct 186 unverified.",
            "For a direct 186 request expect a 2 s cast, the current 20 IS fallback, no own reuse, [STEALTH] CSR_Tester 186 on (bits 2, SL 25), the Sneaking pose, and [MODIFIER] source=186 adds " +
                "209 +25, 509 +10, 115 -200 and state 881. Press slot 3 again after landing. Expect the 186 off record, all four removals and standing restored.",
            "Activate direct 186 again. Use ?qaunload 186 while it is live, then ?qaload 186. Expect one off at unload and no automatic activation on reload. " +
                "Slot 2 still opens ordinary Sneak1111 with its own 209 +25 record. Switch ordinary Sneak off before the reveal check.",
            """Activate direct 186 again on the same stand. Send helper {"actor":"QAEnemy","action":"improved-sneak-reveal"}. It uses the existing stock Countermeasures 293 request and moves QAEnemy to (17000,495,6650), facing CSR. Expect SA 93 / SL 25, [STEALTH] QAEnemy sees CSR_Tester, 186 off (revealed), and the normal standing pose. No PvP rounds."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { CasterZ, ObserverZ, RevealZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, X, Y, z), $"Verified outdoor stand and sight ({X},{Y},{z})");

        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(helper.Handle, helper);
        await PvpOpponentsFixture.EnterAsync(helper);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && owned[BaseAbility] == SneakRank && owned[Mastery] == MasteryRank &&
            !owned.ContainsKey(Sneak) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                owned.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "Legal ranked mastery and base Sneak profile");

        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await helper.WaitForViewNamedAsync(actor.Handle);
        actor.SendRpc(AwakenedStockStealthSpeedMode.SpyUse);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 58 04 00 00");

        // The exact stock packets below are ordinary Sneak controls. 186's first request comes from native QA.
        int before = actor.World.RpcSnapshot().Count;
        int observerViews = helper.World.CreationSnapshot().Count;
        actor.SendRpc(AwakenedStockStealthSpeedMode.SneakUse);
        await StockAbilityProfile.ObserveAsync(actor, before,
            frame => StockAbilityProfile.Bonus(frame, Sneak, 209, SneakRank, 0, false), "Ordinary stock Sneak adds rank 25");
        await RevealAsync(helper);
        await StockAbilityProfile.ObserveAsync(actor, before,
            frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Sneak, false)),
            "The helper reveals the ordinary Sneak control");
        await helper.WaitUntilAsync(() => helper.World.CreationSnapshot().Skip(observerViews).Any(view => view.Name == actor.Handle),
            "The observer regains the revealed ordinary Sneak view", HeadlessPlayer.DefaultTimeout);
        Check.That(context.ActualServer!.DebugLog().Contains($"QAEnemy sees {actor.Handle} (SA 93 SL {SneakRank}", StringComparison.Ordinal),
            "Observer reveal consumes the actual ranked Sneak stat");
        Check.Equal(0, actor.World.Problems.Count + helper.World.Problems.Count, "Stealth control packet framing");
        Console.WriteLine("IMPROVED SNEAK BOOTSTRAP PASS: ranked archive, outdoor sight and exact stock ordinary Sneak control. Native 186 pending.");
        return Array.Empty<byte>();
    }

    private static ushort[] WithPrerequisites(params ushort[] roots) => roots
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    internal static async Task RevealAsync(HeadlessPlayer observer)
    {
        if (observer.Handle != "QAEnemy")
            throw new InvalidOperationException("Use the seeded QAEnemy observer.");

        int before = observer.World.RpcSnapshot().Count;
        observer.SendRpc(StarterStealthObserverMode.StockCountermeasures);
        await StockAbilityProfile.ObserveAsync(observer, before,
            frame => StockAbilityProfile.Bonus(frame, Countermeasures, 204, 80, 30, false), "Countermeasures adds 80 awareness");
        foreach (int z in new[] { (ObserverZ + RevealZ) / 2, RevealZ })
        {
            var coordinates = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes);
            observer.World.SendViewList(Convert.FromHexString("02000108").Concat(coordinates).Concat(new byte[] { 0, 0 }).ToArray());
            observer.SkipReceived();
            observer.SendRpc("81 48");
            await observer.WaitForRpcAsync("81 49");
        }
    }
}
