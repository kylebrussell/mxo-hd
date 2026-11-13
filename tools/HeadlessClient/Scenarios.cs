using System.Text;
using hds;

namespace HeadlessClient;

/// <summary>
/// The scripted gameplay. Client records here are written from the layouts in docs/SECURE-TRADE.md, the retail captures
/// and the handlers' comments, not built with the server's classes. A pass shows the server does what we believe the
/// protocol is; it can't show the real client accepts it.
/// </summary>
public static partial class Scenarios
{
    private static readonly IReadOnlyDictionary<string, string> StillNpcs = new Dictionary<string, string>
    {
        ["MXO_MOB_WANDER"] = "0",
        ["MXO_MOBS"] = "test", // the small list, so a run is fast and NPC positions are known
        ["MXO_AGGRO"] = "0",
    };

    private static readonly IReadOnlyDictionary<string,string> HealthCallbackNpcs = StillNpcs.Concat(new Dictionary<string,string> {
        ["MXO_DEV_COMMANDS"]="1",["MXO_CALLBACK_QA_ROLL15"]="0",["MXO_CALLBACK_QA_ROLL100"]="0"
    }).ToDictionary(p=>p.Key,p=>p.Value);

    public static readonly IReadOnlyList<Scenario> All = new[]
    {
        new Scenario("protocol-family-canary", ProtocolFamilyCanary, StillNpcs),
        new Scenario("zion-prima-chain", ZionPrimaChain, new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="230410"}),
        new Scenario("small03-partial-world", Small03PartialWorld, new Dictionary<string,string> { ["MXO_MOB_WANDER"]="0", ["MXO_AGGRO"]="0" }),
        new Scenario("medium03-partial-world", Medium03PartialWorld, new Dictionary<string,string>{["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"}),
        new Scenario("medium03-prefab-door", Medium03PrefabDoorDiagnostic, new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="0",["MXO_MOBS"]=""}),
        new Scenario("mission-relog-corpse-loot", MissionRelogCorpseLoot, StillNpcs, "mission-persistence-loot.xml"),
        new Scenario("mission-relog-disconnect-move", MissionRelogDisconnectAndMove, StillNpcs),
        new Scenario("mission-relog-object-rollback", MissionRelogObjectsAndRollback, StillNpcs, "mission-persistence.xml"),
        new Scenario("mission-relog-persistence", MissionRelogPersistence, StillNpcs),
        new Scenario("mission-presentation", MissionPresentation, new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1"}),
        new Scenario("machines-prima-chain",MachinesPrimaChain,new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="220110"}),
        new Scenario("resist-damage-radius", ResistDamageRadius, StillNpcs),
        new Scenario("merv-prima-chain", MervPrimaChain, new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="210110"}),
        new Scenario("proxy-technician-control", ProxyTechnicianControlRefusal, new Dictionary<string,string>(StillNpcs) { ["MXO_DEV_COMMANDS"]="1" }),
        new Scenario("proxy-technician-friendly-attacks", ProxyTechnicianFriendlyAttacks, new Dictionary<string,string>(StillNpcs) { ["MXO_DEV_COMMANDS"]="1",["MXO_PROXY_TECHNICIAN_QA_ROLL"]="80" }),
        new Scenario("proxy-technician-lifecycle", ProxyTechnicianLifecycle, new Dictionary<string,string>(StillNpcs) { ["MXO_DEV_COMMANDS"]="1" }),
        new Scenario("consumable-sweep-removal-log", ConsumableSweepRemovalLog, StillNpcs),
        new Scenario("consumable-sweep-cast-contract", ConsumableSweepCastContract, StillNpcs),
        new Scenario("consumable-sweep-cast-cancellation", ConsumableSweepCastCancellation, StillNpcs),
        new Scenario("consumable-sweep-control", ConsumableSweepControlCancellation, StillNpcs),
        new Scenario("consumable-sweep-items", ConsumableSweepItems, StillNpcs),
        new Scenario("consumable-sweep-permissions", ConsumableSweepPermissions, StillNpcs),
        new Scenario("health-boost-pills", HealthBoostPillUse, HealthCallbackNpcs),
        new Scenario("anti-stun-pills", AntiStunPillUse, HealthCallbackNpcs),
        new Scenario("anti-stun-writer-interruption", AntiStunWriterInterruption, HealthCallbackNpcs),
        new Scenario("conditional-daze-fixture", ConditionalDazeFixtureCommand, new Dictionary<string,string>(StillNpcs) { ["MXO_DEV_COMMANDS"]="1" }),
        new Scenario("conditional-daze-fixture-disabled", ConditionalDazeFixtureDisabled, new Dictionary<string,string>(StillNpcs) { ["MXO_DEV_COMMANDS"]="0" }),
        new Scenario("conditional-dazed-stun", ConditionalDazedStun, StillNpcs),
        new Scenario("qa-loadout", QaLoadoutCommands, StillNpcs),
        new Scenario("qa-loadout-disabled", QaLoadoutDisabled, new Dictionary<string,string>(StillNpcs) { ["MXO_DEV_COMMANDS"]="0" }),
        new Scenario("ability-1124-tool-refusal", DisruptConnectionToolRefusal, StillNpcs),
        new Scenario("stealth-shields-native-fixture", StealthShieldNativeFixture, StillNpcs),
        new Scenario("stealth-shields-native-refusal", StealthShieldNativeRefusal, StillNpcs),
        new Scenario("stealth-shields-lifecycle", StealthShieldLifecycle, StillNpcs),
        new Scenario("stealth-shields-terminal-cancel", StealthBullDogDeathCancels, StillNpcs),
        new Scenario("stealth-shields-suppression", StealthShieldSuppression, StillNpcs),
        new Scenario("stealth-shields-bulldog-pvp", StealthBullDogPvpAndViews, StillNpcs),
        new Scenario("stealth-shields-bulldog-npc", StealthBullDogNpc, StillNpcs),
        new Scenario("ability-health-presentation-heal-regen", HealthPresentationHealRegen, HealthCallbackNpcs),
        new Scenario("ability-health-presentation-damage", HealthPresentationDamage, HealthCallbackNpcs),
        new Scenario("ignored-actions-window-close", IgnoredActionsWindowClose, StillNpcs),
        new Scenario("client-actions-research", ClientActionsResearchCanary, StillNpcs),
        new Scenario("ignored-actions-gm", IgnoredActionsGm, new Dictionary<string,string> { ["MXO_GM_CHARACTER_IDS"]="1001", ["MXO_MOB_WANDER"]="0", ["MXO_MOBS"]="test", ["MXO_AGGRO"]="0" }),
        new Scenario("ignored-actions-support", IgnoredActionsSupport, new Dictionary<string,string> { ["MXO_DEV_COMMANDS"]="0", ["MXO_MOB_WANDER"]="0", ["MXO_MOBS"]="test", ["MXO_AGGRO"]="0" }),
        new Scenario("ignored-actions-presence-trade-compile", IgnoredActionsPresenceTradeCompile, StillNpcs),
        new Scenario("ability-zen-master", ZenMaster, HealthCallbackNpcs),
        new Scenario("ability-desperation-snapshot", DesperationSnapshot, HealthCallbackNpcs.Concat(new Dictionary<string,string> { ["MXO_DESPERATION_QA_HOLD_HEALTH"]="1" }).ToDictionary(p=>p.Key,p=>p.Value)),
        new Scenario("ability-concentration-armed-fixture", ConcentrationArmedFixture,
            new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_CAST_INTERRUPT_QA_ROLL"]="85",["MXO_CONCENTRATION_QA_DAMAGE"]="1"}),
        new Scenario("ability-concentration-control",ConcentrationControl,StillNpcs.Concat(new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="1",["MXO_CAST_INTERRUPT_QA_ROLL"]="85"}).ToDictionary(p=>p.Key,p=>p.Value)),
        new Scenario("ability-concentration-loaded",ConcentrationLoaded,StillNpcs.Concat(new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="1",["MXO_CAST_INTERRUPT_QA_ROLL"]="85"}).ToDictionary(p=>p.Key,p=>p.Value)),
        new Scenario("ability-health-proc-lifecycle", HealthProcLifecycle, HealthCallbackNpcs),
        new Scenario("ability-health-reactive-area", HealthReactiveArea, HealthCallbackNpcs),
        new Scenario("ability-health-deflect-lineup", HealthDeflectLineup, HealthCallbackNpcs),
        new Scenario("ability-health-lineup-timings", HealthLineupTimings, HealthCallbackNpcs),
        new Scenario("typed-special-queued", TypedSpecialQueued, StillNpcs),
        new Scenario("typed-special-defense", TypedSpecialDefense, StillNpcs),
        new Scenario("combat-depth-stealth-sight", CombatDepthStealthSight, StillNpcs),
        new Scenario("combat-depth-fixture", CombatDepthFixture, StillNpcs),
        new Scenario("combat-depth-special-shared", CombatDepthSpecialAndShared, StillNpcs),
        new Scenario("combat-depth-mitigation", CombatDepthMitigation, StillNpcs),
        new Scenario("combat-depth-sneak-opener", CombatDepthSneakOpener, StillNpcs),
        new Scenario("ability-drain-firewalls", AbilityDrainFirewalls, StillNpcs),
        new Scenario("ability-weapon-proficiencies", AbilityWeaponProficiencies, StillNpcs),
        new Scenario("ability-disguise-tools", AbilityDisguiseTools, StillNpcs),
        new Scenario("ability-buff-buffer-death", AbilityBuffBufferDeath, StillNpcs),
        new Scenario("ability-beneficial-groups", AbilityBeneficialGroups, StillNpcs),
        new Scenario("ability-tactical-blur", AbilityTacticalBlur, StillNpcs),
        new Scenario("ability-scaled-buffs", AbilityScaledBuffs, StillNpcs),
        new Scenario("ability-scaled-buff-vitals", AbilityScaledBuffVitals, StillNpcs),
        new Scenario("qa-revive-in-place", QaReviveInPlace, StillNpcs),
        new Scenario("ability-resurrection-resources", AbilityResurrectionResources, StillNpcs),
        new Scenario("ability-resurrection-approval-guards", AbilityResurrectionApprovalGuards, StillNpcs),
        new Scenario("ability-control-duration-bonuses", AbilityControlDurationBonuses, StillNpcs),
        new Scenario("ability-control-duration-npc", AbilityControlDurationNpc, StillNpcs),
        new Scenario("buff-dispel-fixture-hit", BuffDispelFixtureHit, StillNpcs.Concat(new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="1",["MXO_BUFF_DISPEL_QA_ROLL100"]="0"}).ToDictionary(p=>p.Key,p=>p.Value)),
        new Scenario("buff-dispel-fixture-miss", BuffDispelFixtureMiss, StillNpcs.Concat(new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="1",["MXO_BUFF_DISPEL_QA_ROLL100"]="99"}).ToDictionary(p=>p.Key,p=>p.Value)),
        new Scenario("buff-dispel-proc-npc", BuffDispelProcNpc, StillNpcs),
        new Scenario("buff-dispel-proc-pvp", BuffDispelProcPvp, StillNpcs),
        new Scenario("hostile-fields-pvp", HostileFieldsPvp, StillNpcs),
        new Scenario("hostile-fields-npc", HostileFieldsNpc, StillNpcs),
        new Scenario("ability-passives-lifecycle", AbilityPassivesLifecycle, StillNpcs),
        new Scenario("ability-heal-magnitudes", AbilityHealMagnitudes, StillNpcs),
        new Scenario("ability-passive-damage", AbilityPassiveDamage, StillNpcs),
        new Scenario("ability-parity-support", AbilityParitySupport, StillNpcs),
        new Scenario("typed-ability-resistance", TypedAbilityResistanceScenario, new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_TYPED_RESISTANCE_QA"]="1",["MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"]="65"}),
        new Scenario("coder-pet-lifecycle", CoderPetLifecycle, StillNpcs),
        new Scenario("coder-pet-resources", CoderPetResources, StillNpcs),
        new Scenario("coder-pet-typed-power-boost", CoderPetPowerBoost, StillNpcs),
        new Scenario("coder-pet-typed-daemon", CoderPetDaemonTyped, StillNpcs),
        new Scenario("mission-container-deposit", MissionContainerDeposit, StillNpcs, "container-put.xml"),
        new Scenario("data-mining-luggable-persistence", DataMiningLuggables, StillNpcs),
        new Scenario("data-mining-full-bag", DataMiningFullBag, StillNpcs),
        new Scenario("data-mining-base-and-margin", DataMiningKnownBaseLocator, StillNpcs),
        new Scenario("ashencourte-partial-world", AshencourtePartialWorld, new Dictionary<string,string>{["MXO_MOBS"]="",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"}),
        new Scenario("data-mining-locator", DataMiningLocator, StillNpcs),
        new Scenario("data-mining-mission-map", DataMiningMissionMap, StillNpcs, "locked-safe.xml"),
        new Scenario("utility-mission-locked-safe", MissionLockedSafe, StillNpcs, "locked-safe.xml"),
        new Scenario("utility-recall-team", RecallTeam, StillNpcs),
        new Scenario("widowsmoor-partial-world", WidowsMoorPartialWorld, StillNpcs),
        new Scenario("utility-security-and-traps", SecurityAndTraps, StillNpcs),
        new Scenario("yuki-partial-world", YukiPartialWorld, StillNpcs),
        new Scenario("death-penalty-persistence", DeathPenaltyPersistence, StillNpcs),
        new Scenario("archive02-partial-world", Archive02PartialWorld, StillNpcs),
        new Scenario("death-reconstruct-and-tutorial-selection", DeathReconstructAndTutorialSelection, StillNpcs),
        new Scenario("medium02-partial-world", Medium02PartialWorld, StillNpcs),
        new Scenario("pvp-archive-transport", PvpArchiveTransport, StillNpcs),
        new Scenario("archive01-partial-world", Archive01PartialWorld, StillNpcs),
        new Scenario("client-combat-actions", ClientCombatActions, StillNpcs),
        new Scenario("buff-dismiss-dev-disabled", BuffDismissDevDisabled,
            new Dictionary<string,string>(StillNpcs) { ["MXO_DEV_COMMANDS"] = "0" }),
        new Scenario("combat-style-switching", CombatStyleSwitching, StillNpcs),
        new Scenario("combat-specials-npc", CombatSpecialsNpc, StillNpcs),
        new Scenario("combat-specials-pvp", CombatSpecialsPvp, StillNpcs),
        new Scenario("native-crafting-requests", NativeCraftingRequests, StillNpcs),
        new Scenario("compile-session-authority", CompileSessionAuthority, StillNpcs),
        new Scenario("writer-session-authority", WriterSessionAuthority, StillNpcs),
        new Scenario("writer-session-dev-disabled", WriterSessionDevDisabled,
            new Dictionary<string,string>(StillNpcs) { ["MXO_DEV_COMMANDS"]="0" }),
        new Scenario("compile-storage-depletion", CompileStorageDepletion, StillNpcs),
        new Scenario("pvp-group-interlock", PvpGroupInterlock, StillNpcs),
        new Scenario("combat-rounds-and-observers", CombatRoundsAndObservers, StillNpcs),
        new Scenario("ability-parity-controls", AbilityParityControls, StillNpcs),
        new Scenario("ability-parity-area", AbilityParityArea, StillNpcs),
        new Scenario("ability-emp-blast", EmpBlast, StillNpcs.Concat(new Dictionary<string,string> { ["MXO_DEV_COMMANDS"]="1" }).ToDictionary(p=>p.Key,p=>p.Value)),
        new Scenario("ability-parity-costs", AbilityParityCosts, StillNpcs),
        new Scenario("ambient-npc-scope", AmbientNpcScope, new Dictionary<string, string>
        {
            ["MXO_MOBS"] = "world", ["MXO_MOB_WANDER"] = "0", ["MXO_AGGRO"] = "0"
        }),
        new Scenario("login", Login),
        new Scenario("login-with-orphaned-crew-member", LoginWithOrphanedCrewMember),
        new Scenario("organization-reputation", OrganizationReputation),
        new Scenario("ordered-rpc-burst", OrderedRpcBurst, StillNpcs),
        new Scenario("many-logins-at-once", ManyLoginsAtOnce),
        new Scenario("vendor-sell-recycle-economy", VendorSellRecycleEconomy, new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1"}),
        new Scenario("secure-trade", SecureTrade),
        new Scenario("trade-cancel-and-refusals", TradeCancelAndRefusals),
        new Scenario("email-send-read-delete", EmailSendReadDelete),
        new Scenario("email-attachments-and-mailbot", EmailAttachmentsAndMailBot),
        new Scenario("ignore-list-add-remove-and-relog", IgnoreListAddRemoveAndRelog),
        new Scenario("chat-rooms-create-talk-moderate-and-find", ChatRoomsCreateTalkModerateAndFind),
        new Scenario("socialbot-in-a-chat-room", SocialBotInAChatRoom),
        new Scenario("subway-ride-by-car", SubwayRideByCar),
        new Scenario("subway-across-districts-and-refusals", SubwayAcrossDistrictsAndRefusals),
        new Scenario("split-and-merge-stacks", SplitAndMergeStacks),
        new Scenario("central-storage-store-relog-retrieve", CentralStorageStoreRelogRetrieve),
        new Scenario("subway-platform-logins-keep-car-views", SubwayPlatformLoginsKeepCarViews),
        new Scenario("large02-partial-world", Large02PartialWorld, StillNpcs),
        new Scenario("team-invite-and-chat", TeamInviteAndChat),
        new Scenario("crew-invite-and-chat", CrewInviteAndChat),
        new Scenario("team-new-leader-and-boot", TeamNewLeaderAndBoot),
        new Scenario("socialbot-on-a-team", SocialBotOnATeam),
        new Scenario("crew-create-first-mate-captain-and-remove", CrewCreateFirstMateCaptainAndRemove),
        new Scenario("faction-ranks-and-removing-a-crew", FactionRanksAndRemovingACrew),
        new Scenario("team-seekers-list", TeamSeekersList),
        new Scenario("paired-emote-ask-accept-and-refuse", PairedEmoteAskAcceptAndRefuse),
        new Scenario("stances-seen-by-others", StancesSeenByOthers),
        new Scenario("socialbot-emotes-and-stances", SocialBotEmotesAndStances),
        new Scenario("give-mission-item-by-dragging", GiveMissionItemByDragging, StillNpcs, "give-item.xml"),
        new Scenario("prompt-and-fx-callbacks", PromptAndFxCallbacks),
        new Scenario("marketplace-purchase", MarketplacePurchase),
        new Scenario("marketplace-seller-in-loading-area", MarketplaceSellerInLoadingArea),
        new Scenario("loading-area-to-a-hardline-data-hardlines-lacks", LoadingAreaToMissingHardline),
        new Scenario("large01-partial-world", Large01PartialWorld, new Dictionary<string,string>{["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"}),
        new Scenario("two-players-fight-one-npc", TwoPlayersFightOneNpc, StillNpcs),
        new Scenario("other-view-level-up-and-equipment", OtherViewLevelUpAndEquipment),
        new Scenario("other-view-gunfire", OtherViewGunfire, StillNpcs),
        new Scenario("large01-world-door-diagnostic", Large01DoorDiagnostic, new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="0"}),
        new Scenario("large02-world-door-diagnostic", Large02DoorDiagnostic, new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="0"}),
        new Scenario("resent-rpc-handled-once", ResentRpcHandledOnce),
        new Scenario("login-reader-error-recovery", LoginReaderCleanup, new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="0"}),
        new Scenario("mission-rewards-welcome", MissionRewardsWelcome, StillNpcs),
        new Scenario("mission-rewards-hothouse", MissionRewardsHothouse, StillNpcs),
        new Scenario("mission-team-relog", MissionTeamRelog, StillNpcs),
        new Scenario("mission-objectives-relog", MissionObjectivesRelog, StillNpcs),
        new Scenario("story-mission-talks", StoryMissionTalks, StillNpcs),
        new Scenario("flood-story-mission", FloodStoryMission, StillNpcs),
        new Scenario("merovingian-chapter-12", MerovingianChapter12, StillNpcs),
        new Scenario("mission-ticket-collector", MissionTicketCollector, StillNpcs),
        new Scenario("mission-ticket-gates", MissionTicketGates, StillNpcs),
        new Scenario("mission-ticket-completion-retry", MissionTicketCompletionRetry, StillNpcs),
        new Scenario("merovingian-chapter-samples", MerovingianChapterSamples, StillNpcs),
        new Scenario("mission-arrivals-and-escort-to-object", MissionArrivalsAndEscortToObject, StillNpcs),
        new Scenario("mission-waves-sides-and-timers", MissionWavesSidesAndTimers, StillNpcs, "encounter-runtime.xml"),
        new Scenario("mission-arrival-navmesh", MissionArrivalNavmesh, StillNpcs, "arrival-navmesh.xml"),
        new Scenario("medium01-partial-world", Medium01PartialWorld, new Dictionary<string,string>{["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"}),
        new Scenario("persisted-heading-login", PersistedHeadingLogin, StillNpcs),
        new Scenario("mission-waves-and-side-switch", MissionWavesAndSideSwitch, StillNpcs),
        new Scenario("moved-mission-item", MovedMissionItem, StillNpcs),
        new Scenario("full-bag-mission-get", FullBagMissionGet, StillNpcs),
        new Scenario("full-bag-mission-provided", FullBagMissionProvided, StillNpcs),
        new Scenario("full-bag-mission-container", FullBagMissionContainer, StillNpcs),
        new Scenario("full-bag-mission-computer", FullBagMissionComputer, StillNpcs),
        new Scenario("full-bag-mission-loot", FullBagMissionLoot, StillNpcs),
        new Scenario("neighbourhood-contact-missions", NeighbourhoodContactMissions),
        new Scenario("azuna-collector", AzunaCollector, StillNpcs),
        new Scenario("legacy-inventory-startup", LegacyInventoryStartup, StillNpcs),
        new Scenario("azuna-collector-full-bag", AzunaCollectorFullBag, StillNpcs),
        new Scenario("azuna-collector-guards", AzunaCollectorGuards, StillNpcs),
        new Scenario("azuna-function-data-loot", AzunaFunctionDataLoot, new Dictionary<string, string>
        {
            ["MXO_MOBS"] = "world", ["MXO_MOB_WANDER"] = "0", ["MXO_AGGRO"] = "0"
        }),
        new Scenario("full-bag-world-loot", FullBagWorldLoot, new Dictionary<string, string>
        {
            ["MXO_MOBS"] = "world", ["MXO_MOB_WANDER"] = "0", ["MXO_AGGRO"] = "0"
        }),
        new Scenario("retained-corpse-loot", RetainedCorpseLoot, new Dictionary<string, string>
        {
            ["MXO_MOBS"] = "world", ["MXO_MOB_WANDER"] = "0", ["MXO_AGGRO"] = "0",
            ["MXO_MOB_CORPSE_SECONDS"] = "300", ["MXO_MOB_RESPAWN_SECONDS"] = "600"
        }),
        new Scenario("full-bag-tutorial-loot", FullBagTutorialLoot, new Dictionary<string, string>
        {
            ["MXO_MOBS"] = "test", ["MXO_MOB_WANDER"] = "0", ["MXO_AGGRO"] = "0", ["MXO_TUTORIAL_WARP"] = "21"
        }),
        new Scenario("container-and-corpse-loot", ContainerAndCorpseLoot, StillNpcs),
        new Scenario("pvp-close-combat-to-death", PvpCloseCombatToDeath, StillNpcs),
        new Scenario("pvp-gunfire", PvpGunfire, StillNpcs),
        new Scenario("pvp-server-flags", PvpServerFlags, PvpServer),
        new Scenario("pvp-flag-abilities", PvpFlagAbilities, StillNpcs),
        new Scenario("duel-to-defeat", DuelToDefeat, StillNpcs),
        new Scenario("duel-decline-surrender-and-distance", DuelDeclineSurrenderAndDistance, StillNpcs),
        new Scenario("duelbot-duels", DuelBotDuels, StillNpcs),
        new Scenario("duelbot-gun-and-pvp", DuelBotGunAndPvp, StillNpcs),
        new Scenario("stealth-sneak-observer", StealthSneakObserver, StillNpcs),
        new Scenario("stealth-breaks", StealthBreaks, StillNpcs),
        new Scenario("stealth-bot-hostile-observer", StealthBotHostileObserver, StillNpcs),
    };

    private static async Task OrderedRpcBurst(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")))[0];
        const int requests = 64;
        for (int reputation = 1; reputation <= requests; reputation++)
        {
            player.SendChat("?rep mero " + reputation);
        }
        await player.WaitUntilAsync(() => player.Received("80 ab 03").Count == requests,
            "All reputation changes in the burst", TimeSpan.FromSeconds(30));
        short[] received = player.Received("80 ab 03").Select(record => BitConverter.ToInt16(record.Bytes, 3)).ToArray();
        Check.Equal(string.Join(",", Enumerable.Range(1, requests)), string.Join(",", received),
            "Received datagrams are dispatched to the character in arrival order");
        Check.Equal(64L, await context.Database.LongAsync($"SELECT repMero FROM characters WHERE charId={player.CharId}"),
            "The final request determines the saved standing");
    }

    private static async Task OrganizationReputation(ScenarioContext context)
    {
        HeadlessPlayer player = await context.AddPlayerAsync(new CharacterSeed("Alice"));
        await player.EnterWorldAsync();

        player.SendChat("?rep mero -20");
        await player.WaitForRpcAsync("80 ab 03 ec ff");
        player.SendChat("?rep machine 82");
        await player.WaitForRpcAsync("80 ab 02 52 00");
        player.SendChat("?rep zion -9");
        await player.WaitForRpcAsync("80 ab 01 f7 ff");
        await context.Database.ExecuteAsync($"UPDATE characters SET repCyph=-6, repNiobe=4, repEpn=5, repGM=7 WHERE charId={player.CharId}");

        // A fresh server on the same test database verifies a new login without waiting for UDP timeout.
        string username = await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING (userId) WHERE charId={player.CharId}");
        using ServerInstance reloaded = await ServerInstance.StartAsync(context.RepositoryRoot, context.Database.Name);
        using HeadlessPlayer relogged = await HeadlessPlayer.LoginAsync(reloaded.Address, username, player.Handle);
        byte[] background = relogged.CharacterData[0x02];
        Check.Equal("faff5200ecff04000500f7ff0700", WireFormat.ToHex(background[1141..1155]), "Saved reputation in the client order");
        await relogged.EnterWorldAsync();
        Check.Equal(0, relogged.World.Problems.Count, "Negative reputation loads into the world");
    }

    // Where data/mob*.csv puts NPCs in the Slums, in both the Debug and Release lists
    private const double NpcX = 24499, NpcY = 495, NpcZ = -1498;

    private static string Le(uint value) => WireFormat.ToHex(BitConverter.GetBytes(value));

    private static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));

    /// <summary>The usual string layout: u16 size with the NUL, the text, a NUL.</summary>
    private static byte[] Sized(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        return BitConverter.GetBytes((ushort)(bytes.Length + 1)).Concat(bytes).Append((byte)0).ToArray();
    }

    /// <summary>A u16-offset string at the offset stored at <paramref name="pointerOffset"/>.</summary>
    private static string StringAtOffset(byte[] record, int pointerOffset)
    {
        int offset = BitConverter.ToUInt16(record, pointerOffset);
        int size = BitConverter.ToUInt16(record, offset);
        return Encoding.ASCII.GetString(record, offset + 2, size - 1);
    }

    /// <summary>Logs in, enters the world, and waits until each of the two can see the other.</summary>
    private static async Task<(HeadlessPlayer A, HeadlessPlayer B, ViewCreation BSeenByA, ViewCreation ASeenByB)> TwoNearby(
        ScenarioContext context, CharacterSeed a, CharacterSeed b)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(a, b);
        ViewCreation bSeenByA = await players[0].WaitForViewNamedAsync(b.Handle);
        ViewCreation aSeenByB = await players[1].WaitForViewNamedAsync(a.Handle);
        return (players[0], players[1], bSeenByA, aSeenByB);
    }

    /// <summary>Both accounts get through auth, margin and the world session, and each sees the other spawn.</summary>
    private static async Task Login(ScenarioContext context)
    {
        (HeadlessPlayer alice, _, ViewCreation bob, ViewCreation aliceSeen) = await TwoNearby(context, new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });
        Check.That(bob.ViewId > 3, "Server view ids start at 4");
        Check.Equal(alice.CharId, BitConverter.ToUInt32(aliceSeen.Attributes["CharacterID"]), "CharacterID in the other player's spawn");
    }

    private static async Task LoginWithOrphanedCrewMember(ScenarioContext context)
    {
        await context.Database.ExecuteAsync(@"
INSERT INTO crews (id,crew_name,master_player_handle,money,org,faction_id,faction_rank,created_at)
VALUES (9000,'LegacyCrew','CrewStartup',0,3,0,0,NOW());
INSERT INTO crew_members (crew_id,char_id,is_captain,is_first_mate,created_at)
VALUES (9000,1001,1,0,NOW()),(9000,999999,0,0,NOW());");
        HeadlessPlayer player = await context.AddPlayerAsync(new CharacterSeed("CrewStartup") { CrewId = 9000 });
        RpcRecord crew = await player.WaitForRpcAsync("80 86", TimeSpan.FromSeconds(15));
        Check.That(Contains(crew.Bytes, "LegacyCrew") && Contains(crew.Bytes, "CrewStartup"),
            "Login sends the crew and its existing member despite an orphaned row");
        await player.EnterWorldAsync();
        player.SendChat("?rep mero 30");
        await player.WaitForRpcAsync("80 ab 03 1e 00");
        Check.That(!context.Server.FullOutput().Contains("Packet handling failed"), "World setup completes without dropping a packet");
    }

    /// <summary>
    /// Everyone logging in at the same moment: nobody gets stuck, or spawns with another player's name or id. The server's
    /// world queries used to share one connection and reader.
    /// </summary>
    private static async Task ManyLoginsAtOnce(ScenarioContext context)
    {
        await Task.WhenAll(Enumerable.Range(1, 12).Select(async i =>
        {
            string handle = "Player" + i;
            HeadlessPlayer player = await context.AddPlayerAsync(new CharacterSeed(handle) { X = 17020 + i * 100 });
            await player.EnterWorldAsync();
            ViewCreation self = player.World.CreationSnapshot().Single(c => c.ViewId == 2);
            Check.Equal(handle, self.Name, "Name in the self spawn");
            Check.Equal(player.CharId, BitConverter.ToUInt32(self.Attributes["CharacterID"]), handle + "'s id in the self spawn");
        }));
    }

    /// <summary>
    /// `81 82` to a player's view, offers on both sides, accepts cleared by a change, and the swap. TargetID on the wire
    /// is `&lt;view u16&gt; &lt;spawn id u8&gt; 00`: the client builds it from the object's comm handle (client.dll 0x622ced10) and
    /// `81 51` carries the same shape in retail (crashover2k capture, `81 51 0f 00 51 00`). No retail or real-client
    /// `81 82` exists, so its shape is inferred from sharing that code path.
    /// </summary>
    private static async Task SecureTrade(ScenarioContext context)
    {
        (HeadlessPlayer alice, HeadlessPlayer bob, ViewCreation bobView, _) = await TwoNearby(context,
            new CharacterSeed("Alice") { Items = new[] { (3, NpcLoot.CodeBit1, 1) } },
            new CharacterSeed("Bob") { X = 17120, Info = 500 });

        alice.SendRpc($"81 82 {Le(bobView.ViewId)} {bobView.SpawnId:x2} 00  00 {Le(0u)} 03 00");

        RpcRecord result = await alice.WaitForRpcAsync("81 83 ?? ?? 00 00 00 00  00 00 00 00 00 03");
        Check.Equal("Bob", StringAtOffset(result.Bytes, 2), "RequestResult TargetName");
        RpcRecord offered = await bob.WaitForRpcAsync("81 84");
        Check.Equal("Alice", StringAtOffset(offered.Bytes, 2), "TradeOffered PartnerName");
        Check.Equal(NpcLoot.CodeBit1, BitConverter.ToUInt32(offered.Bytes, 9), "TradeOffered ClassData");
        Check.Equal((byte)1, offered.Bytes[0x11], "TradeOffered ItemQuantity");

        // Bob offers information: 81 86 with YourItem=1 to him and 0 to Alice
        bob.SendRpc($"81 85 00 01 {Le(200u)} 6b 00");
        await bob.WaitForRpcAsync($"81 86 01 01 01 {Le(200u)} 6b");
        await alice.WaitForRpcAsync($"81 86 01 00 01 {Le(200u)} 6b");

        // Alice accepts; Bob's further change clears that, so his accept alone can't finish it
        alice.SendRpc($"81 85 04 00 {Le(0u)} 6b 00");
        await alice.WaitForRpcAsync("81 87 1c 00 00 8e");
        await bob.WaitForRpcAsync("81 87 1d 00 00 8e");

        bob.SendRpc($"81 85 00 01 {Le(50u)} 6b 00");
        await alice.WaitForRpcAsync($"81 86 01 00 01 {Le(50u)} 6b");
        await bob.WaitForRpcAsync($"81 86 01 01 01 {Le(50u)} 6b");

        bob.SendRpc($"81 85 04 00 {Le(0u)} 6b 00");
        await bob.WaitForRpcAsync("81 87 1c 00 00 8e");
        await alice.WaitForRpcAsync("81 87 1d 00 00 8e");
        await Task.Delay(300);
        Check.Equal(0, alice.Received("81 87 19 00 00 8e").Count + bob.Received("81 87 19 00 00 8e").Count, "COMPLETE records before Alice re-accepts");

        // Reset accept and accept again, then the swap
        bob.SendRpc($"81 85 05 00 {Le(0u)} 6b 00");
        await bob.WaitForRpcAsync("81 87 1e 00 00 8e");
        await alice.WaitForRpcAsync("81 87 1f 00 00 8e");
        bob.SendRpc($"81 85 04 00 {Le(0u)} 6b 00");
        alice.SendRpc($"81 85 04 00 {Le(0u)} 6b 00");

        // Status, items given, items received, signed info change
        await alice.WaitForRpcAsync($"81 87 19 00 00 8e {Le(1u)} {Le(0u)} fa 00 00 00 00 00 00 00");
        await bob.WaitForRpcAsync($"81 87 19 00 00 8e {Le(0u)} {Le(1u)} 06 ff ff ff ff ff ff ff");

        Check.That(alice.Received("5f 03 00 01").Count == 1, "Alice's client told the item left slot 3");
        RpcRecord added = bob.Received("5e").Single(r => BitConverter.ToUInt32(r.Bytes, 3) == NpcLoot.CodeBit1);
        Check.That(alice.Received($"80 e4 {Le(1250u)}").Count == 1, "Alice's information update");
        Check.That(bob.Received($"80 e4 {Le(250u)}").Count == 1, "Bob's information update");

        ushort bobSlot = BitConverter.ToUInt16(added.Bytes, 1);
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={alice.CharId} AND goid={NpcLoot.CodeBit1}"), "Alice's Code Bits in the database");
        Check.Equal((long)NpcLoot.CodeBit1, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={bob.CharId} AND slot={bobSlot}"), "Item in Bob's slot in the database");
        Check.Equal(1250L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={alice.CharId}"), "Alice's information in the database");
        Check.Equal(250L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={bob.CharId}"), "Bob's information in the database");
    }

    private static async Task TradeCancelAndRefusals(ScenarioContext context)
    {
        (HeadlessPlayer alice, HeadlessPlayer bob, ViewCreation bobView, ViewCreation aliceView) = await TwoNearby(context,
            new CharacterSeed("Alice") { Items = new[] { (3, NpcLoot.CodeBit1, 1) } },
            new CharacterSeed("Bob") { X = 17120 });

        // A view that isn't a player: INVALIDPARTNER
        alice.SendRpc($"81 82 {Le((ushort)0x7777)} 01 00  00 {Le(0u)} 03 00");
        await alice.WaitForRpcAsync("81 83 ?? ?? 05 00 00 18");

        // More information than there is: INVALIDITEMOFFERED
        alice.SendRpc($"81 82 {Le(bobView.ViewId)} {bobView.SpawnId:x2} 00  01 {Le(5000u)} 6b 00");
        await alice.WaitForRpcAsync("81 83 ?? ?? 03 00 00 18");

        // A slot with nothing in it: INVALIDITEMOFFERED
        alice.SendRpc($"81 82 {Le(bobView.ViewId)} {bobView.SpawnId:x2} 00  00 {Le(0u)} 09 00");
        await alice.WaitForRpcAsync("81 83 ?? ?? 03 00 00 18");
        Check.Equal(0, bob.Received("81 84").Count, "Offers Bob got from refused requests");

        // A real offer, then Bob declines (action 3): the window closes on both sides and nothing moves
        alice.SendRpc($"81 82 {Le(bobView.ViewId)} {bobView.SpawnId:x2} 00  00 {Le(0u)} 03 00");
        await alice.WaitForRpcAsync("81 83 ?? ?? 00 00 00 00");
        await bob.WaitForRpcAsync("81 84");

        // While that one is open, Bob can't start another: INPROGRESS
        bob.SendRpc($"81 82 {Le(aliceView.ViewId)} {aliceView.SpawnId:x2} 00  01 {Le(5u)} 6b 00");
        await bob.WaitForRpcAsync("81 83 ?? ?? 08 00 00 18");

        bob.SendRpc($"81 85 03 00 {Le(0u)} 6b 00");
        await alice.WaitForRpcAsync("81 87 1b 00 00 8e");
        await bob.WaitForRpcAsync("81 87 1b 00 00 8e");
        Check.Equal((long)NpcLoot.CodeBit1, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={alice.CharId} AND slot=3"), "Alice's item after the cancel");

        // Partner leaves mid-trade: ABORTED to the one left behind
        alice.SendRpc($"81 82 {Le(bobView.ViewId)} {bobView.SpawnId:x2} 00  00 {Le(0u)} 03 00");
        await bob.WaitForRpcAsync("81 84");
        bob.Dispose();
        await alice.WaitForRpcAsync("81 87 23 00 00 0e", TimeSpan.FromSeconds(60));
    }

    private static byte[] GroupInvite(byte groupType, string invitee, string name)
    {
        byte[] sizedInvitee = Sized(invitee);
        List<byte> record = new() { 0x6e, groupType, 0x06, 0x00 };
        record.AddRange(BitConverter.GetBytes((ushort)(6 + sizedInvitee.Length)));
        record.AddRange(sizedInvitee);
        record.AddRange(Sized(name));
        return record.ToArray();
    }

    private static byte[] InviteAnswer(byte groupType, uint result, string inviter)
    {
        List<byte> record = new() { 0x6f, groupType, 0x08, 0x00 };
        record.AddRange(BitConverter.GetBytes(result));
        record.AddRange(Sized(inviter));
        return record.ToArray();
    }

    /// <summary>
    /// The first story mission as far as talking goes: only it is offered, under its plot flag; accepting gets the briefing,
    /// the door waypoint and three open goals; talking closes them in any order and the third opens the fight.
    /// The fight and the exit are covered by hand in the real client.
    /// </summary>
    private static async Task StoryMissionTalks(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice") { X = 58800, Y = 2495, Z = 31900 }))[0];

        player.SendRpc("80 94 05 00 01");
        RpcRecord listed = await player.WaitForTextAsync("Welcome to the Matrix");
        Check.Equal((byte)1, listed.Bytes[14], "Plot flag on the listed mission");
        await player.WaitForRpcAsync("80 97 05 00");
        Check.Equal(1, player.Received("80 96").Count, "Missions offered");

        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForTextAsync("Your introduction assignment inside the Matrix.");

        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9c");
        await player.WaitForTextAsync("Welcome to the Matrix, Alice!");
        await player.WaitForRpcAsync("80 9e 00");
        await player.WaitForTextAsync("Talk to Abalakin");
        await player.WaitForTextAsync("Talk to Rigel");
        await player.WaitForTextAsync("Talk to Crow");

        ViewCreation crow = await player.WaitForViewNamedAsync("Crow");
        ViewCreation rigel = await player.WaitForViewNamedAsync("Rigel");

        // Crow first, out of the order the briefing suggests: the fight opens
        player.SkipReceived();
        player.SendRpc($"80 c7 {Le(crow.ViewId)} {crow.SpawnId:x2} 00 09 00");
        await player.WaitForTextAsync("Kill the simulacrum");
        await player.WaitForTextAsync("Why don't we play along with Crow?");

        player.SkipReceived();
        player.SendRpc($"80 c7 {Le(rigel.ViewId)} {rigel.SpawnId:x2} 00 09 00");
        await player.WaitForTextAsync("We don't want hostile Agents tracking you.");

        // Aborting takes the NPCs away again
        player.SendRpc("80 a6");
        await player.WaitForRpcAsync("80 a2 08");
    }

    private static async Task FloodStoryMission(ScenarioContext context)
    {
        HeadlessPlayer player = await context.AddPlayerAsync(new CharacterSeed("Alice") { X = 58800, Y = 2495, Z = 31900 });

        // Margin character data 02 for an existing character. The intro state follows the seven reputation values.
        Check.Equal((byte)0x07, player.CharacterData[0x02][1155], "Contacts visible on login");
        await player.EnterWorldAsync();

        player.SendRpc("80 94 05 00 03");
        RpcRecord header = await player.WaitForRpcAsync("80 95");
        Check.Equal("8095" + "00000000" + "0500" + "0100" + "00" + "03" + "d2070000" + "380000b4" + "e60d0028",
            WireFormat.ToHex(header.Bytes), "List header for Flood's first mission");
        await player.WaitForTextAsync("The End of the Beginning");
        await player.WaitForRpcAsync("80 97 05 00");

        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForTextAsync("Eliminate Machine opposition and steal vital info.");
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9c");
        await player.WaitForTextAsync("I am glad to see that you have decided to take up employment");
        await player.WaitForRpcAsync("80 9e 00");
    }

    /// <summary>
    /// A neighbourhood contact stands at their static as retail spawned them; using them (80 c7 ... 02 00, as retail's
    /// client sent) gets their list under request number 0 with preview 1 and org 0; one of their generic missions accepts
    /// with org 0 and its target goal.
    /// </summary>
    private static async Task NeighbourhoodContactMissions(ScenarioContext context)
    {
        // Two metres in front of The Chessman's static in Tabor Park (76015, 537, 15645)
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice") { X = 76015, Y = 487, Z = 15845 }))[0];

        ViewCreation chessman = null!;
        await player.WaitUntilAsync(() =>
        {
            chessman = player.ViewsOfGoid(6520).FirstOrDefault(v => v.Attributes.TryGetValue("ContactName", out byte[]? name) &&
                                                                     WireFormat.ZeroTerminated(name) == "The Chessman")!;
            return chessman != null;
        }, "The Chessman's view", TimeSpan.FromSeconds(10));
        Check.Equal("07000000", WireFormat.ToHex(chessman.Attributes["ContactID"]), "Contact id on the view");

        player.SendRpc($"80 c7 {Le(chessman.ViewId)} {chessman.SpawnId:x2} 00 02 00");
        RpcRecord header = await player.WaitForRpcAsync("80 95");
        Check.Equal("8095" + "00000000" + "0000" + "0300" + "01" + "00" + "07000000", WireFormat.ToHex(header.Bytes).Substring(0, 32),
            "List header: request 0, three missions, preview 1, org 0, The Chessman");
        await player.WaitForRpcAsync("80 97 00 00");
        Check.Equal(3, player.Received("80 96").Count, "Missions offered");

        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForTextAsync("Kill one of The Chessman's rivals.");

        player.SendRpc("80 9b 07 00 01");
        RpcRecord accepted = await player.WaitForRpcAsync("80 9c");
        Check.Equal("0000", WireFormat.ToHex(accepted.Bytes).Substring(20, 4), "Org on the accept");
        await player.WaitForRpcAsync("80 9e 00"); // the generated site's elevator door
        await player.WaitForTextAsync("Kill Vesper");

        player.SendRpc("80 a6");
        await player.WaitForRpcAsync("80 a2 08");
    }

    private static async Task MovedMissionItem(ScenarioContext context)
    {
        HeadlessPlayer player = await context.AddPlayerAsync(new CharacterSeed("Alice") { X = 58800, Y = 2495, Z = 31900 });
        await context.Database.ExecuteAsync("CREATE TABLE IF NOT EXISTS char_missions " +
            "(charId INT UNSIGNED NOT NULL, missionId INT NOT NULL, completions INT NOT NULL DEFAULT 1, PRIMARY KEY (charId, missionId)); " +
            $"INSERT INTO char_missions (charId, missionId) VALUES ({player.CharId}, 2201)");
        await player.EnterWorldAsync();
        player.SendRpc("80 94 05 00 02");
        await player.WaitForTextAsync("Watchful Eyes");
        await player.WaitForRpcAsync("80 97 05 00");
        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForRpcAsync("80 99");
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(context, player);
        ViewCreation herschel = await VisitMissionNpc(context, player, "Herschel");
        player.SendRpc($"80 c7 {Le(herschel.ViewId)} {Le((ushort)herschel.SpawnId)} 0d 00");
        await player.WaitForRpcAsync("5e 01 00 a9 1a 00 00");
        player.SendRpc("65 01 00 05 00");
        await player.WaitForRpcAsync("65 01 00 05 00 00");
        await context.Database.ExecuteAsync($"INSERT INTO inventory (charId, goid, slot, count) VALUES ({player.CharId}, 7332, 1, 1)");
        player.SendRpc("80 a6");
        await player.WaitForRpcAsync("5f 05 00 01");
        await player.WaitForRpcAsync("80 a2 08");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6825"),
            "A moved quest item is removed on abort");
        Check.Equal(7332L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={player.CharId} AND slot=1"),
            "The replacement item stays in the old slot");
    }

    private static async Task LegacyInventoryStartup(ScenarioContext context)
    {
        await context.Database.ExecuteAsync("SET SESSION sql_mode=''; ALTER TABLE inventory ENGINE=MyISAM, DROP INDEX inventory_character_slot");
        const string zeroDates = "SELECT COUNT(*) FROM inventory WHERE CAST(updated AS CHAR) LIKE '0000-%' OR CAST(created AS CHAR) LIKE '0000-%'";
        long originalZeroDates = await context.Database.LongAsync(zeroDates);
        using ServerInstance restarted = await ServerInstance.StartAsync(context.RepositoryRoot, context.Database.Name, StillNpcs);
        Check.Equal("InnoDB", await context.Database.ScalarAsync("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventory'"),
            "Inventory is transactional before the restarted server reports ready");
        Check.Equal(originalZeroDates, await context.Database.LongAsync(zeroDates), "Legacy zero timestamps survive startup migration");
        Check.Equal(2L, await context.Database.LongAsync("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventory' AND INDEX_NAME='inventory_character_slot'"),
            "Legacy inventory receives the character/slot index");
    }

    private static async Task AzunaCollector(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")
        {
            District = "dt", DistrictId = 2, X = -146195, Y = -54, Z = 50682,
            Items = new (int, uint, int)[] { (1, 48150, 4), (2, 48150, 5), (97, 48150, 1) }
        }))[0];

        player.SendRpc("80 c8 3d 03 90 28 01 00");
        RpcRecord location = await player.WaitForRpcAsync("81 15");
        RpcRecord dialog = await player.WaitForRpcAsync("81 13");
        Check.Equal(51, location.Bytes.Length, "Collector position record");
        Check.Equal(-146195d, BitConverter.ToDouble(location.Bytes, 18), "Collector window uses Azuna's observed client X");
        Check.Equal(50682d, BitConverter.ToDouble(location.Bytes, 34), "Collector window uses Azuna's observed client Z");
        Check.Equal("c3060004c3060004c3060004c0060004", WireFormat.ToHex(dialog.Bytes[18..]), "Azuna's dialogue IDs");
        Check.Equal(WireFormat.ToHex(location.Bytes[2..18]), WireFormat.ToHex(dialog.Bytes[2..18]), "Collector session");
        for (int item = 0; item < 10; item++)
        {
            RpcRecord row = await player.WaitForRpcAsync("81 14");
            Check.Equal(48150u, BitConverter.ToUInt32(row.Bytes, 19), "Required Function Data");
            Check.Equal(item == 0 ? 48149u : 0u, BitConverter.ToUInt32(row.Bytes, 27), "One Surveillance prize");
        }
        RpcRecord final = await player.WaitForRpcAsync("81 14");
        Check.Equal((byte)1, final.Bytes[35], "Last collector record");

        player.SendRpc("81 16 00 00 00");
        await player.WaitForRpcAsync("81 17 00 00 00 00 00");
        Check.Equal(9L, await context.Database.LongAsync($"SELECT SUM(count) FROM inventory WHERE charId={player.CharId} AND goid=48150 AND slot<97"),
            "A refused trade leaves the bag alone");
        await context.Database.ExecuteAsync($"INSERT INTO inventory (charId, goid, slot, count) VALUES ({player.CharId}, 48150, 3, 2)");

        // A failed reward insert must roll back the already-executed item consumption.
        await context.Database.ExecuteAsync("SET SESSION sql_mode=''; ALTER TABLE inventory ADD CONSTRAINT refuse_collector_reward CHECK (goid <> 48149)");
        player.SendRpc("80 c8 3d 03 90 28 01 00");
        await player.WaitForRpcAsync("81 13");
        player.SendRpc("81 16 00 00 00");
        await player.WaitForRpcAsync("81 17 00 00 00 00 00");
        Check.Equal(11L, await context.Database.LongAsync($"SELECT SUM(count) FROM inventory WHERE charId={player.CharId} AND goid=48150 AND slot<97"),
            "Reward failure rolls consumption back");
        await context.Database.ExecuteAsync("SET SESSION sql_mode=''; ALTER TABLE inventory DROP CHECK refuse_collector_reward");

        player.SendRpc("80 c8 3d 03 90 28 01 00");
        await player.WaitForRpcAsync("81 13");
        player.SendRpc("81 16 00 00 00");
        await player.WaitForRpcAsync("5f 01 00 01");
        await player.WaitForRpcAsync("5f 02 00 01");
        await player.WaitForRpcAsync("69 01 03 00 00 00 00 00 00 00");
        await player.WaitForRpcAsync("5e 01 00 15 bc 00 00");
        await player.WaitForRpcAsync("81 17 00 00 00 00 01");
        player.SendRpc("81 16 00 00 00");

        Check.Equal(1L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={player.CharId} AND slot=3"), "Remainder of the third stack");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=48149"), "Exactly one reward");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={player.CharId} AND slot=97"), "Worn item untouched");
    }

    private static async Task AzunaCollectorFullBag(ScenarioContext context)
    {
        var items = Enumerable.Range(1, 96).Select(slot => (slot, slot == 1 ? 48150u : NpcLoot.CodeBit1, slot == 1 ? 11 : 1)).ToArray();
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")
        {
            District = "dt", DistrictId = 2, X = -146195, Y = -54, Z = 50682, Items = items
        }))[0];
        player.SendRpc("80 c8 3d 03 90 28 01 00");
        await player.WaitForRpcAsync("81 13");
        player.SendRpc("81 16 00 00 00");
        await player.WaitForRpcAsync("81 17 00 00 00 00 00");
        Check.Equal(11L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={player.CharId} AND slot=1"),
            "A full bag refuses the exchange without consuming items");

        await context.Database.ExecuteAsync($"UPDATE inventory SET count=10 WHERE charId={player.CharId} AND slot=1");
        player.SendRpc("80 c8 3d 03 90 28 01 00");
        await player.WaitForRpcAsync("81 13");
        player.SendRpc("81 16 00 00 00");
        await player.WaitForRpcAsync("81 17 00 00 00 00 01");
        Check.Equal(48149L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={player.CharId} AND slot=1"),
            "A full bag can reuse the completely consumed stack's slot");
        Check.Equal(96L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId}"),
            "Every other item is preserved");
    }

    private static async Task AzunaCollectorGuards(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")
        {
            District = "dt", DistrictId = 2, X = -146195, Y = -54, Z = 50682,
            Items = new[] { (1, 48150u, 10) }
        }))[0];

        // A request with no active dialog and malformed requests cannot consume items.
        player.World.SendRpcs(WireFormat.FromHex("81 16 00 00 00"),
            WireFormat.FromHex("80 c8 3d 03 90 28 01 00"));
        await player.WaitForRpcAsync("81 13");
        foreach (string payload in new[] { "", "00", "01 00 00", "00 01 00 00", "00 00 00 00" })
        {
            player.SendRpc("81 16 " + payload);
        }
        // A native close invalidates the owned selection; a replay cannot spend its tokens.
        player.SendRpc("81 17 00 00 00 00 00");
        player.SendRpc("81 16 00 00 00");
        player.SendRpc("81 48"); // dispatch barrier; no active collector sends no exchange reply
        await player.WaitForRpcAsync("81 49");
        Check.Equal(10L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={player.CharId} AND slot=1"), "Closed collector replay preserves tokens");
        player.SendRpc("80 c8 3d 03 90 28 01 00");
        await player.WaitForRpcAsync("81 13");

        player.SendChat("?tp -145195 -55 50682 0 0 0");
        await player.WaitForTextAsync("Teleported to 0/0");
        Check.Equal(10L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={player.CharId} AND slot=1"),
            "Malformed requests leave the data intact");

        // Walking away after opening the collector invalidates an otherwise valid trade.
        player.SendRpc("81 16 00 00 00");
        await player.WaitForRpcAsync("81 17 00 00 00 00 00");
        Check.Equal(10L, await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={player.CharId} AND slot=1"),
            "The trade checks the current position");
        player.SendRpc("80 c8 3d 03 90 28 01 00");
        await player.WaitForTextAsync("Move closer to Azuna to trade.");
        player.SendChat("?tp -146195 -55 50682 0 0 0");
        await player.WaitForTextAsync("Teleported to 0/0");
        player.SendRpc("80 c8 3d 03 90 28 01 00");
        await player.WaitForRpcAsync("81 13");
        player.SendRpc("81 16 00 00 00");
        await player.WaitForRpcAsync("81 17 00 00 00 00 01");
        Check.Equal(48149L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={player.CharId} AND slot=1"),
            "A fresh dialog can exchange normally after a refused trade");
    }

    private static async Task AzunaFunctionDataLoot(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")
        {
            District = "dt", DistrictId = 2, X = -146900, Y = 295, Z = 50200, Level = 50
        }))[0];
        ViewCreation function = await player.WaitForViewNamedAsync("Override Function", TimeSpan.FromSeconds(15));
        player.SendChat("?weaken 1");
        player.SendRpc("3d 01");
        player.SendRpc($"41 {Le(function.ViewId)} {Le((ushort)function.SpawnId)}");
        RpcRecord loot = await player.WaitForRpcAsync("81 19", TimeSpan.FromSeconds(30));
        Check.That(BytePattern.IndexOf(loot.Bytes, WireFormat.FromHex("16 bc 00 00"), 0) >= 0, "Function Data in the loot window");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("5e");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("[LOOT] Item 48150 into slot"), "Function Data granted", TimeSpan.FromSeconds(10));
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=48150"),
            "Function Data is obtained through combat and Take All");
    }

    private const byte Team = 3, Crew = 2;
    private const byte ChatCrew = 0x12, ChatTeam = 0x15;

    private static async Task TeamInviteAndChat(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });
        HeadlessPlayer alice = players[0], bob = players[1];

        // Team chat before there is a team is refused with a notice
        alice.SendChat("anyone?", ChatTeam);
        await alice.WaitForTextAsync("not on a mission team");

        // Decline first: the inviter hears it, and no team exists for Bob
        alice.SendRpc(GroupInvite(Team, "Bob", ""));
        await bob.WaitForRpcAsync("80 8f 0c 00");
        await alice.WaitForRpcAsync("80 8d");
        bob.SendRpc(InviteAnswer(Team, 0x0d00002e, "Alice"));
        await alice.WaitForRpcAsync($"6f 03 08 00 {Le(0x0d00002eu)}");

        // Accept: Bob gets the team, Alice gets him as a member
        alice.SendRpc(GroupInvite(Team, "Bob", ""));
        await bob.WaitForRpcAsync("80 8f 0c 00");
        bob.SendRpc(InviteAnswer(Team, 0, "Alice"));
        await bob.WaitForRpcAsync("80 8e");
        await alice.WaitForRpcAsync($"70 03 {Le(bob.CharId)} 09 00");

        // Team chat reaches the other member with the sender's id and is not echoed
        alice.SendChat("team hello", ChatTeam);
        RpcRecord heard = await bob.WaitForTextAsync("team hello");
        Check.Equal((byte)0x2e, heard.Bytes[0], "Chat record");
        Check.Equal(ChatTeam, heard.Bytes[1], "Chat channel");
        Check.Equal(alice.CharId, BitConverter.ToUInt32(heard.Bytes, 3), "Chat sender id");
        bob.SendChat("team reply", ChatTeam);
        await alice.WaitForTextAsync("team reply");
        await Task.Delay(200);
        Check.Equal(0, alice.World.RpcSnapshot().Count(r => Contains(r.Bytes, "team hello")), "Echoes of Alice's own team chat");

        // Bob leaves: both hear it, and chat stops working for him
        bob.SendRpc("71 03");
        await alice.WaitForRpcAsync($"72 03 {Le(bob.CharId)}");
        await bob.WaitForRpcAsync($"72 03 {Le(bob.CharId)}");
        bob.SendChat("still there?", ChatTeam);
        await bob.WaitForTextAsync("not on a mission team");
    }

    private static async Task CrewInviteAndChat(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });
        HeadlessPlayer alice = players[0], bob = players[1];

        alice.SendChat("crew?", ChatCrew);
        await alice.WaitForTextAsync("not in a crew");

        alice.SendRpc(GroupInvite(Crew, "Bob", "Headless"));
        RpcRecord invite = await bob.WaitForRpcAsync("80 88 07 00");
        Check.That(Contains(invite.Bytes, "Alice") && Contains(invite.Bytes, "Headless"), "Crew invite names the inviter and the crew: " + invite.Hex);

        bob.SendRpc(InviteAnswer(Crew, 0, "Alice"));
        await bob.WaitForRpcAsync("80 86");
        await alice.WaitForRpcAsync("80 86");

        long crewId = await context.Database.LongAsync("SELECT id FROM crews WHERE crew_name='Headless'");
        Check.Equal(crewId, await context.Database.LongAsync($"SELECT crewId FROM characters WHERE charId={bob.CharId}"), "Bob's crew in the database");
        Check.Equal(crewId, await context.Database.LongAsync($"SELECT crewId FROM characters WHERE charId={alice.CharId}"), "Alice's crew in the database");
        Check.Equal(2L, await context.Database.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE crew_id={crewId}"), "Crew members in the database");

        alice.SendChat("crew hello", ChatCrew);
        RpcRecord heard = await bob.WaitForTextAsync("crew hello");
        Check.Equal(ChatCrew, heard.Bytes[1], "Chat channel");
        bob.SendChat("crew reply", ChatCrew);
        await alice.WaitForTextAsync("crew reply");

        // Area chat and a whisper, while we're here
        alice.SendChat("area hello", ChatTypes.Area);
        RpcRecord area = await bob.WaitForTextAsync("area hello");
        Check.Equal(ChatTypes.Area, area.Bytes[1], "Chat channel");

        byte[] handle = Sized("Bob");
        List<byte> whisper = new() { 0x29, 0x07, 0x00 };
        whisper.AddRange(BitConverter.GetBytes((ushort)(7 + handle.Length)));
        whisper.Add(0x00);
        whisper.Add(0x01);
        whisper.AddRange(handle);
        whisper.AddRange(Sized("psst"));
        alice.SendRpc(whisper.ToArray());
        RpcRecord whispered = await bob.WaitForTextAsync("psst");
        Check.Equal((byte)0x11, whispered.Bytes[1], "Whisper channel");

        // Bob leaves: Alice hears it, and the database agrees
        bob.SendRpc("71 02");
        await alice.WaitForRpcAsync($"72 02 {Le(bob.CharId)}");
        await WaitForDatabaseAsync(context, $"SELECT crewId FROM characters WHERE charId={bob.CharId}", "0", "Bob's crew after leaving");
    }

    private static bool Contains(byte[] data, string text) => BytePattern.IndexOf(data, Encoding.ASCII.GetBytes(text), 0) >= 0;

    private static async Task WaitForDatabaseAsync(ScenarioContext context, string sql, string expected, string what)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        string actual;
        do
        {
            actual = await context.Database.ScalarAsync(sql);
            if (actual == expected)
            {
                return;
            }
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);
        throw new ScenarioFailure($"{what}: expected {expected}, got {actual}");
    }

    private const string MarketCategory = "49 54 4f 74"; // "ITOt", as in the retail listing

    private static string ListItem(uint price, byte slot) => $"81 2b {MarketCategory} {Le(price)} 01 {Le((ushort)slot)} 00 00 00 00";

    private static async Task MarketplacePurchase(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("Seller") { Items = new[] { (5, NpcLoot.CodeBit1, 1) } },
            new CharacterSeed("Buyer") { X = 17120 },
            new CharacterSeed("Poor") { X = 17220, Info = 10 });
        HeadlessPlayer seller = players[0], buyer = players[1], poor = players[2];

        seller.SendRpc(ListItem(300, 5));
        await seller.WaitForRpcAsync("5f 05 00 01");
        RpcRecord listed = await seller.WaitForRpcAsync("81 2c");
        uint listing = BitConverter.ToUInt32(listed.Bytes, 2);
        Check.Equal(NpcLoot.CodeBit1, BitConverter.ToUInt32(listed.Bytes, 6), "Listed goid");
        Check.Equal(300u, BitConverter.ToUInt32(listed.Bytes, 14), "Listed price");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={seller.CharId} AND slot=5"), "Seller's slot once listed");

        // It shows up in a search
        buyer.SendRpc($"81 24 {MarketCategory} {Le(0u)} 00 00 00 00 19 00 {Le(999999999u)} {Le(0u)} 01 00 00");
        RpcRecord search = await buyer.WaitForRpcAsync("81 25 09 00");
        Check.That(BytePattern.Parse(Le(listing)).Occurs(search.Bytes), "Search reply holds the listing: " + search.Hex);

        // Too poor: nothing changes
        poor.SendRpc($"81 26 {Le(listing)} 07");
        await poor.WaitForTextAsync("do not have enough information");

        buyer.SendRpc($"81 26 {Le(listing)} 07");
        await buyer.WaitForRpcAsync($"5e 07 00 {Le(NpcLoot.CodeBit1)}");
        await buyer.WaitForRpcAsync($"80 e4 {Le(700u)}");
        await buyer.WaitForRpcAsync($"81 27 {Le(listing)} 07 {Le(NpcLoot.CodeBit1)}");

        // Seller is paid, live and in the database
        await seller.WaitForRpcAsync($"80 e4 {Le(1300u)}");
        await seller.WaitForTextAsync("sold for 300");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT is_sold FROM marketplace WHERE id={listing}"), "Listing sold");
        Check.Equal((long)NpcLoot.CodeBit1, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={buyer.CharId} AND slot=7"), "Buyer's item");
        Check.Equal(700L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={buyer.CharId}"), "Buyer's information");
        Check.Equal(1300L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={seller.CharId}"), "Seller's information");
        Check.Equal(10L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={poor.CharId}"), "Poor buyer's information");

        // Too late
        poor.SendRpc($"81 26 {Le(listing)} 07");
        await poor.WaitForTextAsync("already been sold");
    }

    /// <summary>
    /// The real marketplace window is used from the Loading Area, and a seller there isn't "on world". They should still
    /// be paid live: their in-memory information is what the next save writes, so a credit only in the database is lost.
    /// Gets there the way the client does (exit_to_loadingarea capture): 80 fc 01, 80 fd, 80 fe 01, 81 07, 81 08, reset,
    /// then the world load and 05.
    /// </summary>
    private static async Task MarketplaceSellerInLoadingArea(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("Seller") { Items = new[] { (5, NpcLoot.CodeBit1, 1) } },
            new CharacterSeed("Buyer") { X = 17120 });
        HeadlessPlayer seller = players[0], buyer = players[1];

        seller.SendRpc("80 fc 01");
        await seller.WaitForRpcAsync("80 fd");
        seller.SendRpc("80 fe 01");
        await seller.WaitForRpcAsync("81 07");
        seller.SendRpc("81 08");
        await seller.WaitForRpcAsync("81 09");
        seller.SendRpc("05");
        await Task.Delay(500);

        seller.SendRpc(ListItem(300, 5));
        RpcRecord listed = await seller.WaitForRpcAsync("81 2c");
        uint listing = BitConverter.ToUInt32(listed.Bytes, 2);

        buyer.SendRpc($"81 26 {Le(listing)} 07");
        await buyer.WaitForRpcAsync($"81 27 {Le(listing)}");
        await seller.WaitForRpcAsync($"80 e4 {Le(1300u)}");
        Check.Equal(1300L, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={seller.CharId}"), "Seller's information");
    }

    /// <summary>
    /// The same NPC in each player's views. Nothing in a creation record names the entity, and the Release data has
    /// identical NPCs on one spot, so same-name NPCs are paired by the order they spawned, which is the server's list order.
    /// </summary>
    private static async Task<(ViewCreation SeenByA, ViewCreation SeenByB)> SharedNpc(HeadlessPlayer a, HeadlessPlayer b)
    {
        ViewCreation? seenByA = null, seenByB = null;
        await a.WaitUntilAsync(() =>
        {
            List<ViewCreation> viewsA = a.ViewsOfGoid(MobGoid), viewsB = b.ViewsOfGoid(MobGoid);
            seenByA = viewsA.FirstOrDefault();
            if (seenByA == null)
            {
                return false;
            }
            string? name = seenByA.Name;
            seenByB = viewsB.FirstOrDefault(v => v.Name == name);
            return seenByB != null;
        }, "an NPC both players can see", TimeSpan.FromSeconds(20));
        return (seenByA!, seenByB!);
    }

    /// <summary>Rows in the NPC data the server loads (the test list: see MXO_MOBS above) with this name, within view of NpcX, NpcZ.</summary>
    private static int NpcRowsNear(ScenarioContext context, string name)
    {
        string[] files = { "mob_just_some_and_winter.csv" };
        const double ViewRange = 5000;
        return files
            .SelectMany(file => File.ReadLines(Path.Combine(context.RepositoryRoot, "data", file)).Skip(1))
            .Select(line => line.Split(';'))
            .Count(row => row.Length > 9 && row[0] == "1" && row[2] == name &&
                          Math.Abs(double.Parse(row[7], System.Globalization.CultureInfo.InvariantCulture) - NpcX) <= ViewRange &&
                          Math.Abs(double.Parse(row[9], System.Globalization.CultureInfo.InvariantCulture) - NpcZ) <= ViewRange);
    }

    private const ushort MobGoid = 599;

    private static CharacterSeed NearNpc(string handle, double offset = 0) => new(handle)
    {
        X = NpcX + 200 + offset, Y = NpcY, Z = NpcZ, Level = 50,
    };

    /// <summary>The values after this update in a view list, in the order they arrived: `&lt;view&gt; &lt;bytes&gt; &lt;u16&gt;`.</summary>
    private static List<ushort> ViewUpdateValues(HeadlessPlayer player, ushort viewId, string bytes)
    {
        byte[] needle = WireFormat.FromHex($"{viewId & 0xff:x2}{viewId >> 8:x2}{bytes}");
        List<ushort> values = new();
        foreach (byte[] section in player.World.ViewSnapshot())
        {
            for (int at = BytePattern.IndexOf(section, needle, 0); at >= 0; at = BytePattern.IndexOf(section, needle, at + 1))
            {
                if (at + needle.Length + 2 <= section.Length)
                {
                    values.Add(BitConverter.ToUInt16(section, at + needle.Length));
                }
            }
        }
        return values;
    }

    private const string NpcHealthUpdate = "04 80 80 80 40";
    private const string NpcDeathUpdate = "07 01 1d 0d 00";

    private static int CountViewUpdate(HeadlessPlayer player, ushort viewId, string bytes)
    {
        byte[] needle = WireFormat.FromHex($"{viewId & 0xff:x2}{viewId >> 8:x2}{bytes}");
        int count = 0;
        foreach (byte[] section in player.World.ViewSnapshot())
        {
            for (int at = BytePattern.IndexOf(section, needle, 0); at >= 0; at = BytePattern.IndexOf(section, needle, at + 1))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// One melee and one gun on the same NPC: both see its health fall in the same steps and see it die once, and
    /// neither goes on fighting the corpse.
    /// </summary>
    private static async Task TwoPlayersFightOneNpc(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(NearNpc("Melee"), NearNpc("Gunner", 100));
        HeadlessPlayer melee = players[0], gunner = players[1];
        (ViewCreation mobForMelee, ViewCreation mobForGunner) = await SharedNpc(melee, gunner);

        // Each NPC exists once per row of its data file. Two data loaders racing at startup used to double them all.
        await Task.Delay(1500); // a visibility pass or two, so all of them have spawned
        Check.Equal(NpcRowsNear(context, mobForMelee.Name!), melee.ViewsOfGoid(MobGoid).Count(v => v.Name == mobForMelee.Name), "Views of " + mobForMelee.Name);

        melee.SendRpc("3d 01");
        melee.SendRpc($"40 {Le(mobForMelee.ViewId)} {Le((ushort)mobForMelee.SpawnId)} 00 00 00 00 00 00 00");
        gunner.SendRpc("3d 01");
        gunner.SendRpc($"41 {Le(mobForGunner.ViewId)} {Le((ushort)mobForGunner.SpawnId)}");

        // The fight takes rounds of a few seconds: expect both to see the health go down
        await melee.WaitUntilAsync(() => ViewUpdateValues(melee, mobForMelee.ViewId, NpcHealthUpdate).Count >= 2, "NPC health updates", TimeSpan.FromSeconds(30));
        await gunner.WaitUntilAsync(() => ViewUpdateValues(gunner, mobForGunner.ViewId, NpcHealthUpdate).Count >= 2, "NPC health updates", TimeSpan.FromSeconds(30));

        // It dies, for both
        TimeSpan toDie = TimeSpan.FromSeconds(180);
        await melee.WaitForViewUpdateAsync(mobForMelee.ViewId, NpcDeathUpdate, toDie);
        await gunner.WaitForViewUpdateAsync(mobForGunner.ViewId, NpcDeathUpdate, toDie);

        List<ushort> meleeHealth = ViewUpdateValues(melee, mobForMelee.ViewId, NpcHealthUpdate);
        List<ushort> gunnerHealth = ViewUpdateValues(gunner, mobForGunner.ViewId, NpcHealthUpdate);
        Check.That(meleeHealth.SequenceEqual(meleeHealth.OrderByDescending(h => h)), "Health only falls: " + string.Join(",", meleeHealth));
        Check.That(meleeHealth.Take(Math.Min(meleeHealth.Count, gunnerHealth.Count)).SequenceEqual(gunnerHealth.Take(Math.Min(meleeHealth.Count, gunnerHealth.Count))),
            $"Both saw the same health: {string.Join(",", meleeHealth)} vs {string.Join(",", gunnerHealth)}");

        // Whoever is still in a round when it dies finds out: no second death or reward over the next rounds
        long meleeExp = await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={melee.CharId}");
        long gunnerExp = await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={gunner.CharId}");
        await Task.Delay(TimeSpan.FromSeconds(10));
        Check.Equal(1, CountViewUpdate(melee, mobForMelee.ViewId, NpcDeathUpdate), "NPC deaths seen by the melee player");
        Check.Equal(1, CountViewUpdate(gunner, mobForGunner.ViewId, NpcDeathUpdate), "NPC deaths seen by the gunner");
        Check.Equal(meleeExp, await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={melee.CharId}"), "Melee player's experience after the NPC died");
        Check.Equal(gunnerExp, await context.Database.LongAsync($"SELECT exp FROM characters WHERE charId={gunner.CharId}"), "Gunner's experience after the NPC died");
        Check.That(meleeExp + gunnerExp > 0, "Someone got experience for the kill");
    }

    /// <summary>What the other player's client gets when a player levels up, equips, and changes clothes.</summary>
    private static async Task OtherViewLevelUpAndEquipment(ScenarioContext context)
    {
        (HeadlessPlayer actor, HeadlessPlayer watcher, _, ViewCreation actorView) = await TwoNearby(context,
            new CharacterSeed("Actor") { Level = 1 },
            new CharacterSeed("Watcher") { X = 17120 });

        // Level 2: other-view attributes MaxHealth, Level, Health, then the effect
        actor.SendChat("?addexp 500");
        await watcher.WaitForViewUpdateAsync(actorView.ViewId, "02 80 86 ?? ?? 02 90");

        // Weapon in hand: EquippedItemID, and back out again
        const uint Revolver = 2565;
        actor.SendChat("?additem " + Revolver);
        RpcRecord gun = await actor.WaitForRpcAsync(r => r.Opcode == 0x5e && BitConverter.ToUInt32(r.Bytes, 3) == Revolver, "the gun in the bag");
        byte gunSlot = gun.Bytes[1];
        actor.SendRpc($"63 {gunSlot:x2} 69");
        await watcher.WaitForViewUpdateAsync(actorView.ViewId, $"02 80 80 80 02 {Le(Revolver)}");
        actor.SendRpc($"64 69 {gunSlot:x2}");
        await watcher.WaitForViewUpdateAsync(actorView.ViewId, $"02 80 80 80 02 {Le(0u)}");

        // Clothes: RSIDescription
        const uint Shirt = 48095;
        actor.SendChat("?additem " + Shirt);
        RpcRecord shirt = await actor.WaitForRpcAsync(r => r.Opcode == 0x5e && BitConverter.ToUInt32(r.Bytes, 3) == Shirt, "the shirt in the bag");
        int before = CountViewUpdate(watcher, actorView.ViewId, "02 08");
        actor.SendRpc($"63 {shirt.Bytes[1]:x2} 63");
        await watcher.WaitUntilAsync(() => CountViewUpdate(watcher, actorView.ViewId, "02 08") > before, "the new clothes", HeadlessPlayer.DefaultTimeout);
    }

    /// <summary>
    /// A bystander sees another player's shots. Retail form, for a player's view (RajkoPacketsNearTheEnd
    /// PacketsMaraCThenTimeDOut): `7a 00 03 2a 03 01 0c 37 00 10 67 00 5e 00 00 &lt;x y z f32&gt;`, with the target as the
    /// bystander's own view and spawn id.
    /// </summary>
    private static async Task OtherViewGunfire(ScenarioContext context)
    {
        (HeadlessPlayer shooter, HeadlessPlayer watcher, _, ViewCreation shooterView) = await TwoNearby(context, NearNpc("Shooter"), NearNpc("Watcher", 100));
        (ViewCreation mobForShooter, ViewCreation mobForWatcher) = await SharedNpc(shooter, watcher);

        shooter.SendRpc("3d 01");
        shooter.SendRpc($"41 {Le(mobForShooter.ViewId)} {Le((ushort)mobForShooter.SpawnId)}");

        string shot = $"03 2a ?? 01 0c ?? ?? ?? {Le(mobForWatcher.ViewId)} {Le((ushort)mobForWatcher.SpawnId)} 00";
        await watcher.WaitForViewUpdateAsync(shooterView.ViewId, shot, TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// The client resends an RPC list whose ack is late, with the same counter and bytes. A chat line must be heard once
    /// and experience granted once, however many copies arrive.
    /// </summary>
    private static async Task ResentRpcHandledOnce(ScenarioContext context)
    {
        (HeadlessPlayer alice, HeadlessPlayer bob, _, _) = await TwoNearby(context, new CharacterSeed("Alice") { Level = 1 }, new CharacterSeed("Bob") { X = 17120 });

        alice.SendChat("just the once");
        alice.World.ResendLastRpcs();
        alice.World.ResendLastRpcs();
        await bob.WaitForTextAsync("just the once");

        alice.SendChat("?addexp 7");
        alice.World.ResendLastRpcs();
        await WaitForDatabaseAsync(context, $"SELECT exp FROM characters WHERE charId={alice.CharId}", "7", "Alice's experience");

        // And the next list still gets through
        alice.SendChat("and another");
        await bob.WaitForTextAsync("and another");

        await Task.Delay(500);
        Check.Equal(1, bob.World.RpcSnapshot().Count(r => Contains(r.Bytes, "just the once")), "Copies of the chat line");
        Check.Equal("7", await context.Database.ScalarAsync($"SELECT exp FROM characters WHERE charId={alice.CharId}"), "Alice's experience once it settled");
    }
}
