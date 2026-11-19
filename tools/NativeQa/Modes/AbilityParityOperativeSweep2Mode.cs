using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityOperativeSweep2Mode : NativeQaMode
{
    public override string Name => "ability-parity-operative-sweep-2";

    private static readonly ushort[] VerifiedLoadout =
        new ushort[] { 2, 17, 1120, 1121, 1123, 294, 363, 338, 389, 339, 384, 113 }
            .Concat(AbilityPrerequisites.Current.Closure(363).Keys).Distinct().ToArray();

    public override NativeQaModePlan Plan { get; } = new(
        VerifiedLoadout,
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0" },
        readyMessages: new[]
        {
            "Use ?qaunload 363 and ?qaload 363 on the seeded 72-memory profile. At rank 50 record raw +10 for each of Melee Defense Bonus, Ballistic Defense Bonus and Determination, and the corresponding Status changes.",
            "Select Operative Target and activate Knife Thrower Upgrade 1120 and Master Knife Thrower Upgrade 1121. Record only caster bonus adds and removal on replacement. Expected thrown damage/accuracy and evasion: +4/+15/+10, then +6/+18/+10.",
            "Activate Shifty Technique 294 with Operative Target still selected. Record self-only +80 Combat Evasion and its removal after 30s.",
            "As an extra sample, activate Master Assassin Upgrade 1123. Its caster modifiers are +10 Thrown Damage, +7 Thrown Accuracy and +8 Stealth. It has a separate remaining stealth contest check.",
            "Save the stock requests, visible Status and complete on/off modifier records. This fixture can close four rows after review: 294, 363, 1120 and 1121."
        },
        extraQaMetadata: new() { ["NativeCloseableRows"] = 4, ["NativeCloseableIds"] = "294,363,1120,1121" },
        testMobFixture: "ability-parity-operative-sweep-2.csv");

    internal async Task VerifyHeadlessAsync(HeadlessPlayer actor)
    {
        var active = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Select(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(active.ToHashSet().SetEquals(VerifiedLoadout), "Margin sends the legal complete Improved Invisibility reload profile");
        Check.Equal(72, VerifiedLoadout.Sum(AbilityMemory.Cost), "Sweep 2 fits the 100-memory limit");
        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync("Operative Target");

        async Task Modifier(ushort id, ushort stat, int value, float seconds, bool remove = false)
        {
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Any(record =>
                ManageBonusRecord.TryDecode(record.Bytes, out var m) && m!.SourceId == id && m.TargetId == stat &&
                m.SignedValue == value && m.Duration == seconds && m.AddRemoveType == (remove ? 1 : 0)),
                $"operative batch-two modifier {id}/{stat} {value} for {seconds}s (remove={remove})",
                remove ? TimeSpan.FromSeconds(36) : HeadlessPlayer.DefaultTimeout);
        }
        void Cast(ushort id)
        {
            static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));
            actor.SendRpc($"80 b9 {Le(id)} {Le(target.ViewId)}");
        }

        await Modifier(363, 1143, 10, 0);
        await Modifier(363, 1027, 10, 0);
        await Modifier(363, 575, 10, 0);
        Cast(1120);
        await Modifier(1120, 1034, 4, 0);
        await Modifier(1120, 1032, 15, 0);
        await Modifier(1120, 520, 10, 0);
        Cast(1121);
        await Modifier(1121, 1034, 6, 0);
        await Modifier(1121, 1032, 18, 0);
        await Modifier(1121, 520, 10, 0);
        await Modifier(1120, 520, 10, 0, remove: true);
        Cast(1123);
        await Modifier(1123, 1034, 10, 0);
        await Modifier(1123, 1032, 7, 0);
        await Modifier(1123, 209, 8, 0);
        await Modifier(1121, 520, 10, 0, remove: true);
        Cast(294);
        await Modifier(294, 520, 80, 30);
        await Modifier(294, 520, 80, 30, remove: true);
        Console.WriteLine("  ranked Improved Invisibility, three self-only upgrades and replacement, and Shifty Technique expiry verified");
    }
}
