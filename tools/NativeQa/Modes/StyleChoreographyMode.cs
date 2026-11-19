using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class StyleChoreographyMode : NativeQaMode
{
    public override string Name => "style-choreography";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 101, 132, 133, 129, 147, 158, 246, 197, 412, 407, 267, 428, 241, 105, 250 },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000,innerStrC=1500,innerStrM=1500 WHERE charId IN (110,6111)",
            CombatFixtureLoadout.SeedWeapons
        },
        DevCommands: true, environmentOverrides: new()
        {
            [CombatFixtureWeapons.WeaponMode] = "1",
            ["MXO_CONDITIONAL_THROW_QA"] = "1"
        },
        extraQaMetadata: new() { ["ChoreographyNativeStatus"] = "Pending" },
        readyMessages: new[]
        {
            "Target the isolated 30,000 HP Melee Dummy 3 m west. Wait for each special to resolve before pressing the next one.",
            "Use ?combatdepth fixture-weapon unarmed before the martial arts checks.",
            "Self Defense 17: Head Butt 197 requires a Dazed target. Use a landed Speed round or ?combatdepth daze 30 with the dummy selected. Check HeadButt 284.",
            "Aikido 101: Tomo Nage 412 requires an Off Balance target. Use a landed Grab round or ?combatdepth offbalance 30 with the dummy selected. Check 751. Punch Reversal 407 needs no target condition. Check RevSlamChicken 827/828/829 for Self Defense Speed/Power/Grab.",
            "Karate 132: Sky High Sidekick 267 -> 5937. Kung-Fu 133: Suicidal Butterfly 428 -> 8084.",
            "For gun choreography, use the style-choreography-handgun, style-choreography-rifle and style-choreography-dual-smg modes. Each gun starts in the hand slot at login.",
            "To investigate a true mid-session swap, use ?combatdepth fixture-weapon rifle or dual-smg. Verify the Free Attack dropdown selection before trying ranged Fire!.",
            "Log each landed special name and move ID, compare the motion on screen and save the full outgoing RPC. An ordinary miss does not establish special playback."
        }, testMobFixture: CombatFixtureLoadout.MeleeCsv, loadAbilityAncestors: true);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("A disposable fixture database is required.");
        await CombatFixtureLoadout.AssertWeaponSeedAsync(database);
        using var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        await observer.EnterWorldAsync();
        var dummy = await observer.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(observer, dummy);
        return Array.Empty<byte>();
    }
}

public abstract class GunStyleChoreographyMode : NativeQaMode
{
    public override string Name { get; }
    public override NativeQaModePlan Plan { get; }

    internal uint Weapon { get; }
    internal ushort Style { get; }
    internal ushort Special { get; }
    internal uint Animation { get; }
    internal bool RequiresOffBalance { get; }

    protected GunStyleChoreographyMode(string weaponName, uint weapon, string style, ushort styleId,
        string special, ushort specialId, uint animation, string stance, bool requiresOffBalance = false)
    {
        Name = $"style-choreography-{weaponName}";
        Weapon = weapon;
        Style = styleId;
        Special = specialId;
        Animation = animation;
        RequiresOffBalance = requiresOffBalance;
        Plan = new NativeQaModePlan(
            new ushort[] { 2, 17, styleId, specialId, 330 },
            CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(),
            new[]
            {
                $"UPDATE characters SET y={CombatFixtureLoadout.GroundY},healthC=5000,healthM=5000,innerStrC=1500,innerStrM=1500 WHERE charId=110",
                CombatFixtureLoadout.SeedWeaponsFor(weapon)
            },
            extraQaMetadata: new() { ["ChoreographyNativeStatus"] = "Pending", ["LoginWeaponGoid"] = weapon },
            readyMessages:
            [
                $"CSR starts with {weaponName} ({weapon}) in hand slot 105. Leave the gun equipped.",
                $"Select {style} ({styleId}) and target the isolated 30,000 HP Melee Dummy 3 m west. Use the stock Close Combat action to open an interlock.",
                "If Close Combat is unavailable, land Logic Barrage while at rest and wait for the dummy to initiate interlock.",
                $"The stock Close Combat slot can switch to Self Defense (17) before opening. Once interlocked, reselect {style} ({styleId}) in Actions and confirm it is active.",
                .. (requiresOffBalance ? new[]
                {
                    "Pistol Slide requires the dummy to be Off Balance. Select the stock Grab tactic and wait for a landed round that applies Off Balance (condition 04). Press Pistol Slide during the fresh 10-second window, while its slot is enabled."
                } : Array.Empty<string>()),
                $"Use {special} ({specialId}) and check for landed animation {animation}. Record the opening stance separately when the gun style is active at opening ({stance}).",
                "Record the opening request and the complete outgoing ability RPC. If the gun or action is greyed, retain the tooltip and the preceding draw, selection and target records."
            ], testMobFixture: CombatFixtureLoadout.MeleeCsv, loadAbilityAncestors: true);
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("A disposable fixture database is required.");
        await CombatFixtureLoadout.AssertWeaponSeedAsync(database, Weapon);
        using var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        await observer.EnterWorldAsync();
        var dummy = await observer.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(observer, dummy);
        return Array.Empty<byte>();
    }
}

public sealed class HandgunStyleChoreographyMode() :
    GunStyleChoreographyMode("handgun", CombatFixtureWeapons.Handgun, "Handguns", 129, "Pistol Slide", 241, 9527, "21052–21055", requiresOffBalance: true);

public sealed class RifleStyleChoreographyMode() :
    GunStyleChoreographyMode("rifle", CombatFixtureWeapons.Rifle, "Rifles", 147, "Body Shot", 105, 11364, "21068–21071");

public sealed class DualSmgStyleChoreographyMode() :
    GunStyleChoreographyMode("dual-smg", CombatFixtureWeapons.DualSmgs, "Submachine Guns", 158, "Controlled Burst", 250, 21082, "21076–21079");
