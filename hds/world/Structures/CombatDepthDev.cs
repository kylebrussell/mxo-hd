using System;
namespace hds
{
    /// <summary>Disposable combat fixtures. Every entry checks the dev flag, including direct callers.</summary>
    public static class CombatDepthDev
    {
        public static string Run(WorldClient caller, string[] args)
        {
            if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") != "1") return "Combat depth requires MXO_DEV_COMMANDS=1.";
            string command = args.Length > 0 ? args[0] : "";
            if (command == "hurtme") return ConcentrationDamageQa.Run(caller,args);
            if (command == "specialattack") return TypedAttackDiagnostic.Run(caller,args);
            if (command == "specialdefense")
            {
                // Paired seeded production contests; fixture-only. No live round's RNG or health is changed.
                var move = new AbilityItem(); move.setAbilityID(281);
                int defence = AbilityStats.SpecialDefense(caller, move), hits = 0;
                var rng = new Random(731);
                for (int i = 0; i < 1000; i++)
                    if (CombatInterlock.RollHit(rng, CombatTactic.Speed, 50, CombatTactic.Speed, 50,
                        out _, out _, CombatTacticType.Normal, 0, defence)) hits++;
                ushort before = PvpCombat.HealthOf(caller);
                if (before <= hits) return "Special defence diagnostic requires more than 1000 HP (use ?health 5000).";
                PvpCombat.Damage(caller, hits);
                return "Special defence 281 stat1011 total " + defence + "; seeded landed " + hits + "/1000; diagnostic HP " + before + "->" + PvpCombat.HealthOf(caller) + ".";
            }
            if (command == "daze") return CombatDepthDaze.Run(caller, args);
            if (command == "empweakness" && args.Length == 3 && (args[1] == "on" || args[1] == "off") &&
                ushort.TryParse(args[2],out ushort empHealth) && empHealth > 0)
                return AbilityEmpBlast.Fixture(caller,args[1] == "on",empHealth);
            if (command == "empweakness" && args.Length == 2 && (args[1] == "on" || args[1] == "off"))
                return AbilityEmpBlast.Fixture(caller,args[1] == "on");
            if (command == "determination" && args.Length == 2 && int.TryParse(args[1], out int value) && value >= 0 && value <= 1000)
            {
                var subject = TimedEffects.ForPlayer(caller);
                TimedEffects.RemoveParent(subject, 1102);
                if (value > 0) TimedEffects.Apply(subject, new TempMods.Effect { Ability = 1102, Mods = new[] {
                    new TempMods.Mod { Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = 575,
                        MinValue = value, MaxValue = value, Seconds = 300 } } }, TimeUtils.GetSimTimeSeconds());
                return "Combat depth Determination " + AbilityControlDurations.Determination(caller) + " (known totals, capped).";
            }
            if (command == "sightgap" || command == "sightclear")
            {
                if (caller.playerData.getDistrictId() != 1) return "Sight fixture requires Slums district1.";
                if (caller.playerInstance.OrganizationID.getValue()[0] == 0) return "Sight fixture requires an organisation (use ?org 1).";
                CombatManager.GetHandler(caller).LeaveOtherFights();
                MissionStaging.Teleport(caller,23828.3,1295,13984.9);
                caller.playerInstance.YawInterval.setValue((byte)0);
                caller.messageQueue.addObjectMessage(ServerPackets.BuildSelfPositionHandoff(0,23828.3f,1295,13984.9f,CombatHandler.CombatStartTime()),false);
                var bot = DuelBot.Summon(caller,DuelBot.Mode.Pvp);
                bot.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(
                    command == "sightgap" ? 23540.7 : 23828.3,1295,command == "sightgap" ? 15548.3 : 14184.9));
                DelayedActions.AfterFor(bot,3,()=>Stealth.Begin(bot,Stealth.Defs[Stealth.SneakGrantAbility],0));
                return "Combat depth " + command + ": face north/yaw0; level0 Sneak bot starts in 3 seconds.";
            }
            if (command == "stun")
            {
                var bot = DuelBot.Summon(caller, DuelBot.Mode.Wait); bot.playerInstance.Level.setValue((byte)5);
                TimedEffects.RemoveParent(TimedEffects.ForPlayer(caller), TempMods.ControlImmunityTimerAbility);
                if (TempMods.TryGet(315, out var effect)) TimedEffects.Apply(TimedEffects.ForPlayer(caller), effect,
                    TimeUtils.GetSimTimeSeconds(), bot, "DuelBot");
                return "Combat depth level 5 stun applied; immunity 14 seconds.";
            }
            if (command == "cast" && args.Length == 2 && ushort.TryParse(args[1], out ushort ability) && AbilityCatalog.Find(ability) != null)
            {
                var packet = new PacketContent(); packet.AddUint16(ability, 1); packet.AddUint16(caller.playerData.currentSelectedTargetViewId, 1);
                byte[] bytes = packet.ReturnFinalPacket(); new AbilityHandler().ProcessAbility(ref bytes);
                return "Combat depth cast requested " + ability + ".";
            }
            if(command=="shieldstatus")
            {
                var target=PvpCombat.PlayerInView(caller,caller.playerData.currentSelectedTargetViewId);
                var self=PvpCombat.SubjectOf(caller);
                string report="Stealth shield casterOrg="+self.Organization+" casterFlag="+self.PvpFlag+
                    (target==null?" target missing":" target="+TimedEffects.PlayerName(target)+" targetOrg="+PvpCombat.SubjectOf(target).Organization+
                    " targetFlag="+target.playerData.pvpFlag+" PvP="+PvpCombat.Check(caller,target)+" evasion="+EvasionShield.HasState(target));
                Output.WriteDebugLog("[STEALTH-FREE] "+report);return report;
            }
            if (command == "status")
            {
                Mob selected=AbilityTargets.FindMob(caller,caller.playerData.currentSelectedTargetViewId,out _);
                return selected == null ? "No selected NPC." : "Combat depth combatants " + selected.CombatantCount + ", HP " + selected.getHealthC() + ".";
            }
            if (command == "join")
            {
                Mob mob = AbilityTargets.FindMob(caller, caller.playerData.currentSelectedTargetViewId, out _);
                if (mob == null || mob.getIsDead() || mob.isFriendly) return "Select a live hostile NPC first.";
                WorldClient bot = DuelBot.Summon(caller, DuelBot.Mode.Wait);
                DelayedActions.AfterFor(bot, 3, () => {
                    var view = bot.viewMan.GetViewForEntityAndGo(mob.getEntityId(), NumericalUtils.ByteArrayToUint16(mob.getGoId(), 1));
                    if (!view.viewCreated) { new ServerPackets().SpawnMobView(bot,mob,view); view.viewCreated=true; }
                    var packet = new PacketContent(); packet.AddUint16(view.ViewID,1); packet.AddUint16((ushort)view.spawnId,1);
                    packet.AddByte(0); packet.AddByte(0x6b); byte[] bytes=packet.ReturnFinalPacket();
                    CombatManager.GetHandler(bot).ProcessRequestCloseCombat(ref bytes);
                });
                return "Combat depth DuelBot joins selected NPC in 3 seconds.";
            }
            if (command == "off")
            {
                TimedEffects.RemoveParent(TimedEffects.ForPlayer(caller),1102); DuelBot.Dismiss(caller);
                return "Combat depth fixture removed.";
            }
            return "?combatdepth determination <0..1000> | stun | daze [1..60 seconds] | cast <ability> | join | status | shieldstatus | specialdefense | sightgap | sightclear | off";
        }
    }
}
