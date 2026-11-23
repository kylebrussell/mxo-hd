using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerControlTargetMode;
using static NativeQa.Modes.StarterSoldierResistanceMode;

namespace NativeQa.Modes;

public sealed class StarterTimedGunTargetMode : NativeQaMode
{
    private const int Level = 26, Rank = 19, CasterX = 17000, Y = 495, Z = 2300, DummyX = 17200;
    private static readonly Sample[] Samples = StarterGunmanResistanceMode.Samples
        .Where(sample => sample.Id is 126 or 176).ToArray();
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 129, 126, 158, 176).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-timed-gun-target";

    public override NativeQaModePlan Plan { get; } = new(Loadout, CasterX, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
                "healthC=4000,healthM=4000,innerStrC=800,innerStrM=800 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new ushort[] { 129, 126, 158, 176 },
        testMobFixture: "starter-gun-selected-targets.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.WeaponMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "126,176", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory and rank-{Rank} starter abilities. Handgun starts equipped. On clear outdoor NAV, CSR ({CasterX},{Y},{Z}) faces the 30,000 HP Melee Dummy ({DummyX},{Y},{Z}). Return: ?tp 17000 495 2300 393 11 3 - 40.",
            "Page 2 slots 1/2 are Handguns and Disarming Shot. Slots 3/4 are Submachine Guns and Covering Fire. Select the named dummy and your style. Try 126 at rest, then 176 at rest after ?cd w smg. Retain the first full stock request from each route. Expect 4 s and 20/25 IS if sent, 8 s own and 5 s group (690/689). An ordinary direct hit has no queued round. Record any stock local gate without substituting the interlock capture.",
            "For 126, use ?cd w handgun if needed. Open the dummy radial Close Combat, strafe/reselect if needed, choose Handguns and press slot 2. While the 4 s bar is active send ?cd w smg. Enter submits. The old 126 must neither queue nor become a direct hit. Expect the existing cast cost and reuse, with the selected gun or original interlock fizzle in DebugLog.",
            "After 8 s reuse, equip the handgun again and repeat without swapping. Expect 9522 on a landed special, 11 Ballistic and [MODIFIER] source=126 stat=524 value=-15 for 10 s. Use ?cd stop and wait for the HUD to clear.",
            "With ?cd w smg, reselect the dummy and open radial Close Combat. Choose Submachine Guns, press slot 4, and send ?cd w handgun during the bar. Expect no queued or direct 176. After 8 s, repeat with SMG and no swap. Expect 21083, 18 Ballistic and, only on a normal 15% proc, [MODIFIER] source=176 stat=99 value=0 for 8 s then removal."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { CasterX, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor NAV at ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, CasterX, Y, Z, DummyX, Y, Z), "Clear sight to the dummy");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        Check.That(target.Goid == 599 && BitConverter.ToUInt16(target.Attributes["MaxHealth"]) == 30000 &&
            BitConverter.ToUInt16(target.Attributes["Health"]) > 0, "A living 30,000 HP Melee Dummy is in view");

        foreach (var sample in Samples)
        {
            if (sample.Id == 176) await Equip(sample);
            string queue = $"[COMBAT] {AbilityCatalog.Find(sample.Id).Name} is the next move";
            int logStart = server.DebugLog().Length;
            int rpcStart = await StartCast(sample);
            await Equip(Samples.Single(other => other.Id != sample.Id));
            await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
                $"[ABILITY] {sample.Id} fizzles: the loadout, weapon or interlock changed", StringComparison.Ordinal),
                $"Stock {sample.Id} fizzles after it loses its original interlock", HeadlessPlayer.DefaultTimeout);
            string cancelled = server.DebugLog()[logStart..];
            Check.That(!cancelled.Contains(queue, StringComparison.Ordinal) &&
                !cancelled.Contains($"[ABILITY] {sample.Id} lands", StringComparison.Ordinal), "The cast never queues or lands directly");
            await EvidenceRetestWire.TimerAsync(actor, rpcStart, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, ended: true);

            await Equip(sample);
            for (int attempt = 0; ; ++attempt)
            {
                logStart = server.DebugLog().Length;
                rpcStart = await StartCast(sample);
                string landed = $"[ABILITY] {sample.Id} lands on view {target.ViewId}";
                await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(queue, StringComparison.Ordinal),
                    $"Stock {sample.Id} queues for its original target", HeadlessPlayer.DefaultTimeout);
                await EvidenceRetestWire.TimerAsync(actor, rpcStart, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, ended: true);
                bool hit = server.DebugLog()[logStart..].Contains(landed, StringComparison.Ordinal);
                if (hit)
                    Check.That(server.DebugLog()[logStart..].Contains($"animation {sample.Move} (", StringComparison.Ordinal),
                        $"Stock {sample.Id} retains move {sample.Move}");
                await Stop();
                if (hit) break;
                Check.That(attempt < 3, $"Ability {sample.Id} misses four ordinary fights");
            }
        }
        Check.Equal(0, actor.World.Problems.Count, "Timed starter shot packet framing");
        Console.WriteLine("TIMED GUN TARGET BOOTSTRAP PASS: exact 126 and 176 casts cancel on a live weapon swap, then still queue and land in the unchanged fight.");
        return Array.Empty<byte>();

        Task Equip(Sample sample) =>
            CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, sample.Weapon, sample.Goid);

        async Task<int> StartCast(Sample sample)
        {
            uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
            string wire = WireFormat.ToHex(BitConverter.GetBytes(handle));
            actor.SkipReceived();
            actor.World.SendRpcs(WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle), WireFormat.FromHex($"40 {wire} 01 6b"));
            await actor.WaitForRpcAsync($"40 {wire} 00 6b");
            await actor.WaitForRpcAsync("47");
            int before = actor.World.ViewSnapshot().Count;
            actor.SendRpc(WithHandle(sample.StockStyle, handle));
            byte[] selected = PlayerViewAttributes.BuildSelf((39, PlayerViewAttributes.U32(sample.Style)));
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(before).Any(view => BytePattern.IndexOf(view, selected, 0) >= 0),
                $"Select stock style {sample.Style}", HeadlessPlayer.DefaultTimeout);
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(WithHandle(sample.StockUse, handle));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, 4);
            return cursor;
        }

        async Task Stop()
        {
            actor.SendChat("?cd stop");
            await actor.WaitForTextAsync("Fixture combat ended.");
        }
    }
}
