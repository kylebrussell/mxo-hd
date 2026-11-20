using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class CoderOneCheckBatchMode : NativeQaMode
{
    private const ushort Programmer = 275, CodeHardening = 608, UtilityMaster = 620, MasterUpgrade = 1108, TechnicianAura = 589;
    private static readonly ushort[] Loadout = Profile(2, 17, Programmer, CodeHardening, UtilityMaster, TechnicianAura).Order().ToArray();
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
    {
        (Programmer, 0, new (ushort, int)[] { (530, 10) }),
        (CodeHardening, 0, new (ushort, int)[] { (1139, 5), (1142, 5) }),
        (UtilityMaster, MasterUpgrade, new (ushort, int)[] { (997, 5) })
    };
    // PR312 stock 88a1d93e477a:4666–4667. The Utility Master action uses the same self-cast shape.
    private const string TechnicianRequest = "80b94d02000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-coder-passives-and-utility";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},innerStrC=500,innerStrM=500,alignment=1 WHERE charId IN (110,6111)",
            $"UPDATE characters SET x={CombatFixtureLoadout.CasterX + 200},z={CombatFixtureLoadout.CasterZ} WHERE charId=6111",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new ushort[] { MasterUpgrade, TechnicianAura },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "275,608,620", ["NativeCloseableRows"] = 3, ["BehaviorTestRows"] = "698" },
        readyMessages: new[]
        {
            "Step A. Record Programmer (275) +10 crafting discount, Code Hardening (608) +5 melee and ballistic damage bonuses, and Utility Master (620) +5 melee accuracy. At a Hardline unload/reload each and capture its source rows disappearing and returning.",
            "Step B. Verify one Utility Master action in Actions after reload. Use page 2 slot 1 to get +150 maximum HP, +20 melee and ballistic resistance and +6 melee defense. Capture the 30s reuse.",
            "Optional in the same session. QAFriend has a passive Proxy within 4 m. Slot 2 starts Proxy Technician (589), whose 698 pulse grants that pet +64 melee and ballistic attack. A second press turns it off. Use the helper request for '?pet' to see the bonuses."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        friend.SendChat("?pet summon 27");
        await friend.WaitForRpcAsync("81 76");
        friend.SendRpc("81 7c fe 05 02"); // Passive.
        await friend.WaitForRpcAsync("81 78");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[7]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(!archive.ContainsKey(MasterUpgrade), "Utility Master action is sourced solely by the parent grant");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= 100, "The three passive chains and Proxy Technician fit in memory");
        await caster.EnterWorldAsync();
        await caster.WaitForViewNamedAsync(friend.Handle);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var proof = new List<object>();
        await CheckPassivesAsync(caster, proof, Passives, 0, remove: false);
        ushort[] cycle = { UtilityMaster, CodeHardening, Programmer };
        int cursor = await ChangeLoadoutAsync(caster, archive, cycle, load: false);
        await CheckPassivesAsync(caster, proof, Passives, cursor, remove: true);
        cursor = await ChangeLoadoutAsync(caster, archive, cycle.Reverse().ToArray(), load: true);
        await CheckPassivesAsync(caster, proof, Passives, cursor, remove: false);

        byte[] master = Convert.FromHexString(TechnicianRequest);
        BinaryPrimitives.WriteUInt16LittleEndian(master.AsSpan(2), MasterUpgrade);
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(master);
        foreach (var (stat, value) in new (ushort, int)[] { (106, 150), (180, 20), (181, 20), (1009, 6) })
            await ObserveAsync(caster, cursor, packet => Bonus(packet, MasterUpgrade, stat, value, 0, remove: false),
                $"Utility Master adds shipped stat {stat}");
        await EvidenceRetestWire.TimerAsync(caster, cursor, MasterUpgrade, AbilityCooldowns.OwnTimer, 30);

        caster.SendRpc(TechnicianRequest);
        await caster.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(TechnicianAura, true)),
            "Proxy Technician switches on");
        await friend.WaitUntilAsync(() => server.DebugLog().Split('\n').Any(line =>
            line.Contains("[PROXYTECH] pulse source=CSR_Tester recipient=QAFriend ") && line.Contains("melee=64 ballistic=64")),
            "The friendly proxy receives the level-50 698 payload", HeadlessPlayer.DefaultTimeout);
        await Task.Delay(550);
        caster.SendRpc(TechnicianRequest);
        await caster.WaitForRpcAsync(packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(TechnicianAura, false)),
            "Proxy Technician switches off");
        await Task.Delay(TimeSpan.FromSeconds(5.5));
        friend.SendChat("?pet");
        await friend.WaitForTextAsync("technician melee/ballistic 0/0");
        Check.Equal(0, caster.World.Problems.Count + friend.World.Problems.Count, "Coder batch request framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "coder-passive-records.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();
    }
}
