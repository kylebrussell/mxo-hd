using System;
using System.Timers;
using hds.shared;

namespace hds
{
    /// <summary>
    /// One player firing at another (RPC 41 at a player). As against an NPC, the server fires for the player at the
    /// gun's own interval until the target dies, gets away or the player stops; the target fires back only if their own
    /// client asks to. Records, all retail's for a shot (died_in_pvp capture and the NPC gunfights):
    /// <list type="bullet">
    /// <item>the shooter's own view: <see cref="ServerPackets.BuildRangedShot"/> and RPC <c>4e 00</c>;</item>
    /// <item>everyone else's view of the shooter, the target's too: <see cref="ServerPackets.BuildOtherPlayerRangedShot"/> with
    /// the target as that client knows them: itself as <c>02 00 &lt;self spawn&gt;</c> (retail:
    /// <c>0f 00 03 2a 71 01 0c 37 81 00 02 00 d3 00 00 &lt;x y z&gt;</c>);</item>
    /// <item>the target's health, and an 81 68 combat-log line for each of the two (retail:
    /// <c>81 68 2d 00 1d 00 03 00 00 00 65 00 0a 00 9a 00 66 00 00 00 00 00 00 2d a8 00 00 00 00 0e 00 AfterWhoruNeo 09 00 f1rewa11</c>).</item>
    /// </list>
    /// Hit and damage are the free-attack rules NPC gunfights use (<see cref="FreeAttacks"/>).
    /// </summary>
    public sealed class PvpGunfire
    {
        public WorldClient Shooter { get; }
        public WorldClient Target { get; }
        public bool IsActive { get; private set; }

        private readonly object sync = new object();
        private readonly Random rng = new Random();
        private Timer timer;
        private byte noiseLevel;
        private int outOfRangeTicks;
        private UInt16 combatEffectRecordId;

        private PvpGunfire(WorldClient shooter, WorldClient target)
        {
            Shooter = shooter;
            Target = target;
        }

        /// <summary>The gun the player fires: their Free Fire pick (RPC 3f), else the equipped item.</summary>
        public static UInt32 GunOf(WorldClient player)
        {
            UInt32 weapon = player.playerData.freeFireWeaponGoid;
            byte[] equipped = player.playerInstance.EquippedItemID.getValue();
            if (weapon == 0 && equipped != null && equipped.Length >= 4)
            {
                weapon = NumericalUtils.ByteArrayToUint32(equipped, 1);
            }
            return weapon;
        }

        public static void Start(WorldClient shooter, WorldClient target, UInt32 targetHandle)
        {
            UInt32 gun = GunOf(shooter);
            if (PvpCombat.MetresBetween(shooter, target) > RangedWeapons.RangeMetres(gun))
            {
                Output.WriteDebugLog("[PVP] " + PvpFlag.Name(shooter) + " can't reach " + PvpFlag.Name(target) + " with the gun");
                shooter.playerInstance.CombatantMode.setValue(CombatHandler.CombatantModeIdle);
                shooter.messageQueue.addObjectMessage(ServerPackets.BuildCombatantModeUpdate(CombatHandler.CombatantModeIdle), false);
                shooter.messageQueue.addRpcMessage(PvpCombat.BuildCombatRefused(PvpCombat.RefusedOutOfRange, false, targetHandle));
                shooter.FlushQueue();
                return;
            }

            CombatHandler handler = CombatManager.GetHandler(shooter);
            handler.LeaveOtherFights(keepInterlock: true);
            PvpGunfire fire = new PvpGunfire(shooter, target);
            lock (fire.sync)
            {
                handler.pvpGunfire = fire;
                handler.isCombatRunning = true;
                fire.IsActive = true;
                shooter.playerData.combatGeneration++;
                shooter.playerInstance.CombatantMode.setValue(CombatHandler.CombatantModeReady);
                PvpFlag.OnFightStarts(shooter, true);
                PvpFlag.OnFightStarts(target, false);
                fire.SendCombatEffect(false);
                Output.WriteDebugLog("[PVP] " + PvpFlag.Name(shooter) + " opens fire on " + PvpFlag.Name(target));
                fire.timer = new Timer(RangedWeapons.FireIntervalMs(gun)) { AutoReset = true };
                fire.timer.Elapsed += (s, e) => fire.OnTimer();
                fire.timer.Start();
            }
            fire.Fire(true);
        }

