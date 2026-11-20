using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterCompelCloseCombatMode : NativeQaMode
{
    private const ushort Compel = 150;
    private const int Level = 11, Rank = 10;
    // PR358 stock PacketLog c15509643a25, lines 848–849; self cast has a zero target handle.
    private const string StockCompel = "80b99600000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private static readonly ushort[] Roots = { 2, 17, Compel };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-starter-compel-close-combat";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Enter ordinary Close Combat on Melee Dummy and try Compel Close Combat from the hotbar. Record whether stock suppresses the press. If a request is sent, expect outside-combat refusal with no cast or resource debit.",
            "Leave interlock and immediately use Compel Close Combat at rest. Expect the original two-second cast, 30 Inner Strength cost and caster-only +20 Force Combat for 300 seconds."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "150", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(archive.All(row => row.Level == Rank) &&
            archive.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout), "Exact owned rank-10 profile");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-11 memory");
        await caster.EnterWorldAsync();
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        await StarterInterlockDamageMode.Open(caster, dummy);

        byte[] request = Convert.FromHexString(StockCompel);
        int cursor = caster.World.RpcSnapshot().Count;
        var beforeViews = caster.World.ViewSnapshot();
        ushort before = CurrentInnerStrength(caster);
        byte[] refusal = AbilityRefusals.Encode(new AbilityRefusals.Record(Compel, AbilityRefusals.MustBeOutsideCombat));
        caster.SendRpc(request);
        await caster.WaitForRpcAsync(rpc => rpc.Bytes.AsSpan().SequenceEqual(refusal), "Compel Close Combat outside-combat refusal");
        caster.SendRpc("81 48");
        await caster.WaitForRpcAsync("81 49");

        var castBar = BytePattern.Parse("80 ac 96 00");
        var reuse = BytePattern.Parse("80 bd 01 96 00");
        Check.That(!caster.World.RpcSnapshot().Skip(cursor).Any(rpc =>
            castBar.IsMatch(rpc.Bytes) || reuse.IsMatch(rpc.Bytes) || IsCompelBuff(rpc)), "No cast, reuse or modifier in active interlock");
        Check.That(!AwakenedPowerShotMode.InnerStrengthUpdates(caster.World.ViewSnapshot().Skip(beforeViews.Count)).Any(value => value < before),
            "No Inner Strength debit on refusal");

        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        caster.SendRpc("44 00");
        await caster.WaitForRpcAsync($"46 {handle} 00");
        int outOfCombat = caster.World.RpcSnapshot().Count;
        int outOfCombatViews = caster.World.ViewSnapshot().Count;
        ushort restInnerStrength = CurrentInnerStrength(caster);
        caster.SendRpc(request);
        await caster.WaitForRpcAsync(rpc => castBar.IsMatch(rpc.Bytes) &&
            BitConverter.ToSingle(rpc.Bytes, rpc.Bytes.Length - 4) == 2, "Two-second Compel Close Combat cast after release");
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(outOfCombat).Any(IsCompelBuff),
            "Compel Close Combat +20 Force Combat for 300 seconds", HeadlessPlayer.DefaultTimeout);
        var debits = AwakenedPowerShotMode.InnerStrengthUpdates(caster.World.ViewSnapshot().Skip(outOfCombatViews));
        Check.That(debits.Contains((ushort)(restInnerStrength - 30)), "Confirmed 30 Inner Strength debit after release");
        Check.Equal(0, caster.World.Problems.Count, "Full stock framing");
        Console.WriteLine("  Interlock refused without cost or state; ordinary withdrawal restored the 2-second +20 Force Combat cast");
        return Array.Empty<byte>();
    }

    private static bool IsCompelBuff(RpcRecord rpc) =>
        ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.SourceId == Compel &&
        bonus.TargetId == 519 && bonus.SignedValue == 20 && bonus.Duration == 300 && bonus.AddRemoveType == 0;

    private static ushort CurrentInnerStrength(HeadlessPlayer caster)
    {
        ushort initial = BitConverter.ToUInt16(caster.World.CreationSnapshot().Last(view => view.ViewId == 2).Attributes["InnerStrengthAvailable"]);
        return AwakenedPowerShotMode.InnerStrengthUpdates(caster.World.ViewSnapshot()).LastOrDefault(initial);
    }
}
