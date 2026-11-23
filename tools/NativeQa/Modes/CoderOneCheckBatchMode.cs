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
    // PR312 stock 88a1d93e477a:4666–4667.
    internal const string TechnicianRequest = "80b94d02000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    // PR447 stock 321078e1ed92:4132–4133 and 4261–4262.
    private const string MasterOn = "80b95404000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string MasterOff = "80b95404000000000000000000000000000000000000000000000000585cc60001000000086b0000000000";

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
            "Step A. Record Programmer (275) +10 crafting discount, Code Hardening (608) +5 melee and ballistic damage bonuses, and Utility Master (620) +5 melee accuracy. Cycle each with ?qaunload/?qaload. This first stage already passed natively on PR447.",
            "Step B. Verify one Utility Master action in Actions after reload. Use page 2 slot 1 to get +150 maximum HP, +20 melee and ballistic resistance and +6 melee defense. Capture standalone 30s reuse before the on/modifier records. Press the same slot again within 3–5s, expect switch-off and removal, then verify the remaining reuse holds a third press.",
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

        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(MasterOn);
        var masterStats = new (ushort Stat, int Value)[] { (106, 150), (180, 20), (181, 20), (1009, 6) };
        foreach (var (stat, value) in masterStats)
            await ObserveAsync(caster, cursor, packet => Bonus(packet, MasterUpgrade, stat, value, 0, remove: false),
                $"Utility Master adds shipped stat {stat}");
        await EvidenceRetestWire.TimerAsync(caster, cursor, MasterUpgrade, AbilityCooldowns.OwnTimer, 30);
        byte[] reuse = AbilityCooldowns.BuildTimer(MasterUpgrade, AbilityCooldowns.OwnTimer, 30, true);
        byte[] on = AbilityBuffs.BuildExclusive(MasterUpgrade, true);
        byte[] off = AbilityBuffs.BuildExclusive(MasterUpgrade, false);
        byte[][] activation = caster.World.RpcSnapshot().Skip(cursor).Select(record => record.Bytes).ToArray();
        int timerAt = Array.FindIndex(activation, bytes => bytes.AsSpan().SequenceEqual(reuse));
        int onAt = Array.FindIndex(activation, bytes => bytes.AsSpan().SequenceEqual(on));
        Check.That(timerAt >= 0 && onAt > timerAt, "Utility Master emits standalone reuse before the exclusive on record");
        await Task.Delay(650);
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(MasterOff);
        await ObserveAsync(caster, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(off), "Utility Master switches off during reuse");
        foreach (var (stat, value) in masterStats)
            await ObserveAsync(caster, cursor, packet => Bonus(packet, MasterUpgrade, stat, value, 0, remove: true),
                $"Utility Master removes shipped stat {stat}");
        Check.That(!caster.World.RpcSnapshot().Skip(cursor).Any(packet => packet.Bytes.AsSpan().SequenceEqual(reuse)),
            "Switching Utility Master off does not restart reuse");
        await Task.Delay(650);
        cursor = caster.World.RpcSnapshot().Count;
        caster.SendRpc(MasterOn);
        await ObserveAsync(caster, cursor, packet => AbilityRefusals.TryDecode(packet.Bytes, out var failure) &&
            failure.AbilityID == MasterUpgrade && failure.ErrorID == AbilityCooldowns.DeniedTimerRunning,
            "The original timer still holds reactivation after switch-off");

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
