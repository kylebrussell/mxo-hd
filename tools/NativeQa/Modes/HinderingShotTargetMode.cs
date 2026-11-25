using System.Buffers.Binary;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HinderingShotTargetMode : NativeQaMode
{
    private const ushort HinderingShot = 130, Handguns = CombatStyles.Handguns;
    private const byte Level = 11, Rank = 10, SafeLevel = 10;
    private const int X = 17000, Y = 495, Z = 6400, EnemyZ = 6800;
    private const string Notice = "Select a living enemy before using this ability.";
    // PR389 stock PacketLog d3b7112643d9, lines 3204-3205, and the retained Handguns selection.
    private const string CapturedShot = "80b982000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string CapturedHandguns = "80b981000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private static readonly ushort[] Roots = { 2, 17, Handguns, HinderingShot };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-hindering-shot-target";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, X, Z, Array.Empty<string>(), new[] { CombatFixtureLoadout.SeedWeapons }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y}," +
                "healthC=1000,healthM=1000,innerStrC=200,innerStrM=200 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={X},z={Z},rotation={MobAggro.YawTowards(X, Z, X, EnemyZ)} WHERE charId=110",
            $"UPDATE characters SET x={X},z={EnemyZ} WHERE charId=6114",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, HinderingShot }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = SafeLevel.ToString(),
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [CombatFixtureWeapons.WeaponMode] = "1",
            ["MXO_RANGED_CONTEST_QA"] = "1"
        },
        readyMessages: new[]
        {
            $"Level {Level}, rank {Rank}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{EnemyZ}). Page 2 slots 1 Handguns and 2 Hindering Shot. The single handgun 2565 starts equipped.",
            "Stay at rest and /target QAEnemy. Try slot 2 before selecting Handguns. If the client sends the cast, expect reason 13 with no debit or reuse. Select Handguns, use ?cd w rifle and try slot 2 again. Expect local refusal or reason 15 if sent, with no debit or reuse.",
            "Use ?cd w handgun, reselect Handguns and clear the target. With no selection expect local refusal or the visible live-target notice. Select /target QAEnemy, cast slot 2 and record the pistol motion and whether either actor enters interlock. Expect a two-second cast, 20 IS (200 to 180), 9 ballistic damage, 8 s own reuse and 5 s family 690.",
            "QAEnemy receives [MODIFIER] source130 stats115/877/878 each -50 and stat519 -10; all four last eight seconds and remove together. An ordinary accepted cast in this mode has no queued special exchange.",
            "Ranged contest (generic_free_attacks.AbilityRangeFreeAttack, TestFunction 1): an unforced at-rest 130 rolls 524 vs 1010 and can deflect. Type ?rangedcontest hit before the positive cast above for its exact numbers. Then type ?rangedcontest deflect, wait out 130's 8 s reuse and cast again: the pistol motion still plays, QAEnemy takes no damage or modifiers, CSR sees 'QAEnemy deflected Hindering Shot.' and QAEnemy's combat log shows the deflection line. Type ?rangedcontest off."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "130", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level) });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int z in new[] { Z, EnemyZ })
            Check.That(NavMesh.Locate(1, X, Y, z, out var at) && !at.indoors && at.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, X, Y, z), $"Hindering Shot outdoor stand ({X},{Y},{z})");

        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await EnterLowLevelAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(archive.All(row => row.Level == Rank) &&
            archive.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout),
            "Exact prerequisite profile at rank 10");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-11 memory");
        await EnterLowLevelAsync(caster);
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(CombatFixtureWeapons.Handgun, BitConverter.ToUInt32(self.Attributes["EquippedItemID"]), "Live handgun");
        Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { X, Y, Z }), "Caster stands clear of ambient FX");
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        Check.That(EngageFoesFixture.Position(target).SequenceEqual(new double[] { X, Y, EnemyZ }), "QAEnemy is at the printed stand");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        await Refused(caster, WithTarget(handle), "no selected Handguns style", AbilityRefusals.WrongStyle);
        await SelectHandgunsAsync();
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, context.OwnedDatabase!, "rifle", CombatFixtureWeapons.Rifle);
        await Refused(caster, WithTarget(handle), "rifle equipped", AbilityRefusals.RequiredItemCategory);
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, context.OwnedDatabase!, "handgun", CombatFixtureWeapons.Handgun);
        await SelectHandgunsAsync();

        await Refused(caster, WithTarget(0), "no target");
        ushort otherSpawn = unchecked((ushort)(target.SpawnId + 1));
        await Refused(caster, WithTarget(DuelPackets.Handle(target.ViewId, otherSpawn)), "stale spawn");

        caster.SendChat("?rangedcontest hit");
        await caster.WaitForTextAsync("Ranged contest hit.");
        int targetCursor = enemy.World.RpcSnapshot().Count, castCursor = caster.World.RpcSnapshot().Count;
        await StarterFixedDamageMode.Land(caster, enemy, target, HinderingShot, 9, 2, 8, CapturedShot);
        await EvidenceRetestWire.TimerAsync(caster, castCursor, 690, AbilityCooldowns.GroupTimer, 5);
        var effects = new (ushort Stat, int Value)[] { (115, -50), (877, -50), (878, -50), (519, -10) };
        await enemy.WaitUntilAsync(() => effects.All(expected => enemy.World.RpcSnapshot().Skip(targetCursor).Any(rpc =>
            ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.SourceId == HinderingShot &&
            bonus.TargetId == expected.Stat && bonus.SignedValue == expected.Value && bonus.Duration == 8 &&
            bonus.AddRemoveType == 0)), "All four selected-target Hindering Shot modifiers", HeadlessPlayer.DefaultTimeout);
        // The ranged contest's deflection: the shot is fired (motion) but deals no damage or effects.
        await Task.Delay(TimeSpan.FromSeconds(8.5));
        caster.SendChat("?rangedcontest deflect");
        await caster.WaitForTextAsync("Ranged contest deflect.");
        int deflectLog = context.ActualServer!.DebugLog().Length, deflectCursor = enemy.World.RpcSnapshot().Count;
        caster.SendRpc(WithTarget(handle));
        await caster.WaitForTextAsync("QAEnemy deflected Hindering Shot.");
        await caster.WaitUntilAsync(() => context.ActualServer!.DebugLog()[deflectLog..].Contains("[AT-REST-MOTION] 130 action 0x5d", StringComparison.Ordinal),
            "Deflected 130 still plays the pistol motion", HeadlessPlayer.DefaultTimeout);
        await Task.Delay(TimeSpan.FromSeconds(1));
        Check.That(!enemy.World.RpcSnapshot().Skip(deflectCursor).Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) &&
            bonus!.SourceId == HinderingShot && bonus.AddRemoveType == 0), "A deflected 130 applies no modifiers");
        caster.SendChat("?rangedcontest off");
        await caster.WaitForTextAsync("Ranged contest off.");
        Console.WriteLine("READY BOOTSTRAP 130 deflection: motion played, no damage or modifiers");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Stock request and response framing");
        Console.WriteLine("HINDERING SHOT PASS: wrong style and rifle refused before debit; fresh pistol/Handguns cast landed for 9 with four modifiers.");
        return Array.Empty<byte>();

        async Task SelectHandgunsAsync()
        {
            int viewCursor = caster.World.ViewSnapshot().Count;
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(CapturedHandguns, handle));
            await EvidenceRetestWire.ViewSinceAsync(caster, viewCursor, 2, "02 80 80 80 80 80 10 81 00 00 00");
        }
    }

    private static async Task EnterLowLevelAsync(HeadlessPlayer player)
    {
        await player.EnterWorldAsync();
        byte[] safeSetting = WireFormat.FromHex(PvpOpponentsFixture.SafeSetting);
        safeSetting[^4] = SafeLevel;
        string safe = WireFormat.ToHex(safeSetting);
        await player.WaitUntilAsync(() => player.Received(PvpOpponentsFixture.HostileSetting).Count != 0 &&
            player.Received(safe).Count != 0, "Actual hostile world with level-10 protection", HeadlessPlayer.DefaultTimeout);
        await player.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
    }

    internal static async Task Refused(HeadlessPlayer caster, byte[] request, string input, uint? reason = null)
    {
        int rpcStart = caster.World.RpcSnapshot().Count;
        var views = caster.World.ViewSnapshot();
        ushort before = BitConverter.ToUInt16(caster.World.CreationSnapshot().Last(view => view.ViewId == 2).Attributes["InnerStrengthAvailable"]);
        foreach (byte[] bytes in views)
            if (BuffDrainLifetimeFixture.TryInnerStrength(bytes, out ushort value))
                before = value;

        caster.SkipReceived();
        caster.SendRpc(request);
        if (reason.HasValue)
        {
            byte[] refusal = AbilityRefusals.Encode(new(HinderingShot, reason.Value));
            await caster.WaitForRpcAsync(WireFormat.ToHex(refusal));
        }
        else await caster.WaitForTextAsync(Notice);
        caster.SendRpc("81 48");
        await caster.WaitForRpcAsync("81 49");
        var bar = BytePattern.Parse("80 ac 82 00");
        var reuse = BytePattern.Parse("80 bd 01 82 00");
        Check.That(!caster.World.RpcSnapshot().Skip(rpcStart).Any(rpc => bar.IsMatch(rpc.Bytes) || reuse.IsMatch(rpc.Bytes)),
            $"Hindering Shot with {input} does not start cast/reuse");
        Check.That(!caster.World.ViewSnapshot().Skip(views.Count).Any(bytes =>
            BuffDrainLifetimeFixture.TryInnerStrength(bytes, out ushort value) && value < before),
            $"Hindering Shot with {input} does not spend Inner Strength");
    }

    private static byte[] WithTarget(uint handle)
    {
        byte[] request = Convert.FromHexString(CapturedShot);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), handle);
        return request;
    }
}
