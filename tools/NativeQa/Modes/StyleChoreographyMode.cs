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
        DevCommands: true, environmentOverrides: new() { [CombatFixtureWeapons.WeaponMode] = "1" },
        extraQaMetadata: new() { ["ChoreographyNativeStatus"] = "Pending" },
        readyMessages: new[]
        {
            "Target the isolated 30,000 HP Melee Dummy 3 m west. Wait for each special to resolve before pressing the next one.",
            "Use ?combatdepth fixture-weapon unarmed before the martial arts checks.",
            "Self Defense 17: Head Butt 197 -> HeadButt 284.",
            "Aikido 101: Tomo Nage 412 -> 751. Punch Reversal 407 -> RevSlamChicken 827/828/829 for Self Defense Speed/Power/Grab.",
            "Karate 132: Sky High Sidekick 267 -> 5937. Kung-Fu 133: Suicidal Butterfly 428 -> 8084.",
            "Use ?combatdepth fixture-weapon handgun, re-enter interlock, and select Handguns 129: stand 21052-21055, Pistol Slide 241 -> 9527.",
            "Use ?combatdepth fixture-weapon rifle, re-enter interlock, and select Rifles 147: stand 21068-21071, Body Shot 105 -> 11364.",
            "Use ?combatdepth fixture-weapon dual-smg, re-enter interlock, and select Submachine Guns 158: stand 21076-21079, Controlled Burst 250 -> 21082 after the cast.",
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
