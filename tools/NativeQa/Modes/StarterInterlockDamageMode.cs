using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterInterlockDamageMode : NativeQaMode
{
    private const byte CharacterLevel = 19;
    private const byte Rank = 15;
    private const ushort DisarmingShot = 126, GuardBreaker = 138, IronGuard = 139;
    private static readonly ushort[] Roots = { 2, 17, 101, 129, 133, DisarmingShot, GuardBreaker, IronGuard };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-starter-interlock-bases";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level={CharacterLevel},exp={PlayerProgression.ExperienceForLevel(CharacterLevel)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000,alignment=1 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"UPDATE characters SET x=25101,y=495,z=-1498,alignment=2,level=10,exp={PlayerProgression.ExperienceForLevel(10)},pvpflag=0,healthC=3000,healthM=3000 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114 AND ability_id NOT IN (2147485696,2147501056); UPDATE char_abilities SET level=1 WHERE char_id=6114"
        }, DevCommands: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "6",
            [CombatFixtureWeapons.WeaponMode] = "1"
        },
        readyMessages: new[]
        {
            "Level 19, rank 15, 44/50 memory. With the pistol equipped, use ordinary Close Combat on QAEnemy, reselect Handguns and use Disarming Shot (126). Capture the ordinary landed interlock move and its captured Ballistic Accuracy penalty of -34..-18 for 10 seconds. The shipped attached base is ballistic 11.",
            "Leave the fight, unequip the pistol, and use ordinary Close Combat on Melee Dummy. Reselect Aikido and use Iron Guard (139): physical base 12 and both caster-only +10 defense records for 12 seconds.",
            "Reselect Kung-Fu, choose Grab and wait for the actual Off Balance (04) target condition. Use Guard Breaker (138) while the slot enables: physical base 36 and target -10 melee defense for 15 seconds. Save the actual outgoing stock requests and landed round records."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "126,138,139", ["NativeCloseableRows"] = 0 },
        testMobFixture: CombatFixtureLoadout.MeleeCsv);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        var schema = context.OwnedDatabase ?? throw new InvalidOperationException("Expected disposable schema.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(archive.All(item => item.Level == Rank) &&
            archive.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout), "Exact initial starter loadout");
        Check.Equal(44, Loadout.Sum(AbilityMemory.Cost), "The profile uses 44 of 50 memory");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Handgun);
        var opponent = await caster.WaitForViewNamedAsync(enemy.Handle);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        await Open(caster, opponent);
        await Select(caster, opponent, 129);
        int accuracyStart = enemy.World.RpcSnapshot().Count;
        await Land(caster, opponent, DisarmingShot, server, 11, "Pistol Disarming Shot", pvp: true);
        int accuracy = await Modifier(enemy, DisarmingShot, 524, -34, 10, accuracyStart, maximum: -18);
        await Modifier(enemy, DisarmingShot, 524, accuracy, 10, accuracyStart, remove: true);
        await Leave(caster, opponent);
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, schema, "unarmed", 0);

        await Open(caster, dummy);
        await Select(caster, dummy, 101);
        int guardStart = caster.World.RpcSnapshot().Count;
        await Land(caster, dummy, IronGuard, server, 13, "AikidoAbility IronGuard", pvp: false);
        await Modifier(caster, IronGuard, 1009, 10, 12, guardStart);
        await Modifier(caster, IronGuard, 1010, 10, 12, guardStart);

        await Select(caster, dummy, 133);
        caster.SendRpc("42 02 00 00 00 00 00");
        await Land(caster, dummy, GuardBreaker, server, 40, "KarateAbility GuardBreaker", pvp: false, offBalance: true);
        Check.That(server.DebugLog().Contains("[EFFECTS] 138 on Melee Dummy: Stat 1009 -10 15 s", StringComparison.Ordinal),
            "Landed Guard Breaker maintains its target debuff");
        await Modifier(caster, IronGuard, 1009, 10, 12, guardStart, remove: true);
        await Modifier(caster, IronGuard, 1010, 10, 12, guardStart, remove: true);
        await Leave(caster, dummy);
        Console.WriteLine("  Three starter interlock moves landed with their typed bases, accuracy records and caster defenses");
        return Array.Empty<byte>();
    }

    private static string Le(ushort id) => WireFormat.ToHex(BitConverter.GetBytes(id));
    private static string Handle(ViewCreation target) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));

    private static async Task Open(HeadlessPlayer caster, ViewCreation target)
    {
        string handle = Handle(target);
        caster.SkipReceived();
        caster.World.SendRpcs(
            WireFormat.FromHex($"80 b9 11 00 {handle} 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 34 09 7f 00 00 00 00 ff 08 6b 00 00 00 00 00"),
            WireFormat.FromHex($"40 {handle} 01 6b"));
        await caster.WaitForRpcAsync("4e 01");
        await caster.WaitForRpcAsync($"40 {handle} 00 6b");
        await caster.WaitForRpcAsync("47");
    }

    private static async Task Leave(HeadlessPlayer caster, ViewCreation target)
    {
        caster.SendRpc("44 00");
        await caster.WaitForRpcAsync($"46 {Handle(target)} 00");
    }

    private static async Task Select(HeadlessPlayer caster, ViewCreation target, ushort style)
    {
        int cursor = caster.World.ViewSnapshot().Count;
        var actual = BytePattern.Parse($"02 00 02 80 80 80 80 80 10 {Le(style)} 00 00");
        Use(caster, target, style);
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(cursor).Any(actual.Occurs),
            $"reselected style {style} during the ordinary interlock", HeadlessPlayer.DefaultTimeout);
    }

    private static void Use(HeadlessPlayer caster, ViewCreation target, ushort id) =>
        caster.SendRpc(new AbilityUseRecord(id, DuelPackets.Handle(target.ViewId, target.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());

    private static async Task Land(HeadlessPlayer caster, ViewCreation target, ushort id, ServerInstance server,
        int damage, string animation, bool pvp, bool offBalance = false)
    {
        string name = AbilityCatalog.Find(id).Name;
        string landed = pvp ? $"[PVP] {caster.Handle}'s {name} lands on {target.Name} for {damage}" :
            $"[ABILITY] {id} lands on view {target.ViewId} for {damage}";
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (offBalance)
                await AwaitOffBalance(caster, target);
            int start = server.DebugLog().Length;
            Use(caster, target, id);
            try
            {
                await caster.WaitUntilAsync(() => server.DebugLog()[start..].Split('\n').Any(line => line.TrimEnd('\r').EndsWith(landed, StringComparison.Ordinal)),
                    $"ordinary interlock {id} lands for {damage}", TimeSpan.FromSeconds(12));
                Check.That(server.DebugLog()[start..].Contains($"({animation}", StringComparison.Ordinal),
                    $"landed {id} keeps its current animation {animation}");
                return;
            }
            catch (TimeoutException) when (attempt < 3)
            {
                string log = server.DebugLog()[start..];
                if (log.Contains($"[ABILITY] {id} lands on view", StringComparison.Ordinal) ||
                    log.Contains($"[PVP] {caster.Handle}'s {name} lands ", StringComparison.Ordinal))
                    throw new ScenarioFailure($"Ability {id} landed with an unexpected damage value.");
            }
        }
    }

    private static async Task AwaitOffBalance(HeadlessPlayer caster, ViewCreation target)
    {
        int cursor = caster.World.ViewSnapshot().Count;
        byte[] prefix = WireFormat.FromHex($"{Le(target.ViewId)} 04 80 80 80 80 80 80 02");
        const uint condition = CombatInterlock.InCombatFlag | 0x04;
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(cursor).Any(packet =>
        {
            int index = packet.AsSpan().IndexOf(prefix);
            int offset = index + prefix.Length;
            return index >= 0 && offset + sizeof(uint) <= packet.Length &&
                (BitConverter.ToUInt32(packet, offset) & condition) == condition;
        }), "natural Grab sets Off Balance 04 on the living dummy", TimeSpan.FromSeconds(100));
    }

    private static async Task<int> Modifier(HeadlessPlayer actor, ushort id, ushort stat, int value, int duration,
        int cursor, bool remove = false, int? maximum = null)
    {
        ManageBonusRecord? found = null;
        await actor.WaitUntilAsync(() =>
        {
            foreach (var rpc in actor.World.RpcSnapshot().Skip(cursor))
            {
                if (ManageBonusRecord.TryDecode(rpc.Bytes, out var mod) && mod!.SourceId == id && mod.TargetId == stat &&
                    mod.SignedValue >= value && mod.SignedValue <= (maximum ?? value) &&
                    mod.Duration == duration && mod.AddRemoveType == (remove ? 1 : 0))
                {
                    found = mod;
                    return true;
                }
            }
            return false;
        }, $"sourced {id}/{stat} modifier for {duration}s, removal={remove}", TimeSpan.FromSeconds(15));
        return found!.SignedValue;
    }
}
