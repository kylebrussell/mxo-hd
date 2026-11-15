using HeadlessClient;
using NativeQa;
using hds;
namespace hds.Tests;

public class MissionBoundCabinetFixtureTests
{
    [Fact] public void RealMissionItemGrantKeepsItsActualSlotDescriptorAndInstance()
    {
        var literal=Convert.FromHexString("5E01008C1A00000030008201");
        Assert.True(MissionBoundCabinetFixture.MissionItemGrant(literal,6796,48));
        Assert.False(MissionBoundCabinetFixture.MissionItemGrant(literal,6795,48));
        Assert.False(MissionBoundCabinetFixture.MissionItemGrant(literal,6796,49));
        Assert.False(MissionBoundCabinetFixture.MissionItemGrant(literal[..^1],6796,48));
        Assert.Equal(literal,ServerPackets.BuildMissionItemAdd(1,6796,0,48));
    }
    [Fact] public void BootstrapSeedsOnlyEarlierReceiptsAndNoSelectedProgress()
    {
        var priors=MissionBoundCabinetFixture.Prerequisites;Assert.Equal(23,priors.Distinct().Count());
        Assert.DoesNotContain(201203,priors);Assert.DoesNotContain(201204,priors);Assert.Contains(201202,priors);
        var mode=NativeQaModes.Find(MissionBoundCabinetFixture.Name);Assert.Equal(MissionBoundCabinetFixture.Abilities,mode.Plan.Abilities);
        Assert.Equal(MissionBoundCabinetFixture.SeedPrerequisites(110),mode.Plan.AfterHelpers[0]);
        Assert.Single(Scenarios.All,s=>s.Name==MissionBoundCabinetFixture.Name);
    }
    [Theory][InlineData(null)][InlineData("")][InlineData("bad")]
    public void OwnedWireCannotSilentlyUseAnUnpinnedServer(string? pin)=>Assert.Throws<InvalidDataException>(()=>MissionBoundCabinetFixture.RequireSource(pin,new string('a',64)));
    [Fact] public void SourcePinAndNpcLevelAreActualWireSchemas()
    {
        MissionBoundCabinetFixture.RequireSource(new string('a',64),new string('A',64));
        Assert.Throws<InvalidDataException>(()=>MissionBoundCabinetFixture.RequireSource(new string('b',64),new string('a',64)));
        Assert.Equal(50,MissionBoundCabinetFixture.NpcLevel(new Dictionary<string,byte[]>{{"Level",new byte[]{0x32}}}));
        Assert.Throws<InvalidDataException>(()=>MissionBoundCabinetFixture.NpcLevel(new Dictionary<string,byte[]>{{"Level",new byte[]{0x32,0}}}));
    }
}
