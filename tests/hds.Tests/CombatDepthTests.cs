using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class CombatDepthTests
{
    private static WorldClient Client()
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData(); c.playerData.setDistrictId(1); c.playerInstance = new PlayerCharacter();
        c.playerInstance.Level.setValue((byte)10); c.playerInstance.MaxHealth.setValue((ushort)500);
        c.playerInstance.Health.setValue((ushort)500); c.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(c, long.MaxValue);
        return c;
    }
    [Fact]
    public void FixtureRequiresExactDevFlagBeforeTouchingAnyClient()
    {
        string? previous=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS");
        try { Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","0"); Assert.Contains("requires MXO_DEV_COMMANDS=1",CombatDepthDev.Run(null!,new[]{"stun"})); }
        finally { Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",previous); }
    }
    [Theory]
    [InlineData(10.14f, 10, 50, 10.14f)]
    [InlineData(10.14f, 10, 75, 6.7938f)]
    [InlineData(10.14f, 10, 500, 3.3462f)]
    [InlineData(0, 10, 500, 0)]
    public void ScriptFormulaKeepsFractionalDurations(float duration, int level, int determination, float expected) =>
        Assert.Equal(expected, AbilityControlDurations.Mitigate(duration, level, determination), 4);
    [Fact]
    public void RealTargetTotalsMitigateDebuffsButNotSelfBuffOrImmunity()
    {
        var caster = Client(); var target = Client(); double now = TimeUtils.GetSimTimeSeconds();
        TimedEffects.ForPlayer(target).Effects.Apply(1102, new[]{new TimedEffects.Active {Proc=TempMods.Proc.Stat,Target=575,Value=75,Seconds=60}}, now);
        Assert.Equal(60, AbilityControlDurations.Determination(target));
        Assert.True(TempMods.TryGet(315, out var stun));
        var landed = TimedEffects.Apply(TimedEffects.ForPlayer(target), stun, now, caster);
        Assert.Equal(6.076f, Assert.Single(landed, m=>m.Target==102).Seconds, 4);
        var immunity = TimedEffects.ForPlayer(target).Effects.Snapshot().Single(m=>m.Target==681);
        Assert.Equal(14, immunity.Seconds);
        var self = TimedEffects.Apply(TimedEffects.ForPlayer(target), stun, now, target);
        // Existing immunity refuses only the state. Remove it before checking a self application.
        TimedEffects.ForPlayer(target).Effects.RemoveAll();
        TimedEffects.ForPlayer(target).Effects.Apply(1102, new[]{new TimedEffects.Active {Proc=TempMods.Proc.Stat,Target=575,Value=75,Seconds=60}}, now);
        Assert.Equal(7, Assert.Single(TimedEffects.Apply(TimedEffects.ForPlayer(target), stun, now, target)).Seconds);
        var buff = new TempMods.Effect {Ability=1102,Mods=new[]{new TempMods.Mod{Proc=TempMods.Proc.Stat,Target=575,MinValue=10,MaxValue=10,Seconds=20}}};
        Assert.Equal(20, Assert.Single(TimedEffects.Apply(TimedEffects.ForPlayer(target),buff,now,caster)).Seconds);
    }
    [Fact]
    public void ImplicitImmunityUsesTheUnmitigatedDurationAndNpcReadsItsOwnStat()
    {
        var caster=Client();var target=Client();double now=TimeUtils.GetSimTimeSeconds();
        TimedEffects.ForPlayer(target).Effects.Apply(1102,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=575,Value=60,Seconds=60}},now);
        var effect=new TempMods.Effect{Ability=315,Mods=new[]{new TempMods.Mod{Kind=TempMods.KindState,Proc=TempMods.Proc.State,Target=102,Seconds=4}}};
        var state=Assert.Single(TimedEffects.Apply(TimedEffects.ForPlayer(target),effect,now,caster));
        Assert.Equal(3.472f,state.Seconds,4);
        Assert.Equal(4,TimedEffects.ForPlayer(target).Effects.Snapshot().Single(m=>m.Target==681).Seconds);
        var mob=new Mob();TimedEffects.ForMob(mob).Effects.Apply(1102,new[]{new TimedEffects.Active{Proc=TempMods.Proc.Stat,Target=575,Value=125,Seconds=60}},now);
        Assert.Equal(125,TimedEffects.ForMob(mob).Determination);
        var indefinite=new TempMods.Effect{Ability=315,Mods=new[]{new TempMods.Mod{Proc=TempMods.Proc.Stat,Target=115,MinValue=-10,MaxValue=-10,Seconds=0}}};
        Assert.Equal(0,Assert.Single(TimedEffects.Apply(TimedEffects.ForPlayer(target),indefinite,now,caster)).Seconds);
    }
    [Theory]
    [InlineData(152,75)] [InlineData(153,150)] [InlineData(287,300)] [InlineData(388,1200)]
    public void SneakBonusUsesSourcedConstantsAndExplicitCastSnapshot(ushort ability,int bonus)
    {
        Assert.Equal(bonus,SneakAttacks.Bonus(ability,true)); Assert.Equal(0,SneakAttacks.Bonus(ability,false));
        Assert.Equal(60+bonus,SneakAttacks.Add(60,ability,true));
    }
    [Theory]
    [InlineData(197,284)] [InlineData(198,285)] [InlineData(287,19597)] [InlineData(267,5937)] [InlineData(412,742)]
    public void SpecialsUseTheirOwnShippedMoveAndDuration(ushort ability,uint move)
    {
        var animation=CombatAnimations.Special(ability,true,CombatTactic.Speed)!;
        Assert.Equal(move,animation.Id); Assert.Equal(256u,animation.Flags); Assert.True(animation.DurationMs>0);
        if (ability != 287) Assert.Null(CombatAnimations.Special(ability,false,CombatTactic.Speed));
        else Assert.Equal(19603u,CombatAnimations.Special(ability,false,CombatTactic.Speed)!.Id);
    }
    [Fact]
    public void EveryConfiguredSpecialResolvesToARealShippedMove()
    {
        foreach(var id in CombatAnimations.MappedSpecials) Assert.NotNull(CombatAnimations.Special(id,true,CombatTactic.Speed));
    }
    [Fact]
    public void MultipleSessionsShareOneRetaliationClockAndKeepRemainingTarget()
    {
        var mob=new Mob(); mob.setHealthC(100); var a=Client();var b=Client();
        mob.setCombatTarget(a);mob.setCombatTarget(b); Assert.Equal(2,mob.CombatantCount);
        Assert.True(mob.TryRetaliate(10,4.5)); Assert.False(mob.TryRetaliate(10,4.5));
        mob.RemoveCombatant(b); Assert.Same(a,mob.getCombatTarget()); Assert.False(mob.TryRetaliate(14,4.5));
        Assert.True(mob.TryRetaliate(14.5,4.5)); Assert.True(mob.TryClaimDeath()); Assert.False(mob.TryClaimDeath());
        Assert.False(mob.TryRetaliate(20,4.5));
    }
}
