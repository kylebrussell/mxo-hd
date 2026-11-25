using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public sealed class SneakMovementSpeedBonusTests
{
    [Fact]
    public void CatalogLinksMovementSpeed115ToOwnerPercentage998FromSneak151()
    {
        var run = AbilityCatalog.Find(115);
        Assert.NotNull(run);
        Assert.Equal(AbilityBuffs.RunOwnerMovementBonusStat, AbilityCatalog.IdOf((uint)run.Number("INfluence/AttributeLink")));
        Assert.Equal(1, AbilityCatalog.Find(AbilityBuffs.RunOwnerMovementBonusStat).Number("Ability/ValueType"));

        var sneak = AbilityCatalog.Find(151);
        Assert.NotNull(sneak);
        var mod = Assert.Single(sneak.Modifiers, m => m.Target == AbilityBuffs.RunOwnerMovementBonusStat);
        Assert.Equal(2, mod.Number("ValueFunction"));
        Assert.Equal(0.2, mod.Number("ValueScale"), 4);
    }

    [Theory]
    [InlineData(20, 4, .04f)]
    [InlineData(50, 10, .10f)]
    public void RunChannelConsumesLoadedSneak998PercentageAndTracksArchiveReload(ushort rank, int expectedStat, float expectedScale)
    {
        var actor = Client(50);
        var sneakParent = ((ushort)151, rank);

        Assert.Equal(0f, Scale(actor));
        AbilityPassives.SetLoaded(actor, new[] { sneakParent });
        Assert.Equal(expectedStat, AbilityStats.Player(actor, AbilityBuffs.RunOwnerMovementBonusStat));
        Assert.Equal(expectedScale, Scale(actor), 4);

        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(0, AbilityStats.Player(actor, AbilityBuffs.RunOwnerMovementBonusStat));
        Assert.Equal(0f, Scale(actor), 4);

        AbilityPassives.SetLoaded(actor, new[] { sneakParent });
        Assert.Equal(expectedScale, Scale(actor), 4);
    }

    [Fact]
    public void RunOwnerBonusStacksWithSpeedBuffsTracksLiveReloadAndStaysIsolatedFromStealthChannels()
    {
        var actor = Client(50);
        var sneakParent = ((ushort)151, (ushort)50);
        AbilityPassives.SetLoaded(actor, new[] { sneakParent });
        Assert.Equal(.10f, Scale(actor), 4);

        // Stacks with Hyper-Speed (12, +0.45) and updates live on Archive unload/reload.
        Assert.True(AbilityBuffs.Apply(actor, AbilityBuffs.HyperSpeed));
        Assert.Equal(.55f, Scale(actor), 4);

        AbilityPassives.SetLoaded(actor, Array.Empty<(ushort, ushort)>());
        Assert.Equal(.45f, Scale(actor), 4);

        AbilityPassives.SetLoaded(actor, new[] { sneakParent });
        Assert.Equal(.55f, Scale(actor), 4);

        // Removing Hyper-Speed returns to the 0.10 Sneak 998 baseline, not 0.
        Assert.True(AbilityBuffs.Remove(actor, AbilityBuffs.HyperSpeed));
        Assert.Equal(.10f, Scale(actor), 4);

        // Stacks with Hyper-Sprint (184, +0.15) and Desperate Speed (291, +0.40).
        Assert.True(AbilityBuffs.Apply(actor, AbilityBuffs.HyperSprint));
        Assert.Equal(.25f, Scale(actor), 4);
        Assert.True(AbilityBuffs.Remove(actor, AbilityBuffs.HyperSprint));
        Assert.Equal(.10f, Scale(actor), 4);

        Assert.True(AbilityBuffs.Apply(actor, 291));
        Assert.Equal(.50f, Scale(actor), 4);
        Assert.True(AbilityBuffs.Remove(actor, 291));
        Assert.Equal(.10f, Scale(actor), 4);

        // Channel isolation: entering Sneak (877) or Invisibility Belt (878) drops 998; clearing restores 0.10.
        Assert.NotNull(Stealth.Begin(actor, Stealth.Defs[Stealth.SneakGrantAbility], 50));
        Assert.Equal(0f, Scale(actor), 4);
        Stealth.Cleared(actor);
        Assert.Equal(.10f, Scale(actor), 4);

        Assert.NotNull(Stealth.Begin(actor, Stealth.Defs[Stealth.InvisibilityGrantAbility], 50));
        Assert.Equal(0f, Scale(actor), 4);
        Stealth.Cleared(actor);
        Assert.Equal(.10f, Scale(actor), 4);
    }

    [Fact]
    public void RunOwnerBonusCombinesWithTimedRunDebuffsAndRestoresAfterExpiry()
    {
        var actor = Client(50);
        var subject = TimedEffects.ForPlayer(actor);
        Assert.True(TempMods.TryGet(CombatSpecials.HinderingShot, out var effect));
        try
        {
            AbilityPassives.SetLoaded(actor, new[] { ((ushort)151, (ushort)50) });
            Assert.Equal(.10f, Scale(actor), 4);

            TimedEffects.Apply(subject, effect, TimeUtils.GetSimTimeSeconds());
            Assert.Equal(-.40f, Scale(actor), 4);

            Assert.True(TimedEffects.RemoveParent(subject, CombatSpecials.HinderingShot));
            Assert.Equal(.10f, Scale(actor), 4);
        }
        finally { AbilityBuffs.RemoveAll(actor); }
    }

    private static float Scale(WorldClient client) => BitConverter.ToSingle(client.playerInstance.MovementScale.getValue());

    private static WorldClient Client(byte level)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(50998);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.Health.setValue((ushort)2000);
        client.playerInstance.MaxHealth.setValue((ushort)2000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        client.LivenessClock = () => 0;
        client.Alive = true;
        return client;
    }
}
