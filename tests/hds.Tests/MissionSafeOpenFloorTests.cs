using hds;
using System.Text.Json;
namespace hds.Tests;
public class MissionSafeOpenFloorTests
{
    private static StaticWorldObject Source()=>new(){metrId=1,sectorID=209,staticId=62,mxoStaticId=0x0d10003e,type=BitConverter.GetBytes((ushort)1410),pos_x=27300,pos_y=2146,pos_z=13100,quat="00000000f204353f00000000f304353f"};
    private static MissionPhase Phase()=>new(){objects=new(){new MissionObjectDef{id=1,kind="Safe"}}};
    [Fact] public void ActualChooserAndRestoreExpectationShareTheGeneratedSourceClassification()
    {
        var raw=Source();string before=JsonSerializer.Serialize(raw,MissionProgress.SaveJson);var site=new MissionSite{metrId=1};site.objectSpots.Add(raw);var phase=Phase();
        var choice=Assert.Single(MissionStaging.SelectObjects(phase,site,new Random(220110)));
        Assert.Same(raw,choice.Spot);Assert.Equal((ushort)552,choice.Goid);
        Assert.Equal(choice.Goid,MissionStaging.ExpectedConcrete(phase.objects[0],raw));
        Assert.Equal(before,JsonSerializer.Serialize(raw,MissionProgress.SaveJson));
        Assert.True(MissionOpenFloorAdmission.ChoicesValid(phase,site,new[]{MissionOpenFloorChoice.From(choice)}));
        Assert.False(MissionOpenFloorAdmission.ChoicesValid(phase,site,new[]{new MissionOpenFloorChoice(1,1416,before)}));
    }
    [Fact] public void MissingAuditNeverSilentlyFallsBackToUnsafeGenerated1416()
    {
        var raw=Source();raw.pos_x++;Assert.Equal((ushort)552,MissionStaging.ExpectedConcrete(Phase().objects[0],raw));
        Assert.False(MissionOpenFloorAnchors.TryCurrent(raw,out _,out _));
        Assert.Null(new MissionObjectState{spot=raw,goid=552}.OpenFloor);
        Assert.Equal(raw.pos_x,MissionObjectPoses.For(new MissionObjectState{spot=raw,goid=552}).X);
    }
    [Theory][InlineData(1416)][InlineData(552)]
    public void AlreadyBoundSamePrototypeFurnitureKeepsItsIdentityAndLegacyPose(ushort goid)
    {
        var raw=Source();raw.type=BitConverter.GetBytes(goid);var phase=Phase();var site=new MissionSite{metrId=1};site.realObjects.Add(raw);
        var choice=Assert.Single(MissionStaging.SelectObjects(phase,site,new Random(220110)));
        Assert.Equal(goid,choice.Goid);Assert.Equal(goid,MissionStaging.ExpectedConcrete(phase.objects[0],raw));
        Assert.False(MissionOpenFloorAdmission.Target(choice.Goid,raw));
        var pose=MissionObjectPoses.For(new MissionObjectState{spot=raw,goid=goid});Assert.Equal((raw.pos_x,raw.pos_y,raw.pos_z),(pose.X,pose.Y,pose.Z));
    }
    [Fact] public void NativeFixtureStartsAtOriginalOfferOriginAndRequiresPublishedApproach()
    {
        var mode=NativeQa.NativeQaModes.Find("mission-safe-anchors");
        Assert.Equal((17000,2300),(mode.Plan.X,mode.Plan.Z));Assert.Empty(mode.Plan.Bootstrap);
        Assert.True(mode.Plan.ExtraQaMetadata["OpenFloorWitnessRequired"].GetBoolean());
        Assert.DoesNotContain(mode.Plan.AfterHelpers,sql=>sql.Contains("29100")||sql.Contains("14780"));
        Assert.Contains("never use Centre as a player start or destination",mode.Plan.ReadyMessages.Single());
    }
}
