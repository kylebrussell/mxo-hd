using System;
using hds;
using Xunit;

namespace hds.Tests;

public class SpyAbilityRegistryTests
{
    private sealed class SampleHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new ushort[] { 1127 };
        public bool RetainsStealthDuringCast(ushort abilityId) => true;
        public bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = AbilityRefusals.InvalidTarget;
            return targetViewId == 0;
        }
        public bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage)
        {
            damage = 49;
            return true;
        }
    }

    private sealed class DuplicateHandler : ISpyAbilityHandler
    {
        public ushort[] AbilityIds => new ushort[] { 1127 };
    }

    [Fact]
    public void BuildMapDiscoversHandlersAndRejectsDuplicates()
    {
        var map = SpyAbilityRegistry.BuildMap(new[] { typeof(SampleHandler) });
        Assert.True(map.TryGetValue(1127, out var handler));
        Assert.True(handler!.RetainsStealthDuringCast(1127));
        Assert.True(handler.TryRefuse(null!, 1127, 0, null, out uint reason));
        Assert.Equal(AbilityRefusals.InvalidTarget, reason);
        Assert.False(handler.TryRefuse(null!, 1127, 4, 1, out _));
        Assert.True(handler.TryResolveDamage(AbilityCatalog.Find(1127)!, 11, new Random(1), out ushort damage));
        Assert.Equal((ushort)49, damage);

        Assert.Throws<InvalidOperationException>(() =>
            SpyAbilityRegistry.BuildMap(new[] { typeof(SampleHandler), typeof(DuplicateHandler) }));
    }

    [Fact]
    public void UnregisteredAbilityReturnsDefaultsWithoutSideEffects()
    {
        Assert.False(SpyAbilityRegistry.TryGet(1127, out _));
        Assert.False(SpyAbilityRegistry.RetainsStealthDuringCast(1127));
        Assert.False(SpyAbilityRegistry.TryRefuse(null!, 1127, 0, null, out uint reason));
        Assert.Equal(0u, reason);
        Assert.False(SpyAbilityRegistry.TryResolveDamage(AbilityCatalog.Find(1127)!, 11, new Random(1), out ushort damage));
        Assert.Equal((ushort)0, damage);
    }
}
