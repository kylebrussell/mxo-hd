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

        // The mainframe's Put opens after the disk is taken: still Hack, the only state that lets it be used
        progress.Complete(goals[1]);
        Assert.Equal(MissionStaging.ComputerHack, MissionStaging.ComputerStateFor(progress, Object(progress, 2, 357)));
    }
}
