using hds;
using hds.shared;

namespace hds.Tests;

/// <summary>
/// Object state machines from the client's gameobjects.gob (data/object_states.csv, data/object_usables.csv), checked
/// against what the real client and retail captures showed.
/// </summary>
public class ObjectStatesTests
{
    private const ushort Computer = 3, WallSafe = 1416, Wardrobe = 7538, DojoDoor = 6994, WarehouseDoor = 6617, Booth = 592;

    private static void Load()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "object_states.csv")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        ObjectStates.Load(Path.Combine(dir, "data"));
    }

    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void UseNumbersAreWhatTheClientSends()
    {
        Load();

        // Tried in the client (PR 47): a computer's Hack is 80 c7 ... 04, Take 05; a container's Open 03
        Assert.Equal((byte)4, ObjectStates.UseNumber(Computer, "Hack"));
        Assert.Equal((byte)5, ObjectStates.UseNumber(Computer, "Take"));
        Assert.Equal((byte)8, ObjectStates.UseNumber(Computer, "Put"));
        Assert.Equal((byte)3, ObjectStates.UseNumber(WallSafe, "Open"));
        Assert.Equal((byte)3, ObjectStates.UseNumber(Wardrobe, "Open"));

        // Retail booth uses: 03 jack out, 04 upload, 05 the Loading Area, 08 sync
        Assert.Equal((byte)3, ObjectStates.UseNumber(Booth, "Exit"));
        Assert.Equal((byte)4, ObjectStates.UseNumber(Booth, "Upload"));
        Assert.Equal((byte)5, ObjectStates.UseNumber(Booth, "Loading Area"));
        Assert.Equal((byte)8, ObjectStates.UseNumber(Booth, "Synchronize"));
    }

    [Fact]
    public void ComputerStatesOfferWhatTheClientShowed()
    {
        Load();

        Assert.Equal(0x960u, ObjectStates.DefaultState(Computer));
        Assert.Empty(ObjectStates.Offered(Computer, 0x960));
        Assert.Equal(new byte[] { 5 }, ObjectStates.Offered(Computer, 0x961).ToArray());
        Assert.Equal(new byte[] { 4 }, ObjectStates.Offered(Computer, 0x963).ToArray());
        Assert.Equal(new byte[] { 4 }, ObjectStates.Offered(Computer, 0x964).ToArray());
        // Put, which the client only shows to a player carrying an item with the computer's mission key
        Assert.Equal(new byte[] { 8 }, ObjectStates.Offered(Computer, 0x962).ToArray());
        Assert.Equal("ST_UnhackedGiver", ObjectStates.Find(Computer, 0x964)!.name);
    }

    [Fact]
    public void StateOfferingPicksTheComputerStates()
    {
        Load();

        Assert.Equal(0x961u, ObjectStates.StateOffering(Computer, "Take"));
        Assert.Equal(0x963u, ObjectStates.StateOffering(Computer, "Hack"));
        Assert.Equal(0x962u, ObjectStates.StateOffering(Computer, "Put"));
        Assert.Equal(0x964u, ObjectStates.StateOffering(Computer, "Hack", "Take"));
        Assert.Equal(0x965u, ObjectStates.StateOffering(Computer, "Hack", "Put"));
        Assert.Equal(0u, ObjectStates.StateOffering(Computer, "Open"));

        Assert.Equal(0x835u, ObjectStates.StateOffering(WallSafe, "Open"));
        Assert.Equal(0x836u, ObjectStates.StateOffering(WallSafe, "Pick Lock"));
    }

    [Fact]
    public void UsesMoveObjectsAsRetailsScripts()
    {
        Load();

        // A computer's Take or Hack goes to the state's next: Giver back to Normal, UnhackedGiver to Giver
        Assert.Equal(0x960u, ObjectStates.StateAfterUse(Computer, 0x961, 5));
        Assert.Equal(0x961u, ObjectStates.StateAfterUse(Computer, 0x964, 4));
        Assert.Equal(0x960u, ObjectStates.StateAfterUse(Computer, 0x963, 4));

        // A container's Open to OpenedState (2: the trap went off); Pick Lock and Unlock to UnlockedState
        Assert.Equal(0x834u, ObjectStates.StateAfterUse(WallSafe, 0x835, 3));
        Assert.Equal(0x83bu, ObjectStates.StateAfterUse(WallSafe, 0x835, 3, 2));
        Assert.Equal(0x841u, ObjectStates.StateAfterUse(WallSafe, 0x836, 8));
        Assert.Equal(0x834u, ObjectStates.OpenedState(Wardrobe));

        // The tutorial door: locked (0x836) offers Pick Lock and Unlock; picking it leaves it closed (0x835), which offers
        // Open, as retail's update after goal 20 did
        Assert.Equal(new byte[] { 4, 8 }, ObjectStates.Offered(DojoDoor, 0x836).OrderBy(b => b).ToArray());
        Assert.Equal(0x835u, ObjectStates.StateAfterUse(DojoDoor, 0x836, 4));
        Assert.Equal(new byte[] { 3 }, ObjectStates.Offered(DojoDoor, 0x835).ToArray());

        Assert.Equal(0u, ObjectStates.StateAfterUse(Computer, 0x961, 6));
    }

    [Fact]
    public void StateUpdatesUseEachClassMask()
    {
        Load();

        Assert.Equal("0108" + "61090000", Hex(ObjectStates.BuildStateUpdate(Computer, 0x961)!));
        Assert.Equal("0120" + "35080000", Hex(ObjectStates.BuildStateUpdate(Wardrobe, 0x835)!));
        Assert.Equal(Hex(DoorLifecycle.BuildStateUpdate(0x838)), Hex(ObjectStates.BuildStateUpdate(DojoDoor, 0x838)!));
    }

    [Fact]
    public void DoorsCloseOnTheirOwnTiming()
    {
        Load();

        Assert.Equal((20u, 1u), ObjectStates.DoorTiming(DojoDoor));
        Assert.Equal((20u, 5u), ObjectStates.DoorTiming(WarehouseDoor));

        // The retail capture's door (20 s, then 1 s) and an AttributeClass365 door that takes 5 s to close
        OpenDoor door = new OpenDoor { ViewId = 1, GoId = WarehouseDoor, OpenedAt = 100, Stage = DoorStage.Open };
        Assert.Equal(DoorStage.Closing, DoorLifecycle.NextStage(door, 120));
        door.Stage = DoorStage.Closing;
        Assert.Null(DoorLifecycle.NextStage(door, 124.9));
        Assert.Equal(DoorStage.Closed, DoorLifecycle.NextStage(door, 125));
        door.Stage = DoorStage.Closed;
        Assert.False(DoorLifecycle.ShouldRemove(door, 129.9));
        Assert.True(DoorLifecycle.ShouldRemove(door, 130));
    }

    [Fact]
    public void ThePhoneHasAUsableAndNoStates()
    {
        Load();

        ObjectStates.Usable use = ObjectStates.UsableLabelled(1401, "Use")!;
        Assert.Equal((byte)2, use.use);
        Assert.Equal(1006u, use.executeRule);
        Assert.Empty(ObjectStates.States(1401));
        // It starts off; AttributeClass128's ActiveTracker turns it on, as TalkActiveTracker does an NPC's Talk
        Assert.Equal("ActiveTracker", use.tracker);
        Assert.False(use.startsOn);
        Assert.Equal("TextActiveTracker", ObjectStates.UsableLabelled(Computer, "Search")!.tracker);
    }
}
