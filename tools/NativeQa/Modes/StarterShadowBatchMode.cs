using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class StarterShadowBatchMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort InvisibilityPassive = 175, ShadowDiscipline = 290, DesperateSpeed = 291;
    private const ushort InvisibilityGrant = 1116, ShadowUpgrade = 1117;
    private const uint InvisibilityBeltGoid = 460;
    private const byte FixtureLevel = 50, FixtureRank = 50;

    private static readonly ushort[] ReloadOrder = { InvisibilityPassive, DesperateSpeed, ShadowDiscipline };
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, InvisibilityPassive, DesperateSpeed, ShadowDiscipline };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-shadow";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, 17000, 2300,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(FixtureLevel)},x=CASE WHEN charId=6111 THEN 17200 ELSE 17000 END,y=495,z=2300," +
                "alignment=1,pvpflag=0,healthC=3000,healthM=3000,innerStrC=2000,innerStrM=2000 WHERE charId IN(110,6111)",
            $"UPDATE char_abilities SET level={FixtureRank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}",
            $"DELETE FROM inventory WHERE charId=110 AND (slot=40 OR goid={InvisibilityBeltGoid}); " +
                $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{InvisibilityBeltGoid},40,1,0)"
        },
        DevCommands: true, loadAbilityAncestors: false,
        hotbarAbilities: new ushort[] { SpyStyle, ShadowUpgrade, DesperateSpeed },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "175,290,291,1116,1117",
            ["NativeCloseableRows"] = 1,
            ["BehaviorTestRows"] = "175,290,291,1116,1117",
            ["InvisibilityBeltGoid"] = InvisibilityBeltGoid,
            ["BaselineInvisSeconds"] = 130,
            ["UpgradedInvisSeconds"] = 133,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(FixtureLevel)
        },
        readyMessages: new[]
        {
            "Level 50 rank 50 unarmed Shadow starter batch (175 Invisibility, 290 Shadow [closure candidate], 291 Desperate Speed, 1116 Invisibility grant via Invisibility Belt GOID 460 bag 40, 1117 Shadow Upgrade). QAFriend stands 2 m east as friendly observer. Clear target selection throughout.",
            "Step A (Passives & grants 175/290). Inspect Status Bonuses on login (Invis Movement Speed Bonus 1051 includes +25 from rank-50 175) and verify Actions shows Shadow Upgrade (1117) and Actions > Tools shows Invisibility Belt. Run '?qaunload 290, 1128, 291, 175' (46 -> 37 memory; Status loses 1051 +25; 1117/1116 grants removed), then '?qaload 175, 291, 1128, 290' restoring exact login Status and both grants.",
            "Step B (1116 Invisibility baseline vs 1117 Shadow Upgrade +3 s duration). Press page 2 slot 1 (Spy Style 1112). Activate Actions > Tools > Invisibility Belt (GOID 460, bag 40): expect 50 IS spent, 3.0 s cast bar, 30.0 s own reuse, and '[STEALTH] CSR_Tester 1116 on (bits 1, SL 0, 130 s)'. Toggle Invisibility off (re-activate belt or wait 3 s and cancel). Press page 2 slot 2 (1117 Shadow Upgrade): expect 20 IS spent, 30.0 s own reuse, '[BUFF] 1117 on' (+6 Contested Withdraw 1005, +5 Thrown Acc 1032, +3 Invis Dur 644, +10 Invis Move 878). Once 1116's 30 s reuse finishes, activate Actions > Tools > Invisibility Belt again: expect '[STEALTH] CSR_Tester 1116 on (bits 1, SL 0, 133 s)' (+3 s from 1117). Toggle Invisibility off, then press slot 2 (1117) again for clean '[BUFF] 1117 off'.",
            "Step C (291 Desperate Speed). Press page 2 slot 3 (Desperate Speed 291): expect 50 IS spent, 298.0 s own reuse, '[BUFF] 291 on for 15 s' (+40 Movement Speed 115 / MovementScale 0.40 on own & QAFriend view, +20 Sneak Movement 877, +20 Invis Movement 878). Wait ~16 s and confirm '[BUFF] 291 off' restores MovementScale 0."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", observer);
        await observer.EnterWorldAsync();
        return Array.Empty<byte>();
    }
}