        private void SendCombatEffect(bool off)
        {
            ClientData data = Shooter.playerData;
            if (!off)
            {
                lock (data.activeBuffs)
                {
                    if (data.nextBuffRecordId > UInt16.MaxValue - 2)
                    {
                        data.nextBuffRecordId = AbilityBuffs.FirstRecordId;
                    }
                    combatEffectRecordId = data.nextBuffRecordId;
                    data.nextBuffRecordId += 2;
                }
            }
            else if (combatEffectRecordId == 0)
            {
                return;
            }
            foreach (byte[] record in CombatInterlock.BuildCombatEffectRecords(combatEffectRecordId, off))
            {
                Shooter.messageQueue.addRpcMessage(record);
            }
        }

        private void OnTimer()
        {
            try
            {
                if (!IsActive)
                {
                    return;
                }
                WorldClient t = Target;
                if (!Shooter.Alive || !Shooter.playerData.getOnWorld() || Shooter.playerData.isDead || Shooter.playerData.duelDeath ||
                    !t.Alive || !t.playerData.getOnWorld() || PvpCombat.HandleOf(Shooter, t) == 0 ||
                    PvpCombat.Check(Shooter, t) != PvpRules.Verdict.Allowed)
                {
                    Stop();
                    return;
                }
                if (PvpCombat.MetresBetween(Shooter, t) > RangedWeapons.RangeMetres(GunOf(Shooter)))
                {
                    // As against NPCs: no shot out of range, and the fight ends after three shots' time (ours)
                    if (++outOfRangeTicks >= CombatHandler.MaxOutOfRangeTicks)
                    {
                        Stop();
                    }
                    return;
                }
                outOfRangeTicks = 0;
                Fire(false);
            }
            catch (Exception ex)
            {
                Output.WriteLine("[PVP] Shot failed: " + ex);
            }
        }

