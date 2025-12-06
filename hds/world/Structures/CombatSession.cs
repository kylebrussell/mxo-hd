using System;
using System.Timers;

namespace hds
{
    /// <summary>
    /// Tracks the state of an active combat session between two entities.
    ///
    /// A combat session is created when a player initiates combat with a mob
    /// and is destroyed when combat ends (death, flee, timeout).
    ///
    /// See docs/COMBAT-SYSTEM-ROADMAP.md for combat system documentation.
    /// </summary>
    public class CombatSession
    {
        /// <summary>
        /// Unique identifier for this combat session.
        /// </summary>
        public UInt64 SessionId { get; private set; }

        /// <summary>
        /// The attacking player's client.
        /// </summary>
        public WorldClient Attacker { get; private set; }

        /// <summary>
        /// The defending mob (if fighting an NPC).
        /// </summary>
        public Mob DefenderMob { get; private set; }

        /// <summary>
        /// The defending player (if PvP).
        /// </summary>
        public WorldClient DefenderPlayer { get; private set; }

        /// <summary>
        /// View ID of the target (mob or player).
        /// </summary>
        public UInt32 TargetViewSpawnId { get; private set; }

        /// <summary>
        /// View ID of the ILCombatHandler game object.
        /// </summary>
        public UInt16 CombatHandlerViewId { get; set; }

        /// <summary>
        /// Entity ID of the ILCombatHandler game object.
        /// </summary>
        public UInt64 CombatHandlerEntityId { get; set; }

        /// <summary>
        /// When combat started.
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// Current combat round (increments each update tick).
        /// </summary>
        public UInt32 RoundNumber { get; private set; }

        /// <summary>
        /// Counter for hit animations/effects.
        /// </summary>
        public UInt16 HitCounter { get; private set; }

        /// <summary>
        /// Whether combat is currently active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Combat type (melee or ranged).
        /// </summary>
        public CombatType Type { get; private set; }

        /// <summary>
        /// Current combat tactic for the attacker (player).
        /// </summary>
        public CombatTactic AttackerTactic { get; private set; }

        /// <summary>
        /// Current combat tactic for the defender (mob or player).
        /// Mobs default to None (no tactic).
        /// </summary>
        public CombatTactic DefenderTactic { get; private set; }

        /// <summary>
        /// Timer for combat update ticks.
        /// </summary>
        private Timer combatTimer;

        /// <summary>
        /// Callback for combat update ticks.
        /// </summary>
        public event Action<CombatSession> OnCombatTick;

        /// <summary>
        /// Callback for when combat ends.
        /// </summary>
        public event Action<CombatSession, CombatEndReason> OnCombatEnd;

        private static UInt64 sessionIdCounter = 1;

        public enum CombatType
        {
            Melee = 0,
            Ranged = 1
        }

        public enum CombatEndReason
        {
            AttackerDied,
            DefenderDied,
            AttackerFled,
            DefenderFled,
            Timeout,
            OutOfRange
        }

        /// <summary>
        /// Creates a new combat session for player vs mob combat.
        /// </summary>
        public CombatSession(WorldClient attacker, Mob defender, UInt32 targetViewSpawnId, CombatType type)
        {
            SessionId = sessionIdCounter++;
            Attacker = attacker;
            DefenderMob = defender;
            DefenderPlayer = null;
            TargetViewSpawnId = targetViewSpawnId;
            Type = type;
            StartTime = DateTime.Now;
            RoundNumber = 0;
            HitCounter = 0;
            IsActive = false;
            AttackerTactic = CombatTactic.None;
            DefenderTactic = CombatTactic.None; // Mobs don't use tactics
        }

        /// <summary>
        /// Creates a new combat session for PvP combat.
        /// </summary>
        public CombatSession(WorldClient attacker, WorldClient defender, UInt32 targetViewSpawnId, CombatType type)
        {
            SessionId = sessionIdCounter++;
            Attacker = attacker;
            DefenderMob = null;
            DefenderPlayer = defender;
            TargetViewSpawnId = targetViewSpawnId;
            Type = type;
            StartTime = DateTime.Now;
            RoundNumber = 0;
            HitCounter = 0;
            IsActive = false;
            AttackerTactic = CombatTactic.None;
            DefenderTactic = CombatTactic.None;
        }

