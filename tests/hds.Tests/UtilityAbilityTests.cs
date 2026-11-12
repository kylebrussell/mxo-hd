using System.Xml;
using hds;

namespace hds.Tests;

/// <summary>
/// The utility abilities' rules, from the client's server-side scripts (serverrules.py DoorPickLock, SecurityPBXDisable;
/// ability/spy/misc.py DisarmTraps_Test; rshooks.py ComputeDownloadMissionMapOutcome) and the gob's object data.
/// </summary>
public class UtilityAbilityTests
{
    private static void Load()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "object_states.csv")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        ObjectStates.Load(Path.Combine(dir, "data"));
    }

    [Fact]
    public void ContestIsTheScriptsEvalContest()
    {
        // av = A + aMod + roll; dv = D + dMod + 50
        Assert.Equal(10 + 5 + 40 - (20 + 0 + 50), UtilityAbilities.EvalContest(10, 5, 20, 0, 40));
    }

    [Fact]
    public void PickLockNeedsTheAbilityThenATool()
    {
        Assert.Equal(UtilityAbilities.PickResult.NoAbility, UtilityAbilities.PickLock(0, true));
        Assert.Equal(UtilityAbilities.PickResult.NoTool, UtilityAbilities.PickLock(1, false));
        // "Lock picks always succeed after the progress bar has finished", whatever the level
        Assert.Equal(UtilityAbilities.PickResult.Success, UtilityAbilities.PickLock(1, true));
        Assert.Equal(0x7a000145u, UtilityAbilities.PickLockMessage(UtilityAbilities.PickResult.Success));
        Assert.Equal(0x7a000141u, UtilityAbilities.PickLockMessage(UtilityAbilities.PickResult.NoAbility));
        Assert.Equal(0x7a000142u, UtilityAbilities.PickLockMessage(UtilityAbilities.PickResult.NoTool));
        Assert.Contains(4u, UtilityAbilities.Tools[UtilityAbilities.OpenLocks]); // Lock_Pick
    }

    [Fact]
    public void PickLockMovesLockedObjectsWhereTheirScriptsSay()
    {
        Load();
        // A door's Pick Lock (use 4) goes to ClosedState, which offers Open; a container's (use 8) to UnlockedState
        Assert.Equal(0x835u, ObjectStates.StateAfterUse(638, 0x836, 5, 1));  // TestLockedDoor (Pick Lock is its use 5), which starts locked
        Assert.Equal(0x836u, ObjectStates.DefaultState(638));
        Assert.Equal(0x841u, ObjectStates.StateAfterUse(1416, 0x836, 8, 1)); // Wall Safe
        Assert.True(UtilityAbilities.IsLockable(1416));
        Assert.True(UtilityAbilities.IsLockable(326));   // Filing Cabinet
        Assert.False(UtilityAbilities.IsLockable(3));    // Computer
        Assert.Contains((byte)3, ObjectStates.Offered(1416, 0x841));     // unlocked: Open
        Assert.DoesNotContain((byte)3, ObjectStates.Offered(1416, 0x836)); // locked: no Open
    }

    [Fact]
    public void SecurityDisableFollowsSecurityPbxDisable()
    {
        Assert.Equal(UtilityAbilities.DsdNothing, UtilityAbilities.SecurityDisable(0, true, 0, 10, 99, out string chat));
        Assert.Contains("ability loaded", chat);
        Assert.Equal(UtilityAbilities.DsdNothing, UtilityAbilities.SecurityDisable(1, false, 0, 10, 99, out chat));
        Assert.Contains("disable device tool", chat);
        // Security Systems 0 vs Security_Panel's Difficulty 10: 0 + roll - 60, the alarm under -15 (roll < 45)
        Assert.Equal(UtilityAbilities.DsdAlarm, UtilityAbilities.SecurityDisable(1, true, 0, 10, 44, out chat));
        Assert.Equal("ALARM TRIGGERED", chat);
        Assert.Equal(UtilityAbilities.DsdAccessGranted, UtilityAbilities.SecurityDisable(1, true, 0, 10, 45, out chat));
        Assert.Equal("ACCESS GRANTED", chat);
        Assert.Equal(10, UtilityAbilities.SecurityDifficulty(466));

        Load();
        // The panel's Disable Security Devices: 1 to Disabled (0x83a), 2 to Alarming (0x83b)
        Assert.Equal(0x83au, ObjectStates.StateAfterUse(466, 0x839, 1, 1));
        Assert.Equal(0x83bu, ObjectStates.StateAfterUse(466, 0x839, 1, 2));
    }

    [Fact]
    public void DisarmTrapsFollowsTheScript()
    {
        Assert.Equal(UtilityAbilities.DisarmResult.NotTrapped, UtilityAbilities.DisarmTrap(false, 50, 0, 0, true, 99));
        // A trap nobody detected goes off
        Assert.Equal(UtilityAbilities.DisarmResult.Critical, UtilityAbilities.DisarmTrap(true, 50, 0, 0, false, 99));
        // Level 10, DisarmDifficulty 0: 10 + roll - 50
        Assert.Equal(UtilityAbilities.DisarmResult.Disarmed, UtilityAbilities.DisarmTrap(true, 10, 0, 0, true, 41));
        Assert.Equal(UtilityAbilities.DisarmResult.Failed, UtilityAbilities.DisarmTrap(true, 10, 0, 0, true, 40));
        Assert.Equal(UtilityAbilities.DisarmResult.Failed, UtilityAbilities.DisarmTrap(true, 10, 0, 0, true, 16));
        Assert.Equal(UtilityAbilities.DisarmResult.Critical, UtilityAbilities.DisarmTrap(true, 10, 0, 0, true, 15));
        Assert.Equal(0x7600024eu, UtilityAbilities.DisarmMessage(UtilityAbilities.DisarmResult.Disarmed));
        Assert.Equal(0x7600024fu, UtilityAbilities.DisarmMessage(UtilityAbilities.DisarmResult.Failed));
        Assert.Equal(0x76000250u, UtilityAbilities.DisarmMessage(UtilityAbilities.DisarmResult.Critical));
    }

    [Fact]
    public void MissionMapFlagsFollowTheHook()
    {
        Assert.Equal(0, UtilityAbilities.MissionMapFlags(0, 0, 10, 99));
        // Level 10 x 4 = 40, difficulty 10: 40 + roll - 60
        Assert.Equal(0, UtilityAbilities.MissionMapFlags(10, 0, 10, 19));
        Assert.Equal(1, UtilityAbilities.MissionMapFlags(10, 0, 10, 20));
        Assert.Equal(1 | 2, UtilityAbilities.MissionMapFlags(10, 0, 10, 30));
        Assert.Equal(1 | 2 | 4 | 8 | 16 | 32, UtilityAbilities.MissionMapFlags(10, 0, 10, 70));
        Assert.Equal(1 | 2 | 4, UtilityAbilities.MissionMapFlags(10, 5, 10, 35)); // Map Downloading adds its level
    }

    [Fact]
    public void MissionObjectsCanBeLocked()
    {
        var doc = new XmlDocument();
        doc.LoadXml("<mission id=\"1\" title=\"t\" org=\"3\"><phase site=\"Normal\"><object id=\"1\" kind=\"Safe\" locked=\"1\" />" +
                    "<object id=\"2\" kind=\"Computer\" /><objective goal=\"1\" text=\"x\" type=\"Search\" object=\"1\" item=\"6786\" />" +
                    "</phase></mission>");
        Mission mission = MissionXml.Parse(doc.DocumentElement!);
        Assert.True(mission.phases[0].objects[0].locked);
        Assert.False(mission.phases[0].objects[1].locked);
    }
}
