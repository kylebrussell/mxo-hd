using System;
using System.Collections.Generic;
using System.Threading;
using hds.shared;

namespace hds
{
    public class Mob
    {
        private ulong entityID;
        private ushort mobID;       
        private ushort district;
        private string districtName;
        private byte[] goId = {0x57, 0x02};
        private ushort level = 1;
        private ushort healthC;
        private ushort healthM;
        private ushort hitCounter;
        private string rsi_hex;
        private uint rotation;
        private double xPos;
        private double yPos;
        private double zPos;
        public double xBase;
        public double yBase;
        public double zBase;
        private LtVector3f destination;
        private string name;
        private uint weapon;
        private int state; // 0= nothing, 1=rotating (just an example - must check this near from the npc class)
        private bool is_dead;
        private bool is_lootable;
        private bool is_spawned = true;
        public int DeathGeneration { get; private set; }
        public bool isUpdateable = false;

        // Friendly NPCs are spawned as non-enemies (no combat offered); mission NPCs remember their mission id
        public bool isFriendly = false;
        public MobAggroKind aggroKind = MobAggroKind.Unfiltered;
        public ushort missionNpcId = 0;
        public ushort missionInstance = 0;
        public byte missionPhase = 0;
        public bool missionGiver = false; // offers the client's Get interaction
        public bool missionTaker = false; // offers the client's Give interaction
        public bool hostileMissionKey = false; // the tutorial's opponents carry the mission's key: the client won't fight them there without it
        public CoderPets.Pet pet; // set on a coder simulacrum (NPC_BASE_SIMULACRA); null on every other NPC

        // Per-object counter carried by animation blocks in movement updates (retail increments it per use)
        private byte animationSeq = 0;

        public byte NextAnimationSeq()
        {
            return ++animationSeq;
        }

        // Combat tracking - which player is this mob fighting
        private WorldClient combatTarget = null;

        // Sim time (s) until which the NPC is stunned and doesn't attack; see AbilityTargets
        private double stunnedUntil = 0;

        public void Stun(double until)
        {
            stunnedUntil = Math.Max(stunnedUntil, until);
        }

        public bool IsStunned(double now)
        {
            return now < stunnedUntil || TimedEffects.ForMob(this).Effects.HasAt(TempMods.StunAbility, now) ||
                TimedEffects.ForMob(this).Effects.HasAt(TempMods.MezAbility, now);
        }

        // ConditionStateFlags on this NPC's view: in combat, plus the tactic conditions it suffers
        private UInt32 conditionFlags = 0;
        private readonly System.Collections.Generic.Dictionary<UInt32, int> conditionTokens =
            new System.Collections.Generic.Dictionary<UInt32, int>();

        /// <summary>
        /// NPC view update of ConditionStateFlags (attribute 43): <c>04 80 80 80 80 80 80 02 &lt;flags u32&gt;</c>, e.g. retail's
        /// <c>... 02 00 00 10 00</c> when a fight starts and <c>... 02 08 00 10 00</c> while the NPC is Staggered.
        /// </summary>
        public static byte[] BuildConditionUpdate(UInt32 flags)
        {
            var pak = new PacketContent();
            pak.AddHexBytes("04808080808080" + "02");
            pak.AddUint32(flags, 1);
            return pak.ReturnFinalPacket();
        }

        private void SendConditionFlags()
        {
            if (is_dead)
            {
                return;
            }
            var pak = new PacketContent();
            pak.AddByteArray(BuildConditionUpdate(conditionFlags));
            Store.world.SendViewUpdateToClientsWhoHasMobSpawned(pak, this);
        }

        /// <summary>Marks the NPC as in a fight (or out of it, which also ends its conditions) on everyone's screen.</summary>
        public void SetInCombat(bool inCombat)
        {
            lock (conditionTokens)
            {
                conditionFlags = inCombat ? conditionFlags | CombatInterlock.InCombatFlag : 0;
                if (!inCombat)
                {
                    conditionTokens.Clear();
                }
            }
            SendConditionFlags();
        }

