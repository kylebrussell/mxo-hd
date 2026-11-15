using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class SharpshooterFreeFireTests
{
    private static WorldClient Client(int district=1)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        c.Alive=true;c.playerData.setOnWorld(true);c.playerData.setDistrictId((ushort)district);c.playerInstance.Level.setValue((byte)50);
        c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(100,495,100));typeof(WorldClient).GetField("lastFlushTick",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,long.MaxValue);return c;
    }
    private static Mob Npc(int district=1)
    {var m=new Mob();m.setEntityId(148000);m.setDistrict((ushort)district);m.setHealthC(1000);m.setHealthM(1000);m.setLevel(50);m.setIsSpawned(true);return m;}
    [Theory]
    [InlineData(0x38,1.2f,1,-5,0)][InlineData(0x37,3f,1,-15,0)][InlineData(0x39,2f,1,-10,0)]
    [InlineData(0x38,3.01f,1,5,5)][InlineData(0x37,10f,1,15,15)][InlineData(0x39,20f,1,10,10)]
    [InlineData(0x38,1.2f,2,-20,0)][InlineData(0x38,4f,2,-10,5)]
    [InlineData(0,1f,2,0,0)][InlineData(0x3b,1f,1,0,0)]
    public void RecoveredMathAndExplicitOursDistanceBoundary(byte weapon,float metres,int opponents,int before,int loaded)
    {Assert.Equal(before,SharpshooterFreeFire.Modifier(weapon,false,metres,opponents,false));Assert.Equal(loaded,SharpshooterFreeFire.Modifier(weapon,false,metres,opponents,true));}
    [Theory][InlineData(0x37)][InlineData(0x38)][InlineData(0x39)]
    public void GunTargetsRetainZeroPenaltyOrAdvantage(byte weapon)
    {Assert.Equal(0,SharpshooterFreeFire.Modifier(weapon,true,1,3,false));Assert.Equal(0,SharpshooterFreeFire.Modifier(weapon,true,20,3,true));}
    [Fact]public void UnknownDistanceDoesNotInventAContactOrModifier()
    {foreach(float m in new[]{float.NaN,float.PositiveInfinity,-1})Assert.Equal(0,SharpshooterFreeFire.Modifier(0x37,false,m,2,false));}
    [Fact]public void CallbackOnly148UsesLoadedMembershipIncludingLevelZeroAndNormalRemoval()
    {
        var c=Client();Assert.Empty(AbilityPassives.Modifiers(148));Assert.False(AbilityPassives.IsLoaded(c,148));
        AbilityPassives.SetLoaded(c,new(ushort,ushort)[]{(148,0)});Assert.True(AbilityPassives.IsLoaded(c,148));Assert.Equal(0,AbilityPassives.Level(c,148));
        AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());Assert.False(AbilityPassives.IsLoaded(c,148));
        var m=AbilityCatalog.Find(148)!.Modifiers.ToArray();Assert.Contains(m,x=>x.Text("StatModInitFunc")=="disableFiringInMeleePenInit");Assert.Contains(m,x=>x.Text("StatModInitFunc")=="disableFiringMeleeRangePenInit");
    }
    [Fact]public void NpcCountsAreDistinctLiveCurrentCombatantsAndIncludeCurrentShooterOnce()
    {
        var a=Client();var b=Client();var off=Client();off.playerData.setOnWorld(false);var dead=Client();dead.playerInstance.Health.setValue((ushort)0);var remote=Client(2);var m=Npc();
        foreach(var c in new[]{a,a,b,off,dead,remote})m.setCombatTarget(c);
        Assert.Equal(2,SharpshooterFreeFire.NpcOpponents(m,a));m.RemoveCombatant(b);Assert.Equal(1,SharpshooterFreeFire.NpcOpponents(m,a));
        a.playerData.worldChangePending=true;Assert.Equal(0,SharpshooterFreeFire.NpcOpponents(m,a));m.setIsDead(true);Assert.Equal(0,SharpshooterFreeFire.NpcOpponents(m,b));
    }
    private static void Set(object value,string field,object data)=>value.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(value,data);
    [Fact]public void PlayerCountsDeduplicateLiveGunfireAndRejectEndedDeadOffworldOrOtherTargets()
    {
        var target=Client();var other=Client();var a=Client();var b=Client();var unrelated=Client();var handlers=new List<CombatHandler>();
        var previous=WorldServer.Clients;WorldServer.Clients=new Dictionary<string,WorldClient>();
        void Fire(WorldClient source,WorldClient victim)
        {var h=CombatManager.GetHandler(source);handlers.Add(h);var f=(PvpGunfire)RuntimeHelpers.GetUninitializedObject(typeof(PvpGunfire));Set(f,"<Shooter>k__BackingField",source);Set(f,"<Target>k__BackingField",victim);Set(f,"<IsActive>k__BackingField",true);h.pvpGunfire=f;}
        try
        {
            Fire(a,target);Fire(b,target);Fire(unrelated,other);Assert.Equal(2,SharpshooterFreeFire.PlayerOpponents(target,a));
            Set(handlers[1].pvpGunfire,"<IsActive>k__BackingField",false);Assert.Equal(1,SharpshooterFreeFire.PlayerOpponents(target,a));
            Set(handlers[1].pvpGunfire,"<IsActive>k__BackingField",true);b.playerData.isDead=true;Assert.Equal(1,SharpshooterFreeFire.PlayerOpponents(target,a));
            b.playerData.isDead=false;b.playerData.setOnWorld(false);Assert.Equal(1,SharpshooterFreeFire.PlayerOpponents(target,a));
            target.playerData.isDead=true;Assert.Equal(0,SharpshooterFreeFire.PlayerOpponents(target,a));
        }
        finally{try{foreach(var h in handlers)h.pvpGunfire=null;foreach(var c in new[]{a,b,unrelated}){c.Alive=false;CombatManager.RemoveClient(c);}}finally{WorldServer.Clients=previous;}}
    }
    [Fact]public void CurrentInterlockOpponentsAreActualLinksNotWholeFighterCount()
    {
        var target=Client();var a=Client();var b=Client();var unrelated=Client();var t=new PvpFight.Side{Client=target};var aa=new PvpFight.Side{Client=a};var bb=new PvpFight.Side{Client=b};var uu=new PvpFight.Side{Client=unrelated};
        t.Other=aa;aa.Other=t;bb.Other=t;uu.Other=aa;
        var fight=(PvpFight)RuntimeHelpers.GetUninitializedObject(typeof(PvpFight));Set(fight,"sync",new object());Set(fight,"members",new List<PvpFight.Side>{t,aa,bb,uu});Set(fight,"<IsActive>k__BackingField",true);
        Assert.Equal(new[]{a,b},fight.CurrentOpponents(target));bb.Present=false;Assert.Equal(new[]{a},fight.CurrentOpponents(target));Set(fight,"<IsActive>k__BackingField",false);Assert.Empty(fight.CurrentOpponents(target));
    }
    [Theory][InlineData("world")][InlineData("life")][InlineData("owner")][InlineData("dead")][InlineData("offworld")][InlineData("district")]
    public void CapturedShotCannotGrantANewOwnerLifeOrWorldAPenaltyAdvantage(string change)
    {
        var a=Client();var b=Client();b.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(220,495,100));var shot=SharpshooterFreeFire.AgainstPlayer(a,b,537);
        switch(change){case "world":a.playerData.worldGeneration++;break;case "life":b.playerData.lifeGeneration++;break;case "owner":a.playerData=new ClientData();break;case "dead":b.playerData.isDead=true;break;case "offworld":a.playerData.setOnWorld(false);break;case "district":b.playerData.setDistrictId(2);break;}
        Assert.Equal(100,shot.AddToAttack(100,537));
    }
    [Fact]public void NormalModifierChangesAttackInputOnlyWithTheSameContestAndDamageDraws()
    {
        var a=new TrackingRandom(17);var b=new TrackingRandom(17);
        var off=FreeAttacks.Fire(a,50,CombatTactic.Speed,CombatTacticType.Normal,50,CombatTactic.Speed,537,1000-5);
        var on=FreeAttacks.Fire(b,50,CombatTactic.Speed,CombatTacticType.Normal,50,CombatTactic.Speed,537,1000);
        Assert.True(off.Hit);Assert.True(on.Hit);Assert.True(on.AttackRoll>off.AttackRoll);Assert.Equal(off.DefenseRoll,on.DefenseRoll);Assert.Equal(off.Damage,on.Damage);
        Assert.Equal(a.Draws,b.Draws);Assert.Equal(a.Doubles,b.Doubles);Assert.Equal(a.Next(),b.Next());
    }
    [Fact]public void InapplicableAndLoadedShotsBypassTheActualCountProvider()
    {
        var c=Client();int scans=0;int Count(){scans++;return 3;}
        var gunTarget=new SharpshooterFreeFire.Shot(()=>true,()=>1f,()=>537,c,537,Count,"unit");
        Assert.False(gunTarget.CountSampled);Assert.Equal(100,gunTarget.AddToAttack(100,537));Assert.Equal(0,scans);
        var noGun=new SharpshooterFreeFire.Shot(()=>true,()=>1f,()=>0,c,0,Count,"unit");
        Assert.False(noGun.CountSampled);Assert.Equal(100,noGun.AddToAttack(100,0));Assert.Equal(0,scans);
        var invalid=new SharpshooterFreeFire.Shot(()=>false,()=>1f,()=>0,c,537,Count,"unit");
        Assert.False(invalid.CountSampled);Assert.Equal(100,invalid.AddToAttack(100,537));Assert.Equal(0,scans);
        AbilityPassives.SetLoaded(c,new(ushort,ushort)[]{(148,50)});
        var loaded=new SharpshooterFreeFire.Shot(()=>true,()=>1f,()=>0,c,537,Count,"unit");
        Assert.False(loaded.CountSampled);Assert.Equal(100,loaded.AddToAttack(100,537));Assert.Equal(0,scans);
        AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
        var applicable=new SharpshooterFreeFire.Shot(()=>true,()=>1f,()=>0,c,537,Count,"unit");
        Assert.True(applicable.CountSampled);Assert.Equal(3,applicable.Opponents);Assert.Equal(80,applicable.AddToAttack(100,537));Assert.Equal(1,scans);
    }
    [Fact]public void ABypassedCountCannotBeReusedAfterWeaponOrLoadedStateChanges()
    {
        var c=Client();uint targetWeapon=537;int scans=0;
        var gunTarget=new SharpshooterFreeFire.Shot(()=>true,()=>4f,()=>targetWeapon,c,537,()=>{scans++;return 3;},"unit");
        targetWeapon=0;Assert.Equal(100,gunTarget.AddToAttack(100,537));Assert.Equal(0,scans); //not an unearned+5 after the swap
        AbilityPassives.SetLoaded(c,new(ushort,ushort)[]{(148,50)});
        var loaded=new SharpshooterFreeFire.Shot(()=>true,()=>4f,()=>targetWeapon,c,537,()=>{scans++;return 3;},"unit");
        AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
        Assert.Equal(100,loaded.AddToAttack(100,537));Assert.Equal(0,scans);
    }
    private sealed class TrackingRandom:Random
    [Fact]public void DefenderUsesCurrentEquippedWeaponNotAStaleFreeFirePick()
    {
        var a=Client();var b=Client();b.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(220,495,100));
        b.playerData.freeFireWeaponGoid=537;b.playerInstance.EquippedItemID.setValue((uint)0);
        var fists=SharpshooterFreeFire.AgainstPlayer(a,b,537);Assert.True(fists.CountSampled);Assert.Equal(95,fists.AddToAttack(100,537));
        b.playerInstance.EquippedItemID.setValue((uint)537);
        var gun=SharpshooterFreeFire.AgainstPlayer(a,b,537);Assert.False(gun.CountSampled);Assert.Equal(100,gun.AddToAttack(100,537));
        b.playerInstance.EquippedItemID.setValue((uint)0);Assert.Equal(100,gun.AddToAttack(100,537));
    }
    private sealed class TrackingRandom:Random
    {
        public List<(int Min,int Max,int Value)> Draws=new();public List<double> Doubles=new();public TrackingRandom(int seed):base(seed){}
        public override double NextDouble(){double value=base.NextDouble();Doubles.Add(value);return value;}
        public override int Next(int min,int max){int value=base.Next(min,max);Draws.Add((min,max,value));return value;}
        public override int Next(int max){int value=base.Next(max);Draws.Add((0,max,value));return value;}
    }
}
