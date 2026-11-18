using System.Xml;
using hds;

namespace hds.Tests;

/// <summary>What a mission object offers the player (docs/MISSIONS.md, "Clicking them" and "Computers need a state").</summary>
public class MissionObjectTests
{
    private const string Computer = @"<mission id=""9001"" title=""C"" description=""D"" plot=""1"" order=""1"" exp=""10"" info=""10"">
        <phase site=""Office"">
          <object id=""1"" kind=""Computer"" />
          <object id=""2"" kind=""Mainframe"" />
          <objective goal=""1"" text=""Get the disk from the computer"" type=""Search"" object=""1"" item=""6831"" />
          <objective goal=""2"" text=""Hack the mainframe"" type=""Search"" object=""2"" />
          <objective goal=""3"" text=""Put the disk in the mainframe"" type=""Put"" object=""2"" item=""6831"" after=""1"" />
        </phase>
      </mission>";

    private static MissionProgress Progress()
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(Computer);
        return new MissionProgress(MissionXml.Parse(doc.DocumentElement!));
    }

    private static MissionObjectState Object(MissionProgress progress, int id, ushort goid)
    {
        return new MissionObjectState { def = progress.Phase!.objects.First(o => o.id == id), goid = goid };
    }

    [Fact]
    public void ComputersAreTheClientsStateMachineObjects()
    {
        Assert.True(MissionStaging.IsComputer(3));     // Computer
        Assert.True(MissionStaging.IsComputer(357));   // MainFrame_Computer
        Assert.False(MissionStaging.IsComputer(1416)); // Wall Safe
    }

    [Fact]
    public void ComputersAndContainersAreClickedTheRestAreReached()
    {
        Assert.True(MissionStaging.IsClickable(3));
        Assert.True(MissionStaging.IsClickable(1416));  // Wall Safe
        Assert.True(MissionStaging.IsClickable(326));   // Filing Cabinet
        Assert.True(MissionStaging.IsClickable(7538));  // Wardrobe
        Assert.False(MissionStaging.IsClickable(1401)); // Blinking_Desk_Phone
    }

    [Theory]
    [InlineData(326, false)]
    [InlineData(1401, true)]
    public void MissionPhonesKeepTheirSourceAndPresentAsDeskPhones(ushort sourceGoid, bool bound)
    {
        var source = new StaticWorldObject
        {
            metrId = 2, mxoStaticId = 42, type = BitConverter.GetBytes(sourceGoid),
            pos_x = 10, pos_y = 145, pos_z = 20, quat = "0000000000000000000000000000803f"
        };
        var state = new MissionObjectState { def = new MissionObjectDef { kind = "Phone" }, spot = source, goid = sourceGoid };
        Assert.Equal((ushort)1401, state.PresentationGoid);
        Assert.False(MissionStaging.IsClickable(state.PresentationGoid));

        var phone = new AttributeClass128("Blinking_Desk_Phone", state.PresentationGoid);
        MissionStaging.ConfigureSourceBinding(phone, state);
        Assert.Equal(bound ? source.mxoStaticId : uint.MaxValue, phone.GetRelatedStaticObjId());
        var pose = MissionObjectPoses.ForPresentation(state);
        Assert.Equal((source.pos_x, source.pos_z), (pose.X, pose.Z));
        if (bound) Assert.Equal(source.pos_y, pose.Y);
        else Assert.InRange(pose.Y, 160.14, 160.15);
        Assert.Equal(sourceGoid, state.goid);
        Assert.Equal(145, source.pos_y);
    }

    [Fact]
    public void APhoneBesideClutterUsesTheFirstTopThatClearsTheScene()
    {
        var source = new MissionComputerAnchors.Pose(10, 80, 20, Convert.FromHexString("0000000000000000000000000000803f"));
        var coverage = new MissionOpenFloorResolver.Bounds(-200, 200, -200, 200);
        var box = new MissionOpenFloorResolver.StaticObstacle("box", null, null, 140, 230,
            new MissionOpenFloorResolver.Bounds(-30, 40, -15, 75).Polygon, "observed box");
        var wall = new MissionOpenFloorResolver.StaticObstacle("wall", null, null, 255, 450,
            new MissionOpenFloorResolver.Bounds(-150, 150, -15, 75).Polygon, "observed wall");
        var scene = new MissionOpenFloorResolver.StaticScene(1, true, coverage, 0, 500, new[] { box, wall }, "complete test scene");

        Assert.True(MissionPhonePlacement.TryClearSurface(scene, 42, source, 162, null, out var phone));
        Assert.InRange(phone.Y, 230.14, 230.15);
        Assert.Equal((source.X, source.Z), (phone.X, phone.Z));
        Assert.Equal(80, source.Y);
    }

    [Fact]
    public void AFreePhoneFitsAnExposedStripBetweenTabletopProps()
    {
        var source = new MissionComputerAnchors.Pose(0, 0, 0, Convert.FromHexString("0000000000000000000000000000803f"));
        var tabletop = new MissionOpenFloorResolver.Bounds(-40, 40, -80, 80);
        var near = new MissionOpenFloorResolver.StaticObstacle("near", null, null, 50, 70,
            new MissionOpenFloorResolver.Bounds(-40, 40, -80, -20).Polygon, "props before gap");
        var far = new MissionOpenFloorResolver.StaticObstacle("far", null, null, 50, 70,
            new MissionOpenFloorResolver.Bounds(-40, 40, 40, 80).Polygon, "props after gap");
        var scene = new MissionOpenFloorResolver.StaticScene(1, true, tabletop, 0, 500, new[] { near, far }, "complete test scene");

        Assert.True(MissionPhonePlacement.TryClearSurface(scene, 42, source, 50, tabletop, out var phone));
        Assert.Equal(0, phone.X);
        Assert.InRange(phone.Z, 4, 5);
        Assert.InRange(phone.Y, 50.14, 50.15);
    }

    [Fact]
    public void AComputerOffersTakeForAnItemAndHackOtherwise()
    {
        MissionProgress progress = Progress();

        Assert.Equal(MissionStaging.ComputerTake, MissionStaging.ComputerStateFor(progress, Object(progress, 1, 3)));
        Assert.Equal(MissionStaging.ComputerHack, MissionStaging.ComputerStateFor(progress, Object(progress, 2, 357)));
    }

    [Fact]
    public void AComputerWithNoOpenGoalOffersNothing()
    {
        MissionProgress progress = Progress();
        List<MissionObjective> goals = progress.Phase!.missionObjectives;

        progress.Complete(goals[0]);
        Assert.Equal(MissionStaging.ComputerIdle, MissionStaging.ComputerStateFor(progress, Object(progress, 1, 3)));

        // The mainframe's Put opens after the disk is taken, exposing native Put with the matching key.
        progress.Complete(goals[1]);
        Assert.Equal(MissionStaging.ComputerPut, MissionStaging.ComputerStateFor(progress, Object(progress, 2, 357)));
    }
}
