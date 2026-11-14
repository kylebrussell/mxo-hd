using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>Only610's stored on-use TauntAggro initializer; no new threat arithmetic or player taunt.</summary>
    public static class AbilityEngageFoes
    {
        public const ushort Ability=610;
        internal static bool HasInitializer(ushort ability) => ability==Ability && AbilityCatalog.Find(ability)?.Modifiers.Any(m=>
            m.Number("WhenEnabled",-1)==2 && m.Number("DurationType",-1)==1 && m.Text("StatModInitFunc")=="TauntAggro")==true;

        public sealed class Cast
        {
            internal readonly WorldClient Source;
            internal readonly AbilityCastInterrupts.Cast Normal;
            internal readonly ClientView Primary;
            internal readonly Mob PrimaryMob;
            internal readonly WorldClient PrimaryPlayer;
            internal readonly ClientData PrimaryOwner;
            internal readonly int PrimaryDeath,PrimaryWorld,PrimaryLife;
            internal readonly uint PrimarySpawn;
            internal readonly ulong PrimaryEntity;
            internal readonly long Transport;
            internal readonly uint Character,District;
            internal readonly HashSet<Mob> Applied=new HashSet<Mob>();
            internal Cast(WorldClient source,ushort view,AbilityCastInterrupts.Cast normal)
            {
                Source=source;Normal=normal;Primary=source.viewMan.getViewById(view);
                PrimaryMob=AbilityTargets.FindMob(source,view,out _);PrimaryPlayer=PrimaryMob==null?PvpCombat.PlayerInView(source,view):null;
                PrimaryOwner=PrimaryPlayer?.playerData;
                PrimarySpawn=Primary?.spawnId??0;PrimaryEntity=Primary?.entityId??0;
                PrimaryDeath=PrimaryMob?.DeathGeneration??0;PrimaryWorld=PrimaryPlayer?.playerData.worldGeneration??0;PrimaryLife=PrimaryPlayer?.playerData.lifeGeneration??0;
                Transport=source.TransportFence.Capture();Character=source.playerData.getCharID();District=source.playerData.getDistrictId();
            }
            internal ushort PrimaryView => Primary?.ViewID??0;
            internal bool SourceCurrent => Source!=null && Normal!=null && ReferenceEquals(Source,Normal.Client) && ReferenceEquals(Source.playerData,Normal.Data) &&
                Source.playerData.getCharID()==Character && Source.Alive && Source.playerData.getOnWorld() &&
                !Source.playerData.worldChangePending && !Source.playerData.DistrictIsLA() && !Source.playerData.isDead && !Source.playerData.duelDeath &&
                Source.playerData.worldGeneration==Normal.World && Source.playerData.lifeGeneration==Normal.Life &&
                Source.playerData.getDistrictId()==District && Source.TransportFence.IsCurrent(Transport) && PvpCombat.HealthOf(Source)>0;
            internal bool LandingCurrent => SourceCurrent && Normal.Completed && ControlActions.CanUseAbilities(Source);
            internal bool Current => LandingCurrent && Primary!=null && Primary.viewCreated && !Primary.viewNeedsToBeDeleted &&
                ReferenceEquals(Source.viewMan.getViewById(Primary.ViewID),Primary) && Primary.spawnId==PrimarySpawn && Primary.entityId==PrimaryEntity &&
                (PrimaryMob!=null ? ReferenceEquals(AbilityTargets.FindMob(Source,Primary.ViewID,out _),PrimaryMob) && !PrimaryMob.getIsDead() &&
                    PrimaryMob.getHealthC()>0 && PrimaryMob.DeathGeneration==PrimaryDeath : PrimaryPlayer!=null && PrimaryPlayer.Alive &&
                    ReferenceEquals(PrimaryPlayer.playerData,PrimaryOwner) && PrimaryPlayer.playerData.getOnWorld() && !PrimaryPlayer.playerData.worldChangePending &&
                    PrimaryPlayer.playerData.worldGeneration==PrimaryWorld && PrimaryPlayer.playerData.lifeGeneration==PrimaryLife &&
                    !PrimaryPlayer.playerData.isDead && !PrimaryPlayer.playerData.duelDeath && PvpCombat.HealthOf(PrimaryPlayer)>0);
        }

        internal sealed class Recipient
        {
            internal readonly Cast Cast;
            internal readonly Mob Mob;
            internal readonly ClientView View;
            internal readonly ulong Entity;
            internal readonly uint Spawn;
            internal readonly int Death;
            internal Recipient(Cast cast,Mob mob,ClientView view)
            {Cast=cast;Mob=mob;View=view;Entity=mob.getEntityId();Spawn=view.spawnId;Death=mob.DeathGeneration;}
            internal bool Current => Cast.SourceCurrent && Mob!=null && View!=null && !Mob.isFriendly && !Mob.getIsDead() && Mob.getHealthC()>0 &&
                Mob.DeathGeneration==Death && Mob.getEntityId()==Entity && Mob.getDistrict()==Cast.District && View.entityId==Entity &&
                View.viewCreated && !View.viewNeedsToBeDeleted && View.spawnId==Spawn && ReferenceEquals(Cast.Source.viewMan.getViewById(View.ViewID),View) &&
                ReferenceEquals(AbilityTargets.FindMob(Cast.Source,View.ViewID,out _),Mob);
        }

        // A retained taunt must not retain the whole cast's primary/recipient set,
        // or keep a disconnected client alive through a long-lived NPC.
        private sealed class Lifetime
        {
            private readonly WeakReference<WorldClient> source;
            private readonly WeakReference<ClientData> owner;
            private readonly Mob mob;private readonly ClientView view;
            private readonly uint character,district,spawn;private readonly ulong entity;
            private readonly int world,life,death;private readonly long transport;
            internal Lifetime(Recipient stamp)
            {
                source=new WeakReference<WorldClient>(stamp.Cast.Source);owner=new WeakReference<ClientData>(stamp.Cast.Normal.Data);
                mob=stamp.Mob;view=stamp.View;character=stamp.Cast.Character;district=stamp.Cast.District;spawn=stamp.Spawn;entity=stamp.Entity;
                world=stamp.Cast.Normal.World;life=stamp.Cast.Normal.Life;death=stamp.Death;transport=stamp.Cast.Transport;
            }
            internal bool Current()=>source.TryGetTarget(out var client) && owner.TryGetTarget(out var data) &&
                ReferenceEquals(client.playerData,data) && data.getCharID()==character && data.worldGeneration==world && data.lifeGeneration==life &&
                client.Alive && data.getOnWorld() && !data.worldChangePending && !data.DistrictIsLA() && !data.isDead && !data.duelDeath &&
                data.getDistrictId()==district && client.TransportFence.IsCurrent(transport) && PvpCombat.HealthOf(client)>0 &&
                !mob.getIsDead() && mob.getHealthC()>0 && !mob.isFriendly && mob.DeathGeneration==death && mob.getEntityId()==entity &&
                mob.getDistrict()==district && view.viewCreated && !view.viewNeedsToBeDeleted && view.spawnId==spawn && view.entityId==entity &&
                ReferenceEquals(client.viewMan.getViewById(view.ViewID),view) && ReferenceEquals(AbilityTargets.FindMob(client,view.ViewID,out _),mob);
        }

        internal static Cast Capture(WorldClient source,ushort ability,ushort view,AbilityCastInterrupts.Cast normal) =>
            HasInitializer(ability)?new Cast(source,view,normal):null;
        internal static Recipient Before(Cast cast,WorldClient source,Mob mob,ClientView view,ushort ability)
        {
            if(!HasInitializer(ability) || cast==null || !ReferenceEquals(cast.Source,source) || !cast.LandingCurrent || mob==null || view==null)return null;
            var stamp=new Recipient(cast,mob,view);return stamp.Current?stamp:null;
        }
        internal static bool After(Recipient recipient)
        {
            if(recipient==null || !recipient.Cast.LandingCurrent || !recipient.Current)return false;
            lock(recipient.Cast.Applied)if(!recipient.Cast.Applied.Add(recipient.Mob))return false;
            if(!recipient.Cast.LandingCurrent || !recipient.Current)return false;
            // Reuse647's established consumer, retaining610's lifecycle witness with the stored owner.
            var lifetime=new Lifetime(recipient);
            if(!CombatSpecials.ApplyNpcTaunt(recipient.Cast.Source,recipient.Mob,lifetime.Current))return false;
            Output.WriteDebugLog("[ENGAGE-FOES] source="+recipient.Cast.Character+" entity="+recipient.Entity+" view="+recipient.View.ViewID+
                " spawn="+recipient.Spawn+" death="+recipient.Death+" most-hated applied");
            return true;
        }
    }
}
