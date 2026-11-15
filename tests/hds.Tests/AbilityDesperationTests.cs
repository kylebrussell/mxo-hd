using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class AbilityDesperationTests
{
    private static WorldClient Client(ushort health=250)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)20);c.playerInstance.MaxHealth.setValue((ushort)500);c.playerInstance.Health.setValue(health);
        c.playerInstance.InnerStrengthMax.setValue((ushort)200);c.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }
    [Fact]
    public void FixedHealthFixtureRequiresExactDevFlagAndDisposableDatabase()
    {
        string[] names={"MXO_DEV_COMMANDS","MXO_DB_NAME","MXO_DESPERATION_QA_HOLD_HEALTH"};
        var old=names.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            Environment.SetEnvironmentVariable(names[2],"1");Environment.SetEnvironmentVariable(names[1],"mxotest_desperation");
            Environment.SetEnvironmentVariable(names[0],"true");Assert.False(AbilityDesperation.HoldQaHealth);
            Environment.SetEnvironmentVariable(names[0],"1");Assert.True(AbilityDesperation.HoldQaHealth);
            Environment.SetEnvironmentVariable(names[1],"reality_hd");Assert.False(AbilityDesperation.HoldQaHealth);
            Environment.SetEnvironmentVariable(names[1],"mxotest_desperation");Environment.SetEnvironmentVariable(names[2],"true");Assert.False(AbilityDesperation.HoldQaHealth);
        }
        finally {for(int i=0;i<names.Length;i++)Environment.SetEnvironmentVariable(names[i],old[i]);}
    }
    [Theory]
    [InlineData(251,500,0,10)] [InlineData(250,500,0,50)] [InlineData(249,500,0,50)]
    [InlineData(100,500,30,104)] [InlineData(251,500,15,11)] [InlineData(0,500,130,229)]
    [InlineData(1,0,0,0)] [InlineData(250,501,0,50)]
    public void SourcedHealthBoundaryAndIntegerStorage(int health,int max,int bonus,int expected)
        => Assert.Equal(expected,AbilityDesperation.Snapshot(health,max,bonus));
    [Fact]
    public void ApplicationSnapshotsAndOriginalOffRecordRetainsValueWithoutCombatChanges()
    {
        var c=Client();int attack=AbilityStats.Attack(c,false),defense=AbilityStats.Defense(c,false);
        try
        {
            Assert.True(AbilityBuffs.Apply(c,1149));
            var active=c.playerData.activeBuffs[1149];
            Assert.Equal(20,active.Definition.Seconds);Assert.Null(active.Definition.HealthChangedProc);
            Assert.Equal(50,AbilityStats.Player(c,1044));Assert.Equal(10,AbilityBuffBuffer.Used(c));
            c.playerInstance.Health.setValue((ushort)400);AbilityHealthCallbacks.HealthChanged(c,-150,true);
            Assert.Equal(50,AbilityStats.Player(c,1044));
            Assert.Equal(attack,AbilityStats.Attack(c,false));Assert.Equal(defense,AbilityStats.Defense(c,false));
            Assert.True(AbilityBuffs.Remove(c,1149));Assert.Equal(0,AbilityStats.Player(c,1044));Assert.Equal(0,AbilityBuffBuffer.Used(c));
            var expected=AbilityBuffs.BuildModifier(AbilityBuffs.KindStat,active.RecordIds[0],1149,1044,50,true,20);
            Assert.Contains(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),m=>m.content.Skip(1).SequenceEqual(expected));
        }
        finally {AbilityBuffs.RemoveAll(c);}
    }
    [Fact]
    public void CappedBonusIsReadOnceBeforeSnapshotAndCappedInitiativeDoesNotRewriteRecords()
    {
        var c=Client(100);
        try
        {
            AbilityBuffs.Apply(c,6);
            c.playerData.activeBuffs[6].Definition=new AbilityBuffs.Buff { Seconds=20, Modifiers=new[]{new AbilityBuffs.Modifier(AbilityBuffs.KindStat,1046,999)} };
            Assert.Equal(130,AbilityStats.Player(c,1046));
            AbilityBuffs.Apply(c,1149);
            int value=AbilityDesperation.Snapshot(100,500,130);
            Assert.Equal(value,c.playerData.activeBuffs[1149].Definition.Modifiers.Single().Value);
            AbilityBuffs.Remove(c,6);Assert.Equal(0,AbilityStats.Player(c,1046));
            Assert.Equal(value,AbilityStats.Player(c,1044));
        }
        finally {AbilityBuffs.RemoveAll(c);}
    }
    [Fact]
    public void UnloadExpiryAndBufferCapacityUseExistingLifecycle()
    {
        var c=Client();c.playerInstance.Level.setValue((byte)1);
        try
        {
            AbilityPassives.SetLoaded(c,new[]{((ushort)1149,(ushort)1)});
            AbilityBuffs.Apply(c,1149);AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
            Assert.False(AbilityBuffs.IsActive(c,1149));
            AbilityBuffs.Apply(c,1149);TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds()+21);
            Assert.False(AbilityBuffs.IsActive(c,1149));
            Assert.True(TempMods.TryGet(481,out var overflow));
            TimedEffects.Apply(TimedEffects.ForPlayer(c),overflow,TimeUtils.GetSimTimeSeconds(),c,"Source");
            Assert.False(AbilityBuffs.Apply(c,1149));Assert.Equal(0,AbilityStats.Player(c,1044));
            TimedEffects.RemoveParent(TimedEffects.ForPlayer(c),481);
            Assert.True(AbilityBuffs.Apply(c,1149));Assert.Equal(10,AbilityBuffBuffer.Used(c));
            Assert.True(AbilityCooldowns.TryGet(1149,out var cd));Assert.Equal(90,cd.Seconds);
        }
        finally {AbilityBuffs.RemoveAll(c);TimedEffects.RemoveParent(TimedEffects.ForPlayer(c),481);}
    }
    [Fact]
    public void ExistingStatCapRemainsAuthoritativeAndTerminalCleanupRemovesSnapshot()
    {
        var c=Client(1);c.playerInstance.Level.setValue((byte)1);
        try
        {
            AbilityBuffs.Apply(c,1149);Assert.Equal(12,AbilityStats.Player(c,1044));
            AbilityBuffs.RemoveAll(c);Assert.Empty(c.playerData.activeBuffs);Assert.Equal(0,AbilityStats.Player(c,1044));
            Assert.Equal(10,AbilityCosts.Cost(1149));
        }
        finally {AbilityBuffs.RemoveAll(c);}
    }
}
