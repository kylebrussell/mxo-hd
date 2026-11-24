using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class DeadlyAimEnrageMode : EnrageDurationArchiveMode
{
    public DeadlyAimEnrageMode() : base(339, new (ushort, int)[] { (1041, 25), (1068, 5) }, withdraw: false) { }

    public override string Name => "ability-parity-deadly-aim-enrage-duration";
}

public abstract class EnrageDurationArchiveMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Home = 6400, TargetZ = 6600, Level = 50, EnemyLevel = 16;
    private const ushort Smash = 145, Karate = 132, State = TempMods.EnrageAbility;
    private const float StateSeconds = 10, Reuse = 12;
    private static readonly StarterSoldierResistanceMode.Sample Sample =
        StarterSoldierResistanceMode.Samples.Single(sample => sample.Id == Smash);
    private readonly ushort parent;
    private readonly (ushort Stat, int Value)[] stats;
    private readonly bool withdraw;
    private readonly ushort[] loadout;

    protected EnrageDurationArchiveMode(ushort parent, (ushort Stat, int Value)[] stats, bool withdraw)
    {
        this.parent = parent;
        this.stats = stats;
        this.withdraw = withdraw;
        loadout = DisablingShotMode.Closure(2, 17, Karate, Smash, CombatStyles.SpyStyle, parent);
        var messages = new List<string>
        {
            $"Level 50, {loadout.Sum(AbilityMemory.Cost)}/100 memory. CSR faces QAEnemy 2 m ahead on verified outdoor NAV. Stay on READY HOME unless displaced. If needed, type the printed warp in chunks of 16 characters into the same chat line, then press Enter once.",
            "Page 2 Karate, Overhand Smash and Withdraw. /target QAEnemy, right-click Close Combat, select Karate again, and press slot 2. The disposable aid forces only the state branch of a landed 145. Retry a miss after 12 s. The level-16 helper is an admission and hit-rate control.",
            $"For each of the loaded, unloaded (?qaunload {parent}) and reloaded (?qaload {parent}) passes, expect source={parent} stat=1068 +5 at entry, removal and restoration. A landed 145 must produce [MODIFIER] char=6114 source=145 stat=93 for 10 s and one matching removal. Stop the current fight with ?cd stop after the stage. Wait out reuse before another attempt.",
            "The shipped state 93 has no duration-stat link. A stable 10 s Enrage with 1068 present and absent is the expected result. No rifle or Free Fire is involved."
        };
        if (withdraw)
            messages.Add("After each Enrage expires, press stock Withdraw before stopping the fight. Check [WITHDRAW INPUT OURS] with the direct input and bonus1045=10/0/10. With 384 active the initial records also show 1045 +10 and 995 +15. Existing native 388 establishes the 8 to 9.2 s linked Stun.");
        else
            messages.Add("Existing native PR478 establishes the 1041 +25 control: 282 landed 12.5 s Blind from the 10 s input. Here the 1041 and 1068 add/remove/add must travel together.");
        Plan = new(loadout, X, Home, Array.Empty<string>(),
            new[] { $"DELETE FROM inventory WHERE charId IN (110,6114) AND slot={CombatFixtureWeapons.HandSlot}" },
            new[]
            {
                PvpOpponentsFixture.NativeSql,
                $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Home},rotation=0,healthC=30000,healthM=30000,innerStrC=1000,innerStrM=1200 WHERE charId=110",
                $"UPDATE characters SET level={EnemyLevel},exp={PlayerProgression.ExperienceForLevel(EnemyLevel)},x={X},y={Y},z={TargetZ},rotation=128,healthC=30000,healthM=30000 WHERE charId=6114",
                "DELETE FROM char_abilities WHERE char_id=6114",
                $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES (6114,400,{AbilityMessages.AbilityGameObject(2)},16,1),(6114,401,{AbilityMessages.AbilityGameObject(17)},16,1)",
                $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
            },
            DevCommands: true, hotbarAbilities: new[] { Karate, Smash }, hotbarWithdraw: true,
            testMobFixture: "martial-player-controls.csv",
            environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
                ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
                [StarterInterlockControlQa.Mode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
            },
            readyMessages: messages.ToArray(),
            extraQaMetadata: new()
            {
                ["ScopedStockRows"] = parent.ToString(), ["NativeCloseableRows"] = 1, ["MemoryUsed"] = loadout.Sum(AbilityMemory.Cost)
            });
    }

    public override NativeQaModePlan Plan { get; }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("The fixture needs repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Console.WriteLine($"READY HOME {InvisibilityBeltWatcherMode.Warp(X, Y, Home, 0, inside: false)}");
        Console.WriteLine($"READY QAEnemy {InvisibilityBeltWatcherMode.Warp(X, Y, TargetZ, 128, inside: false)}");
        Check.That(NavMesh.HasSightPath(1, X, Y, Home, X, Y, TargetZ), "Open outdoor sight for the ordinary enemy");
        Check.Equal((ushort)0, AbilityControlDurations.BonusStat(State), "The shipped Enraged template has no duration link");
        var server = context.ActualServer ?? throw new InvalidOperationException("Disposable server required.");
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, loadout, Level);
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint selection = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(selection));
        await VerifyRecordsAsync(0, remove: false);
        await MeasureAsync(loaded: true);
        int off = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { parent }, load: false);
        await VerifyRecordsAsync(off, remove: true);
        await MeasureAsync(loaded: false);
        int on = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { parent }, load: true);
        await VerifyRecordsAsync(on, remove: false);
        await MeasureAsync(loaded: true);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Archive control and interlock traffic frame cleanly");
        return Array.Empty<byte>();

        async Task VerifyRecordsAsync(int at, bool remove)
        {
            foreach (var (stat, value) in stats)
                await StockAbilityProfile.ObserveAsync(actor, at,
                    frame => StockAbilityProfile.Bonus(frame, parent, stat, value, 0, remove),
                    $"Archive source={parent} stat={stat} remove={remove}");
        }

        async Task MeasureAsync(bool loaded)
        {
            int opened = actor.World.RpcSnapshot().Count;
            actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, selection),
                WireFormat.FromHex($"40 {handle} 01 6b"));
            await StockAbilityProfile.ObserveAsync(actor, opened,
                rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 1, "Stock interlock opened");
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(Sample.StockStyle, selection));
            await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 84 00 00 00");

            int recipient = enemy.World.RpcSnapshot().Count;
            bool IsState(RpcRecord frame, bool remove) => StockAbilityProfile.Bonus(frame, Smash, State, 0, StateSeconds, remove);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int start = actor.World.RpcSnapshot().Count;
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(Sample.StockUse, selection));
                await EvidenceRetestWire.TimerAsync(actor, start, Smash, AbilityCooldowns.OwnTimer, Reuse, ended: true);
                if (enemy.World.RpcSnapshot().Skip(recipient).Any(frame => IsState(frame, false))) break;
            }
            var added = await StockAbilityProfile.ObserveAsync(enemy, recipient, frame => IsState(frame, false), "Landed Enrage still has the shipped 10 s duration");
            var removed = await StockAbilityProfile.ObserveAsync(enemy, recipient, frame => IsState(frame, true), "Matching Enrage term");
            var ledger = enemy.World.RpcSnapshot().ToArray();
            Check.That(Array.IndexOf(ledger, added) < Array.IndexOf(ledger, removed), "State removal follows its addition");
            if (withdraw)
            {
                string bonus = loaded ? "10" : "0";
                int at = server.DebugLog().Length;
                actor.SendRpc("44 01");
                await actor.WaitUntilAsync(() => server.DebugLog()[at..].Split('\n').Any(line =>
                    line.Contains("[WITHDRAW INPUT OURS] CSR_Tester", StringComparison.Ordinal) &&
                    line.Contains($"bonus1045={bonus}", StringComparison.Ordinal)),
                    $"Ordinary Withdraw uses only the loaded 1045 bonus ({bonus})", HeadlessPlayer.DefaultTimeout);
            }
            int stopped = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            actor.SendChat("?cd stop");
            await actor.WaitForTextAsync("Fixture combat ended.");
            if (!withdraw)
                await StockAbilityProfile.ObserveAsync(actor, stopped,
                    rpc => rpc.Bytes is [0x46, ..] && rpc.Bytes.Length >= 6 && rpc.Bytes[5] == 0, "Interlock stopped before Archive");
        }
    }
}