        private void Fire(bool firstShot)
        {
            bool died = false;
            lock (sync)
            {
                if (!IsActive)
                {
                    return;
                }
                WorldClient shooter = Shooter, target = Target;
                if (!ControlActions.CanUseWeapons(TimedEffects.ForPlayer(shooter).Effects)) return;
                UInt32 gun = GunOf(shooter);
                byte attackClass = RangedWeapons.AttackClass(gun);
                if (attackClass == 0)
                {
                    attackClass = RangedWeapons.ClassPistol;
                }
                noiseLevel = RangedWeapons.NextNoiseLevel(noiseLevel, RangedWeapons.Noise(gun));

                FreeAttacks.Shot shot;
                lock (rng)
                {
                    shot = FreeAttacks.Fire(rng, PvpFlag.LevelOf(shooter), shooter.playerData.combatTactic, CombatTactics.Type(shooter),
                        PvpFlag.LevelOf(target), target.playerData.combatTactic, gun,
                        AbilityStats.Attack(shooter, true), AbilityStats.Defense(target, true),
                        AbilityStats.Player(shooter, 1139), AbilityStats.Player(shooter, 171) / 100.0,
                        AbilityStats.Toughness(target, true));
                }

                double x = 0, y = 0, z = 0;
                NumericalUtils.LtVector3dToDoubles(shooter.playerInstance.Position.getValue(), ref x, ref y, ref z);
                shooter.playerData.emoteCounter++;
                byte counter = shooter.playerData.emoteCounter;
                if (firstShot)
                {
                    shooter.playerData.conditionStateFlags |= PvpFlag.InCombatBit;
                    shooter.playerInstance.ConditionStateFlags.setValue(PvpFlag.FlagsOf(shooter));
                    PvpFlag.ToWatchers(shooter, PvpFlag.BuildOtherFlags(PvpFlag.FlagsOf(shooter)));
                }
                shooter.messageQueue.addObjectMessage(ServerPackets.BuildRangedShot(counter, PvpCombat.HandleOf(shooter, target),
                    (float)x, (float)y, (float)z, CombatHandler.CombatStartTime(), noiseLevel, firstShot, shot.Hit, attackClass,
                    PvpFlag.FlagsOf(shooter)), false);
                shooter.messageQueue.addRpcMessage(ServerPackets.BuildRangedShotRpc());

                // Everyone who sees the shooter, the target among them
                foreach (WorldClient watcher in SocialHandler.OnlineWhere(c => c != shooter))
                {
                    ClientView shooterView = watcher.viewMan.FindViewForEntity(shooter.playerData.getEntityId());
                    UInt32 targetForWatcher = PvpCombat.HandleOf(watcher, target);
                    if (shooterView == null || !shooterView.viewCreated || targetForWatcher == 0)
                    {
                        continue;
                    }
                    watcher.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(shooterView.ViewID,
                        ServerPackets.BuildOtherPlayerRangedShot(counter, attackClass, shot.Hit, targetForWatcher, (float)x, (float)y, (float)z)), false);
                    watcher.FlushQueue();
                }

                UInt16 damage = 0;
                if (shot.Hit)
                {
                    damage = (UInt16)Math.Round(shot.Damage * PvpCombat.IncomingDamageMultiplier(target));
                    died = PvpCombat.Damage(target, damage);
                }
                Output.WriteDebugLog($"[PVP] {PvpFlag.Name(shooter)} fires at {PvpFlag.Name(target)}: " +
                                     (shot.Hit ? "hit for " + damage : "miss") + $" (roll {shot.AttackRoll} vs {shot.DefenseRoll})");

                string shooterName = PvpFlag.Name(shooter), targetName = PvpFlag.Name(target);
                shooter.messageQueue.addRpcMessage(ServerPackets.BuildCombatLog(shot.Hit, damage, shot.Hit ? shot.Absorbed : (Int16)0,
                    shot.AttackRoll, shot.DefenseRoll, true, gun, targetName, shooterName, shooter.playerData.combatTactic));
                target.messageQueue.addRpcMessage(ServerPackets.BuildCombatLog(shot.Hit, damage, shot.Hit ? shot.Absorbed : (Int16)0,
                    shot.AttackRoll, shot.DefenseRoll, false, gun, targetName, shooterName, shooter.playerData.combatTactic));
                shooter.FlushQueue();
                target.FlushQueue();
            }
            if (died)
            {
                PvpCombat.Defeated(Target, Shooter);
            }
        }

        /// <summary>The shooter stops: CombatEffect off, and the stance and combat flag reset follows as after any fight.</summary>
        public void Stop()
        {
            lock (sync)
            {
                if (!IsActive)
                {
                    return;
                }
                IsActive = false;
                timer?.Stop();
                timer?.Dispose();
                timer = null;
                CombatHandler handler = CombatManager.GetHandler(Shooter);
                if (handler.pvpGunfire == this)
                {
                    handler.pvpGunfire = null;
                    handler.isCombatRunning = handler.pvpFight != null;
                }
                Output.WriteDebugLog("[PVP] " + PvpFlag.Name(Shooter) + " stops firing at " + PvpFlag.Name(Target));
                if (Shooter.Alive && Shooter.playerData.getOnWorld())
                {
                    SendCombatEffect(true);
                    if (handler.pvpFight == null)
                    {
                        new ServerPackets().ScheduleLeaveCombatMode(Shooter);
                    }
                    Shooter.FlushQueue();
                }
            }
        }
    }
}
