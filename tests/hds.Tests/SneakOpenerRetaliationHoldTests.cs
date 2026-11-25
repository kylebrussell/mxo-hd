using hds;
using Xunit;

namespace hds.Tests;

public class SneakOpenerRetaliationHoldTests
{
    [Fact]
    public void RetaliationWaitsForTheOpenerExchangeThenResumes()
    {
        var dummy = new Mob();
        var other = new Mob();
        Assert.Equal(0, SneakOpenerRound.RetaliationHold(dummy, 100));

        // Twist Throw 2900 ms + 0.7 s, the same delay HideFighters uses.
        SneakOpenerRound.Hold(dummy, 3.6, 100);
        Assert.Equal(3.6, SneakOpenerRound.RetaliationHold(dummy, 100), 6);
        Assert.Equal(1.6, SneakOpenerRound.RetaliationHold(dummy, 102), 6);
        Assert.Equal(0, SneakOpenerRound.RetaliationHold(dummy, 103.6));
        Assert.Equal(0, SneakOpenerRound.RetaliationHold(dummy, 200));
        Assert.Equal(0, SneakOpenerRound.RetaliationHold(other, 101));

        // A later, longer exchange extends the hold; a shorter one never shortens it.
        SneakOpenerRound.Hold(dummy, 5, 200);
        SneakOpenerRound.Hold(dummy, 1, 200);
        Assert.Equal(5, SneakOpenerRound.RetaliationHold(dummy, 200), 6);
        SneakOpenerRound.Hold(null, 5, 0);
        SneakOpenerRound.Hold(other, 0, 0);
        Assert.Equal(0, SneakOpenerRound.RetaliationHold(other, 0));
    }
}
