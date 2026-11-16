using NativeQa;
namespace hds.Tests;
public class ZionNextFiveCurrentModeMetadataTests
{
    [Fact] public void CurrentBaseAndHistoricalProofAreSeparate()
    {
        var plan=NativeQaModes.Find("zion-critical-next-five").Plan;
        Assert.Equal("9894edab3d9bdc09c5eec77e081afc1f22864fa3",plan.ExtraQaMetadata["QaBaseMaster"].GetString());
        Assert.Equal("e79906f0d493f631637a5052881e718bea6bb126",plan.ExtraQaMetadata["HistoricalQaBaseMaster"].GetString());
        Assert.Equal("4bb27ecb6ba0f598663f96d3873f7e8febc6d040",plan.ExtraQaMetadata["HistoricalGameplayProofProducer"].GetString());
        Assert.True(plan.ExtraQaMetadata["RequiresPublic269"].GetBoolean());Assert.True(plan.ExtraQaMetadata["RequiresMidphase271"].GetBoolean());
    }
}
