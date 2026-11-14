using System;
using System.Linq;

namespace hds
{
    /// <summary>tempmods.DealDamageIfOffBalance: an on-use, initialization-only thrown damage component.
    /// Existing base damage and this server's influence/resistance policies remain separate.</summary>
    public static class ConditionalThrownDamage
    {
        internal readonly struct Fence
        {
            internal readonly int World,Life,TargetWorld,TargetLife;
            internal readonly uint District,SourceCharacter,TargetCharacter;
            internal readonly ClientData SourceOwner,TargetOwner;
            internal readonly uint NpcSpawn;
            internal Fence(WorldClient source,WorldClient target=null,uint npcSpawn=0)
            {SourceOwner=source.playerData;TargetOwner=target?.playerData;SourceCharacter=SourceOwner.getCharID();TargetCharacter=TargetOwner?.getCharID()??0;NpcSpawn=npcSpawn;World=source.playerData.worldGeneration;Life=source.playerData.lifeGeneration;District=source.playerData.getDistrictId();
             TargetWorld=target?.playerData.worldGeneration??0;TargetLife=target?.playerData.lifeGeneration??0;}
            internal bool Current(WorldClient source)=>source!=null&&ReferenceEquals(SourceOwner,source.playerData)&&SourceCharacter==source.playerData.getCharID()&&source.Alive&&source.playerData.getOnWorld()&&!source.playerData.worldChangePending&&
                !source.playerData.isDead&&!source.playerData.duelDeath&&World==source.playerData.worldGeneration&&Life==source.playerData.lifeGeneration&&District==source.playerData.getDistrictId();
            internal bool TargetCurrent(WorldClient target)=>target!=null&&ReferenceEquals(TargetOwner,target.playerData)&&TargetCharacter==target.playerData.getCharID()&&target.Alive&&target.playerData.getOnWorld()&&!target.playerData.worldChangePending&&
                !target.playerData.isDead&&!target.playerData.duelDeath&&TargetWorld==target.playerData.worldGeneration&&TargetLife==target.playerData.lifeGeneration&&District==target.playerData.getDistrictId();
        }
        public static bool Handles(ushort ability)=>ability==279||ability==282;
        internal static int Value(ushort ability,int level)
        {
            if(!Handles(ability))return 0;
            var modifier=AbilityCatalog.Find(ability)?.Modifiers.SingleOrDefault(m=>m.Text("StatModInitFunc")=="DealDamageIfOffBalance"&&
                m.Number("WhenEnabled",-1)==2&&m.Number("DurationType",-1)==1);
            return modifier!=null&&AbilityValues.TryValue(modifier,0,level,out int value)?Math.Max(0,value):0;
        }
        internal static int Resolve(ushort ability,int level,TimedEffects.Subject target,double now)
        {
            if(target==null||!target.Alive||((target.ConditionFlags&CombatInterlock.ConditionBit(CombatTactic.Grab))==0&&
                !target.Effects.HasAt(TempMods.OffBalanceAbility,now)))return 0;
            return Value(ability,level);
        }
        internal static void Npc(WorldClient source,Mob target,ushort view,ushort ability,uint fx,Fence fence)
        {
            if(!Handles(ability)||!fence.Current(source)||target==null||target.getIsDead()||target.getHealthC()==0||target.getDistrict()!=fence.District)return;
            var live=AbilityTargets.FindMob(source,view,out var visible);
            if(!ReferenceEquals(live,target)||visible==null||!visible.viewCreated||visible.spawnId!=fence.NpcSpawn)return;
            int value=Resolve(ability,source.playerInstance.Level.getValue()[0],TimedEffects.ForMob(target),TimeUtils.GetSimTimeSeconds());
            if(value<=0)return;
            int raw=AbilityStats.ScaleAbilityDamage(source,ability,value);
            ushort damage=TypedAbilityResistance.Npc(source,target,ability,raw,7);
            ushort before=target.getHealthC();
            if(!CombatManager.GetHandler(source).ApplyAbilityToNpc(target,view,damage,fx))return;
            if(damage>0)source.messageQueue.addRpcMessage(AbilityMessages.Build(AbilityMessages.DamageCaster,true,AbilityMessages.ByGameObject,
                AbilityMessages.AbilityGameObject(ability),AbilityMessages.TypeGameObject,PvpFlag.Name(source),target.getName(),damage,raw-damage));
            source.FlushQueue();
            Output.WriteDebugLog($"[CONDITIONAL-THROW] {ability} on {target.getName()} value{value} raw{raw} final{damage} hp{before}->{target.getHealthC()}");
        }
        internal static void Player(WorldClient source,WorldClient target,ushort ability,Fence fence)
        {
            if(!Handles(ability)||!fence.Current(source)||!fence.TargetCurrent(target)||PvpCombat.HealthOf(target)==0)return;
            int value=Resolve(ability,source.playerInstance.Level.getValue()[0],TimedEffects.ForPlayer(target),TimeUtils.GetSimTimeSeconds());
            if(value<=0)return;
            int raw=AbilityStats.ScaleAbilityDamage(source,ability,value);
            ushort damage=TypedAbilityResistance.Player(source,target,ability,raw,7);
            if(!fence.Current(source)||!fence.TargetCurrent(target)||PvpCombat.Check(source,target)!=PvpRules.Verdict.Allowed)return;
            string casterName=PvpFlag.Name(source),targetName=PvpFlag.Name(target);
            source.messageQueue.addRpcMessage(AbilityMessages.Build(AbilityMessages.DamageCaster,true,AbilityMessages.ByGameObject,
                AbilityMessages.AbilityGameObject(ability),AbilityMessages.TypeGameObject,casterName,targetName,damage,raw-damage));
            target.messageQueue.addRpcMessage(AbilityMessages.Build(AbilityMessages.DamageTarget,false,AbilityMessages.ByGameObject,
                AbilityMessages.AbilityGameObject(ability),AbilityMessages.TypeGameObject,casterName,targetName,damage,raw-damage));
            ushort before=PvpCombat.HealthOf(target);
            bool died=PvpCombat.Damage(target,damage);
            Output.WriteDebugLog($"[CONDITIONAL-THROW] {ability} on {targetName} value{value} raw{raw} final{damage} hp{before}->{PvpCombat.HealthOf(target)}");
            if(died)PvpCombat.Defeated(target,source);
            source.FlushQueue();
        }
    }
}
