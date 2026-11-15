using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class HealthBoostPillsTests
{
    [Theory]
    [InlineData(187,2407,100,0)]
    [InlineData(672,39411,100,10)]
    [InlineData(188,39412,200,20)]
    [InlineData(189,39413,300,30)]
    [InlineData(673,39414,400,40)]
    public void StoredItemsAndBuffs(int id,int item,int amount,int level)
    {
        ushort ability=(ushort)id;
        Assert.Equal((uint)item,HealthBoostPills.Item(ability));
        Assert.Equal(level,HealthBoostPills.RequiredLevel(ability));
        var pill=ConsumableItems.Items[(uint)item];
        Assert.Equal(ability,pill.AbilityId);
        Assert.Equal(0,pill.Group); // no shipped family timer; old HealPill10 mapping was unproven
        Assert.Equal(30,pill.Seconds);
        Assert.True(AbilityBuffs.TryGet(ability,out var buff));
        Assert.Equal(600,buff.Seconds);
        var mod=Assert.Single(buff.Modifiers);
        Assert.Equal(106,mod.Stat);
        Assert.Equal(amount,mod.Value);
        Assert.Equal(1003,buff.Family);
        Assert.True(AbilityCooldowns.TryGet(ability,out var cd));
        Assert.Equal(30,cd.Seconds);
        Assert.Equal(0,cd.Group);
    }
    private static WorldClient Client()
    {
        var client=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData=new ClientData(); client.playerInstance=new PlayerCharacter(); client.messageQueue=new MessageQueue();
        client.playerInstance.MaxHealth.setValue((ushort)500); client.playerInstance.Health.setValue((ushort)400);
        client.playerInstance.InnerStrengthMax.setValue((ushort)200); client.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(client,long.MaxValue);
        return client;
    }
    private static ushort Value(hds.Attribute value)=>NumericalUtils.ByteArrayToUint16(value.getValue(),1);
    [Fact]
    public void BoostDoesNotHealAndExpiryClampsHealthWithoutLosingBaseline()
    {
        var c=Client();
        Assert.True(AbilityBuffs.Apply(c,673,c));
        Assert.Equal(900,Value(c.playerInstance.MaxHealth)); Assert.Equal(400,Value(c.playerInstance.Health));
        c.playerInstance.Health.setValue((ushort)850);
        TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds()+601);
        Assert.Equal(500,Value(c.playerInstance.MaxHealth)); Assert.Equal(500,Value(c.playerInstance.Health));
        Assert.Empty(c.playerData.activeBuffs);
    }
    [Fact]
    public void ReplacingBoostKeepsOneFamilyAndOldParentRemovalKeepsNewBoost()
    {
        var c=Client();
        Assert.True(AbilityBuffs.Apply(c,672,c));
        Assert.True(AbilityBuffs.Apply(c,673,c));
        Assert.Single(c.playerData.activeBuffs); Assert.Equal(900,Value(c.playerInstance.MaxHealth));
        AbilityBuffs.Remove(c,672); Assert.Equal(900,Value(c.playerInstance.MaxHealth));
        AbilityBuffs.Remove(c,673); Assert.Equal(500,Value(c.playerInstance.MaxHealth));
    }
}
