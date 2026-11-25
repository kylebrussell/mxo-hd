using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Explicit sourced health callbacks; deterministic catalog recognition never samples runtime RNG.</summary>
    public static class AbilityHealthCallbacks
    {
        public const ushort LineUpAbility = 506;
        internal const ushort ReactiveCode = 941, ReactiveCodeEffect = 942;
        // Organization E toggles and the internal effects their health-change callbacks reapply (tempmods.py 2621-2658).
        public const ushort SystemControl = 1169, Retribution = 1173, ChampionOfZion = 1177;
        public const ushort SystemControlEffect = 1180, ChampionOfZionEffect = 1181, RetributionEffect = 1182;
        // ApplyMachine/Merv/ZionEffectHealthChance: random.randrange(0, 100) < 5. The stored callback MinValue 240 is the
        // AddRecastTimer term value on the same modifier, not this chance.
        public const int OrganizationEffectChance = 5;
        public static ushort OrganizationEffect(string proc) => proc switch
        {
            "ApplyMachineEffectHealthChance" => SystemControlEffect,
            "ApplyMervEffectHealthChance" => RetributionEffect,
            "ApplyZionEffectHealthChance" => ChampionOfZionEffect,
            _ => 0
        };
        public static bool OrganizationParent(ushort id) => id == SystemControl || id == Retribution || id == ChampionOfZion;
        private static readonly Random random = new Random();
        public static Func<int> Random15 = () => { lock(random) return random.Next(32768); };
        public static Func<int> Random100 = () => { lock(random) return random.Next(100); };
        private static int? QaRoll(string name,int maximum) => Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS")=="1" &&
            (Environment.GetEnvironmentVariable("MXO_DB_NAME")??"").StartsWith("mxotest_",StringComparison.Ordinal) &&
            int.TryParse(Environment.GetEnvironmentVariable(name),out int roll) && roll>=0 && roll<=maximum ? roll : (int?)null;
        public static int Roll15() => QaRoll("MXO_CALLBACK_QA_ROLL15",32767) ?? Math.Clamp(Random15(),0,32767);
        private sealed class FixedRollRandom:Random
        {
            private readonly int value;
            public FixedRollRandom(int value) { this.value=value; }
            public override int Next(int maximum)=>Math.Min(value,Math.Max(0,maximum-1));
        }
        public static Random TermRandom() => QaRoll("MXO_CALLBACK_QA_ROLL100",99) is int roll ? new FixedRollRandom(roll) : null;
        public static int Roll100() => QaRoll("MXO_CALLBACK_QA_ROLL100",99) ?? Math.Clamp(Random100(),0,99);
        public static bool Template(ushort id) => id==196 || id==693 || id==861 || id==941 || id==943 || OrganizationParent(id);

        public static bool TryGet(ushort id,int casterLevel,int roll15,out AbilityBuffs.Buff buff)
        {
            buff=null;
            if(!Template(id)) return false;
            var entry=AbilityCatalog.Find(id);
            var mods=entry.Modifiers.Where(m=>m.Number("WhenEnabled",-1)==2).ToList();
            var callback=mods.FirstOrDefault(m=>!string.IsNullOrEmpty(m.Text("StatModHealthChangedFunc")));
            int callbackValue=callback==null?0:(int)callback.Number("MinValue");
            if(id==196 && !AbilityValues.TryRandomValue(callback,roll15,out callbackValue)) return false;
            var drain=mods.FirstOrDefault(m=>m.Text("StatModUpdateFunc")=="ISDrainWithAbilityDeactivation");
            var stats=mods.Where(m=>m.Target!=0 && m.Target!=710 && m!=drain).Select(m=>
            {
                AbilityValues.TryValue(m,0,casterLevel,out int value);
                return new AbilityBuffs.Modifier(AbilityBuffs.KindStat,m.Target,value);
            }).ToList();
            bool toggle=entry.Number("Ability/IsAToggleAbility")==1 && id!=693;
            if(callback!=null) stats.Add(new AbilityBuffs.Modifier(TempMods.KindFx,id,callbackValue));
            buff=new AbilityBuffs.Buff {
                Seconds=toggle?0:(float)mods.Max(m=>m.Number("Duration")), Exclusive=toggle, Toggle=toggle,
                Family=entry.Number("Ability/StackingType")==0?0:1000+(int)entry.Number("Ability/StackingType"),
                Modifiers=stats.ToArray(), HealthChangedProc=callback?.Text("StatModHealthChangedFunc"),
                HealthChangedValue=OrganizationParent(id)?OrganizationEffectChance:callbackValue, DrainAmount=(int)(drain?.Number("MinValue")??0),
                DrainSeconds=drain?.Number("UpdateFuncInterval")??0, TermSweepChance=id==693?4:0
            };
            return true;
        }

        public static bool InternalEffect(ushort id) => id==692 || id==693 || id==862 || id==872 || id==ReactiveCodeEffect || id==944 ||
            id==SystemControlEffect || id==ChampionOfZionEffect || id==RetributionEffect;
        public static string Describe(ushort id) => id switch
        {
            196 => "health callback: one native function0 sample10..19 per application; external damage/healing triggers stored self heal; nested-parent/HP0 guards are ours",
            506 => "Line Up:20s ballistic accuracy/12s damage; damage-only parent removal while12s component lives; Subject preserves prior concealment; init reapply1137",
            693 => "Deflect Virus child:5s caster-level defences; term4% negative-condition cleanse, including refresh/dispel",
            871 => "while-loaded permanent health listener reapplies872; stored level99 prerequisite makes retail50 parent inaccessible",
            872 => "internal871 health-change child:15% integer-percent roll, IS+5 capped at maximum; native RNG sequence unknown",
            861 => "Balance:30s health-change listener reapplies862; no damage-only filter",
            862 => "internal Balance effect: native function0 NeutralHeal6 sample50..99 to self, no healing influence",
            941 => "Reactive Code:7IS/s toggle; health changes reapply942, including external healing",
            942 => "internal Reactive effect: neutral5 sample50..149 to legal hostile players/NPCs within3m",
            943 => "Code Conversion:6IS/s toggle; health changes roll30% to reapply944",
            944 => "internal Code Conversion: sample60..109 times caster healing influence to nearby friendly players; selector7 policy, no friendly-NPC healing",
            SystemControl => "Organization E toggle: 25 IS, 25 IS/5s drain; damage that leaves the owner below maximum health rolls 5% to reapply 1180; TestOrgAbility has no stored OrganizationID here; 240s own and shared family reuse on toggle-off",
            Retribution => "Organization E toggle: 25 IS, 25 IS/5s drain; damage that leaves the owner below maximum health rolls 5% to reapply 1182; TestOrgAbility has no stored OrganizationID here; 240s own and shared family reuse on toggle-off",
            ChampionOfZion => "Organization E toggle: 25 IS, 25 IS/5s drain; damage that leaves the owner below maximum health rolls 5% to reapply 1181; TestOrgAbility has no stored OrganizationID here; 240s own and shared family reuse on toggle-off",
            SystemControlEffect => "internal1169 System Control effect: Stun5s on legal hostile players/NPCs within5m in sight; AddControlImmunityTimerVal10s once per immunity window; direct cast refused",
            ChampionOfZionEffect => "internal1177 Champion of Zion effect: NeutralHeal6 250 without healing influence and ZionRestoreIS+25 (capped) to the owner and friendly players within5m in sight; selector7 policy, no friendly-NPC healing; direct cast refused",
            RetributionEffect => "internal1173 Retribution effect: neutral5 fixed150 to legal hostile players/NPCs within5m in sight; direct cast refused",
            _ => ""
        };

        private sealed class Pending
        {
            public WorldClient Client;
            public AbilityBuffs.Active Active;
            public int Damage,World,Life;
            public bool BelowMaximum, Passive;
            public int PassiveGeneration;
        }
        [ThreadStatic] private static Queue<Pending> pending;
        [ThreadStatic] private static HashSet<(WorldClient,ushort)> fired;
        [ThreadStatic] private static bool dropped;
        // OURS: a parent runs once per synchronous health transaction, including cross-player proc chains.
        // Native Interlock.giveHealth vs AbilityInv damage event delivery is unavailable. This avoids recursive
        // self-healing and reactive ping-pong without filtering ordinary external healing callbacks.
        public const int MaxCallbacksPerTransaction=256;
        public static void HealthChanged(WorldClient client,int damage,bool belowMaximum)
        {
            if(damage==0 || client.playerData.isDead || client.playerData.duelDeath) return;
            var snapshot=new List<AbilityBuffs.Active>();
            lock(client.playerData.activeBuffs)
                snapshot.AddRange(client.playerData.activeBuffs.Values.Where(a=>!string.IsNullOrEmpty(a.Definition?.HealthChangedProc)));
            bool outer=pending==null;
            if(outer) { pending=new Queue<Pending>(); fired=new HashSet<(WorldClient,ushort)>(); dropped=false; }
            // 871 is a permanent while-enabled listener, not a cast buff or an implicit grant of872.
            var passives=client.playerData.passives;
            lock(passives)
                if(passives.Loaded.ContainsKey(871) && !fired.Contains((client,(ushort)871)) && pending.Count<MaxCallbacksPerTransaction)
                    pending.Enqueue(new Pending {Client=client,Passive=true,PassiveGeneration=passives.Generation,
                        Damage=damage,World=client.playerData.worldGeneration,Life=client.playerData.lifeGeneration});
            foreach(var active in snapshot)
            {
                if(fired.Contains((client,active.AbilityId))) continue;
                if(pending.Count>=MaxCallbacksPerTransaction) { dropped=true; continue; }
                pending.Enqueue(new Pending {Client=client,Active=active,Damage=damage,BelowMaximum=belowMaximum,
                    World=client.playerData.worldGeneration,Life=client.playerData.lifeGeneration});
            }
            if(!outer) return;
            try
            {
                int count=0;
                while(pending.Count>0 && count++<MaxCallbacksPerTransaction)
                {
                    var p=pending.Dequeue(); var c=p.Client;
                    if(p.World!=c.playerData.worldGeneration || p.Life!=c.playerData.lifeGeneration || c.playerData.isDead || c.playerData.duelDeath) continue;
                    if(p.Passive)
                    {
                        lock(c.playerData.passives)
                            if(p.PassiveGeneration!=c.playerData.passives.Generation || !c.playerData.passives.Loaded.ContainsKey(871)) continue;
                        if(fired.Add((c,(ushort)871))) ZenMaster(c,p.PassiveGeneration,p.World,p.Life);
                        continue;
                    }
                    lock(c.playerData.activeBuffs)
                        if(!c.playerData.activeBuffs.TryGetValue(p.Active.AbilityId,out var current) || current.Generation!=p.Active.Generation) continue;
                    if(p.Active.Definition.Seconds>0 && TimeUtils.GetSimTimeSeconds()>=p.Active.Started+p.Active.Definition.Seconds) continue;
                    if(!fired.Add((c,p.Active.AbilityId))) continue;
                    var b=p.Active.Definition;
                    if(b.HealthChangedProc=="OnHealthReduceHealProc")
                    {
                        // The script itself does not inspect damageTaken. OURS: no recovery from health0 in a fatal transaction.
                        if(PvpCombat.HealthOf(c)>0) AbilityHandler.HealSelf(c,(ushort)Math.Max(0,b.HealthChangedValue));
                    }
                    else if(b.HealthChangedProc=="ApplyAuraEffectOnHealthChange") Reapply(c,(ushort)b.HealthChangedValue);
                    else if(b.HealthChangedProc=="ApplyNextAuraEffectHealthChance")
                    {
                        int roll=Roll100();
                        if(roll<b.HealthChangedValue) Reapply(c,(ushort)(p.Active.AbilityId+1));
                    }
                    else if(OrganizationEffect(b.HealthChangedProc) is var organization && organization!=0)
                    {
                        // The script returns for healing and when the owner is at maximum health before rolling.
                        if(p.Damage>0 && p.BelowMaximum)
                        {
                            int roll=Roll100();
                            Output.WriteDebugLog("[HEALTH-EFFECT] "+p.Active.AbilityId+" roll="+roll+" chance="+b.HealthChangedValue);
                            if(roll<b.HealthChangedValue) Reapply(c,organization);
                        }
                    }
                    Output.WriteDebugLog("[HEALTH-CALLBACK] "+p.Active.AbilityId+" on "+TimedEffects.PlayerName(c)+" delta="+p.Damage);
                }
                if(pending.Count>0 || dropped) Output.WriteDebugLog("[HEALTH-CALLBACK] transaction safety budget exhausted; remaining callbacks dropped");
            }
            finally { pending=null; fired=null; }
        }

        public static int Sample(ushort effect,int roll15)
        {
            var modifier=AbilityCatalog.Find(effect)?.Modifiers.FirstOrDefault(m=>m.Target==108);
            return modifier!=null && AbilityValues.TryRandomValue(modifier,roll15,out int value)?Math.Max(0,value):0;
        }

        private static void ZenMaster(WorldClient source,int generation,int world,int life)
        {
            var modifier=AbilityCatalog.Find(872).Modifiers.Single();
            // OURS: integer-percent mapping with System.Random entropy; native RNG sequence is unknown.
            if(Roll100()>=modifier.Number("ProcTriggerChance")*100) return;
            ushort before,inner;
            lock(source.playerData.passives)
            {
                if(generation!=source.playerData.passives.Generation || !source.playerData.passives.Loaded.ContainsKey(871) ||
                    world!=source.playerData.worldGeneration || life!=source.playerData.lifeGeneration || source.playerData.isDead || source.playerData.duelDeath) return;
                before=AbilityHandler.CurrentInnerStrength(source);
                inner=(ushort)Math.Min(AbilityHandler.MaxInnerStrength(source),before+(int)modifier.Number("MinValue"));
                if (inner != before) source.playerInstance.InnerStrengthAvailable.setValue(inner);
            }
            if (inner == before)
            {
                Output.WriteDebugLog("[HEALTH-EFFECT] 871->872 skips restore at cap " + inner);
                return;
            }
            new ServerPackets().sendISCurrent(source,inner);
            uint line=(uint)modifier.Number("ActivationStringOnSelf");
            string name=TimedEffects.PlayerName(source);
            source.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(line,true,872,name,name,inner-before));
            ServerPackets.SendPlayerAbilityEffect(source,(uint)modifier.Number("ActivationFX"));
            Output.WriteDebugLog("[HEALTH-EFFECT] 871->872 restores IS to "+inner);
        }

        public static void Reapply(WorldClient source,ushort effect)
        {
            if(effect==872) return; // Only the validated permanent-parent health transaction can execute this child.
            if(effect!=862 && effect!=ReactiveCodeEffect && effect!=944 && effect!=SystemControlEffect &&
                effect!=ChampionOfZionEffect && effect!=RetributionEffect) return;
            if(effect==862) { Heal(source,source,effect); return; }
            double x=0,y=0,z=0;
            NumericalUtils.LtVector3dToDoubles(source.playerInstance.Position.getValue(),ref x,ref y,ref z);
            double radius=AbilityRadius.Metres(source,effect);
            if(effect==SystemControlEffect) { SystemControlArea(source,x,y,z,radius); return; }
            if(effect==ChampionOfZionEffect)
            {
                foreach(var target in OrganizationFriends(source,x,y,z,radius)) ChampionOfZionLand(source,target);
                return;
            }
            if(effect==944)
            {
                // CastDirectObjects7: nearby friendlies, not the team-only selector9. Player friendliness reuses
                // the existing support/PvP policy; friendly NPC/pet healing is deliberately not inferred.
                foreach(var target in SocialHandler.OnlineWhere(c=>c.playerData.getDistrictId()==source.playerData.getDistrictId() &&
                    !c.playerData.isDead && !c.playerData.duelDeath && PvpCombat.Check(source,c)!=PvpRules.Verdict.Allowed))
                {
                    double tx=0,ty=0,tz=0; NumericalUtils.LtVector3dToDoubles(target.playerInstance.Position.getValue(),ref tx,ref ty,ref tz);
                    if(AbilityTargets.WithinArea(x,y,z,tx,ty,tz,radius)) Heal(source,target,effect);
                }
            }
            else
            {
                // The reapply effect stores a viral test and neutral health damage as separate fields.
                var contest = AbilityCatalog.Find(effect).Number("Ability/TestFunction") == 2 ? new AbilityItem() : null;
                contest?.setAbilityID(effect);
                foreach(var target in AbilityHostile.PlayersInArea(source,x,y,z,radius))
                {
                    if (contest != null && !FreeCastViralContest.Player(source, contest, target)) continue;
                    PvpFlag.OnFightStarts(source,true);
                    int damage=PunishingBlows.Player(source,target,Sample(effect,Roll15()));
                    target.messageQueue.addRpcMessage(AbilityMessages.Damage(false,effect,TimedEffects.PlayerName(source),TimedEffects.PlayerName(target),damage,0));
                    source.messageQueue.addRpcMessage(AbilityMessages.Damage(true,effect,TimedEffects.PlayerName(source),TimedEffects.PlayerName(target),damage,0));
                    if(effect==RetributionEffect) Output.WriteDebugLog("[HEALTH-EFFECT] "+effect+" hits "+TimedEffects.PlayerName(target)+" for "+damage);
                    if(PvpCombat.Damage(target,damage,ExecutionFx(effect))) PvpCombat.Defeated(target,source);
                    source.FlushQueue();
                }
                foreach(var (mob,view) in AbilityHostile.MobsInArea(source,x,y,z,radius))
                {
                    if (contest != null && !FreeCastViralContest.Npc(source, contest, mob, source.viewMan.getViewById(view))) continue;
                    var damage=PunishingBlows.Npc(source,mob,Sample(effect,Roll15()));
                    if(effect==RetributionEffect) Output.WriteDebugLog("[HEALTH-EFFECT] "+effect+" hits NPC "+mob.getName()+" for "+damage);
                    CombatManager.GetHandler(source).ApplyAbilityToNpc(mob,view,damage,ExecutionFx(effect));
                }
            }
        }

        // 942 and 1182 both store ExecutionFX; 942's is the previously hard-coded 0x28000a43.
        private static uint ExecutionFx(ushort effect) => (uint)AbilityCatalog.Find(effect).Number("Ability/ExecutionFX");

        private static void SystemControlArea(WorldClient source,double x,double y,double z,double radius)
        {
            string name=TimedEffects.PlayerName(source);
            foreach(var target in AbilityHostile.PlayersInArea(source,x,y,z,radius))
            {
                PvpFlag.OnFightStarts(source,true);
                TimedEffects.ApplyToPlayer(target,SystemControlEffect,name,source);
                Output.WriteDebugLog("[HEALTH-EFFECT] "+SystemControlEffect+" controls "+TimedEffects.PlayerName(target));
                target.FlushQueue();
            }
            foreach(var (mob,view) in AbilityHostile.MobsInArea(source,x,y,z,radius))
            {
                TimedEffects.ApplyToMob(source,mob,view,SystemControlEffect);
                Output.WriteDebugLog("[HEALTH-EFFECT] "+SystemControlEffect+" controls NPC "+mob.getName());
            }
            source.FlushQueue();
        }

        /// <summary>CastDirectObjects7 around the owner: the owner and same-district friendly players in radius and sight.</summary>
        internal static List<WorldClient> OrganizationFriends(WorldClient source,double x,double y,double z,double radius) =>
            SocialHandler.OnlineWhere(c=>c.playerData.getDistrictId()==source.playerData.getDistrictId() &&
                !c.playerData.isDead && !c.playerData.duelDeath && (c==source || FriendInArea(source,c,x,y,z,radius)));

        private static bool FriendInArea(WorldClient source,WorldClient target,double x,double y,double z,double radius)
        {
            if(PvpCombat.Check(source,target)==PvpRules.Verdict.Allowed) return false;
            double tx=0,ty=0,tz=0; NumericalUtils.LtVector3dToDoubles(target.playerInstance.Position.getValue(),ref tx,ref ty,ref tz);
            var view=source.viewMan?.FindViewForEntity(target.playerData.getEntityId());
            return AbilityTargets.WithinArea(x,y,z,tx,ty,tz,radius) && view?.viewCreated==true && !view.viewNeedsToBeDeleted &&
                CombatSight.Clear(source,x,y,z,tx,ty,tz);
        }

        private static void ChampionOfZionLand(WorldClient source,WorldClient target)
        {
            if(target.playerData.isDead || target.playerData.duelDeath || PvpCombat.HealthOf(target)==0) return;
            Heal(source,target,ChampionOfZionEffect);
            // ZionRestoreIS: takeInnerStrength(-Value), Value 25 from the stored init modifier; capped at the live maximum.
            var restore=AbilityCatalog.Find(ChampionOfZionEffect).Modifiers.Single(m=>m.Text("StatModInitFunc")=="ZionRestoreIS");
            ushort before=AbilityHandler.CurrentInnerStrength(target);
            ushort inner=(ushort)Math.Min(AbilityHandler.MaxInnerStrength(target),before+(int)restore.Number("MinValue"));
            if(inner!=before)
            {
                target.playerInstance.InnerStrengthAvailable.setValue(inner);
                new ServerPackets().sendISCurrent(target,inner);
            }
            Output.WriteDebugLog("[HEALTH-EFFECT] "+ChampionOfZionEffect+" restores IS of "+TimedEffects.PlayerName(target)+" "+before+" -> "+inner);
            target.FlushQueue();
        }

        private static void Heal(WorldClient source,WorldClient target,ushort effect)
        {
            if(target.playerData.isDead || target.playerData.duelDeath || PvpCombat.HealthOf(target)==0) return;
            int amount=Sample(effect,Roll15());
            // NeutralHeal6 has no influence; Heal3 uses the established HealDamage174 link.
            if(effect==944) amount=AbilityHealing.ApplyInfluence(amount, AbilityStats.Influence(source,174));
            ushort before=PvpCombat.HealthOf(target);
            AbilityHandler.HealSelf(target,(ushort)Math.Clamp(amount,0,ushort.MaxValue));
            Output.WriteDebugLog("[HEALTH-EFFECT] "+effect+" heals "+TimedEffects.PlayerName(target)+" "+before+" -> "+PvpCombat.HealthOf(target));
            uint fx=(uint)AbilityCatalog.Find(effect).Modifiers.Select(m=>m.Number("ActivationFX")).FirstOrDefault(n=>n>0);
            ServerPackets.SendPlayerAbilityEffect(target,fx);
        }

        internal static uint? LineUpRefusal(WorldClient client, CombatHandler fight)
        {
            if (!AbilityPassives.IsLoaded(client, LineUpAbility)) return AbilityRefusals.NotLoaded;
            if (fight.isCombatRunning || fight.HasActiveInterlock) return AbilityRefusals.MustBeOutsideCombat;
            if (CombatStyles.Active(client) != CombatStyles.Rifles) return AbilityRefusals.WrongStyle;
            if (RangedWeapons.AttackClass(SharpshooterFreeFire.EquippedWeapon(client)) != RangedWeapons.ClassRifle)
                return AbilityRefusals.RequiredItemCategory;
            if (!Stealth.IsOn(client, Stealth.ConcealmentGrantAbility)) return AbilityRefusals.CasterStateRequired;
            return null;
        }

        public static bool LineUp(WorldClient client,bool wasConcealed)
        {
            lock(client.playerData.upgradeSync) return LineUpCore(client,wasConcealed);
        }
        private static bool LineUpCore(WorldClient client,bool wasConcealed)
        {
            if(PvpCombat.HealthOf(client)==0 || !AbilityBuffBuffer.CanApply(client, LineUpAbility)) return false;
            var old=new List<ushort>();
            lock(client.playerData.activeBuffs) old.AddRange(client.playerData.activeBuffs.Keys.Where(id=>AbilityCatalog.Find(id)?.Number("Ability/StackingType")==39));
            foreach(var id in old) AbilityBuffs.Remove(client,id);
            AbilityBuffs.RemoveTimedFamilyConflicts(client, LineUpAbility);
            var entry=AbilityCatalog.Find(LineUpAbility);
            int level = client.playerInstance.Level.getValue()[0];
            var mods=entry.Modifiers.Where(m=>m.Target!=0 && m.Target!=710).Select(m=>
            {
                AbilityValues.TryValue(m,0,level,out int value);
                return new TempMods.Mod { Kind=TempMods.KindStat, Target=m.Target, MinValue=value,MaxValue=value,
                    Seconds=(float)m.Number("Duration"),Proc=TempMods.Proc.Stat,
                    Break=m.Text("StatModHealthChangedFunc")=="OnHealthReduceDeactivateAbility"?TempMods.BreakRule.Damage:TempMods.BreakRule.None };
            }).ToArray();
            TimedEffects.Apply(TimedEffects.ForPlayer(client),new TempMods.Effect {Ability=LineUpAbility,Mods=mods},TimeUtils.GetSimTimeSeconds(),client,TimedEffects.PlayerName(client));
            var aura = entry.Modifiers.Single(m => m.Text("StatModInitFunc") == "ApplyAuraEffect");
            AbilityValues.TryValue(aura, 0, level, out int abilityId);
            ushort grant = (ushort)abilityId;
            if (wasConcealed) Stealth.Begin(client, Stealth.Defs[grant], Stealth.AbilityLevel(client, grant));
            Stealth.Begin(client, Stealth.Defs[grant], Stealth.AbilityLevel(client, grant));
            Output.WriteDebugLog($"[HEALTH-EFFECT] LineUp506 subject={(wasConcealed ? 1 : 2)} reapply{grant}");
            return true;
        }
    }
}