        /// <summary>Gives the NPC a tactic condition for some seconds. Landing it again restarts the time.</summary>
        public void ApplyCondition(UInt32 bit, double seconds)
        {
            if (bit == 0)
            {
                return;
            }
            int token;
            lock (conditionTokens)
            {
                conditionFlags |= bit;
                conditionTokens.TryGetValue(bit, out token);
                conditionTokens[bit] = ++token;
            }
            SendConditionFlags();
            int mine = token;
            DelayedActions.After(seconds, () =>
            {
                lock (conditionTokens)
                {
                    if (!conditionTokens.TryGetValue(bit, out int current) || current != mine)
                    {
                        return;
                    }
                    conditionTokens.Remove(bit);
                    conditionFlags &= ~bit;
                }
                SendConditionFlags();
            });
        }

        /// <summary>Staggered lowers DamageResistance by 5 (retail: 80 bc 201 stat 144 -5).</summary>
        public float IncomingDamageMultiplier()
        {
            return (conditionFlags & CombatInterlock.ConditionBit(CombatTactic.Power)) != 0 ? 1.05f : 1f;
        }
        private UInt16 mobHitCounter = 0;

        public enum statusList
        {
            WALKING = 0x00,
            WALKING_BACK = 0x01,
            STANDING = 0x02,
            STANDING_BACK = 0x03,
            STANDING_COMBAT = 0x04,
            COMBAT = 0x05
        }

        public int currentState = 0x02;
        public List<PacketContent> updateData;
        public Timer mobUpdateTimer;


        public enum Animations
        {
            STAND = 0x00,
            WALK = 0x01,
            WALK_BACKWARDS = 0x02
        }


        public Mob()
        {
            updateData = new List<PacketContent>();
        }

        public void setGoId(byte[] objectID)
        {
            goId = objectID;
        }

        public void setEntityId(ulong _entityId)
        {
            entityID = _entityId;
        }

        public void setMobId(ushort mobID)
        {
            this.mobID = mobID;
        }

        public void setDistrict(ushort _district)
        {
            district = _district;
        }

        public void setDistrictName(string _districtName)
        {
            districtName = _districtName;
        }

        public void setLevel(ushort level)
        {
            this.level = level;
        }

        public void setHealthC(ushort currentHealth)
        {
            healthC = currentHealth;
        }

        public void setHealthM(ushort maxHealth)
        {
            healthM = maxHealth;
        }

        public void setRsiHex(string _rsihex)
        {
            rsi_hex = _rsihex;
        }

        public void setRotation(uint rotation)
        {
            this.rotation = rotation;
        }

        public void setXPos(double xPosition)
        {
            xPos = xPosition;
        }

        public void setYPos(double yPosition)
        {
            yPos = yPosition;
        }

        public void setZPos(double zPosition)
        {
            zPos = zPosition;
        }

        public void setName(string mobName)
        {
            name = mobName;
        }

        public void setWeapon(uint weapon)
        {
            this.weapon = weapon;
        }

        public void setState(int state)
        {
            this.state = state;
        }

        public void setIsDead(bool dead)
        {
            if (dead && !is_dead)
            {
                DeathGeneration++;
            }
            is_dead = dead;
        }

