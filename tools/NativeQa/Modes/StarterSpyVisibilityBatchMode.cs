using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterSpyVisibilityBatchMode : NativeQaMode
{
    private const ushort Awakened = 2, Detection = 298;
    private const ushort SneakParent = 151, SpyParent = 155, Sneak = 1111, Upgrade = 1113;
    private const byte Level = 19, Rank = 15;
    private const int StartX = 17000, CheckX = 17500, EnemyX = 19000, GroundY = 495, Z = 2300;
    private const string StockSneak = "80b95704000000000000000000000000000000000000000000000000585cc20001000000086b0000000000";
    private const string StockUpgrade = "80b95904000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-starter-spy-visibility";
    public override NativeQaModePlan Plan { get; } = new(
        StockAbilityProfile.Profile(2, 17, SneakParent, SpyParent).Order().ToArray(), StartX, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET x={StartX},y={GroundY},z={Z},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},alignment=1,pvpflag=0 WHERE charId=110",
            $"UPDATE characters SET x={EnemyX},y={GroundY},z={Z},rotation=192,level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},alignment=2,pvpflag=0 WHERE charId=6114",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            $"INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                $"(6114,400,2147485696,{Level},1),(6114,401,2147501056,{Level},1),(6114,402,2147788800,{Level},1)"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Sneak, Upgrade },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "2,298,1111,1113", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Level 19, rank 15, outdoors and at rest. QAEnemy carries rank-19 Awakened and faces west. Start at (17000,495,2300). Clear selection. Press page 2 slot 1 Sneak and walk five metres east along z=2300 to about x=17500.",
            "Wait for one periodic contest on QAEnemy. Capture SA 5 and SL 15 in DebugLog. A reveal or a miss is valid. The observer loses CSR's view on Sneak and regains it when Sneak ends. Toggle Sneak off if needed and return to the start.",
            "Send QAEnemy helper chat ?qaload 298. Repeat the same Sneak walk. The observer now contests at SA 10 versus SL 15. Toggle Sneak off if needed and return to the start.",
            "Send QAEnemy helper chat ?qaunload 298 and then ?qaunload 2. Repeat to get SA 1 versus SL 15. End Sneak and return to the start.",
            "Send QAEnemy helper chat ?qaload 2, select no target, use slot 2 Spy Upgrade and slot 1 Sneak. Walk to the same check point. Capture SA 5 versus SL 23. Each periodic contest uses normal random rolls. Enter submits any commands."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        var owned = AbilityMemoryUiFixture.Decode(observer.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await PvpOpponentsFixture.EnterAsync(observer);
        await ChangeObserverAsync(Detection, 5, load: false);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } spy) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(spy);
        NativeQaReadiness.RequireNoAmbientAggro(server, spy);
        await spy.WaitForViewNamedAsync(observer.Handle);
        await CheckSneakAsync(5, 15);
        await MoveAsync(StartX);
        await ChangeObserverAsync(Detection, 5, load: true);
        await CheckSneakAsync(10, 15);
        await MoveAsync(StartX);
        await ChangeObserverAsync(Detection, 5, load: false);
        await ChangeObserverAsync(Awakened, 4, load: false);
        await CheckSneakAsync(1, 15);
        await MoveAsync(StartX);
        await ChangeObserverAsync(Awakened, 4, load: true);

        int cursor = spy.World.RpcSnapshot().Count;
        spy.SendRpc(StockUpgrade);
        await StockAbilityProfile.ObserveAsync(spy, cursor, frame => StockAbilityProfile.Bonus(frame, Upgrade, 209, 8, 0, false),
            "Spy Upgrade adds eight Stealth points");
        await CheckSneakAsync(5, 23);
        Check.Equal(0, spy.World.Problems.Count + observer.World.Problems.Count, "Spy visibility packet framing");
        return Array.Empty<byte>();

        async Task ChangeObserverAsync(ushort ability, int value, bool load)
        {
            int cursor = await StockAbilityProfile.ChangeLoadoutAsync(observer, owned, new[] { ability }, load);
            await StockAbilityProfile.ObserveAsync(observer, cursor,
                frame => StockAbilityProfile.Bonus(frame, ability, Stealth.StealthDetectionAbility, value, 0, remove: !load),
                $"Observer Awareness responds to {ability}, loaded={load}");
        }

        async Task CheckSneakAsync(int awareness, int stealth)
        {
            var visible = await observer.WaitForViewNamedAsync(spy.Handle);
            int logAt = server.DebugLog().Length;
            int views = observer.World.ViewSnapshot().Count;
            int creations = observer.World.CreationSnapshot().Count;
            spy.SendRpc(StockSneak);
            byte[] delete = { 1, 1, 0, (byte)visible.ViewId, (byte)(visible.ViewId >> 8) };
            await observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(views)
                .Any(list => BytePattern.IndexOf(list, delete, 0) >= 0), "Sneak hides CSR from the hostile observer", HeadlessPlayer.DefaultTimeout);
            await MoveAsync(CheckX);
            string detected = $"QAEnemy sees CSR_Tester (SA {awareness} SL {stealth} d ";
            string missed = $"missed (SA {awareness} SL {stealth})";
            await spy.WaitUntilAsync(() =>
            {
                string log = server.DebugLog()[logAt..];
                return log.Contains(detected, StringComparison.Ordinal) || log.Contains(missed, StringComparison.Ordinal);
            }, $"Periodic detection uses SA {awareness} and SL {stealth}", TimeSpan.FromSeconds(12));
            if (!server.DebugLog()[logAt..].Contains(detected, StringComparison.Ordinal))
                spy.SendRpc(StockSneak);
            await observer.WaitUntilAsync(() => observer.World.CreationSnapshot().Skip(creations).Any(view => view.Name == spy.Handle),
                "Ending Sneak restores the hostile player view", HeadlessPlayer.DefaultTimeout);
        }

        async Task MoveAsync(int x)
        {
            byte[] coordinates = new[] { (float)x, (float)GroundY, (float)Z }.SelectMany(BitConverter.GetBytes).ToArray();
            spy.World.SendViewList(Convert.FromHexString("02000108").Concat(coordinates).Concat(new byte[] { 0, 0 }).ToArray());
            int barrier = spy.World.RpcSnapshot().Count;
            spy.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(spy, barrier, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes),
                "Movement request processed before the next cast");
        }
    }
}
