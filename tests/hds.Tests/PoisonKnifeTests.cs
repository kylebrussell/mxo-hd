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
