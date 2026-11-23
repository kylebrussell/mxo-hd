using System.Xml;
using hds;
using hds.shared;

namespace hds.Tests;

/// <summary>
/// The utility abilities' rules, from the client's server-side scripts (serverrules.py DoorPickLock, SecurityPBXDisable;
/// ability/spy/misc.py DisarmTraps_Test; rshooks.py ComputeDownloadMissionMapOutcome) and the gob's object data.
/// </summary>
[Collection("AbilityParityWorld")]
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

    [Theory]
    [InlineData(0, 0u, 0x60000025u)]
    [InlineData(0, 100u, 0x60000025u)]
    [InlineData(4, 0u, 0x60000027u)]
    [InlineData(4, 100u, 0u)]
    public void HackSelectionUsesServerAbilityAndFactionWithoutSpendingResources(int level, uint faction, uint refusal)
    {
        WorldClient client = HackSelectionPlayer(level, faction);
        // The requested faction and construct IDs differ from the player's actual faction.
        ReceiveHackSelection(client, "d20400002e160000");

        byte[] expected = refusal == 0
            ? ServerPackets.BuildSystemChatMessage("Faction construct hacking is unavailable: this server has no faction construct instances. No information was charged.", "SYSTEM")
            : DuelPackets.BuildSystemString(refusal);
        var response = Assert.Single(client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>());
        int sizePrefix = response.content[0] >= 0x80 ? 2 : 1;
        Assert.Equal(expected, response.content[sizePrefix..]);
        Assert.Empty(client.messageQueue.ObjectMessagesQueue);
        Assert.Equal(9000L, client.playerData.getInfo());
        Assert.Equal((ushort)80, NumericalUtils.ByteArrayToUint16(client.playerInstance.InnerStrengthAvailable.getValue(), 1));
    }

    [Theory]
    [InlineData("d20400002e1600", true, false, MxOLocations.SLUMS)]
    [InlineData("d20400002e16000000", true, false, MxOLocations.SLUMS)]
    [InlineData("d20400002e160000", false, false, MxOLocations.SLUMS)]
    [InlineData("d20400002e160000", true, true, MxOLocations.SLUMS)]
    [InlineData("d20400002e160000", true, false, MxOLocations.LA)]
    public void HackSelectionIgnoresBadSizesAndRequestsOutsideTheWorld(string payload, bool onWorld, bool pending, MxOLocations district)
    {
        WorldClient client = HackSelectionPlayer(4, 100);
        client.playerData.setOnWorld(onWorld);
        client.playerData.worldChangePending = pending;
        client.playerData.setDistrictId((uint)district);

        ReceiveHackSelection(client, payload);

        Assert.Empty(client.messageQueue.RPCMessagesQueue);
        Assert.Empty(client.messageQueue.ObjectMessagesQueue);
        Assert.Equal(9000L, client.playerData.getInfo());
    }

    private static WorldClient HackSelectionPlayer(int level, uint faction)
    {
        var client = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.setInfo(9000);
        client.playerInstance.FactionID.setValue(faction);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)80);
        if (level != 0)
        {
            client.playerData.passives.Loaded[UtilityAbilities.HackConstructKey] = (ushort)level;
        }
        return client;
    }

    private static void ReceiveHackSelection(WorldClient client, string payload)
    {
        byte[] body = Convert.FromHexString(payload);
        byte[] wire = new byte[] { 1, 0, 1, 1, (byte)(body.Length + 2), 0x80, 0xf6 }.Concat(body).ToArray();
        WorldClient previous = Store.currentClient;
        try
        {
            Store.currentClient = client;
            Assert.Equal(wire.Length, new Protocol04Manager().parse(0, ref wire));
        }
        finally
        {
            Store.currentClient = previous;
        }
    }

    [Fact]
    public void DownloadedMapSurvivesRebuildUntilAbilityLeavesMembership()
    {
        var client = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)10);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        var loaded = new (ushort, ushort)[] { (UtilityAbilities.DownloadMissionMap, 10) };
        var previousRoll = UtilityAbilities.Roll;
        try
        {
            UtilityAbilities.Roll = () => 99;
            AbilityPassives.SetLoaded(client, loaded);
            UtilityAbilities.OnMissionAccepted(client);
            Assert.Equal(63, UtilityAbilities.MissionMapAccess(client));
            AbilityPassives.SetLoaded(client, loaded.Concat(new (ushort, ushort)[] { (2, 10) }));
            Assert.Equal(63, UtilityAbilities.MissionMapAccess(client));

            AbilityPassives.SetLoaded(client, Array.Empty<(ushort, ushort)>());
            Assert.Equal(0, UtilityAbilities.MissionMapAccess(client));
            AbilityPassives.SetLoaded(client, loaded);
            Assert.Equal(0, UtilityAbilities.MissionMapAccess(client));

            UtilityAbilities.OnMissionAccepted(client);
            Assert.Equal(63, UtilityAbilities.MissionMapAccess(client));
        }
        finally
        {
            UtilityAbilities.Roll = previousRoll;
        }
    }

    [Fact]
    public void RecallMissionTeamRequiresLoadedHackerAtRestAndNeedsNoSelection()
    {
        const ushort team = UtilityAbilities.RecallMissionTeam, member = UtilityAbilities.RecallTeamMember;
        var client = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(14948);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        var previousClients = WorldServer.Clients;
        var combat = CombatManager.GetHandler(client);

        try
        {
            WorldServer.Clients = new();
            Assert.Equal(AbilityRefusals.NotLoaded, UtilityAbilities.RecallRefusal(client, team, 0, null, out _));
            AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (17, 50), (CombatStyles.HackerStyle, 50), (team, 50), (member, 50) });
            Assert.Equal(AbilityRefusals.WrongStyle, UtilityAbilities.RecallRefusal(client, team, 0, null, out _));
            CombatStyles.Select(client, CombatStyles.HackerStyle);
            combat.isCombatRunning = true;
            Assert.Equal(AbilityRefusals.MustBeOutsideCombat, UtilityAbilities.RecallRefusal(client, team, 0, null, out _));
            combat.isCombatRunning = false;
            Assert.Null(UtilityAbilities.RecallRefusal(client, team, 0, null, out var target));
            Assert.Null(target);
            Assert.Equal(AbilityRefusals.InvalidTarget, UtilityAbilities.RecallRefusal(client, member, 0, null, out _));
        }
        finally
        {
            combat.isCombatRunning = false;
            CombatManager.RemoveClient(client);
            WorldServer.Clients = previousClients;
        }
    }

    [Fact]
    public void RecallCannotInterruptARecipientWorldTransition()
    {
        var recipient = new ClientData();
        recipient.setDistrictId(1);
        Assert.False(UtilityAbilities.RecallRecipientInWorld(recipient));

        recipient.setOnWorld(true);
        Assert.True(UtilityAbilities.RecallRecipientInWorld(recipient));
        recipient.worldChangePending = true;
        Assert.False(UtilityAbilities.RecallRecipientInWorld(recipient));

        recipient.worldChangePending = false;
        recipient.waitForRPCShutDown = true;
        Assert.False(UtilityAbilities.RecallRecipientInWorld(recipient));
        recipient.waitForRPCShutDown = false;
        recipient.setDistrictId((uint)MxOLocations.LA);
        Assert.False(UtilityAbilities.RecallRecipientInWorld(recipient));
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