        /// <summary>
        /// Changes the attacker's combat tactic.
        /// Returns false if the player doesn't have enough IS.
        /// </summary>
        /// <param name="newTactic">New tactic to use</param>
        /// <returns>True if tactic was changed, false if insufficient IS</returns>
        public bool SetAttackerTactic(CombatTactic newTactic)
        {
            if (newTactic == AttackerTactic)
                return true; // No change needed

            // Check if player has enough IS
            byte[] isBytes = Attacker.playerInstance.InnerStrengthAvailable.getValue();
            UInt16 currentIS = isBytes != null && isBytes.Length >= 2
                ? NumericalUtils.ByteArrayToUint16(isBytes, 1)
                : (UInt16)0;

            if (currentIS < CombatCalculator.TACTIC_CHANGE_IS_COST)
                return false; // Not enough IS

            // Deduct IS cost
            UInt16 newIS = (UInt16)(currentIS - CombatCalculator.TACTIC_CHANGE_IS_COST);
            Attacker.playerInstance.InnerStrengthAvailable.setValue(newIS);

            AttackerTactic = newTactic;
            return true;
        }

        /// <summary>
        /// Sets the defender's tactic (for PvP).
        /// </summary>
        public void SetDefenderTactic(CombatTactic newTactic)
        {
            DefenderTactic = newTactic;
        }

        /// <summary>
        /// Starts the combat session and begins the update timer.
        /// </summary>
        /// <param name="tickIntervalMs">Milliseconds between combat ticks (default 3000)</param>
        public void Start(int tickIntervalMs = 3000)
        {
            if (IsActive) return;

            IsActive = true;
            StartTime = DateTime.Now;
            RoundNumber = 0;

            // Set the mob into combat state if applicable
            if (DefenderMob != null)
            {
                DefenderMob.currentState = (int)Mob.statusList.COMBAT;
            }

            combatTimer = new Timer(tickIntervalMs);
            combatTimer.Elapsed += OnTimerTick;
            combatTimer.AutoReset = true;
            combatTimer.Enabled = true;
        }

        /// <summary>
        /// Stops the combat session.
        /// </summary>
        public void Stop(CombatEndReason reason)
        {
            if (!IsActive) return;

            IsActive = false;
            combatTimer?.Stop();
            combatTimer?.Dispose();
            combatTimer = null;

            // Reset mob state if applicable
            if (DefenderMob != null && !DefenderMob.getIsDead())
            {
                DefenderMob.currentState = (int)Mob.statusList.STANDING;
            }

            OnCombatEnd?.Invoke(this, reason);
        }

        /// <summary>
        /// Timer tick handler - advances combat round.
        /// </summary>
        private void OnTimerTick(object sender, ElapsedEventArgs e)
        {
            if (!IsActive) return;

            RoundNumber++;
            OnCombatTick?.Invoke(this);
        }

        /// <summary>
        /// Increments and returns the hit counter for animation sequencing.
        /// </summary>
        public UInt16 NextHitCounter()
        {
            return ++HitCounter;
        }

        /// <summary>
        /// Gets the defender's current health.
        /// </summary>
        public UInt16 GetDefenderHealth()
        {
            if (DefenderMob != null)
            {
                return DefenderMob.getHealthC();
            }
            // TODO: Get player health for PvP
            return 0;
        }

        /// <summary>
        /// Gets the defender's max health.
        /// </summary>
        public UInt16 GetDefenderMaxHealth()
        {
            if (DefenderMob != null)
            {
                return DefenderMob.getHealthM();
            }
            // TODO: Get player max health for PvP
            return 0;
        }

        /// <summary>
        /// Applies damage to the defender.
        /// Returns true if the defender died.
        /// </summary>
        public bool ApplyDamageToDefender(UInt16 damage, UInt32 fxId)
        {
            if (DefenderMob != null)
            {
                DefenderMob.HitEnemyWithDamage(damage, fxId);
                return DefenderMob.getHealthC() <= 0;
            }
            // TODO: Handle PvP damage
            return false;
        }

        /// <summary>
        /// Checks if the defender is dead.
        /// </summary>
        public bool IsDefenderDead()
        {
            if (DefenderMob != null)
            {
                return DefenderMob.getIsDead() || DefenderMob.getHealthC() <= 0;
            }
            // TODO: Check player death for PvP
            return false;
        }

        /// <summary>
        /// Gets the defender's view ID (lower 16 bits of TargetViewSpawnId).
        /// </summary>
        public UInt16 GetDefenderViewId()
        {
            return (UInt16)(TargetViewSpawnId & 0xFFFF);
        }

        /// <summary>
        /// Marks the defender mob as dead and lootable.
        /// </summary>
        public void MarkDefenderDead()
        {
            if (DefenderMob != null)
            {
                DefenderMob.setIsDead(true);
                DefenderMob.setIsLootable(true);
                DefenderMob.isUpdateable = false; // Stop AI updates
            }
        }
    }
}
