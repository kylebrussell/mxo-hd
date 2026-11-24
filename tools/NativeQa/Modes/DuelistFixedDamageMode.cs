using hds;

namespace NativeQa.Modes;

public sealed class DuelistFixedDamageMode : MartialFixedDamageMode
{
    // Initial stock eligible 501 and queued 502 from PR995.
    public DuelistFixedDamageMode() : base("duelist", 129, PistolSlideMode.StockHandguns,
        new Move[]
        {
            new(501, 109, 129, 104, 10, 287, "80b9f501040002000000000000000000000000000000000000000000f5010000006b0000086b0000000000"),
            new(502, 45, 53, 42, 10, 0, "80b9f601040002000000000000000000000000000000000000000000f6010000006b0000086b0000000000", CastSeconds: 4)
        },
        new[] { "Dual handguns start equipped. 501 receives Daze. Watch the 4 s cast before 502's queue and retain its short or medium pose." },
        level: 46, rank: 46, weapon: CombatFixtureWeapons.DualHandguns, mobFixture: "duelist-fixed-damage.csv")
    {
    }
}
