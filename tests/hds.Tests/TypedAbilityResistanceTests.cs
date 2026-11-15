using hds;
using System.Runtime.CompilerServices;
namespace hds.Tests;
[Collection("DelayedActions")]
public class TypedAbilityResistanceTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);return c;
    }
    [Theory]
    [InlineData(0,180)] [InlineData(1,181)] [InlineData(2,157)] [InlineData(7,1033)]
    [InlineData(3,0)] [InlineData(5,0)] [InlineData(6,0)] [InlineData(-1,0)] [InlineData(4,0)]
    public void OnlyExplicitSupportedDamageTypesHaveResistance(int type,int stat)=>Assert.Equal(stat,TypedAbilityResistance.Stat(type));
    [Theory]
    [InlineData(100,0,100)] [InlineData(100,75,82)] [InlineData(100,-75,117)]
    [InlineData(100,240,52)] [InlineData(100,100000,52)] [InlineData(0,75,0)] [InlineData(1,75,0)]
    [InlineData(65535,-100000,65535)]
    public void NormalizationFloorAndCapsAreExplicit(int raw,int total,int expected)=>Assert.Equal(expected,TypedAbilityResistance.Amount(raw,50,50,total));
    [Fact]
    public void ActualHealthAndDotComponentsAreSeparateAndHealingIsExcluded()
    {
        Assert.Equal(2,TypedAbilityResistance.StoredType(330));Assert.Equal(7,TypedAbilityResistance.StoredType(1135));
        Assert.Equal(-1,TypedAbilityResistance.StoredType(371));Assert.Equal(-1,TypedAbilityResistance.StoredType(474));
        Assert.Equal(2,TypedAbilityResistance.StoredType(474,true));
        Assert.True(TempMods.TryGet(474,out var effect));
        var dot=Assert.Single(effect.Mods.Where(m=>m.Proc==TempMods.Proc.Dot));Assert.Equal(2,dot.DamageType);
        Assert.Equal(2,Assert.Single(TimedEffects.Instantiate(effect,new Random(1)).Where(m=>m.Proc==TempMods.Proc.Dot)).DamageType);
    }
    [Fact]
    public void RealBuffsAffectOnlyTheirTypeAndRemovalRestoresExactRawDamage()
    {
        var source=Client();var target=Client();
        Assert.Equal(100,TypedAbilityResistance.Player(source,target,1135,100));
        Assert.True(AbilityBuffs.Apply(target,5));Assert.Equal(75,AbilityStats.Player(target,1033));Assert.Equal(0,AbilityStats.Toughness(target,false));Assert.Equal(0,AbilityStats.Toughness(target,true));
        Assert.Equal(82,TypedAbilityResistance.Player(source,target,1135,100));Assert.Equal(100,TypedAbilityResistance.Player(source,target,330,100));
        Assert.Equal(100,TypedAbilityResistance.Player(target,target,1135,100));
        Assert.True(AbilityBuffs.Remove(target,5));Assert.Equal(100,TypedAbilityResistance.Player(source,target,1135,100));
        Assert.True(AbilityBuffs.Apply(target,8));Assert.Equal(82,TypedAbilityResistance.Player(source,target,330,100));
        Assert.Equal(100,TypedAbilityResistance.Player(source,target,371,100));AbilityBuffs.RemoveAll(target);
    }
    [Theory]
    [InlineData("0","mxotest_x","1","65",1135)] [InlineData("1","mxo","1","65",1135)]
    [InlineData("1","mxotest_","1","65",1135)] [InlineData("1","mxotest_x;bad","1","65",1135)]
    [InlineData("1","mxotest_x","0","65",1135)] [InlineData("1","mxotest_x","1","49",1135)]
    [InlineData("1","mxotest_x","1","81",1135)] [InlineData("1","mxotest_x","1","65",330)]
    public void FixtureDrawRequiresActualDisposableDatabaseModeAndExactDomain(string dev,string db,string enabled,string raw,ushort ability)=>
        Assert.Null(TypedResistanceQa.Draw(dev,db,enabled,raw,ability,50,81));
    [Fact]
    public void ScopedDrawBoundsAndExplicitRngWin()
    {
        Assert.Equal(50,TypedResistanceQa.Draw("1","mxotest_x","1","50",1135,50,81));
        Assert.Equal(80,TypedResistanceQa.Draw("1","mxotest_x","1","80",1135,50,81));
        Assert.Equal(new Random(1).Next(50,81),TypedResistanceQa.Draw("1","mxotest_x","1","65",1135,50,81,new Random(1)));
    }
    [Fact]
    public void CapAndClockExpiryReadTheCurrentTargetTotals()
    {
        var source=Client();var target=Client();var holder=TimedEffects.ForPlayer(target).Effects;
        holder.Apply(2000,new[]{new TimedEffects.Active{Target=157,Value=500,Seconds=1,Proc=TempMods.Proc.Stat}},0);
        Assert.Equal(240,AbilityStats.Player(target,157));Assert.Equal(52,TypedAbilityResistance.Player(source,target,330,100));
        holder.Advance(2,new List<TimedEffects.Active>(),new List<TimedEffects.Active>());
        Assert.Equal(100,TypedAbilityResistance.Player(source,target,330,100));
        Assert.True(AbilityBuffs.Apply(target,8));Assert.Equal(82,TypedAbilityResistance.Player(source,target,330,100));
        typeof(PlayerEffects).GetMethod("Due",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
            .Invoke(TimedEffects.ForPlayer(target),new object[]{TimeUtils.GetSimTimeSeconds()+21});
        Assert.Equal(100,TypedAbilityResistance.Player(source,target,330,100));
    }
    [Fact]
    public void MixedParentDotComponentsRetainTheirOwnTypeAndUnknownScriptIsExcluded()
    {
        using var json=System.Text.Json.JsonDocument.Parse("""
        {"Viral/WhenEnabled":2,"Viral/TargetAttribute":2148668416,"Viral/DamageType":2,"Viral/StatModUpdateFunc":"ViralDOT",
         "Thrown/WhenEnabled":2,"Thrown/TargetAttribute":2148668416,"Thrown/DamageType":7,"Thrown/StatModUpdateFunc":"ThrownDOT",
         "Unknown/WhenEnabled":2,"Unknown/TargetAttribute":2148668416,"Unknown/DamageType":2,"Unknown/StatModUpdateFunc":"UnprovenDOT",
         "IS/WhenEnabled":2,"IS/TargetAttribute":2147593216,"IS/DamageType":2}
        """);
        var entry=new AbilityCatalog.Entry();typeof(AbilityCatalog.Entry).GetField("Fields",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(entry,json.RootElement.Clone());
        var types=entry.Modifiers.Select(m=>TypedAbilityResistance.ModifierType(m,true)).ToArray();
        Assert.Equal(new[]{2,7,-1,-1},types);
        Assert.Equal(-1,TypedAbilityResistance.ModifierType(entry.Modifiers.Single(m=>m.Component=="IS"),false));
        var first=new TempMods.Effect{Ability=2000,Mods=new[]{new TempMods.Mod{Proc=TempMods.Proc.Dot,Target=1157,DamageType=types[0],Seconds=10}}};
        var second=new TempMods.Effect{Ability=2000,Mods=new[]{new TempMods.Mod{Proc=TempMods.Proc.Dot,Target=1157,DamageType=types[1],Seconds=20}}};
        var holder=new TimedEffects.Holder();holder.Apply(2000,TimedEffects.Instantiate(first,new Random(1)),0);
        var old=holder.Apply(2000,TimedEffects.Instantiate(second,new Random(1)),1);
        Assert.Equal(2,Assert.Single(old).DamageType);Assert.Equal(7,Assert.Single(holder.Snapshot()).DamageType);
        holder.Advance(11,new List<TimedEffects.Active>(),new List<TimedEffects.Active>());
        Assert.Equal(7,Assert.Single(holder.Snapshot()).DamageType);
    }

    [Fact]
    public void FixtureLearnedSpyStyleFiftyContributesTwentyNineBeforeTargetResistance()
    {
        var modifier=CombatStyles.Modifiers(1112).Single(m=>m.Target==1034);
        Assert.True(AbilityValues.TryValue(modifier,50,50,out int value));Assert.Equal(29,value);
        Assert.Equal(68,TypedAbilityResistance.Amount(83,50,50,75));
    }

    [Fact]
    public void EnvironmentDatabaseSpoofCannotEnableProductionFixtureDraw()
    {
        var config=hds.shared.Store.config;
        var keys=new[]{"MXO_DEV_COMMANDS","MXO_DB_NAME","MXO_TYPED_RESISTANCE_QA","MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"};
        var old=keys.ToDictionary(k=>k,Environment.GetEnvironmentVariable);
        try
        {
            var foreign=new ServerConfig("");typeof(ServerConfig).GetField("_dbParams",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(foreign,new DbParams{DatabaseName="mxo_foreign"});
            hds.shared.Store.config=foreign;Environment.SetEnvironmentVariable(keys[0],"1");Environment.SetEnvironmentVariable(keys[1],"mxotest_spoof");Environment.SetEnvironmentVariable(keys[2],"1");Environment.SetEnvironmentVariable(keys[3],"65");
            Assert.Null(TypedResistanceQa.Draw(1135,50,81));
        }
        finally {hds.shared.Store.config=config;foreach(var key in keys)Environment.SetEnvironmentVariable(key,old[key]);}
    }

    [Fact]
    public void ModeHasOnlyTheNarrowDamageDrawOverrideAndNoGmOrGlobalRolls()
    {
        var p=new NativeQa.Modes.TypedResistanceMode().Plan;
        Assert.True(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Equal(2,p.EnvironmentOverrides.Count);Assert.Equal("1",p.EnvironmentOverrides["MXO_TYPED_RESISTANCE_QA"]);
        Assert.Equal("65",p.EnvironmentOverrides["MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"]);
        Assert.Equal(new ushort[]{2,17,1006,1112,5,8,330,1135,474},p.Abilities);
    }

    [Fact]
    public void NpcAndPlayerLandingsShareTheSameNormalizedAmountWithoutTouchingOrdinaryToughness()
    {
        var source=Client();var mob=new Mob();mob.setLevel(50);
        Assert.Equal(100,TypedAbilityResistance.Npc(source,mob,330,100));
        TimedEffects.ForMob(mob).Effects.Apply(2000,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=157,Value=75,Seconds=20}},0);
        Assert.Equal(82,TypedAbilityResistance.Npc(source,mob,330,100));Assert.Equal(100,TypedAbilityResistance.Npc(source,mob,1135,100));
        Assert.Equal(0,AbilityStats.Toughness(mob,false));Assert.Equal(0,AbilityStats.Toughness(mob,true));
        Assert.Equal(82,TypedAbilityResistance.Npc(source,mob,474,100,2));
        TimedEffects.ForMob(mob).Effects.RemoveParent(2000);Assert.Equal(100,TypedAbilityResistance.Npc(source,mob,330,100));
    }

}
