using hds.world.Skill;
using System;
using System.Collections.Generic;

namespace hds
{
	public class ClientData
	{
		
		// Mxo coords
		private int[] rsiValues;
		
		// MXO Client values (server data)
		private string uniqueKey;
		private UInt16 pss;
		private UInt16 cseq;
		private UInt16 sseq;
		private UInt16 sseqACKBYCLIENT;
		private UInt16 RPCCounter;
		private UInt32 charID;
		private UInt16 objectID;
        private UInt64 entityId;
		// Character Player Values
		private long exp;
		private long cash;
		private string district;
        private uint districtId;
		private bool onWorld;
		private string missionTeam;
		
		public List<BuffSkill> currentBuffs;
		public List<Mission> currentMissionList;
		public int selectedMissionIndex = -1;   // last mission whose info the client asked for
		public ushort missionInstanceCounter = 0x2e; // retail instances looked like 0x2f, 0x48
		public Mission activeMission;
		
		private float clientSimTime;
        public long lastSimTimeUpdate = 0; // 0 until the first sim-time stamped packet went out
        public float lastSimTimeSeconds = 0; // sim time (seconds) stamped on the last timed packet
        private bool UDPSessionEstablished;

        public UInt16 currentSelectedTargetViewId = 2;
        public ushort currentSelectedTargetSpawnId = 1; // we need both for combat

        // This vars needs to be refaktored / deleted (they are more for testing)
        public UInt16 currentTestRPC = 55;  // 0-127 = DONE, all 0x81XX -> done ToDo: 127-256 and 80XX(didnt found higher) Needs to be removed (hell the only way to store the current value) Start Point is : 32769 / Brek Point 1 : 32827 | Break Point 2: 33056 (need later for Subway)
        public UInt32 lastClickedObjectId = 0; // Temp for Hardline ObjectID Tracking - call upload hardline before teleporting so we know the last clicked object ID and save it (if not isset) to the Hardline list to assing it
        public UInt16 spawnViewUpdateCounter = 1; // maybe the wording is wrong - need to change this later
        private UInt16 jumpID = 1;
        public bool isJumping = false;

		public UInt32 jackoutStartTime;
		public bool isJackoutInProgress;

		public readonly NpcLootCache loot = new NpcLootCache();

		// Counts emotes; the own-view emote update carries it so each emote is a new one
		public byte emoteCounter = 0;
		// The stance the player is in (client 34 / 33): 0 standing, 0a sitting, 11..15 dancing (EmoteHandler)
		public byte stance = 0;
		// Callbacks for prompts (81 8f / 81 90) and FX tokens (81 42 / 81 91) this session (ClientCallbacks)
		public ClientCallbacks.State callbacks;

		// Last ability used, to drop the duplicate the client sends for a double-click
		public UInt16 lastAbilityId = 0;
		public double lastAbilityTime = -1;
		public double castEndsAt = -1;

		// Sim time (s) at which each ability's or cooldown group's cooldown ends; see AbilityCooldowns
		public readonly Dictionary<UInt16, double> cooldownEndsAt = new Dictionary<UInt16, double>();

		// Buffs the player has on, by ability id, and the next buff record id; see AbilityBuffs
		public readonly Dictionary<UInt16, AbilityBuffs.Active> activeBuffs = new Dictionary<UInt16, AbilityBuffs.Active>();
		public UInt16 nextBuffRecordId = AbilityBuffs.FirstRecordId;
		public int buffGeneration = 0;
        // Actual clamped vital deltas, so removing a large negative buff restores the original maximum.
        public int buffHealthMaximumDelta, buffISMaximumDelta;
        public readonly AbilityPassives.State passives = new AbilityPassives.State();

