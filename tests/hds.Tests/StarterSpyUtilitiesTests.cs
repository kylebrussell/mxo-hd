using hds;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class StarterSpyUtilitiesTests
{
    private const ushort DisarmTraps = UtilityAbilities.DisarmTraps;                       // 119
    private const ushort OpenLocks = UtilityAbilities.OpenLocks;                           // 142
    private const ushort DisableSecurityDevices = UtilityAbilities.DisableSecurityDevices; // 218
    private const byte FixtureLevel = 50;

    private static void LoadObjectStates()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "object_states.csv")))
            dir = Path.GetDirectoryName(dir)!;
        ObjectStates.Load(Path.Combine(dir, "data"));
    }

    [Fact]
    public void StarterSpyUtilitiesPlanMemoryToolsHotbarAndObjectTransitionsAreExact()
    {
        LoadObjectStates();
        var plan = NativeQa.NativeQaModes.Find("ability-parity-starter-spy-utilities").Plan;
        Assert.Equal(new[] { DisarmTraps }, plan.HotbarAbilities);

        int mem = plan.Abilities.Sum(AbilityMemory.Cost);
        Assert.Equal(42, mem);
        Assert.True(mem <= AbilityMemory.ForLevel(FixtureLevel));

        Assert.Contains(DisarmTraps, plan.Abilities);
        Assert.Contains(OpenLocks, plan.Abilities);
        Assert.Contains(DisableSecurityDevices, plan.Abilities);

        // Tools seeded in bag slots 1..3 match UtilityAbilities.Tools.
        Assert.Contains(4u, UtilityAbilities.Tools[OpenLocks]);
        Assert.Contains(616u, UtilityAbilities.Tools[DisableSecurityDevices]);

        // Filing Cabinet (326): LockedState 0x836 + Pick Lock (use 8) -> UnlockedState 0x841; also supports TrappedState 0x839.
        Assert.True(UtilityAbilities.IsLockable(326));
        Assert.Equal(0x841u, ObjectStates.StateAfterUse(326, 0x836, 8, 1));
        Assert.NotNull(ObjectStates.Find(326, 0x839));
        Assert.NotNull(ObjectStates.Find(326, 0x83a));
        Assert.NotNull(ObjectStates.Find(326, 0x83b));

        // Security_Desk (377): Armed 0x839 + use 1 -> Disabled 0x83a (result 1) / Alarming 0x83b (result 2), Difficulty 0.
        Assert.Equal(0, UtilityAbilities.SecurityDifficulty(377));
        Assert.Equal(0x83au, ObjectStates.StateAfterUse(377, 0x839, 1, 1));
        Assert.Equal(0x83bu, ObjectStates.StateAfterUse(377, 0x839, 1, 2));

        // Disarm Traps (119): 10 IS, 2.0 s cast.
        Assert.Equal((ushort)10, AbilityCosts.Cost(DisarmTraps));
        Assert.Equal(2f, AbilityCatalog.Find(DisarmTraps)!.CastSeconds);
    }
}
