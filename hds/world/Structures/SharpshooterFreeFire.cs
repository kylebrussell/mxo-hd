using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;
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
        internal static uint EquippedWeapon(WorldClient c)
        {
            byte[] bytes=c?.playerInstance?.EquippedItemID?.getValue();
            return bytes!=null&&bytes.Length>=4?NumericalUtils.ByteArrayToUint32(bytes,1):0;
        }
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
            private readonly Func<uint> defenderWeapon;
            private readonly WorldClient player;
            private readonly uint weapon;
            private readonly string actor;
            private readonly uint targetWeapon;
            private readonly bool loaded,validAtCapture;
            public int Opponents {get;}
            public bool CountSampled {get;}
            internal Shot(Func<bool> current,Func<float> distance,Func<uint> defenderWeapon,WorldClient player,uint weapon,Func<int> count,string actor)
            {
                this.current=current;this.distance=distance;this.defenderWeapon=defenderWeapon;this.player=player;this.weapon=weapon;this.actor=actor;
                validAtCapture=current();targetWeapon=validAtCapture?defenderWeapon():0;
                loaded=validAtCapture&&player!=null&&AbilityPassives.IsLoaded(player,148);
                //Each skipped dimension is irrelevant to the literal formula. Fence it again before use.
                CountSampled=validAtCapture&&RangedWeapons.AttackClass(weapon)!=0&&!RangedWeapons.IsGun(targetWeapon)&&!loaded;
                Opponents=CountSampled?count():0;
            }
            public int AddToAttack(int existing,uint actualWeapon)
            {
                if(actualWeapon!=weapon||!validAtCapture||!current()||defenderWeapon()!=targetWeapon||
                    (player!=null&&AbilityPassives.IsLoaded(player,148))!=loaded)return existing;
                float metres=distance();bool gun=RangedWeapons.IsGun(targetWeapon);
                int modifier=Modifier(RangedWeapons.AttackClass(weapon),gun,metres,Opponents,loaded);
                Output.WriteDebugLog($"[SHARPSHOOTER OURS] actor={actor} class{RangedWeapons.AttackClass(weapon)} defenderGun{gun} metres{metres:F3} opponents{Opponents} countSampled{CountSampled} loaded148{loaded} modifier{modifier} attackBonus{existing}->{existing+modifier}");
                return existing+modifier;
            }
        }
        public static Shot AgainstNpc(WorldClient source,Mob target,uint weapon)
        {
            var a=Stamp(source);var b=Stamp(target);
            return new Shot(()=>a()&&b()&&source.playerData.getDistrictId()==target.getDistrict(),
                ()=>CombatCalculator.CalculateDistanceMetres(source.playerInstance.Position.getValue(),target.getXPos(),target.getYPos(),target.getZPos()),
                ()=>target.getWeapon(),source,weapon,()=>NpcOpponents(target,source),Name(source));
        }
        public static Shot AgainstPlayer(WorldClient source,WorldClient target,uint weapon)
        {
            var a=Stamp(source);var b=Stamp(target);
            return new Shot(()=>a()&&b()&&source.playerData.getDistrictId()==target.playerData.getDistrictId(),
                ()=>PvpCombat.MetresBetween(source,target),()=>EquippedWeapon(target),source,weapon,()=>PlayerOpponents(target,source),Name(source));
        }
        public static Shot FromNpc(Mob source,WorldClient target,uint weapon)
        {
            var a=Stamp(source);var b=Stamp(target);
            return new Shot(()=>a()&&b()&&source.getDistrict()==target.playerData.getDistrictId(),
                ()=>CombatCalculator.CalculateDistanceMetres(target.playerInstance.Position.getValue(),source.getXPos(),source.getYPos(),source.getZPos()),
                ()=>EquippedWeapon(target),null,weapon,()=>PlayerOpponents(target,currentNpc:source),source?.getName()??"");
        }
    }
}
