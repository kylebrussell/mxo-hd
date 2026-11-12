using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    /// <summary>
    /// Utility and world-interaction abilities: Open Locks, Disable Security Devices, Disarm Traps, Recall Team Member and
    /// Recall Mission Team, Download Mission Map, Hack Construct Key and Find Data Node (docs/UTILITY-ABILITIES.md).
    ///
    /// The rules are the client's own server-side Python (research/client-scripts): serverrules.py DoorPickLock (4000),
    /// SecurityPBXDisable (1012) and HardlineHackConstruct (10024); ability/spy/misc.py DisarmTraps_Test and
    /// LocateDataNode_DirectObject; ability/hacker/misc.py RecallTeamMember_* and RecallMissionTeam_*; rshooks.py
    /// ComputeDownloadMissionMapOutcome. The tools are the gob items whose AbilityPrerequisite/ReqAbilityID names the
    /// ability (Inventory.findToolForAbility). String ids are stringtable_client's.
    /// </summary>
    public static class UtilityAbilities
    {
        // Ability ids (abilityIDs.csv)
        public const UInt16 DownloadMissionMap = 44, DisarmTraps = 119, OpenLocks = 142, HackConstructKey = 166,
            LocateDataNode = 215, DisableSecurityDevices = 218, RecallTeamMember = 382, RecallMissionTeam = 486,
            DataNodeGoggles = 538, DataTap = 539, SeeLuggables = 540, DetectLuggables = 541, FindDataNode = 621,
            SecuritySystems = 640, MapDownloading = 641;

        // stringtable_client
        public const UInt32 PickLockAbilityFailed = 0x7a000141; // "Lock picking failed! Make sure you have %1z loaded."
        public const UInt32 PickLockToolFailed = 0x7a000142;    // "Lock picking failed! Make sure you have a lockpick tool in your inventory."
        public const UInt32 PickLockFailed = 0x7a000144;        // "Lock Pick unsuccessful." (only in DoorPickLock's dead contest code)
        public const UInt32 PickLockSuccess = 0x7a000145;       // "Lock Picked Successfully!"
        public const UInt32 TrapDisarmed = 0x7600024e;          // "Trap was successfully disarmed!"
        public const UInt32 TrapDisarmFailure = 0x7600024f;     // "Trap was not disarmed successfully!"
        public const UInt32 TrapDisarmCritical = 0x76000250;    // "Trap went off while being disarmed!"
        public const UInt32 TeleportingTarget = 0x7a00015a;     // "Your team member %2s is attempting to teleport you to their location."
        public const UInt32 TeleportingCaster = 0x7a00015b;     // "You begin to teleport a team member to your location."
        public const UInt32 TeleportingTeamCaster = 0x7a000164; // "You begin to teleport your team to your location."
        public const UInt32 RecallAcrossConstructs = 0x7a0002a1; // "You cannot recall that player. He is in a different construct."
        public const UInt32 ToolFail = 0x6000002e;              // ID_ABILITY_TOOL_FAIL "A tool is required to activate this ability."
        public const UInt32 HackConstructAbilityFail = 0x60000025; // "HackConstruct ability is required."
        public const UInt32 HackConstructFactionFail = 0x60000027; // "You must be in a faction to hack constructs."
        public const UInt32 LocateDataNodeOn = 0x7c000011;      // "You begin searching for Data Nodes."
        public const UInt32 LocateDataNodeOff = 0x7c000012;     // "You stop searching for Data Nodes."

        // ltfxmap.py
        public const UInt32 FxRecallTeamMemberPhaseOut = 0x280006d1, FxRecallMissionTeamPhaseOut = 0x280006d2,
            FxGenericAbilityUpgrade = 0x2800041b;

        // Object states (StateMachine ids in gameobjects.gob)
        public const UInt32 ClosedState = 0x835, LockedState = 0x836, TrappedState = 0x839, DisarmedState = 0x83a,
            TriggeredState = 0x83b, UnlockedState = 0x841;

        // Server rule ids (serverrules.py)
        public const UInt32 RuleDoorPickLock = 4000, RuleSecurityPbxDisable = 1012;

        /// <summary>
        /// Tools by the ability their AbilityPrerequisite/ReqAbilityID names (gameobjects.gob): Lock_Pick 4 and
        /// Digital_Camera 379 (Open Locks), DisableDeviceTool 616, TrapDetectToolBasic 2500 (Disarm Traps),
        /// DataNodeLocator_HandHeld 422, DataNodeLocatorGoggles 423 and BasicDataNodeLocatorGoggles 38209 (Locate Data
        /// Node, whose mastery Find Data Node is), AdvancedDataNodeLocatorGoggles 38208 (Siphon Data Node 622).
        /// </summary>
        public static readonly IReadOnlyDictionary<UInt16, UInt32[]> Tools = new Dictionary<UInt16, UInt32[]>
        {
            { OpenLocks, new UInt32[] { 4, 379 } },
            { DisableSecurityDevices, new UInt32[] { 616 } },
            { DisarmTraps, new UInt32[] { 2500 } },
            { FindDataNode, new UInt32[] { 422, 423, 38209, 38208 } },
            { LocateDataNode, new UInt32[] { 422, 423, 38209, 38208 } },
        };

        /// <summary>DataNodeLocator/Range (metres) and /Type (0 handheld, 1 goggles) of the locator tools.</summary>
        public static readonly IReadOnlyDictionary<UInt32, (float range, byte type)> Locators = new Dictionary<UInt32, (float, byte)>
        {
            { 422, (30f, 0) }, { 423, (5f, 1) }, { 38209, (5f, 1) }, { 38208, (15f, 1) },
        };

        /// <summary>Tool/TrapDisarmBonus of the Disarm Traps tools: TrapDetectToolBasic stores only TrapDetectBonus 5.</summary>
        public static int TrapDisarmBonus(UInt32 tool) => 0;

        private static readonly object randomLock = new object();
        private static readonly Random random = new Random();

        /// <summary>random.randrange(100). Tests replace it.</summary>
        public static Func<int> Roll = () => { lock (randomLock) return random.Next(100); };

        // ---- The rules, as pure functions ----------------------------------------------------------------------------

        /// <summary>serverrules.py evalContest: (A + aMod + d100) - (D + dMod + 50). The attacker wins above 0.</summary>
        public static int EvalContest(int a, int aMod, int d, int dMod, int roll) => a + aMod + roll - (d + dMod + 50);

        public enum PickResult { Success, NoAbility, NoTool }

        /// <summary>
        /// DoorPickLock (rule 4000) as it shipped: the ability loaded, then a lockpick tool, then success ("Lock picks always
        /// succeed after the progress bar has finished"). The contest after its early return (LockPickingAbility against
        /// Difficulty x 4) never ran.
        /// </summary>
        public static PickResult PickLock(int openLocksLevel, bool hasTool)
        {
            if (openLocksLevel <= 0) return PickResult.NoAbility;
            return hasTool ? PickResult.Success : PickResult.NoTool;
        }

        public static UInt32 PickLockMessage(PickResult result) =>
            result == PickResult.Success ? PickLockSuccess : result == PickResult.NoAbility ? PickLockAbilityFailed : PickLockToolFailed;

        /// <summary>SecurityPBXDisable results: 0 nothing happens, 1 access granted (DisarmedState), 2 alarm (TriggeredState).</summary>
        public const byte DsdNothing = 0, DsdAccessGranted = 1, DsdAlarm = 2;

        /// <summary>
        /// SecurityPBXDisable (rule 1012): Disable Security Devices loaded and a disable tool carried, then a contest of the
        /// Security Systems skill against the device's Difficulty; under -15 the alarm goes off, otherwise access is granted.
        /// </summary>
        public static byte SecurityDisable(int dsdLevel, bool hasTool, int securitySystemsLevel, int difficulty, int roll,
            out string chat)
        {
            if (dsdLevel <= 0)
            {
                chat = "Operation failed! Make sure you have the Disable Security Devices ability loaded.";
                return DsdNothing;
            }
            if (!hasTool)
            {
                chat = "Operation failed! Make sure you have a disable device tool in your inventory.";
                return DsdNothing;
            }
            if (EvalContest(securitySystemsLevel, 0, difficulty, 0, roll) < -15)
            {
                chat = "ALARM TRIGGERED";
                return DsdAlarm;
            }
            chat = "ACCESS GRANTED";
            return DsdAccessGranted;
        }

        public enum DisarmResult { NotTrapped, Disarmed, Failed, Critical }

        /// <summary>
        /// DisarmTraps_Test: the object must be trappable and armed. A trap nobody has detected (DetectTrapFX unset) goes off;
        /// otherwise Disarm Traps' level plus the tool's TrapDisarmBonus contests the trap's DisarmDifficulty: above 0 it's
        /// disarmed, above -25 it isn't, below that it goes off.
        /// </summary>
        public static DisarmResult DisarmTrap(bool armed, int level, int toolBonus, int disarmDifficulty, bool detected, int roll)
        {
            if (!armed) return DisarmResult.NotTrapped;
            if (!detected) return DisarmResult.Critical;
            int delta = EvalContest(level, toolBonus, 0, disarmDifficulty, roll);
            return delta > 0 ? DisarmResult.Disarmed : delta > -25 ? DisarmResult.Failed : DisarmResult.Critical;
        }

        public static UInt32 DisarmMessage(DisarmResult result) =>
            result == DisarmResult.Disarmed ? TrapDisarmed : result == DisarmResult.Failed ? TrapDisarmFailure : TrapDisarmCritical;

        /// <summary>
        /// rshooks.py ComputeDownloadMissionMapOutcome: Download Mission Map's level x 4 plus Map Downloading's, plus d100,
        /// against the mission's difficulty + 50. Below 0 the download fails (0); otherwise access flag 1, plus 2, 4, 8, 16
        /// and 32 for each further 10 points.
        /// </summary>
        public static int MissionMapFlags(int downloadMapLevel, int mapDownloadingLevel, int missionDifficulty, int roll)
        {
            int ability = downloadMapLevel * 4 + mapDownloadingLevel;
            if (ability == 0) return 0;
            int outcome = ability + roll - (missionDifficulty + 50);
            if (outcome < 0) return 0;
            int flags = 1;
            for (int step = 1; step <= 5; step++)
            {
                if (outcome >= step * 10) flags |= 1 << step;
            }
            return flags;
        }

        // ---- Runtime --------------------------------------------------------------------------------------------------

        public static bool IsDataNodeSearch(UInt16 ability) => ability == LocateDataNode || ability == FindDataNode;
        /// <summary>Actual tool AbilityPrerequisite/ReqAbilityID from the GOB, not a mastery alias.</summary>
        public static ushort LocatorRequirement(uint tool) => tool == 38208 ? (ushort)622 : Locators.ContainsKey(tool) ? LocateDataNode : (ushort)0;
        public static bool Handles(UInt16 ability) =>
            ability == DisarmTraps || ability == RecallTeamMember || ability == RecallMissionTeam || IsDataNodeSearch(ability);

        public static int Level(WorldClient client, UInt16 ability) => AbilityPassives.Level(client, ability);

        /// <summary>The object has a LockedState whose Pick Lock runs rule 4000 (doors, safes, cabinets, desks, boxes).</summary>
        public static bool IsLockable(UInt16 goid) =>
            ObjectStates.Find(goid, LockedState) != null && ObjectStates.UsableLabelled(goid, "Pick Lock")?.executeRule == RuleDoorPickLock;

        /// <summary>The first carried tool for the ability, or 0 (Inventory.findToolForAbility).</summary>
        public static UInt32 ToolFor(WorldClient client, UInt16 ability)
        {
            if (!Tools.TryGetValue(ability, out UInt32[] tools)) return 0;
            foreach (Crafting.InventoryItem item in Store.dbManager.WorldDbHandler.GetInventoryItems(client.playerData.getCharID()))
            {
                if (item.Slot >= 1 && item.Slot <= 96 && tools.Contains(item.Goid) &&
                    (!IsDataNodeSearch(ability) || Level(client, LocatorRequirement(item.Goid)) > 0)) return item.Goid;
            }
            return 0;
        }

        private static void ToCaster(WorldClient client, UInt32 stringId, UInt16 ability, string target = "")
        {
            client.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(stringId, true, ability,
                TimedEffects.PlayerName(client), target ?? "", 0));
        }

        private static void ToTarget(WorldClient target, UInt32 stringId, UInt16 ability, string caster)
        {
            target.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(stringId, false, ability, caster,
                TimedEffects.PlayerName(target), 0));
        }

        private class Locked
        {
            public UInt16 goid;
            public Action onUnlocked;
        }

        // Mission objects spawned locked, per client: entity -> what they are
        private static readonly ConditionalWeakTable<WorldClient, Dictionary<UInt64, Locked>> locked =
            new ConditionalWeakTable<WorldClient, Dictionary<UInt64, Locked>>();

        // Trapped objects whose trap is showing (DetectTrapFX set), per client
        private static readonly ConditionalWeakTable<WorldClient, HashSet<UInt64>> detectedTraps =
            new ConditionalWeakTable<WorldClient, HashSet<UInt64>>();

        /// <summary>A mission object spawned in its LockedState: only Pick Lock (rule 4000) gets it open.</summary>
        public static void RegisterLocked(WorldClient client, UInt64 entity, UInt16 goid, Action onUnlocked = null)
        {
            Dictionary<UInt64, Locked> map = locked.GetOrCreateValue(client);
            lock (map) map[entity] = new Locked { goid = goid, onUnlocked = onUnlocked };
        }

        public static bool IsLocked(WorldClient client, UInt64 entity)
        {
            if (!locked.TryGetValue(client, out Dictionary<UInt64, Locked> map)) return false;
            lock (map) return map.ContainsKey(entity);
        }

        public static void MarkTrapDetected(WorldClient client, UInt64 entity)
        {
            HashSet<UInt64> set = detectedTraps.GetOrCreateValue(client);
            lock (set) set.Add(entity);
        }

        private static bool TrapDetected(WorldClient client, UInt64 entity)
        {
            if (!detectedTraps.TryGetValue(client, out HashSet<UInt64> set)) return false;
            lock (set) return set.Contains(entity);
        }

        /// <summary>The state the data's script moves to for this use and rule result (a door's Pick Lock to 0x835).</summary>
        private static UInt32 After(UInt16 goid, UInt32 state, byte use, byte result) =>
            ObjectStates.StateAfterUse(goid, state, use, result);

        private static void SendState(WorldClient client, UInt64 entity, UInt16 goid, UInt32 state)
        {
            if (ObjectStates.SetState(client, entity, state)) return;
            ClientView view = client.viewMan.FindViewForEntity(entity);
            byte[] update = view == null ? null : ObjectStates.BuildStateUpdate(goid, state);
            if (update != null) client.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view.ViewID, update), false);
        }

        /// <summary>
        /// A use (80 c7) of a locked mission object or of a followed object whose usable runs one of these rules (Pick Lock,
        /// Disable Security Devices). Applies the rule and, as its script does, moves the object on the result. True when the
        /// use was handled here and nothing else should see it.
        /// </summary>
        public static bool OnObjectUse(WorldClient client, UInt64 entity, byte use)
        {
            Locked mission = null;
            if (locked.TryGetValue(client, out Dictionary<UInt64, Locked> map))
            {
                lock (map) map.TryGetValue(entity, out mission);
            }

            UInt16 goid;
            UInt32 state;
            if (mission != null)
            {
                goid = mission.goid;
                state = LockedState;
            }
            else
            {
                (goid, state) = ObjectStates.TrackedObject(client, entity);
                if (goid == 0) return false;
            }

            ObjectStates.Usable usable = ObjectStates.FindUsable(goid, use);
            UInt32 rule = usable?.executeRule ?? 0;
            if (rule == RuleDoorPickLock)
            {
                PickResult result = PickLock(Level(client, OpenLocks), ToolFor(client, OpenLocks) != 0);
                ToCaster(client, PickLockMessage(result), OpenLocks);
                Output.WriteDebugLog("[UTILITY] Pick Lock on " + goid + ": " + result);
                if (result == PickResult.Success)
                {
                    UInt32 to = After(goid, state, use, 1);
                    if (to != 0) SendState(client, entity, goid, to);
                    if (mission != null)
                    {
                        lock (map) map.Remove(entity);
                        mission.onUnlocked?.Invoke();
                    }
                }
                client.FlushQueue();
                return true;
            }

            if (rule == RuleSecurityPbxDisable && mission == null)
            {
                int difficulty = SecurityDifficulty(goid);
                byte result = SecurityDisable(Level(client, DisableSecurityDevices), ToolFor(client, DisableSecurityDevices) != 0,
                    Level(client, SecuritySystems), difficulty, Roll(), out string chat);
                new ServerPackets().sendSystemChatMessage(client, chat, "SYSTEM");
                Output.WriteDebugLog("[UTILITY] Disable Security Devices on " + goid + ": result " + result + " (" + chat + ")");
                UInt32 to = result == DsdNothing ? 0 : After(goid, state, use, result);
                if (to != 0) SendState(client, entity, goid, to);
                client.FlushQueue();
                return true;
            }

            // A locked mission object offers nothing else that opens it (Unlock wants a key item)
            if (mission != null)
            {
                Output.WriteDebugLog("[UTILITY] Use " + use + " of a locked mission object ignored");
                return true;
            }
            return false;
        }

        /// <summary>DisableUsage/Difficulty of the security devices (Security_Panel 466: 10; Security_Desk 377: 0).</summary>
        public static int SecurityDifficulty(UInt16 goid) => goid == 466 ? 10 : 0;

        /// <summary>When a cast of one of these abilities ends.</summary>
        public static void Land(WorldClient client, UInt16 ability, UInt16 targetView)
        {
            switch (ability)
            {
                case DisarmTraps:
                    Disarm(client, targetView);
                    break;
                case RecallTeamMember:
                case RecallMissionTeam:
                    Recall(client, ability, targetView);
                    break;
                case FindDataNode:
                case LocateDataNode:
                    ToggleFindDataNode(client,ability);
                    break;
            }
            client.FlushQueue();
        }

        private static void Disarm(WorldClient client, UInt16 targetView)
        {
            ClientView view = client.viewMan.getViewById(targetView);
            (UInt16 goid, UInt32 state) = view == null ? ((UInt16)0, 0u) : ObjectStates.TrackedObject(client, view.entityId);
            if (goid == 0)
            {
                Output.WriteDebugLog("[UTILITY] Disarm Traps: view " + targetView + " is no object the server follows");
                return;
            }

            UInt32 tool = ToolFor(client, DisarmTraps);
            DisarmResult result = DisarmTrap(state == TrappedState, Level(client, DisarmTraps), TrapDisarmBonus(tool), 0,
                TrapDetected(client, view.entityId), Roll());
            Output.WriteDebugLog("[UTILITY] Disarm Traps on " + goid + " in 0x" + state.ToString("x") + ": " + result);
            if (result == DisarmResult.NotTrapped) return; // retail's test returned 0 with no message
            ToCaster(client, DisarmMessage(result), DisarmTraps);
            if (result == DisarmResult.Disarmed) SendState(client, view.entityId, goid, DisarmedState);
            if (result == DisarmResult.Critical) SendState(client, view.entityId, goid, TriggeredState);
        }

        private static void Recall(WorldClient caster, UInt16 ability, UInt16 targetView)
        {
            LiveTeam team = SocialGroups.Instance.FindTeamOf(caster.playerData.getCharID());
            List<WorldClient> targets;
            if (ability == RecallTeamMember)
            {
                WorldClient target = AbilitySupport.PlayerInView(caster, targetView);
                // RecallTeamMember_Test: "Can't recall self"; the client only offers it on a team member
                if (target == null || target == caster || team == null || !team.HasMember(target.playerData.getCharID()))
                {
                    Output.WriteDebugLog("[UTILITY] Recall Team Member: view " + targetView + " is no other team member");
                    return;
                }
                targets = new List<WorldClient> { target };
                ToCaster(caster, TeleportingCaster, ability, TimedEffects.PlayerName(target));
            }
            else
            {
                targets = team == null ? new List<WorldClient>() : SocialHandler.OnlineWhere(c => c != caster &&
                    team.HasMember(c.playerData.getCharID())).ToList();
                ToCaster(caster, TeleportingTeamCaster, ability);
            }
            caster.FlushQueue();

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(caster.playerInstance.Position.getValue(), ref x, ref y, ref z);
            string casterName = TimedEffects.PlayerName(caster);
            foreach (WorldClient target in targets)
            {
                // RecallTeamMember_DirectObject
                if (target.playerData.getDistrictId() != caster.playerData.getDistrictId())
                {
                    ToCaster(caster, RecallAcrossConstructs, ability, TimedEffects.PlayerName(target));
                    caster.FlushQueue();
                    continue;
                }
                if (CombatManager.GetHandler(target).isCombatRunning || target.playerData.isDead || target.playerData.duelDeath)
                {
                    // Retail sent ID_CLIENT_TELEPORTING_INCOMBAT / _DEAD, which the 7.6005 client's table doesn't have
                    Output.WriteDebugLog("[UTILITY] Recall: " + TimedEffects.PlayerName(target) + " is fighting or dead");
                    continue;
                }

                ToTarget(target, TeleportingTarget, ability, casterName);
                ServerPackets.SendPlayerAbilityEffect(target,
                    ability == RecallTeamMember ? FxRecallTeamMemberPhaseOut : FxRecallMissionTeamPhaseOut);
                MissionStaging.Teleport(target, x, y, z);
                target.FlushQueue();
                Output.WriteDebugLog("[UTILITY] Recalled " + TimedEffects.PlayerName(target) + " to " + casterName + " at " +
                                     x + "," + y + "," + z);
            }
        }

        // Find Data Node is a toggle (IsAToggleAbility)
        public static bool IsSearchingForDataNodes(WorldClient client) => DataMining.Searching(client);

        /// <summary>Idempotent deactivation: a concurrent tool/death cleanup must never turn a pickup into a new search.</summary>
        public static bool StopFindDataNode(WorldClient client)
        {
            if (!DataMining.Stop(client,out ushort ability)) return false;
            ToCaster(client, LocateDataNodeOff, ability);
            return true;
        }

        /// <summary>
        /// Find Data Node (the mastery of Locate Data Node): needs a locator tool (Ability/RequiresTool), then searches until
        /// used again. LocateDataNode_DirectObject reads the tool's Range and Type; with goggles See Luggables is passed to
        /// the visual search, with the handheld Detect Luggables asks for the nearest luggable. Those searches are the
        /// client's 80 cb/cd/ce records, sent by DataMining.Check (docs/DATA-MINING.md).
        /// </summary>
        private static void ToggleFindDataNode(WorldClient client, ushort ability)
        {
            if (StopFindDataNode(client)) return;
            if (Level(client,ability) <= 0) return;

            UInt32 tool = ToolFor(client, ability);
            if (tool == 0)
            {
                client.messageQueue.addRpcMessage(DuelPackets.BuildSystemString(ToolFail));
                return;
            }

            DataMining.Start(client, tool,ability);
            ToCaster(client, LocateDataNodeOn, ability);
            (float range, byte type) = Locators[tool];
            Output.WriteDebugLog("[UTILITY] Find Data Node with tool " + tool + ": range " + range + " m, " +
                                 (type == 0 ? "handheld" : "goggles") +
                                 (type == 1 && Level(client, SeeLuggables) > 0 ? ", See Luggables" : "") +
                                 (type == 0 && Level(client, DetectLuggables) > 0 ? ", Detect Luggables" : ""));
        }

        // Download Mission Map's access flags for the mission running, per client
        private static readonly ConditionalWeakTable<WorldClient, StrongBox<int>> mapFlags =
            new ConditionalWeakTable<WorldClient, StrongBox<int>>();

        public static int MissionMapAccess(WorldClient client) =>
            mapFlags.TryGetValue(client, out StrongBox<int> flags) ? flags.Value : 0;

        /// <summary>
        /// A mission was accepted: Download Mission Map's roll (rshooks.py). Our missions are made at the player's level, so
        /// that is the difficulty. Success plays FX_CHARACTER_GENERIC_ABILITY_UPGRADE as retail did; retail's
        /// ID_ABILITY_DOWNLOAD_MISSION_MAP_SUCCESS / _FAILED lines aren't in the 7.6005 client's string table.
        /// </summary>
        public static void OnMissionAccepted(WorldClient client)
        {
            int dmm = Level(client, DownloadMissionMap);
            int flags = MissionMapFlags(dmm, Level(client, MapDownloading), MissionStaging.PlayerLevel(client), Roll());
            mapFlags.GetOrCreateValue(client).Value = flags;
            if (dmm <= 0) return;
            Output.WriteDebugLog("[UTILITY] Download Mission Map: access flags " + flags);
            if (flags != 0) ServerPackets.SendPlayerAbilityEffect(client, FxGenericAbilityUpgrade);
        }

        /// <summary>
        /// The hardline's Hack Construct (booth use 7, rule 10024 HardlineHackConstruct): Hack Construct Key loaded and a
        /// faction. The construct war that follows doesn't exist here, so it stops at the checks.
        /// </summary>
        public static void HackConstruct(WorldClient client)
        {
            if (Level(client, HackConstructKey) <= 0)
            {
                client.messageQueue.addRpcMessage(DuelPackets.BuildSystemString(HackConstructAbilityFail));
            }
            else
            {
                client.messageQueue.addRpcMessage(DuelPackets.BuildSystemString(HackConstructFactionFail));
            }
            Output.WriteDebugLog("[UTILITY] Hack Construct at a hardline: level " + Level(client, HackConstructKey));
        }

        /// <summary>
        /// Dev (MXO_DEV_COMMANDS=1): <c>?trapobj &lt;goid&gt; [hidden]</c> spawns an object in its TrappedState at the player's
        /// feet (as ?objstate does), its trap detected unless "hidden", for trying Disarm Traps.
        /// </summary>
        public static void DevTrapObject(WorldClient client, string[] args)
        {
            if (args.Length < 2 || !UInt16.TryParse(args[1], out UInt16 goid))
            {
                new ServerPackets().sendSystemChatMessage(client, "?trapobj <goid> [hidden]", "BROADCAST");
                return;
            }

            bool hidden = args.Length > 2 && args[2] == "hidden";
            List<string> objArgs = new List<string> { "?objstate", goid.ToString(), "0x" + TrappedState.ToString("x"), "TrapArmed=01" };
            if (!hidden) objArgs.Add("DetectTrapFX=55060022"); // FX_OBJECT_SELECT_TARGET_SELECT_CHAR, DetectTraps' detected FX
            ObjectStates.DevCommand(client, objArgs.ToArray());
            UInt64 entity = ObjectStates.LastDevObject(client);
            if (entity != 0 && !hidden) MarkTrapDetected(client, entity);
        }
    }
}
