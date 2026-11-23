using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class PoisonKnifeTests
{
    private static WorldClient Client(byte level = 4)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)1000);
        client.playerInstance.MaxHealth.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void PoisonKnifeAndWoundingThrowRejectEmptySelectionWithNativeInvalidTarget()
    {
        var caster = Client(4);
        Assert.True(ThrownDotAdmission.TryRefuse(caster, 146, 0, null, out uint reason146));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason146);

        Assert.True(ThrownDotAdmission.TryRefuse(caster, 281, 0, null, out uint reason281));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason281);

        Assert.False(ThrownDotAdmission.TryRefuse(caster, 134, 0, null, out _));
    }

    [Fact]
    public void PoisonKnifeUsesTheRangedTestAndThrownDamageAsSeparateChannels()
    {
        var knife = AbilityCatalog.Find(CombatSpecials.PoisonKnife);
        int testFunction = (int)knife.Number("Ability/TestFunction");
        Assert.Equal(1, testFunction);
        Assert.Equal(524, AbilityStats.SpecialAttackStat(testFunction));
        Assert.Equal(1010, AbilityStats.SpecialDefenseStat(testFunction));
        Assert.Equal(7, TypedAbilityResistance.StoredType(CombatSpecials.PoisonKnife, dot: true));
        Assert.True(PoisonKnifeContest.Applies(146, AbilityCastOrigin.NormalFreeCast));
        Assert.False(PoisonKnifeContest.Applies(146, AbilityCastOrigin.AlreadyResolved));
        Assert.False(PoisonKnifeContest.Applies(281, AbilityCastOrigin.NormalFreeCast));

        var hit = new FreeCastViralContest.Result { Attack = 34, Defense = 32 };
        var miss = new FreeCastViralContest.Result { Attack = 43, Defense = 75 };
        // The original Vector26 454 ranged result envelopes, with only ability 454 changed to 146.
        Assert.Equal(Convert.FromHexString("816717002000D602007A0192000000000000003000000807004B6F72726F6E000E00416674657257686F72754E656F00040022000000000000002000000000000000"),
            PoisonKnifeContest.Message("Korron", "AfterWhoruNeo", hit));
        Assert.Equal(Convert.FromHexString("8167170020001802007A0192000000000000003000000807004B6F72726F6E000E00416674657257686F72754E656F0004002B000000000000004B00000000000000"),
            PoisonKnifeContest.Message("Korron", "AfterWhoruNeo", miss));
        Assert.False(new FreeCastViralContest.Result { Attack = 32, Defense = 32 }.Hit);
    }

    [Fact]
    public void PoisonKnifeFixtureDrawsAreLimitedToTheNamedFixtureRelationships()
    {
        const string disposable = "mxotest_knife";
        Assert.True(PoisonKnifeContest.FixtureResult("players", "1", disposable, 110, "CSR_Tester", true, false, 6114, "QAEnemy"));
        Assert.False(PoisonKnifeContest.FixtureResult("players", "1", disposable, 110, "CSR_Tester", true, false, 6112, "QAFar"));
        Assert.Null(PoisonKnifeContest.FixtureResult("players", "1", disposable, 110, "CSR_Tester", true, false, 6111, "QAFriend"));
        Assert.Null(PoisonKnifeContest.FixtureResult("players", "1", "reality_hd", 110, "CSR_Tester", true, false, 6114, "QAEnemy"));
        Assert.Null(PoisonKnifeContest.FixtureResult("players", "0", disposable, 110, "CSR_Tester", true, false, 6114, "QAEnemy"));
        Assert.Null(PoisonKnifeContest.FixtureResult("players", "1", disposable, 2, "TheNeo", true, false, 6114, "QAEnemy"));
        Assert.Null(PoisonKnifeContest.FixtureResult("players", "1", disposable, 110, "CSR_Tester", false, false, 6114, "QAEnemy"));
    }

    [Fact]
    public void PoisonKnifeLevel4SpyStyleScalesDotTotalTo18With2DamageTicksAndShippedTimers()
    {
        var caster = Client(4);
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (2, 4), (1112, 4) });
        CombatStyles.Select(caster, 1112);

        // Rank-4 Spy Style: 4 + trunc(4 * 0.5) = +6% thrown damage -> trunc(17 * 1.06) = 18.
        Assert.Equal(6, AbilityStats.Influence(caster, 1034));
        ushort scaledTotal = AbilityStats.ScaleTypedAbilityDamage(caster, 7, 17);
        Assert.Equal((ushort)18, scaledTotal);
        Assert.Equal(2, TempMods.DotTick(scaledTotal, 25f, 2.5));
        Assert.Equal((ushort)0, CombatSpecials.Damage(146, null));
        Assert.Equal((ushort)1157, TempMods.DOTPlaceHolderAbility);

        Assert.Equal((ushort)15, AbilityCosts.Cost(146));
        Assert.Equal(2f, AbilityCatalog.Find(146).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(146, out var cd146));
        Assert.Equal(20f, cd146.Seconds);
        Assert.Equal((ushort)1216, cd146.Group);
        Assert.Equal(3f, cd146.GroupSeconds);

        Assert.Equal((ushort)20, AbilityCosts.Cost(281));
        Assert.Equal(2f, AbilityCatalog.Find(281).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(281, out var cd281));
        Assert.Equal(10f, cd281.Seconds);
        Assert.Equal((ushort)1215, cd281.Group);
        Assert.Equal(3f, cd281.GroupSeconds);
    }
}
