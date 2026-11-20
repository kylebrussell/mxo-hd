using System.Buffers.Binary;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HinderingShotTargetMode : NativeQaMode
{
    private const ushort HinderingShot = 130, Handguns = 129;
    private const byte Level = 11, Rank = 10;
    private const string Notice = "Select a living enemy before using this ability.";
    // PR389 stock PacketLog d3b7112643d9, lines 3204–3205.
    private const string CapturedShot = "80b982000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private static readonly ushort[] Roots = { 2, 17, Handguns, HinderingShot };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-hindering-shot-target";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=1000,healthM=1000,innerStrC=200,innerStrM=200,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END WHERE charId IN (110,6114)",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            "UPDATE characters SET x=25200,z=-1498 WHERE charId=6114",
            CombatFixtureLoadout.SeedWeapons
        }, DevCommands: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10"
        },
        readyMessages: new[]
        {
            "Select Handguns with the pistol equipped. Stay outside interlock and clear the target. Try Hindering Shot with no selection and retain the result. If the stock client emits a request, expect the live-target notice with no IS debit, cast bar or reuse. If the client suppresses it, record that observation.",
            "Select QAEnemy four metres away and use the ordinary hotbar cast. Expect the 2-second cast, 9 ballistic damage, four eight-second target modifiers and 8-second own reuse."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "130", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level) });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(archive.All(row => row.Level == Rank) &&
            archive.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout),
            "Exact prerequisite profile at rank 10");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-11 memory");
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(CombatFixtureWeapons.Handgun, BitConverter.ToUInt32(self.Attributes["EquippedItemID"]), "Live handgun");
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        await StarterFixedDamageMode.SelectStyle(caster, self, Handguns, 171);

        await Refused(caster, WithTarget(0), "no target");
        ushort otherSpawn = unchecked((ushort)(target.SpawnId + 1));
        await Refused(caster, WithTarget(DuelPackets.Handle(target.ViewId, otherSpawn)), "stale spawn");

        int targetCursor = enemy.World.RpcSnapshot().Count;
        await StarterFixedDamageMode.Land(caster, enemy, target, HinderingShot, 9, 2, 8, CapturedShot);
        var effects = new (ushort Stat, int Value)[] { (115, -50), (877, -50), (878, -50), (519, -10) };
        await enemy.WaitUntilAsync(() => effects.All(expected => enemy.World.RpcSnapshot().Skip(targetCursor).Any(rpc =>
            ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.SourceId == HinderingShot &&
            bonus.TargetId == expected.Stat && bonus.SignedValue == expected.Value && bonus.Duration == 8 &&
            bonus.AddRemoveType == 0)), "All four selected-target Hindering Shot modifiers", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Stock request and response framing");
        Console.WriteLine("  Missing/stale 130 had no IS, cast or reuse; fresh selection landed for 9 with all four modifiers");
        return Array.Empty<byte>();
    }

    private static async Task Refused(HeadlessPlayer caster, byte[] request, string input)
    {
        int rpcStart = caster.World.RpcSnapshot().Count;
        var views = caster.World.ViewSnapshot();
        ushort before = BitConverter.ToUInt16(caster.World.CreationSnapshot().Last(view => view.ViewId == 2).Attributes["InnerStrengthAvailable"]);
        foreach (byte[] bytes in views)
            if (BuffDrainLifetimeFixture.TryInnerStrength(bytes, out ushort value))
                before = value;

        caster.SkipReceived();
        caster.SendRpc(request);
        await caster.WaitForTextAsync(Notice);
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
