using NativeQa;

namespace hds.Tests;

public class SeverArteryNativeModeTests
{
    [Fact]
    public void OrganizationMetadataDoesNotReplaceTheStandardField()
    {
        var state = NativeQaModes.Find("sever-artery-states").Plan.StateWithMetadata(new NativeQaFixtureState());
        Assert.Null(state["CasterOrganization"]);
        Assert.Equal(1, state["SeverArteryCasterOrganization"]!.GetValue<int>());
    }
}
