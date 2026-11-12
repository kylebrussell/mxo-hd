using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using hds.shared;
using System.Runtime.CompilerServices;

namespace hds
{
    /// <summary>spy/sneaks.py172, spy/misc.py719 and tempmods.py2380. Shield resistance/520 is not reconstructed.</summary>
    public static class StealthShieldAbilities
    {
        public const ushort BullDog=1110, DisableEvasion=1125, NullTarget=710;
        public const byte Aggro=12, KnifeThrow=64;
        public const double RestealthSeconds=1.5, MotionSeconds=1.5; // OURS replay/cleanup window; native clip duration is not recovered.
        private static readonly Lazy<HashSet<uint>> tools=new Lazy<HashSet<uint>>(()=>
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("stealth_throw_tools.json")));
            return doc.RootElement.GetProperty("tools").EnumerateArray().Select(e=>e.GetUInt32()).ToHashSet();
        });
        public static bool IsTool(uint goid)=>tools.Value.Contains(goid);
        private sealed class Session { internal int Action; internal Motion Motion; }
        private sealed class Motion { internal int Token,World,Life;internal ulong Target;internal byte Counter,PreviousStance;internal double Until; internal readonly Dictionary<WorldClient,(uint Actor,uint Target)> Sent=new Dictionary<WorldClient,(uint Actor,uint Target)>(); }
        private static readonly ConditionalWeakTable<WorldClient,Session> sessions=new ConditionalWeakTable<WorldClient,Session>();
        private static Session Of(WorldClient c)=>sessions.GetValue(c,_=>new Session());
        public sealed class Cast
        {
            internal int World,Life,Action,StealthVersion,Level;
            internal float InvisibleRemaining;
            internal bool Invisible;
            internal object Target; internal int TargetWorld,TargetLife,TargetDeath;
        }
        public static string Describe(ushort id)=>AbilityCatalog.Canonical(id) switch
        {
            BullDog=>"NeutralDOT terminal at .5s: raw10 once (OURS absent-interval fallback, not retail-exact); builtin Aggro12/KnifeThrow64 per-view target; native damage/clip timing unresolved",
            DisableEvasion=>"normal hostile cast: active EvasionState624 target + ITOt tool; suppress only217 child for8s; caster681 marker1.5s then guarded invis/sneak reapply; shield-health/520/throw contest unresolved",
            EvasionShield.Parent=>"parent217 and record IDs retained while1125 suppresses624; own/observer/late-view shield/state updates; shield damage/resistance/520 consumer not reconstructed",
            _=>null
        };
        public static bool Handles(ushort id)=>AbilityCatalog.Canonical(id)==BullDog || AbilityCatalog.Canonical(id)==DisableEvasion;
        public static string Refusal(WorldClient caster,ushort id,ushort view)
        {
            if(!Handles(id))return null;
            if(!Stealth.IsHidden(caster))return "This stealth throw requires Sneak or Invisibility.";
            if(id==DisableEvasion)
            {
                if(!Store.dbManager.WorldDbHandler.GetInventoryItems(caster.playerData.getCharID()).Any(i=>i.Slot<97&&IsTool(i.Goid)))
                    return "Disable Evasion requires a carried ITOt tool.";
                var victim=PvpCombat.PlayerInView(caster,view);
                bool evasion=victim!=null&&EvasionShield.HasState(victim);
                if(!evasion)return "Disable Evasion requires an active Evasion State.";
            }
            return null;
        }
        public static Cast Capture(WorldClient caster,ushort targetView=0)
        {
            var state=Stealth.Current(caster);
            bool invis=state?.Def.Kind==Stealth.Kind.Invisibility || state?.Def.Kind==Stealth.Kind.Imparted;
            var target=targetView>2?PvpCombat.PlayerInView(caster,targetView):null;
            var mob=targetView>2&&target==null?AbilityTargets.FindMob(caster,targetView,out _):null;
            return new Cast {Target=(object)target??mob,TargetWorld=target?.playerData.worldGeneration??0,TargetLife=target?.playerData.lifeGeneration??0,TargetDeath=mob?.DeathGeneration??0,World=caster.playerData.worldGeneration,Life=caster.playerData.lifeGeneration,Level=state?.Level??1,
                Invisible=invis,InvisibleRemaining=invis?(float)Math.Max(0,state.Expires-TimeUtils.GetSimTimeSeconds()):0};
        }
        public static void Arm(WorldClient caster,Cast cast)
        { cast.Action=Of(caster).Action;cast.StealthVersion=Stealth.Version(caster); }
        public static bool Live(WorldClient caster,Cast cast)=>caster!=null && caster.Alive && caster.playerData.getOnWorld() &&
            !caster.playerData.isDead && !caster.playerData.duelDeath && caster.playerData.worldGeneration==cast.World && caster.playerData.lifeGeneration==cast.Life;
        public static void ActionTaken(WorldClient caster)
        {
            var session=Of(caster);session.Action++;
            if(session.Motion!=null){var motion=session.Motion;session.Motion=null;
                if(caster.playerData.getOnWorld()&&!caster.playerData.isDead&&!caster.playerData.duelDeath)ResetMotion(caster,motion.PreviousStance);}
        }
        public static void Execute(WorldClient caster,AbilityItem ability,ushort targetView,Cast cast)
        {
            if(!Live(caster,cast)||!ControlActions.CanUseAbilities(caster)||Of(caster).Action!=cast.Action)return;
            ushort id=ability.getAbilityID();
            Mob mob=AbilityTargets.FindMob(caster,targetView,out var mobView);
            WorldClient target=mob==null?PvpCombat.PlayerInView(caster,targetView):null;
            if(!ReferenceEquals(cast.Target,(object)target??mob)||target!=null&&(target.playerData.worldGeneration!=cast.TargetWorld||target.playerData.lifeGeneration!=cast.TargetLife)||mob!=null&&mob.DeathGeneration!=cast.TargetDeath)return;
            if(mob==null && (target==null || PvpCombat.Check(caster,target)!=PvpRules.Verdict.Allowed))return;
            if(mob!=null && (mob.getIsDead()||mob.isFriendly))return;
            if(target!=null && (target.playerData.isDead||target.playerData.duelDeath))return;
            double range=AbilityCatalog.Find(id)?.Number("Ability/SelectionRange")??0;
            double distance=target!=null?PvpCombat.MetresBetween(caster,target):CombatCalculator.CalculateDistanceMetres(caster.playerInstance.Position.getValue(),mob.getXPos(),mob.getYPos(),mob.getZPos());
            if(range>0 && distance>range)return;
            if(id==DisableEvasion && (target==null||!EvasionShield.HasState(target)||!Store.dbManager.WorldDbHandler.GetInventoryItems(caster.playerData.getCharID()).Any(i=>i.Slot<97&&IsTool(i.Goid))))return;
            var subject=target!=null?(TimedEffects.Subject)TimedEffects.ForPlayer(target):TimedEffects.ForMob(mob);
            int targetWorld=target?.playerData.worldGeneration??0,targetLife=target?.playerData.lifeGeneration??0;
            int death=mob?.DeathGeneration??0;
            double now=TimeUtils.GetSimTimeSeconds();
            PvpFlag.OnFightStarts(caster,target!=null);
            if(id==BullDog)
            {
                PlayThrow(caster,target?.playerData.getEntityId()??mob.getEntityId(),cast);
                var effect=new TempMods.Effect{Ability=id,Mods=new[]{new TempMods.Mod{Kind=TempMods.KindLinked,Target=NullTarget,
                    MinValue=10,MaxValue=10,Seconds=.5f,Proc=TempMods.Proc.Terminal}}};
                TimedEffects.Apply(subject,effect,now,caster,TimedEffects.PlayerName(caster),targetView,configure:m=>m.OnExpired=(s,a)=>
                {
                    // OURS:1110 omits UpdateFuncInterval. Use its raw10 once on expiry, not guessed50/instant base damage.
                    if(!Live(caster,cast)||target!=null&&(target.playerData.worldGeneration!=targetWorld||target.playerData.lifeGeneration!=targetLife)||
                        mob!=null&&(mob.getIsDead()||mob.DeathGeneration!=death))return;
                    if(target!=null && PvpCombat.Check(caster,target)!=PvpRules.Verdict.Allowed)return;
                    s.Ticked(a,10);
                    Output.WriteDebugLog("[STEALTH-FREE]1110 terminal10 OURS on "+(target!=null?TimedEffects.PlayerName(target):mob.getName()));
                });
            }
            else if(id==DisableEvasion)
            {
                var effect=new TempMods.Effect{Ability=id,Mods=new[]{new TempMods.Mod{Kind=TempMods.KindStat,Target=NullTarget,
                    Seconds=8,Proc=TempMods.Proc.Terminal}}};
                TimedEffects.Apply(subject,effect,now,caster,TimedEffects.PlayerName(caster),targetView);
                // Native script schedules the self marker after a successful throw. We do so on landed/validated cast.
                var marker=new TempMods.Effect{Ability=id,Mods=new[]{new TempMods.Mod{Kind=TempMods.KindStat,Target=Stealth.ISDrainTargetAbility,
                    Seconds=(float)RestealthSeconds,Proc=TempMods.Proc.Terminal,MinValue=cast.Invisible?(int)cast.InvisibleRemaining:-1,MaxValue=cast.Invisible?(int)cast.InvisibleRemaining:-1}}};
                TimedEffects.Apply(TimedEffects.ForPlayer(caster),marker,now,caster,TimedEffects.PlayerName(caster),configure:m=>m.OnExpired=(s,a)=>Restore(caster,cast,a.Value));
                Output.WriteDebugLog("[STEALTH-FREE]1125 landed; target suppression8s; caster restealth1.5s");
            }
            caster.FlushQueue();target?.FlushQueue();
        }
        internal static bool CanRestore(WorldClient caster,Cast cast)=>Live(caster,cast)&&Of(caster).Action==cast.Action &&
            Stealth.Version(caster)==cast.StealthVersion && caster.playerData.stance!=8 &&
            !(caster.playerInstance.JumpEndTime.getValue() is byte[] jump && jump.Length>=4 && BitConverter.ToUInt32(jump,0)>CombatHandler.CombatStartTime());
        internal static bool PendingRestealth(WorldClient caster)=>TimedEffects.ForPlayer(caster).Effects.Snapshot().Exists(m=>
            m.Parent==DisableEvasion&&m.Target==Stealth.ISDrainTargetAbility&&m.Expires>TimeUtils.GetSimTimeSeconds());
        private static void Restore(WorldClient caster,Cast cast,int markerValue)
        {
            if(!CanRestore(caster,cast))return;
            ushort ability=markerValue>0?Stealth.InvisibilityGrantAbility:Stealth.SneakGrantAbility;
            // misc.py303 consumes GhostStrike's signed integer value as the override duration.
            Stealth.Begin(caster,Stealth.Defs[ability],cast.Level,durationOverride:markerValue>0?(float?)markerValue:null);
            caster.FlushQueue();
            Output.WriteDebugLog("[STEALTH-FREE]1125 restored "+ability);
        }
        public static byte[] BuildThrow(bool own,byte counter,ushort ability,uint target)
        {
            var bytes=new List<byte>();if(own)bytes.AddRange(new byte[]{2,0});
            // Reader6267192c: built-in action byte64 has no named-action25 u16 prefix.
            // Target kind1 resolves a receiver-local view/spawn handle (62671a21..55); bit0x40 would name a GOID.
            bytes.AddRange(new byte[]{1,0x20,counter,1,Aggro,KnifeThrow});bytes.AddRange(BitConverter.GetBytes(ability));bytes.AddRange(BitConverter.GetBytes(target));return bytes.ToArray();
        }
        private static void PlayThrow(WorldClient caster,ulong target,Cast cast)
        {
            var state=Of(caster);var motion=new Motion{Token=state.Action,World=cast.World,Life=cast.Life,Target=target,
                Counter=++caster.playerData.emoteCounter,PreviousStance=caster.playerData.stance,Until=TimeUtils.GetSimTimeSeconds()+MotionSeconds};
            state.Motion=motion;
            var ownTarget=caster.viewMan.FindViewForEntity(target);
            if(ownTarget?.viewCreated==true)caster.messageQueue.addObjectMessage(BuildThrow(true,motion.Counter,BullDog,DuelPackets.Handle(ownTarget.ViewID,ownTarget.spawnId)),false);
            foreach(var observer in SocialHandler.OnlineWhere(c=>c!=caster&&c.playerData.getDistrictId()==caster.playerData.getDistrictId()))
            {var actor=observer.viewMan.FindViewForEntity(caster.playerData.getEntityId());if(actor?.viewCreated==true)SendMotion(observer,caster,actor.ViewID,motion);}
            DelayedActions.AfterFor(caster,MotionSeconds,()=>
            {
                if(!Live(caster,cast)||state.Motion!=motion)return;state.Motion=null;
                ResetMotion(caster,motion.PreviousStance);
            });
        }
        private static void SendMotion(WorldClient observer,WorldClient caster,ushort actor,Motion motion)
        {
            if(Of(caster).Motion!=motion||Of(caster).Action!=motion.Token||motion.Until<=TimeUtils.GetSimTimeSeconds()||
                caster.playerData.worldGeneration!=motion.World||caster.playerData.lifeGeneration!=motion.Life||caster.playerData.isDead||caster.playerData.duelDeath)return;
            var target=observer.viewMan.FindViewForEntity(motion.Target);if(target?.viewCreated!=true)return;
            var actorView=observer.viewMan.getViewById(actor);if(actorView?.viewCreated!=true)return;
            uint actorHandle=DuelPackets.Handle(actor,actorView.spawnId);
            uint targetHandle=DuelPackets.Handle(target.ViewID,target.spawnId);
            lock(motion.Sent){if(motion.Sent.TryGetValue(observer,out var sent)&&sent==(actorHandle,targetHandle))return;motion.Sent[observer]=(actorHandle,targetHandle);}
            observer.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(actor,BuildThrow(false,motion.Counter,BullDog,DuelPackets.Handle(target.ViewID,target.spawnId))),false);observer.FlushQueue();
        }
        private static void ResetMotion(WorldClient caster,byte stance)
        {
            double x=0,y=0,z=0;NumericalUtils.LtVector3dToDoubles(caster.playerInstance.Position.getValue(),ref x,ref y,ref z);
            byte counter=++caster.playerData.emoteCounter;
            caster.messageQueue.addObjectMessage(DeathPresentation.Build(true,counter,stance,0,DeathPresentation.NoAbility,(float)x,(float)y,(float)z,CombatHandler.CombatStartTime()),false);
            PvpFlag.ToWatchers(caster,DeathPresentation.Build(false,counter,stance,0,DeathPresentation.NoAbility,(float)x,(float)y,(float)z));caster.FlushQueue();
        }
        public static void CheckObservers()
        {
            foreach(var pair in sessions)
            {
                var caster=pair.Key;var motion=pair.Value.Motion;
                if(motion==null||motion.World!=caster.playerData.worldGeneration||motion.Life!=caster.playerData.lifeGeneration||
                    caster.playerData.isDead||motion.Until<=TimeUtils.GetSimTimeSeconds())continue;
                foreach(var observer in SocialHandler.OnlineWhere(c=>c!=caster&&c.playerData.getDistrictId()==caster.playerData.getDistrictId()))
                {var actor=observer.viewMan.FindViewForEntity(caster.playerData.getEntityId());if(actor?.viewCreated==true)SendMotion(observer,caster,actor.ViewID,motion);}
            }
        }
        public static void AfterSpawn(WorldClient observer,WorldClient caster,ushort actor)
        {
            var motion=Of(caster).Motion;
            if(motion!=null&&motion.World==caster.playerData.worldGeneration&&motion.Life==caster.playerData.lifeGeneration&&
                !caster.playerData.isDead&&motion.Until>TimeUtils.GetSimTimeSeconds())SendMotion(observer,caster,actor,motion);
        }
    }
}
