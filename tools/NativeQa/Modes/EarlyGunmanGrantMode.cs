using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class EarlyGunmanGrantMode : NativeQaMode
{
    private const ushort Gunman = 128, Upgrade = 1070;
    private const int Level = 19, Rank = 15;
    // PR343 PacketLog 606366a42641, lines 971–972. Only view/spawn changes in the replay.
    private const string StockUpgrade = "80b92e04040002000000000000000000000000000000000000000000785bb60001000000086b0000000000";
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] GunmanPassive =
    {
        (Gunman, Upgrade, new[] { ((ushort)1046, 1) })
    };

    public override string Name => "ability-parity-starter-gunman-grant";
    public override NativeQaModePlan Plan { get; } = new(
        AbilityPrerequisites.Current.Closure(Upgrade).Keys.Concat(new ushort[] { 2, 17, Gunman }).Distinct().Order().ToArray(),
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={CombatFixtureLoadout.GroundY} WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110"
        },
        DevCommands: true,
        testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Level 19, 36/49 memory with Gunman 128 and every explicit upgrade prerequisite loaded. Immediately after login, find one Gunman Upgrade in Actions. Gunman Upgrade is granted; it is not owned in the Archive.",
            "Unload and reload Gunman 128 using the loadout UI or ?qaunload 128 / ?qaload 128. Check that the upgrade disappears and returns exactly once. Capture the matching Initiative +1 remove and add records.",
            "After reload, add Gunman Upgrade from Actions to the hotbar. Select Melee Dummy 3 m west and use Gunman Upgrade. Expect only CSR to get +4 ballistic accuracy, +70 maximum HP, +2 ballistic damage and +4 ballistic defense. Record the request and 30 s reuse."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "128", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var actor = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[7])
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(archive.ContainsKey(Gunman) && !archive.ContainsKey(Upgrade), "Only the Gunman parent is owned");
        var loadout = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]);
        Check.Equal(36, loadout.Sum(row => AbilityMemory.Cost(AbilityCatalog.IdOf(row.Descriptor))),
            "The early Gunman profile uses 36 of its 49 memory");

        await actor.EnterWorldAsync(loadingComplete: false);
        await Barrier(actor);
        var hiddenLoad = StockAbilityProfile.GrantRecord(Upgrade, remove: false);
        Check.That(!actor.World.RpcSnapshot().Any(hiddenLoad), "Self creation alone does not register the upgrade");
        int readyCursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc("0c 01");
        var proof = new List<object>();
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, GunmanPassive, readyCursor, remove: false);
        actor.SendRpc("0c 01");
        await Barrier(actor);
        Check.Equal(1, actor.World.RpcSnapshot().Count(hiddenLoad), "Duplicate readiness is idempotent");

        int unloadCursor = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Gunman }, load: false);
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, GunmanPassive, unloadCursor, remove: true);
        int loadCursor = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Gunman }, load: true);
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, GunmanPassive, loadCursor, remove: false);
        Check.Equal(2, actor.World.RpcSnapshot().Count(hiddenLoad), "Login and reload each add the hidden upgrade once");

        var dummy = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        byte[] request = Convert.FromHexString(StockUpgrade);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), DuelPackets.Handle(dummy.ViewId, dummy.SpawnId));
        int castCursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(request);
        foreach (var (stat, value) in new (ushort Stat, int Value)[] { (524, 4), (106, 70), (171, 2), (1010, 4) })
        {
            var bonus = await StockAbilityProfile.ObserveAsync(actor, castCursor,
                packet => StockAbilityProfile.Bonus(packet, Upgrade, stat, value, 0, remove: false), $"Gunman Upgrade {stat} +{value}");
            proof.Add(new { Stat = stat, Value = value, Record = bonus.Hex });
        }
        byte[] timer = AbilityCooldowns.BuildTimer(Upgrade, AbilityCooldowns.OwnTimer, 30, true);
        var reuse = await StockAbilityProfile.ObserveAsync(actor, castCursor,
            packet => packet.Bytes.AsSpan().SequenceEqual(timer), "Gunman Upgrade 30-second reuse");
        proof.Add(new { Request = Convert.ToHexString(request), Reuse = reuse.Hex });
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "early-gunman-grant.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();
    }

    private static async Task Barrier(HeadlessPlayer actor)
    {
        actor.SendRpc("81 48");
        await actor.WaitForRpcAsync("81 49");
    }
}
