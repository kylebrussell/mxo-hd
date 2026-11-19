using hds;

namespace NativeQa.Modes;

public abstract class FreeAttackCandidateMode : NativeQaMode
{
    public override string Name { get; }
    public override NativeQaModePlan Plan { get; }

    protected FreeAttackCandidateMode(string name, bool testLogin, string[] instructions)
    {
        Name = name;
        var environment = new Dictionary<string, string> { [CombatFixtureWeapons.WeaponMode] = "1" };
        if (testLogin) environment[FreeAttackBootstrap.Mode] = FreeAttackBootstrap.Login;
        Plan = new NativeQaModePlan(
            new ushort[] { 2, 17, 129, 147 }, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
            Array.Empty<string>(), Array.Empty<string>(),
            new[]
            {
                $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000 WHERE charId=110",
                CombatFixtureLoadout.SeedWeapons
            },
            DevCommands: true,
            environmentOverrides: environment,
            readyMessages: instructions, testMobFixture: CombatFixtureLoadout.MeleeCsv, loadAbilityAncestors: true);
    }
}

public sealed class FreeAttackBootstrapMode() : FreeAttackCandidateMode("free-attack-bootstrap", true, new[]
{
    "Start a fresh client process. The Redpill Special revolver (2565) starts equipped.",
    "Capture initial 3f fists, loading complete and the first stock 3e in-world selection. The server then sends 5f from hand slot 105 and 5e into a free bag slot.",
    "Capture any automatic bag selection. The server sends 65 back to slot 105 on the selection report or after two seconds.",
    "Check the revolver stays equipped with no duplicate in bag. Record any automatic 3f kind 16, especially the final hand slot 105.",
    "Right-click Melee Dummy and choose Fire!. Capture 3d, 41 and companion 40, plus the shot and UI result."
});

public sealed class FreeAttackEquipMode() : FreeAttackCandidateMode("free-attack-equip", false, new[]
{
    "Start a fresh client process with Free Attack showing fists. The Redpill Special starts equipped and rifle 692 is in bag slot 11.",
    "Use ?combatdepth fixture-weapon rifle. Capture 5f and 5e for bag slot 11, both 65 moves, and the own equipped attribute.",
    "Confirm one rifle in hand and one revolver in the bag. Record any automatic 3f kind 16, especially the final hand slot 105.",
    "Right-click Melee Dummy and choose Fire!. Capture 3d, 41 and companion 40, plus the shot and UI result."
});
