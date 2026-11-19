using hds;

namespace NativeQa.Modes;

public sealed class GunmanParityMode : NativeQaMode
{
    public override string Name => "gunman-upgrades-and-styles";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 125, 128, 129, 147, 158, 239, 240, 242, 440, 441, 1060, 1070, 1074, 1075, 1076 },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            CombatFixtureLoadout.SeedWeapons
        },
        DevCommands: true,
        environmentOverrides: new() { [CombatFixtureWeapons.WeaponMode] = "1" },
        readyMessages: new[]
        {
            "Gunman Upgrade 1070, Expert Rifleman Upgrade 1074, Gunslinger Upgrade 1075, Duelist Upgrade 1076, Operative Upgrade 1060. Select Melee Dummy and use each from Actions. Verify it affects only CSR and replaces the previous upgrade.",
            "Compare Status deltas in order: Ballistic Accuracy / Max Health / Ballistic Damage / Ballistic Defense = 4/70/2/4, 9/120/4/4, 4/100/2/6, 3/110/4/10. Operative gives +4 each melee and ballistic accuracy with the previous max-health and defense removed.",
            "Use actual hand equip and select 129, 147 and 158 in separate interlocks. The Close Combat slot may switch to Self Defense. Record each equipped style, the exact round/stand records and status changes.",
            "With Handguns active in interlock, land Pistol Evasive 242. Record outgoing cast, whether it queues, and the hit, then caster-only +20 thrown, ballistic and melee defense for 12 seconds. Record expiry and any distinct Block presentation."
        }, testMobFixture: CombatFixtureLoadout.MeleeCsv, loadAbilityAncestors: true);
}
