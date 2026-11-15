using System;
using System.Collections.Generic;
using System.Linq;
namespace hds
{
    /// <summary>Recovered gun-vs-fists math, OURS ordinary-free-fire caller/distance/opponent policy.
    /// Per-shot snapshots only. No changes to interlock formulas, damage, RNG or weapon reach.</summary>
    public static class SharpshooterFreeFire
    {
        internal static bool Live(WorldClient c)=>c!=null&&c.Alive&&c.playerData!=null&&c.playerInstance!=null&&PvpCombat.HealthOf(c)>0&&c.playerData.getOnWorld()&&
            !c.playerData.isDead&&!c.playerData.duelDeath&&!c.playerData.worldChangePending;
        internal static bool Live(Mob m)=>m!=null&&!m.getIsDead()&&m.getHealthC()>0&&m.getIsSpawned();
        private static string Name(WorldClient c)=>c?.playerInstance==null?"":PvpFlag.Name(c);
        private static Func<bool> Stamp(WorldClient c)
        {
            if(c?.playerData==null)return ()=>false;
            var owner=c.playerData;var world=owner.worldGeneration;var life=owner.lifeGeneration;var district=owner.getDistrictId();
            return ()=>Live(c)&&ReferenceEquals(c.playerData,owner)&&owner.worldGeneration==world&&owner.lifeGeneration==life&&owner.getDistrictId()==district;
        }
        private static Func<bool> Stamp(Mob m)
        {
            if(m==null)return ()=>false;
            var entity=m.getEntityId();var death=m.DeathGeneration;var district=m.getDistrict();var instance=m.missionInstance;var phase=m.missionPhase;
            return ()=>Live(m)&&m.getEntityId()==entity&&m.DeathGeneration==death&&m.getDistrict()==district&&m.missionInstance==instance&&m.missionPhase==phase;
        }
        public static int Modifier(byte gunClass,bool defenderHasGun,float metres,int opponents,bool loaded148)
        {
            if(!float.IsFinite(metres)||metres<0)return 0;
            return CombatRules.GunAgainstFistsModifier(gunClass,defenderHasGun,CombatHandler.InMeleeReach(metres),opponents,
                intoMeleePenaltyNegated:loaded148,shortRangePenaltyNegated:loaded148);
        }
        internal static int NpcOpponents(Mob target,WorldClient currentShooter)
        {
            if(!Live(target))return 0;
            var clients=new HashSet<WorldClient>(target.CombatantsSnapshot().Where(c=>Live(c)&&c.playerData.getDistrictId()==target.getDistrict()));
            if(Live(currentShooter)&&currentShooter.playerData.getDistrictId()==target.getDistrict())clients.Add(currentShooter);
            return clients.Count;
        }
        internal static int PlayerOpponents(WorldClient target,WorldClient currentShooter=null,Mob currentNpc=null)
        {
            if(!Live(target))return 0;
            var clients=new HashSet<WorldClient>();var fights=new HashSet<PvpFight>();
            //The registry lock is released before any fight snapshot; callers take this BEFORE their shot/RNG locks.
            foreach(var pair in CombatManager.SnapshotHandlers())
            {
                var h=pair.Value;var fire=h.pvpGunfire;
                if(fire!=null&&fire.IsActive&&ReferenceEquals(fire.Shooter,pair.Key)&&ReferenceEquals(fire.Target,target)&&Live(pair.Key))clients.Add(pair.Key);
                var fight=h.pvpFight;
                if(fight!=null&&fights.Add(fight))foreach(var c in fight.CurrentOpponents(target))if(Live(c))clients.Add(c);
            }
            clients.RemoveWhere(c=>!Live(c)||ReferenceEquals(c,target)||c.playerData.getDistrictId()!=target.playerData.getDistrictId());
            if(Live(currentShooter)&&!ReferenceEquals(currentShooter,target)&&currentShooter.playerData.getDistrictId()==target.playerData.getDistrictId())clients.Add(currentShooter);
            Mob[] mobs;lock(WorldServer.mobs.SyncRoot)mobs=WorldServer.mobs.Cast<object>().OfType<Mob>().ToArray();
            var npc=new HashSet<Mob>(mobs.Where(m=>Live(m)&&m.getDistrict()==target.playerData.getDistrictId()&&
                m.currentState==(int)Mob.statusList.COMBAT&&ReferenceEquals(m.getCombatTarget(),target)));
            if(Live(currentNpc)&&currentNpc.getDistrict()==target.playerData.getDistrictId())npc.Add(currentNpc);
            return clients.Count+npc.Count;
        }
        public sealed class Shot
        {
            private readonly Func<bool> current;
            private readonly Func<float> distance;
            private readonly Func<bool> defenderGun;
            private readonly WorldClient player;
            private readonly uint weapon;
            private readonly string actor;
            public int Opponents {get;}
            internal Shot(Func<bool> current,Func<float> distance,Func<bool> defenderGun,WorldClient player,uint weapon,int opponents,string actor)
            {this.current=current;this.distance=distance;this.defenderGun=defenderGun;this.player=player;this.weapon=weapon;Opponents=opponents;this.actor=actor;}
            public int AddToAttack(int existing,uint actualWeapon)
            {
                if(actualWeapon!=weapon||!current())return existing;
                bool loaded=player!=null&&AbilityPassives.IsLoaded(player,148);float metres=distance();bool gun=defenderGun();
                int modifier=Modifier(RangedWeapons.AttackClass(weapon),gun,metres,Opponents,loaded);
                Output.WriteDebugLog($"[SHARPSHOOTER OURS] actor={actor} class{RangedWeapons.AttackClass(weapon)} defenderGun{gun} metres{metres:F3} opponents{Opponents} loaded148{loaded} modifier{modifier} attackBonus{existing}->{existing+modifier}");
                return existing+modifier;
            }
        }
        public static Shot AgainstNpc(WorldClient source,Mob target,uint weapon)
        {
            var a=Stamp(source);var b=Stamp(target);int count=NpcOpponents(target,source);
            return new Shot(()=>a()&&b()&&source.playerData.getDistrictId()==target.getDistrict(),
                ()=>CombatCalculator.CalculateDistanceMetres(source.playerInstance.Position.getValue(),target.getXPos(),target.getYPos(),target.getZPos()),
                ()=>RangedWeapons.IsGun(target.getWeapon()),source,weapon,count,Name(source));
        }
        public static Shot AgainstPlayer(WorldClient source,WorldClient target,uint weapon)
        {
            var a=Stamp(source);var b=Stamp(target);int count=PlayerOpponents(target,source);
            return new Shot(()=>a()&&b()&&source.playerData.getDistrictId()==target.playerData.getDistrictId(),
                ()=>PvpCombat.MetresBetween(source,target),()=>RangedWeapons.IsGun(PvpGunfire.GunOf(target)),source,weapon,count,Name(source));
        }
        public static Shot FromNpc(Mob source,WorldClient target,uint weapon)
        {
            var a=Stamp(source);var b=Stamp(target);int count=PlayerOpponents(target,currentNpc:source);
            return new Shot(()=>a()&&b()&&source.getDistrict()==target.playerData.getDistrictId(),
                ()=>CombatCalculator.CalculateDistanceMetres(target.playerInstance.Position.getValue(),source.getXPos(),source.getYPos(),source.getZPos()),
                ()=>RangedWeapons.IsGun(PvpGunfire.GunOf(target)),null,weapon,count,source?.getName()??"");
        }
    }
}
