using System.Diagnostics;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterCoderSplashBatchMode;

namespace NativeQa.Modes;

public sealed class StarterCoderCastMembershipMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, CasterZ = 8100, NearZ = 6600;
    private const int SecondaryX = X + 100, SecondaryZ = 6500, StartingIs = 500;
    private const ushort Style = CombatStyles.CoderStyle, Lockdown = 21, Tinkering = 36;
    private static readonly ushort[] Hotbar = Samples.Select(sample => sample.Id).Prepend(Lockdown).Prepend(Style).ToArray();
    // Load all ranked prerequisites. The seeder owns the additional discipline ancestors in the Archive.
    private static readonly ushort[] Loadout = Hotbar.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);

    public override string Name => "ability-parity-starter-coder-cast-membership";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, CasterZ, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6112,6114)",
            $"UPDATE characters SET x={X},y={Y},z={CasterZ},rotation={MobAggro.YawTowards(X,CasterZ,X,NearZ)}," +
                $"innerStrC={StartingIs},innerStrM=800 WHERE charId=110",
            $"UPDATE characters SET x={X},y={Y},z={NearZ},alignment=2 WHERE charId=6114",
            $"UPDATE characters SET x={SecondaryX},y={Y},z={SecondaryZ},alignment=2 WHERE charId=6112",
            "DELETE FROM char_abilities WHERE char_id IN (6112,6114)"
        }, DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "21,33,36,514", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy ({X},{Y},{NearZ}) 15 m away. QAFar ({SecondaryX},{Y},{SecondaryZ}) is 1.42 m from QAEnemy. Outdoor NAV and sight verified away from WinterFrost. Page 2 is Coder Style/21/33/36/514. Both helpers remain on their stands.",
            "Choose Coder Style and type /target QAEnemy. Type ?qaunload with 514, 36, 33 and 21 individually. Try the corresponding shortcuts. A local unavailable control is fine. Any request that goes out receives one reason 7 and leaves 500 IS, with no cast, reuse, damage or modifier. Reload with ?qaload in order 21, 33, 36, 514. If a shortcut disappears, use Actions.",
            "Start Tinkering (36) and type ?qaunload 36 during its 6 s cast. Require [ABILITY] 36 fizzles: the Coder ability was unloaded during the cast, 20 IS (500 to 480) and 30 s own reuse, with no [PVP] damage to either helper. Reload it after the bar. Keep the same selected target and stay out of interlock.",
            "As reuse clears, land 21, 33, 514, then a fresh 36. Lockdown costs 10 IS, 2 s cast, 13.5 s own reuse and [MODIFIER] char=6114 source=21 stat=99 value=0 for 12 s. Static Blast costs 15 IS, 2 s cast, 6 s own and 4 s shared reuse, damage 24 on QAEnemy only; the root survives. Both Tinkering spells cast 6 s with 30 s own reuse, 514 costs 40 and hits both for 6 each, 36 costs 20 and hits both for 4 each. Final IS 395. Projected DONE delta 0."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Coder membership needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, CasterZ), (X, NearZ), (SecondaryX, SecondaryZ) })
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1,
                $"Verified outdoor stand ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), "The caster has clear sight");
        }

        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var profile = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => profile.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                profile.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "The full starter Coder profile fits");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var primary = context.Helpers["QAEnemy"];
        var secondary = context.Helpers["QAFar"];
        var target = await caster.WaitForViewNamedAsync(primary.Handle);
        await caster.WaitForViewNamedAsync(secondary.Handle);
        caster.SendRpc(StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc("81 51 " + WireFormat.ToHex(BitConverter.GetBytes(handle)));

        ushort[] batch = { Lockdown, 33, Tinkering, 514 };
        byte[] lockdown = StarterHackerControlTargetMode.WithHandle(StockLockdown, handle);
        var requests = Samples.ToDictionary(sample => sample.Id, sample =>
            StarterHackerControlTargetMode.WithHandle(sample.Request, handle));
        requests.Add(Lockdown, lockdown);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, batch, load: false);
        foreach (ushort id in batch)
            await StarterHackerControlTargetMode.RefusedAsync(caster, id, requests[id], AbilityRefusals.NotLoaded);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, batch, load: true);
        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.15));

        int fizzleAt = caster.World.RpcSnapshot().Count, selfView = caster.World.ViewSnapshot().Count;
        int primaryAt = primary.World.RpcSnapshot().Count, secondaryAt = secondary.World.RpcSnapshot().Count;
        int debugAt = server.DebugLog().Length;
        caster.SkipReceived();
        var bar = Stopwatch.StartNew();
        caster.SendRpc(requests[Tinkering]);
        await EvidenceRetestWire.CastBarAsync(caster, Tinkering, 6);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, new[] { Tinkering }, load: false);
        Check.That(bar.Elapsed.TotalSeconds < 6, "The standard Archive accepted the unload during the cast bar");
        await EvidenceRetestWire.TimerAsync(caster, fizzleAt, Tinkering, AbilityCooldowns.OwnTimer, 30);
        await caster.WaitUntilAsync(() => server.DebugLog()[debugAt..].Contains("[ABILITY] 36 fizzles: the Coder ability was unloaded during the cast"),
            "Tinkering fizzles after its committed ordinary unload", HeadlessPlayer.DefaultTimeout);
        await ExpectIsAsync(selfView, 480);
        byte[] primarySplash = AbilityMessages.Damage(false, Tinkering, caster.Handle, primary.Handle, 4, 0);
        byte[] secondarySplash = AbilityMessages.Damage(false, Tinkering, caster.Handle, secondary.Handle, 4, 0);
        Check.That(!primary.World.RpcSnapshot().Skip(primaryAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(primarySplash)) &&
            !secondary.World.RpcSnapshot().Skip(secondaryAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(secondarySplash)),
            "The withdrawn cast did not damage the primary or splash recipient");
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, new[] { Tinkering }, load: true);

        int rootAt = primary.World.RpcSnapshot().Count, rootCast = caster.World.RpcSnapshot().Count;
        caster.SkipReceived();
        caster.SendRpc(lockdown);
        await EvidenceRetestWire.CastBarAsync(caster, Lockdown, 2);
        await EvidenceRetestWire.TimerAsync(caster, rootCast, Lockdown, AbilityCooldowns.OwnTimer, 13.5f);
        await StockAbilityProfile.ObserveAsync(primary, rootAt, frame => RootRecord(frame, remove: false), "Reloaded Lockdown still roots for 12 s");
        await StockAbilityProfile.ChangeLoadoutAsync(caster, profile, new[] { Lockdown }, load: false);
        await LandAsync(33);
        Check.That(!primary.World.RpcSnapshot().Skip(rootAt).Any(frame => RootRecord(frame, remove: true)),
            "The landed Lockdown survives its unload and Static Blast");
        await LandAsync(514);
        byte[] reuseEnd = AbilityCooldowns.BuildTimer(Tinkering, AbilityCooldowns.OwnTimer, 30, start: false);
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(fizzleAt)
            .Any(frame => frame.Bytes.AsSpan().SequenceEqual(reuseEnd)), "Tinkering finished its normal reuse",
            TimeSpan.FromSeconds(34));
        int finalViews = caster.World.ViewSnapshot().Count;
        await LandAsync(Tinkering);
        await ExpectIsAsync(finalViews, 395);
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Coder membership and normal effects are framed");
        Console.WriteLine("CODER CAST MEMBERSHIP PASS: 21/33/36/514 reason 7, ordinary in-flight 36 unload, and reloaded root and damage.");
        return Array.Empty<byte>();

        async Task LandAsync(ushort id)
        {
            var sample = Samples.Single(row => row.Id == id);
            int cursor = secondary.World.RpcSnapshot().Count;
            await StarterFixedDamageMode.Land(caster, primary, target, sample.Id, sample.Damage, sample.Cast, sample.Reuse, sample.Request);
            byte[] damage = AbilityMessages.Damage(false, id, caster.Handle, secondary.Handle, sample.Damage, 0);
            if (sample.Splash)
                await StockAbilityProfile.ObserveAsync(secondary, cursor, frame => frame.Bytes.AsSpan().SequenceEqual(damage),
                    $"Reloaded {id} hits its splash recipient for {sample.Damage}");
            else
                Check.That(!secondary.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Bytes.AsSpan().SequenceEqual(damage)),
                    "Static Blast stays single-target");
        }

        Task ExpectIsAsync(int from, int expected) => caster.WaitUntilAsync(() =>
            caster.World.ViewSnapshot().Skip(from).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(value => value == expected),
            $"CSR has {expected} IS", HeadlessPlayer.DefaultTimeout);

        static bool RootRecord(RpcRecord frame, bool remove) => ManageBonusRecord.TryDecode(frame.Bytes, out var root) &&
            root!.SourceId == Lockdown && root.Type == TempMods.KindState && root.TargetId == TempMods.RootedAbility &&
            (remove ? root.AddRemoveType == 1 : root.AddRemoveType == 0 && root.Duration == 12);
    }
}