		// The tactic button pressed (RPC 42) and the record ids of its modifier records; see CombatTactics
		public CombatTactic combatTactic = CombatTactic.Speed;
        public UInt32 combatSelection = CombatSelection.NoAbility;
        public UInt16 combatStyle = AbilityProgression.SelfDefenseAbility;
        public UInt16 martialArt;
        public readonly object craftingSync = new object();
        public float deathPenalty = 1;
        public double deathPenaltyAt, deathPenaltySentAt;
        public bool deathPenaltyLoaded;
        public ushort deathPenaltyRecord;
		public CombatTacticType combatTacticType = CombatTacticType.Normal;

		// The part of a point of Inner Strength regenerated but not yet granted; see AbilityHandler.RegenAmount
		public double innerStrengthRegenCarry = 0;
		public UInt16[] tacticRecordIds = null;

		// Bumped when a fight starts, so a delayed out-of-combat reset from the last fight doesn't end a new one
		public int combatGeneration = 0;

		// Death: set when the player dies, respawnPending once they asked to respawn (81 4c)
		public bool isDead = false;
        public int worldGeneration, lifeGeneration;
        public readonly object lifeSync = new object();

		// PvP (docs/PVP.md): the flag is ConditionStateFlags bit 0x01000000; conditionStateFlags holds the other bits
		// last sent (in combat, tactic conditions). duelId pairs two duelists (even and odd); duelDeath is the loser
		// lying down until they stand up (81 4c).
		public bool pvpFlag = false;
		public UInt32 conditionStateFlags = 0;
		public UInt32 duelId = 0;
		public bool duelDeath = false;
		public double lastDuelEndedAt = -1000;
		public bool isBot = false;
		public readonly Dictionary<UInt32, int> conditionTokens = new Dictionary<UInt32, int>();

		// Sim time (s) an NPC last attacked this player on its own (MobAggro)
		public double lastAggroAt = -1000;
		public bool respawnPending = false;

		// Running mission: how far through it the player is, and the NPCs it spawned
		public MissionProgress missionProgress;
        public readonly object missionSync = new object();
		public TutorialMission tutorial;
		public MissionSite missionPreview; public Mission missionPreviewFor; // a generated mission's first site, picked for its info
		public StaticWorldObject elevatorPanel; // the panel last used, for the floor the player picks next
		public UInt16? elevatorRegionOverride; // ?elevator <n>: tries a region value on the next panel (dev)
		public bool leavingTutorial; // jacks straight into Richland from the Loading Area, as retail did
		// Ids of finished missions; null until the first mission list loads them
		public System.Collections.Generic.HashSet<int> completedMissions;
		public readonly System.Collections.Generic.List<Mob> missionMobs = new System.Collections.Generic.List<Mob>();
        public StaticWorldObject activeCollector;
        public Vendor activeVendor;
        public int activeVendorGeneration;

		// Doors this client has opened; DoorLifecycle closes and removes them on the retail schedule
		public readonly System.Collections.Generic.List<OpenDoor> openDoors = new System.Collections.Generic.List<OpenDoor>();

        public bool waitForRPCShutDown;
        public bool worldChangePending;
        public UInt16 tutorialConstructId;
        internal ushort selfSpawnIdCounter;
        /// <summary>Goid of the gun the client picked for Free Fire (RPC 3f), 0 when none.</summary>
        public UInt32 freeFireWeaponGoid;
	    public UInt32 lastSaveTime;

	    public ClientData ()
		{
			rsiValues = new int[22];
			RPCCounter = 0;
			
			pss = 0x00;
			onWorld = false;
			SetupPlayerData();

		}
	    
	    public bool DistrictIsLA(){
		    return (this.districtId == (int)MxOLocations.LA);
	    }

        public byte assignSpawnIdCounter()
        {
            byte temp = (byte)spawnViewUpdateCounter;
            spawnViewUpdateCounter++;
            if (spawnViewUpdateCounter == 256)
                spawnViewUpdateCounter = 1;
            return temp;
        }

        public void SetupPlayerData()
        {
            // This init buffs, current skills, missions etc.
            currentBuffs = new List<BuffSkill>();

        }
		
		
		public void setClientSimTime(float _newTime){
			clientSimTime = _newTime;
		}

