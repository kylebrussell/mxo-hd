using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterStealthObserverMode : NativeQaMode
{
    private const ushort Detection = 298, Awareness = 156, Countermeasures = 293;
    private const int StartX = 17000, NearX = 18000, ObserverX = 19000, Y = 495, Z = 2300;
    private const int SneakRank = 15;
    // PR530 ordinary Sneak and PR465 ordinary Stealth Countermeasures.
    private const string StockSneak = "80b95704000000000000000000000000000000000000000000000000585cc20001000000086b0000000000";
    internal const string StockCountermeasures = "80b92501000000000000000000000000000000000000000000000000585cac0001000000086b0000000000";
    private static readonly ushort[] ObserverRoots = { 2, 17, Awareness, Countermeasures, Detection };
    private static readonly ushort[] ObserverLoadout = Closure(ObserverRoots);
    private static readonly ushort[] SneakLoadout = Closure(new ushort[] { 2, 17, 151 });
    private static string SneakRows => string.Join(",", SneakLoadout.Select((ability, index) =>
        $"(6114,{400 + index},{0x80000000u | ((uint)ability << 10)},{SneakRank},1)"));

    public override string Name => "ability-parity-starter-stealth-observer";
    public override NativeQaModePlan Plan { get; } = new(ObserverLoadout, ObserverX, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET x={ObserverX},y={Y},z={Z},rotation=192,alignment=1,pvpflag=0,level=50,exp={PlayerProgression.ExperienceForLevel(50)},innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={StartX},y={Y},z={Z},alignment=2,pvpflag=0,level=50,exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId=6114",
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + SneakRows
        }, DevCommands: true, hotbarAbilities: new[] { Awareness, Countermeasures },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "2,156,293,298", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            """Stay at (19000,495,2300) facing west toward QAEnemy before it hides. Each pass uses {"actor":"QAEnemy","action":"stealth-observer-pass"}; the helper returns to 20 m west, uses ordinary Sneak and approaches to 10 m. Wait for one contest before repeating. A miss and a reveal both qualify for the baseline checks.""",
            "First use ?qaunload 298 to remove Detection. Record the baseline SA 13 / SL 15. Load 298 with ?qaload 298 and repeat for SA 28 / SL 15. Unload it again using ?qaunload 298.",
            "Use page 2 slot 2 for Stealth Countermeasures and promptly run the same pass. At SA 93 / SL 15, QAEnemy should reveal and become visible. After the modifier expires at 30 s, repeat to get SA 13 again.",
            "Then use page 2 slot 1 for Stealth Awareness and capture its first stock use. Repeat at SA 28 / SL 15 with 298 still unloaded. Record the 156 source, +15 modifier, 300 s duration and 15 s own reuse. The exact 156 client request is a native capture step. Enter submits chat."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(helper.Handle, helper);
        await PvpOpponentsFixture.EnterAsync(helper);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        if (context.StockProbe is not { } observer) return Array.Empty<byte>();
        Check.That(ObserverLoadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Legal observer memory");
        var owned = AbilityMemoryUiFixture.Decode(observer.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await PvpOpponentsFixture.EnterAsync(observer);
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);

        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(observer, owned, new[] { Detection }, load: false);
        await CheckModifierAsync(unloaded, Detection, 15, 0, remove: true);
        await ContestAsync(13);
        int loaded = await StockAbilityProfile.ChangeLoadoutAsync(observer, owned, new[] { Detection }, load: true);
        await CheckModifierAsync(loaded, Detection, 15, 0, remove: false);
        await ContestAsync(28);
        unloaded = await StockAbilityProfile.ChangeLoadoutAsync(observer, owned, new[] { Detection }, load: false);
        await CheckModifierAsync(unloaded, Detection, 15, 0, remove: true);

        int beforeBuff = observer.World.RpcSnapshot().Count;
        observer.SendRpc(StockCountermeasures);
        await CheckModifierAsync(beforeBuff, Countermeasures, 80, 30, remove: false);
        await EvidenceRetestWire.TimerAsync(observer, beforeBuff, Countermeasures, AbilityCooldowns.OwnTimer, 15);
        await ContestAsync(93, mustReveal: true);
        await observer.WaitUntilAsync(() => observer.World.RpcSnapshot().Skip(beforeBuff)
            .Any(frame => StockAbilityProfile.Bonus(frame, Countermeasures, 204, 80, 30, true)),
            "Countermeasures expires and withdraws its modifier", TimeSpan.FromSeconds(32));
        await ContestAsync(13);
        Check.Equal(0, observer.World.Problems.Count + helper.World.Problems.Count, "Stealth observer packet framing");
        return Array.Empty<byte>();

        Task<RpcRecord> CheckModifierAsync(int after, ushort ability, int value, int duration, bool remove) =>
            StockAbilityProfile.ObserveAsync(observer, after,
                frame => StockAbilityProfile.Bonus(frame, ability, 204, value, duration, remove),
                $"Awareness modifier {ability}, {value}, remove={remove}");

        async Task ContestAsync(int awareness, bool mustReveal = false)
        {
            int before = server.DebugLog().Length;
            int views = observer.World.CreationSnapshot().Count;
            await RunPassAsync(helper);
            string reveal = $"CSR_Tester sees QAEnemy (SA {awareness} SL {SneakRank}";
            string miss = $"missed (SA {awareness} SL {SneakRank})";
            await observer.WaitUntilAsync(() =>
            {
                string sample = server.DebugLog()[before..];
                return sample.Contains(reveal, StringComparison.Ordinal) ||
                    !mustReveal && sample.Contains(miss, StringComparison.Ordinal);
            }, $"Observer contests at SA {awareness}, SL {SneakRank}", TimeSpan.FromSeconds(12));
            if (mustReveal)
                await observer.WaitUntilAsync(() => observer.World.CreationSnapshot().Skip(views).Any(view => view.Name == helper.Handle),
                    "The same observer regains the revealed helper view", HeadlessPlayer.DefaultTimeout);
        }
    }

    internal static async Task RunPassAsync(HeadlessPlayer helper)
    {
        if (helper.Handle != "QAEnemy")
            throw new InvalidOperationException("Use QAEnemy for the Sneak pass.");
        helper.SkipReceived();
        helper.SendChat("?stealth off");
        helper.SendChat($"?tp {StartX} {Y} {Z} 0 0 0");
        await helper.WaitForTextAsync("Teleported to 0/0");
        helper.SendRpc(StockSneak);
        byte[] on = AbilityBuffs.BuildExclusive(1111, true);
        await helper.WaitForRpcAsync(frame => frame.Bytes.AsSpan().SequenceEqual(on), "The helper activates ordinary Sneak");
        foreach (int x in new[] { (StartX + NearX) / 2, NearX })
        {
            byte[] coordinates = new[] { (float)x, (float)Y, (float)Z }.SelectMany(BitConverter.GetBytes).ToArray();
            helper.World.SendViewList(Convert.FromHexString("02000108").Concat(coordinates).Concat(new byte[] { 0, 0 }).ToArray());
            helper.SkipReceived();
            helper.SendRpc("81 48");
            await helper.WaitForRpcAsync("81 49");
        }
    }

    private static ushort[] Closure(ushort[] roots) => roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();
}
