using System;

namespace hds
{
    /// <summary>OURS deterministic entry scores, not a recovered retail contest or1048->519 attribute link.
    /// Only a new player interlock against current Evade Combat217 uses these scores.</summary>
    public static class CombatInsightEngagement
    {
        internal readonly struct Scores
        {
            internal readonly long Force,Evade;
            internal readonly bool Computed;
            internal Scores(long force,long evade){Force=force;Evade=evade;Computed=true;}
            internal bool Allows=>Force>=Evade;
        }

        internal static Scores Calculate(int tacticForce,int directForce,int insight,int tacticEvade,int directEvade,int evadeBonus=0)
        {
            long baseline=Math.Max(0L,100L+tacticForce+directForce);
            // Decimal keeps the integer quotient exact even for hostile integer-limit inputs.
            long force=(long)(baseline*(100m+Math.Max(0,insight))/100m);
            long evadeBase=Math.Max(0L,100L+tacticEvade+directEvade);
            long evade=(long)(evadeBase*Math.Max(0m,100m+evadeBonus)/100m)+10;
            return new Scores(force,evade);
        }

        internal sealed class Scope
        {
            private readonly WorldClient source,target;
            private readonly ClientData sourceOwner,targetOwner;
            private readonly ClientView view;
            private readonly uint character,targetCharacter,district,spawn;
            private readonly ulong entity;
            private readonly int world,life,targetWorld,targetLife;
            private readonly long transport;
            internal Scope(WorldClient source,WorldClient target,uint handle)
            {
                this.source=source;this.target=target;sourceOwner=source.playerData;targetOwner=target.playerData;
                character=sourceOwner.getCharID();targetCharacter=targetOwner.getCharID();district=sourceOwner.getDistrictId();
                world=sourceOwner.worldGeneration;life=sourceOwner.lifeGeneration;targetWorld=targetOwner.worldGeneration;targetLife=targetOwner.lifeGeneration;
                // Never acquire another client's transport gate from this incoming request's publication scope.
                transport=source.TransportFence.Capture();
                view=source.viewMan.getViewById((ushort)handle);spawn=handle>>16;entity=targetOwner.getEntityId();
            }
            private static bool Living(WorldClient client,ClientData owner)=>ReferenceEquals(client.playerData,owner)&&client.Alive&&owner.getOnWorld()&&
                !owner.worldChangePending&&!owner.DistrictIsLA()&&!owner.isDead&&!owner.duelDeath&&PvpCombat.HealthOf(client)>0;
            internal bool Current=>Living(source,sourceOwner)&&Living(target,targetOwner)&&sourceOwner.getCharID()==character&&targetOwner.getCharID()==targetCharacter&&
                sourceOwner.worldGeneration==world&&sourceOwner.lifeGeneration==life&&targetOwner.worldGeneration==targetWorld&&targetOwner.lifeGeneration==targetLife&&
                sourceOwner.getDistrictId()==district&&targetOwner.getDistrictId()==district&&source.TransportFence.IsCurrent(transport)&&
                view!=null&&view.viewCreated&&!view.viewNeedsToBeDeleted&&view.spawnId==spawn&&view.entityId==entity&&targetOwner.getEntityId()==entity&&
                ReferenceEquals(source.viewMan.getViewById(view.ViewID),view);
        }

        internal static bool Allows(WorldClient source,WorldClient target,uint handle,out Scores scores)
        {
            scores=default;
            // Positive520 alone never recreates an evasion gate after1125 suppression or217 removal.
            if(!EvasionShield.HasState(target))return true;
            var scope=new Scope(source,target,handle);
            if(!scope.Current)return false;
            scores=Calculate(CombatTactics.Get(source.playerData.combatTactic).ForceCombat,AbilityStats.Player(source,519),
                AbilityStats.Player(source,1048),CombatTactics.Get(target.playerData.combatTactic).EvadeInterlock,
                AbilityStats.Player(target,520),AbilityStats.Player(target,1049));
            if(!scope.Current)return false;
            // A removed/suppressed217 no longer resists entry; no sticky gate or shield-health pool.
            if(!EvasionShield.HasState(target))return true;
            Output.WriteDebugLog($"[COMBAT-INSIGHT OURS] {PvpFlag.Name(source)} -> {PvpFlag.Name(target)} force{scores.Force} evade{scores.Evade} allowed{scores.Allows}");
            return scores.Allows;
        }
    }
}
