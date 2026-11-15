using HeadlessClient;
using NativeQa;
using hds;
namespace hds.Tests;

public class MissionBoundCabinetFixtureTests
{
    [Fact] public void CheckpointHexPreservesJsonQuotesBackslashesControlsAndUnicode()
    {
        string expected="quote\" slash\\ newline\n tab\t 雪";
        string json=System.Text.Json.JsonSerializer.Serialize(new{Text=expected});
        string hex=Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(json));
        string decoded=MissionBoundCabinetFixture.DecodeCheckpointHex(hex);
        Assert.Equal(json,decoded);using var doc=System.Text.Json.JsonDocument.Parse(decoded);
        Assert.Equal(expected,doc.RootElement.GetProperty("Text").GetString());
    }
    [Theory][InlineData("")][InlineData("NULL")][InlineData("0")][InlineData("GG")][InlineData("FF")][InlineData("C328")]
    public void MissingMalformedOrInvalidUtf8CheckpointIsNotNormalized(string hex)=>
        Assert.Throws<InvalidDataException>(()=>MissionBoundCabinetFixture.DecodeCheckpointHex(hex));
    [Fact] public void EarnedNonstackableInventoryHasOneActualOwnerItemSlotAndRawZeroCount()
    {
        var observed=MissionBoundCabinetFixture.RequireMissionInventory("110\t6796\t7\t0\n",110,6796,7);
        Assert.Equal(new MissionBoundCabinetFixture.InventoryProof(110,6796,7,0),observed);
    }
    [Theory][InlineData("")][InlineData("110\t6796\t7\t0\n110\t6796\t7\t0\n")]
    [InlineData("111\t6796\t7\t0")][InlineData("110\t6795\t7\t0")][InlineData("110\t6796\t8\t0")]
    [InlineData("110\t6796\t7\t1")][InlineData("110\t6796\t7\t-1")][InlineData("110\t6796\t7")]
    public void MissingDuplicateOrMismatchedInventoryCannotProveCarryOwnership(string rows)=>
        Assert.Throws<InvalidDataException>(()=>MissionBoundCabinetFixture.RequireMissionInventory(rows,110,6796,7));
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
