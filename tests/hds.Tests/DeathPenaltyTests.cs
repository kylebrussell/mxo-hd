using hds;

namespace hds.Tests;

public class DeathPenaltyTests
{
    [Fact]
    public void PenaltyPresentationRequiresALiveWorldView()
    {
        var data = new ClientData();
        data.setDistrictId(1);
        Assert.False(DeathPenalty.CanPresent(data));
        data.setOnWorld(true);
        Assert.True(DeathPenalty.CanPresent(data));
        data.respawnPending = true;
        Assert.False(DeathPenalty.CanPresent(data));
        data.respawnPending = false;
        data.worldChangePending = true;
        Assert.False(DeathPenalty.CanPresent(data));
        data.worldChangePending = false;
        data.waitForRPCShutDown = true;
        Assert.False(DeathPenalty.CanPresent(data));
        data.waitForRPCShutDown = false;
        data.setDistrictId((uint)MxOLocations.LA);
        Assert.False(DeathPenalty.CanPresent(data));
    }

    [Fact]
    public void ADeathLosesFifteenPercentAndRecoversOverFiveMinutes()
    {
        float factor = DeathPenalty.AfterDeath(1);
        Assert.Equal(.85f, factor, 5);
        Assert.Equal(.925f, DeathPenalty.Recover(factor, 150), 5);
        Assert.Equal(1f, DeathPenalty.Recover(factor, 300), 5);
        Assert.Equal(factor, DeathPenalty.Recover(factor, -30));
        Assert.Equal(1f, DeathPenalty.Recover(factor, 30000));
        Assert.Equal(.7f, DeathPenalty.AfterDeath(factor), 5); // the factor observed in ordinary retail deaths
        Assert.False(AbilityCancellation.CanDismiss(DeathPenalty.Ability));
    }

    [Fact]
    public void DeathPenaltyIsTheFloatAtSelfAttributeThirtyEight()
    {
        Assert.Equal(new byte[] { 2,0,2,0x80,0x80,0x80,0x80,0x80,8,0x9a,0x99,0x59,0x3f },
            PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfDeathPenalty, PlayerViewAttributes.F32(.85f))));
        Assert.Equal(0f, DeathPenalty.AfterDeath(.1f));
        Assert.Equal(1f, DeathPenalty.Recover(.7f, 600));
    }
}
