using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterConcealmentRankMode : NativeQaMode
{
    private const int CharacterLevel = 19;
    private const ushort Parent = 503, Rank = 18, Rifles = 147;
    private const ushort Granted = Stealth.ConcealmentGrantAbility;
    private static readonly ushort[] Roots = { 2, 17, Rifles, Parent };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-concealment-rank";

    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
            $"UPDATE characters SET level={CharacterLevel},exp={PlayerProgression.ExperienceForLevel(CharacterLevel)},y=495 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{CharacterLevel}) WHERE char_id=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110 AND ability_id={0x80000000u | ((uint)Parent << 10)}"
        }, DevCommands: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, readyMessages: new[]
        {
            "Level 19 with rank-18 Concealment (503), 40/49 memory, and a rifle equipped. Unload and reload 503 in the Archive. Capture the removal and restored rank-18 1137 grant frames.",
            "Select Rifles (147). If Actions shows the granted Concealment action, use it and check for Stealth +18. Report any Actions visibility gap separately. With 503 loaded, ?stealth conceal can still sample this rank and modifier through the existing fixture command."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "503,1137", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var rows = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(rows.Keys.ToHashSet().SetEquals(Loadout), "Level-19 Concealment prerequisite profile");
        Check.Equal(Rank, rows[Parent].Level, "The learned parent has rank 18");
        Check.Equal(40, Loadout.Sum(AbilityMemory.Cost), "Concealment uses 40 of 49 memory");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);

        int unload = await StockAbilityProfile.ChangeLoadoutAsync(caster, rows, new[] { Parent }, load: false);
        var removed = await StockAbilityProfile.ObserveAsync(caster, unload,
            StockAbilityProfile.GrantRecord(Granted, remove: true), "Unloading rank-18 Concealment removes 1137");
        int load = await StockAbilityProfile.ChangeLoadoutAsync(caster, rows, new[] { Parent }, load: true);
        var restored = await StockAbilityProfile.ObserveAsync(caster, load,
            packet => packet.Bytes.AsSpan().SequenceEqual(ServerPackets.BuildAbilityLoad(Granted, Rank, AbilityProgression.HiddenSlot)),
            "The live 1137 grant carries its learned parent rank");

        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        await StarterFixedDamageMode.SelectStyle(caster, self, Rifles, 171);
        byte[] use = new AbilityUseRecord(Granted, DuelPackets.Handle(self.ViewId, self.SpawnId),
            0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
        int activate = caster.World.RpcSnapshot().Count;
        caster.SendRpc(use);
        var magnitude = await StockAbilityProfile.ObserveAsync(caster, activate,
            packet => StockAbilityProfile.Bonus(packet, Granted, 209, Rank, 0, false), "Ordinary Concealment publishes Stealth +18");
        await caster.WaitForRpcAsync("80 bd 05 71 04 00 00 00 00 00 01");
        caster.SendRpc(use);
        var off = await caster.WaitForRpcAsync("80 bd 05 71 04 00 00 00 00 00 00");
        Check.Equal(0, caster.World.Problems.Count, "Concealment grant and modifier framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starter-concealment-rank.json"),
            JsonSerializer.Serialize(new { CharacterLevel, ParentRank = Rank, Removed = removed.Hex, Restored = restored.Hex,
                Activation = Convert.ToHexString(use), Stealth = magnitude.Hex, Off = off.Hex },
                new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }
}