        private readonly object deathLock = new object();
        private readonly object combatSync = new object();
        private readonly object hitSync = new object();
        private readonly System.Collections.Generic.HashSet<WorldClient> combatants = new System.Collections.Generic.HashSet<WorldClient>();
        private double nextRetaliation;
        public int CombatantCount { get { lock (combatSync) return combatants.Count; } }
        /// <summary>One NPC action clock shared by every player's session (including movement).
        /// Pairwise presentation remains separate; adding attackers must not multiply retaliation.</summary>
        public bool TryRetaliate(double now, double interval)
        {
            lock (combatSync)
            {
                if (is_dead || healthC == 0 || now < nextRetaliation) return false;
                nextRetaliation = now + interval;
                Output.WriteDebugLog("[COMBAT-NPC] action " + name + " at " + now.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
        }
        public void RemoveCombatant(WorldClient player)
        {
            lock (combatSync)
            {
                combatants.Remove(player);
                if (combatTarget == player) combatTarget = System.Linq.Enumerable.FirstOrDefault(combatants);
                if (combatants.Count == 0) { nextRetaliation = 0; if (!is_dead) currentState = (int)statusList.STANDING; }
            }
        }

        /// <summary>
        /// Marks the NPC dead for whoever got there first; false when it already was. Each player's fight runs on its own
        /// timer, so two players can land the last blow together, and only one of them may send the death and get the reward.
        /// </summary>
        public bool TryClaimDeath()
        {
            lock (deathLock)
            {
                if (is_dead)
                {
                    return false;
                }
                is_dead = true;
                DeathGeneration++;
                return true;
            }
        }

        public void setIsLootable(bool lootable)
        {
            is_lootable = lootable;
        }

        public void setIsSpawned(bool spawned)
        {
            is_spawned = spawned;
        }

        /// <summary>Back to life at the spawn point with full health, for a respawn after death.</summary>
        public void ResetForRespawn()
        {
            healthC = healthM;
            is_dead = false;
            is_lootable = false;
            lock (combatSync) { combatants.Clear(); combatTarget = null; nextRetaliation = 0; }
            mobHitCounter = 0;
            hitCounter = 0;
            xPos = xBase;
            yPos = yBase;
            zPos = zBase;
            currentState = (int)statusList.STANDING;
        }

        public ulong getEntityId()
        {
            return entityID;
        }

        public ushort getMobId()
        {
            return mobID;
        }

        public ushort getDistrict()
        {
            return district;
        }

        public string getDistrictName()
        {
            return districtName;
        }

        public byte[] getGoId()
        {
            return goId;
        }

        public ushort getLevel()
        {
            return level;
        }

        public ushort getHealthC()
        {
            return healthC;
        }

        public ushort getHealthM()
        {
            return healthM;
        }

        public string getRsiHex()
        {
            return rsi_hex;
        }

        public uint getRotation()
        {
            return rotation;
        }

        public double getXPos()
        {
            return xPos;
        }

        public double getYPos()
        {
            return yPos;
        }

        public double getZPos()
        {
            return zPos;
        }

        public string getName()
        {
            return name;
        }

        public uint getWeapon()
        {
            return weapon;
        }

        public int getState()
        {
            return state;
        }

        public bool getIsDead()
        {
            return is_dead;
        }

        public bool getIsLootable()
        {
            return is_lootable;
        }

        public bool getIsSpawned()
        {
            return is_spawned;
        }


        public GameObject getCreationData()
        {
            if (pet != null) return CoderPets.BuildCreation(this);
            var themob = new GameObjectDefinitions().Object599;
            themob.DisableAllAttributes();

            themob.CharacterName.enable();
            themob.Health.enable();
            themob.MaxHealth.enable();
            themob.Level.enable();
            themob.NPCRank.enable();
            themob.RSIDescription.enable();
            themob.MoreInfoID.enable();
            themob.StopFollowActiveTracker.enable();
            themob.ScriptCounter.enable();
            themob.Description.enable();
            themob.CombatantMode.enable();
            themob.Position.enable();
            themob.YawInterval.enable();
            themob.IsDead.enable();
            themob.IsEnemy.enable();
            themob.IsFriendly.enable();
            themob.StealthAwareness.enable();
            themob.TitleAbility.enable();
            themob.ConditionStateFlags.enable();
            themob.CurrentState.enable();

            themob.CharacterName.setValue(getName());
            themob.Health.setValue(getHealthC());
            themob.MaxHealth.setValue(getHealthM());
            themob.Level.setValue(getLevel());
            themob.NPCRank.setValue(1);
            themob.RSIDescription.setValue(StringUtils.hexStringToBytes(getRsiHex()));
            themob.MoreInfoID.setValue(StringUtils.hexStringToBytes(getRsiHex())); // ToDo: Make dynamic
            themob.StopFollowActiveTracker.setValue(0x01);
            themob.ScriptCounter.setValue(1);
            themob.Description.setValue(StringUtils.hexStringToBytes(getRsiHex())); // ToDo: Make dynamic
            themob.CombatantMode.setValue(0x22);
            themob.Position.setValue(NumericalUtils.doublesToLtVector3d(getXPos(), getYPos(), getZPos()));
            themob.YawInterval.setValue((byte) getRotation());
            themob.IsDead.setValue(false);
            themob.IsEnemy.setValue(true);
            themob.IsFriendly.setValue(false);

            if (isFriendly)
            {
                if (missionInstance != 0)
                {
                    // Retail talkable mission NPCs carry the mission's key: 00 <instance u16> 02 (Regulus: 00 48 00 02).
                    // Hostile mission NPCs don't get it: with it the client stayed locked in close combat after the kill.
                    themob.MissionKey.enable();
                    themob.MissionKey.setValue(new byte[] { missionPhase, (byte)missionInstance, (byte)(missionInstance >> 8), 0x02 });
                }

                // Retail talkable NPCs (Regulus, Armor in the first-mission capture) send DontAttack,
                // TalkDefaultable and TalkActiveTracker and no IsEnemy/IsFriendly; double-clicking them then
                // sends 80 c7 <view> <spawn> 09 00 instead of starting combat.
                themob.IsEnemy.disable();
                themob.IsFriendly.disable();
                themob.DontAttack.enable();
                themob.DontAttack.setValue(0x01);
                themob.TalkDefaultable.enable();
                themob.TalkDefaultable.setValue(0x01);
                themob.TalkActiveTracker.enable();
                themob.TalkActiveTracker.setValue(0x01);
                if (missionGiver)
                {
                    themob.GiverActiveTracker.enable();
                    themob.GiverActiveTracker.setValue(0x01);
                }

                if (missionTaker)
                {
                    themob.TakerActiveTracker.enable();
                    themob.TakerActiveTracker.setValue(0x01);
                }
            }
            if (!isFriendly && hostileMissionKey)
            {
                themob.MissionKey.enable();
                themob.MissionKey.setValue(new byte[] { missionPhase, (byte)missionInstance, (byte)(missionInstance >> 8), 0x02 });
            }

            themob.StealthAwareness.setValue(0x11);
            byte[] titleAbility = {0x00, 0x10, 0x00, 0x00};
            themob.TitleAbility.setValue(titleAbility);
            themob.ConditionStateFlags.setValue(StringUtils.hexStringToBytes("00001000"));
            themob.CurrentState.setValue(StringUtils.hexStringToBytes("06080000"));

            if (RangedWeapons.IsGun(getWeapon()))
            {
                // Retail NPCs with guns carried them in EquippedItemID (NPC view attribute 33), e.g.
                // 80 80 80 80 a0 b4 02 00 00 = AutomaticRifle1 on "Chopper Jumper" (luizoe Unknown log line 32719).
                themob.EquippedItemID.enable();
                themob.EquippedItemID.setValue(getWeapon());
            }

            return themob;
        }

        public int generatePathTable()
        {
            // We firs test this with just one moove from point x to y
            // To calc this we need to know the time the object needs to reach this point,
            // the new point and the rotation to calc pretty to sync client and server position is right
            // to get a smooth walking :)
            var math = new Maths();

            // First choose a new pos in the range
            var newPos = math.RandomPointOnCircle((float) xBase, (float) yBase, (float) zBase, 5.0f * 100);
            #if DEBUG
            Output.WriteDebugLog("Mob Goes from X: " + getXPos() + " , Z: " + getZPos() + " to X: " + newPos.x +
                                 ", Z: " + newPos.z);
            #endif


            var xNew = (double) newPos.x;
            var zNew = (double) newPos.z;

            destination = newPos;
            // Try to calculate rotation

            // Oh this seems to match ...needs more testing later when we fixed random pos
            var yaw = Math.Atan((xNew - getXPos()) / (zNew - getZPos())) * 128 / Math.PI;
            var calcRotation = Math.Atan2(Math.Cos(xNew), Math.Sin(zNew) * Math.Sin(zNew)) * 128 / Math.PI;
            var testRot = Math.Atan2(xNew, zNew) * 180 / Math.PI;
            var testRot2 = Math.Atan2(xNew, zNew) * 128 / Math.PI;

            #if DEBUG
            Output.WriteDebugLog("Test Rot with 360 : " + testRot + "| 255 : " + testRot2 + " AND THE YAW: " + yaw +
                                 " (Cast to uint16 : " + Convert.ToInt16(yaw) + " )");
            #endif


            var yawVal = (int) Convert.ToInt16(yaw);

            if (zNew < getZPos() || xNew < getXPos())
            {
                #if DEBUG
                Output.WriteDebugLog("Need to adjust YAW + 128 from :" + yawVal + " to: " + (yawVal + 128));
                #endif
                yawVal = yawVal + 128;
            }
            else
            {
                #if DEBUG
                Output.WriteDebugLog("Need to adjust YAW - 128 from :" + yawVal + " to: " + (yawVal - 128));
                #endif
                yawVal = yawVal - 128;
            }
            
            #if DEBUG
            Output.WriteDebugLog("YAW VAL :" + yawVal);
            #endif

            rotation = (ushort) yawVal;
            #if DEBUG
            Output.WriteDebugLog("Calc Rotation : " + calcRotation + " and to UINT : " + (uint) calcRotation);
            #endif

            // Calculate the distance for seconds to move
            var requiredSeconds = (int) (math.distance2Coords((float) xPos, (float) xNew, (float) zPos, (float) zNew) /
                                         0.176 / 500 * 0.9586);
            return requiredSeconds;
        }

        /// <summary>
        /// Mobs wander around their spawn point. MXO_MOB_WANDER=0 keeps them still, so a scripted test
        /// against the real client can find a mob at the same place on screen every time.
        /// </summary>
        public static readonly bool WanderEnabled = Environment.GetEnvironmentVariable("MXO_MOB_WANDER") != "0";

        public void DoMobUpdate(object e)
        {
            if (pet != null) return; // simulacra move with CoderPets, not the wander timer
            if (!ControlActions.CanMove(TimedEffects.ForMob(this).Effects))
            {
                mobUpdateTimer = new Timer(DoMobUpdate, this, 1000, 0);
                return;
            }
            if (isUpdateable)
            {
                var nextMoveTime = 5;
                var rand = new Random();
                switch (currentState)
                {
                    case (int) statusList.WALKING_BACK:
                    case (int) statusList.WALKING:
                        // stop npc
                        currentState = (int) statusList.STANDING;
                        setXPos(destination.x);
                        setZPos(destination.z);
                        updateAnimation((byte) Animations.STAND);
                        nextMoveTime = rand.Next(5, 15);
                        break;
                    case (int) statusList.STANDING_BACK:
                    case (int) statusList.STANDING:
                        if (!WanderEnabled)
                        {
                            break; // stay at the spawn point (MXO_MOB_WANDER=0)
                        }
                        // Starts walking
                        currentState = (int) statusList.WALKING;
                        // ToDo: start walking NPC and recalculate
                        nextMoveTime = generatePathTable();
                        updateAnimation((byte) Animations.WALK);
                        // Generate walking packet message (raw)
                        break;
                    case (int) statusList.COMBAT:
                        // ToDo: he should attack the player who is in combat with him
                        // But he shouldnt move
                        break;
                }

                mobUpdateTimer = new Timer(DoMobUpdate, this, nextMoveTime * 1000, 0);
            }
            
        }

        public void HitEnemyWithDamage(ushort hitValue, uint hitFxId)
        {
            lock (hitSync)
            {
                if (is_dead || healthC == 0) return;
                ushort before = healthC;
                // Change to combat so that he isnt moving anymore
                currentState = (int) statusList.COMBAT;
                if (healthC - hitValue <= 0)
                    healthC = 0;
                else
                    healthC -= hitValue;
                hitCounter++;
                if (before != healthC) TimedEffects.HealthChanged(TimedEffects.ForMob(this), before - healthC, healthC < healthM);

                var hitPacket = new PacketContent();
                hitPacket.AddByteArray(BuildHitUpdate(healthC, hitFxId, (byte)hitCounter));
                Store.world.SendViewUpdateToClientsWhoHasMobSpawned(hitPacket, this);
            }
        }

        /// <summary>
        /// NPC view update for a hit, matching retail:
        /// <c>04 80 80 80 40 &lt;health u16&gt;</c> for health alone (what retail sent for most hits), or with an
        /// effect <c>04 80 80 80 c0 &lt;health u16&gt; c0 &lt;effect id u32&gt; 01 &lt;counter u8&gt;</c> (ability hits; retail
        /// effect ids are 0x28xxxxxx). The group bytes select attribute 27 (health) and 34/35 (effect, counter).
        /// </summary>
        public static byte[] BuildHitUpdate(UInt16 health, uint effectId, byte counter)
        {
            var pak = new PacketContent();
            pak.AddByte(0x04);
            pak.AddByte(0x80);
            pak.AddByte(0x80);
            pak.AddByte(0x80);
            if (effectId == 0)
            {
                pak.AddByte(0x40);
                pak.AddUint16(health, 1);
                return pak.ReturnFinalPacket();
            }
            pak.AddByte(0xc0);
            pak.AddUint16(health, 1);
            pak.AddByte(0xc0);
            pak.AddUint32(effectId, 1);
            pak.AddByte(0x01);
            pak.AddByte(counter);
            return pak.ReturnFinalPacket();
        }

        public void updateAnimation(byte animationByte)
        {
            // Through Animation Packet here if you want to start / stop walking etc.
            // aka 0x0e
            var pak = new PacketContent();
            pak.AddByte(0x02);
            pak.AddByte(0x0e);
            pak.AddByte(animationByte);
            pak.AddByte((byte) rotation);
            pak.AddFloatLtVector3f((float) getXPos(), (float) getYPos(), (float) getZPos());

            Store.world.SendViewUpdateToClientsWhoHasMobSpawned(pak, this);
        }

        /// <summary>
        /// Sets the combat target for this mob.
        /// </summary>
        public void setCombatTarget(WorldClient target)
        {
            lock (combatSync) { combatants.Add(target); combatTarget = target; }
        }

        /// <summary>
        /// Gets the current combat target.
        /// </summary>
        public WorldClient getCombatTarget()
        {
            return combatTarget;
        }

        /// <summary>
        /// Clears the combat target when combat ends.
        /// </summary>
        public void clearCombatTarget()
        {
            lock (combatSync) { combatants.Clear(); combatTarget = null; nextRetaliation = 0; }
            if (!is_dead)
            {
                currentState = (int)statusList.STANDING;
            }
        }

        /// <summary>
        /// Called on combat tick to make this mob attack its target.
        /// Returns true if the player died from this attack.
        /// </summary>
        public bool updateCombat(WorldClient target = null)
        {
            // The player of the fight being ticked. combatTarget is only whoever engaged this NPC last: with two
            // players on one NPC, hitting it damaged one player while the other's fight reported the death.
            WorldClient victim = target ?? combatTarget;
            if (victim == null || is_dead)
            {
                return false;
            }

            // A stunned NPC skips its attack (Stun 1 / Stun 2.0)
            if (IsStunned(TimeUtils.GetSimTimeSeconds()))
            {
                Output.WriteDebugLog($"[COMBAT] {name} is stunned and doesn't attack");
                return false;
            }

            // Get player's current health
            byte[] healthBytes = victim.playerInstance.Health.getValue();
            UInt16 playerHealth = NumericalUtils.ByteArrayToUint16(healthBytes, 1);

            // Get player's max health for reference
            byte[] maxHealthBytes = victim.playerInstance.MaxHealth.getValue();
            UInt16 playerMaxHealth = NumericalUtils.ByteArrayToUint16(maxHealthBytes, 1);

            // Get player level
            byte[] levelBytes = victim.playerInstance.Level.getValue();
            UInt16 playerLevel = (levelBytes != null && levelBytes.Length > 0) ? levelBytes[0] : (UInt16)1;

            // Calculate damage using mob's level vs player's level
            UInt16 damage = CombatCalculator.CalculateMobDamageWithTactics(level, playerLevel);
            uint fxId = CombatCalculator.GetRandomMeleeHitFx();

            Output.WriteDebugLog($"[COMBAT] {name} (Lv{level}) hits player (Lv{playerLevel}) for {damage} damage");

            return HitCombatTarget(damage, fxId, victim);
        }

        /// <summary>Action counter of this NPC's gunshots (the 2a record's counter).</summary>
        public byte rangedActionCounter;

        /// <summary>
        /// Takes damage off the combat target's health and sends the player their new health. Returns true when the
        /// player died.
        /// </summary>
        public bool HitCombatTarget(UInt16 damage, uint fxId = 0, WorldClient target = null)
        {
            WorldClient victim = target ?? combatTarget;
            if (victim == null)
            {
                return false;
            }
            UInt16 playerHealth = NumericalUtils.ByteArrayToUint16(victim.playerInstance.Health.getValue(), 1);
            UInt16 newHealth = playerHealth <= damage ? (UInt16)0 : (UInt16)(playerHealth - damage);

            // Update player's health attribute
            victim.playerInstance.Health.setValue(newHealth);
            TimedEffects.HealthChanged(TimedEffects.ForPlayer(victim), playerHealth - newHealth,
                newHealth < NumericalUtils.ByteArrayToUint16(victim.playerInstance.MaxHealth.getValue(), 1));

            newHealth = PvpCombat.HealthOf(victim); // callbacks may heal or cause another health change

            // Increment hit counter for animation sequencing
            mobHitCounter++;

            // Send health update packet to the player (view ID 2 is self-view)
            SendPlayerDamagePacket(victim, newHealth, fxId, mobHitCounter);
            PvpFlag.ToWatchers(victim, PlayerViewAttributes.Build((PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(newHealth))));

            Output.WriteDebugLog($"[COMBAT] Player health: {playerHealth} -> {newHealth}");

            // Check if player died
            if (newHealth <= 0)
            {
                Output.WriteDebugLog($"[COMBAT] Player died!");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends a damage/health update packet to the player.
        /// Uses same format as HitEnemyWithDamage but for player self-view.
        /// </summary>
        private void SendPlayerDamagePacket(WorldClient client, UInt16 newHealth, uint fxId, UInt16 hitCounter)
        {
            // The player's own view has its own attribute layout: this used to send the NPC hit format
            // (04 80 80 80 c0 ...) to view 2, which the client couldn't parse, so the player's health never
            // changed on screen and the rest of the packet was lost.
            client.messageQueue.addObjectMessage(ServerPackets.BuildSelfHealthUpdate(newHealth), false);
        }
    }
}