        public void setJumpID(UInt16 jumpID)
        {
            this.jumpID = jumpID;
        }

        public UInt16 getJumpID()
        {
            return jumpID;
        }

        public void incrementJumpID()
        {
            jumpID++;
            //Output.WriteLine("JUMP ID IS NOW " + this.jumpID.ToString());
			if (jumpID==65534){ //Safe way
                jumpID = 1;
			}
        }
		
		public float getClientSimTime(){
			return clientSimTime;
		}
		
		
		public void setRsiValues(int[] newRSI){
			rsiValues = newRSI;
		}
		
		public int[] getRsiValues(){
			return rsiValues;
		}
		
		public void setOnWorld(bool newValue){
			lock (lifeSync)
            {
                if (!newValue) worldGeneration++;
                onWorld = newValue;
            }
		}		
		
		public bool getOnWorld(){
			return onWorld;
		}

        public void setMissionTeam(string newValue)
        {
            missionTeam = newValue;
            
        }

        public string getMissionTeam()
        {
            return missionTeam;
        }

        public void setUDPSessionEstablished(bool value)
        {
            UDPSessionEstablished = value;
        }

        public bool getUDPSEssionEstablished()
        {
            return UDPSessionEstablished;
        }

        public UInt16 calculateNextPossibleSseq()
        {
            UInt16 futureSseq = sseq;
            futureSseq++;
            if(futureSseq==4096)
            {
               futureSseq=0;
            }
            return futureSseq;
        }

		public void IncrementSseq(){
			sseq++;
			if (sseq==4096){ //Safe way
				sseq = 0;
			}
		}
		
		public UInt16 getPss(){
			return pss;
		}
		
		public UInt16 getCseq(){
			return cseq;
		}
		
		public UInt16 getSseq(){
			return sseq;
		}
		
		public UInt16 getACK(){
			return sseqACKBYCLIENT;
		}
		
		public void setPss(UInt16 pss){
			this.pss = pss;
		}
		
		public void setCseq(UInt16 cseq){
			this.cseq = cseq;
		}
		
		public void setSseq(UInt16 sseq){
			this.sseq = sseq;
		}
		
		public void setACK(UInt16 ack){
			sseqACKBYCLIENT = ack;
		}
		
		public void setRPCCounter(UInt16 rpc){
			RPCCounter = rpc;
		}
		
		public UInt16 getRPCCounter(){
			return RPCCounter;
		}
		
		public UInt32 getCharID(){
			return charID;
		}
		
		public void setCharID(UInt32 charID){
			this.charID = charID;
		}
		
		public UInt16 getObjectID(){
			return objectID;
		}
		
		public void setObjectID(UInt16 objectID){
			this.objectID = objectID;
		}

        public UInt64 getEntityId()
        {
            return entityId;
        }

        public void setEntityId(UInt64 _entityId)
        {
            entityId = _entityId;
        }
		
		public void setUniqueKey(string key){
			uniqueKey = key;
		}
		
		public string getUniqueKey(string key){
			return uniqueKey;
		}
		
		
		public void setExperience(long _exp){
			exp = _exp;
		}
		public long getExperience(){
			return exp;
		}
		
		public void setInfo(long _cash){
			cash = _cash;
		}
		
		public long getInfo(){
			return cash;
		}
		
		public void setDistrict(string _dist){
			district = _dist;
		}
		
		public string getDistrict(){
			return district;
		}

        public void setDistrictId(uint _districtId)
        {
            districtId = _districtId;
        }

        /// <summary>The last own-view teleport's id (PacketsUtils.createTeleportPacket).</summary>
        public byte teleportCounter;

        public uint getDistrictId()
        {
            return districtId;
        }
		

        public bool getRPCShutDown()
        {
            return waitForRPCShutDown;
        }

        public void setRPCShutDown(bool state)
        {
            waitForRPCShutDown = state;
        }
	}
}

