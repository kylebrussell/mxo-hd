# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\rshooks.py
# Compiled at: 2008-11-05 02:01:38
import random, traceback, ability.utility, obj, RewardSelection, stringtable_client as ST, ltfxmap as FX
from crafting_server import *
DEFAULT_VENDOR_PRICE = 1000

def ComputeDownloadMissionMapOutcome(subject, missionDifficulty):
    print 'ComputeDownloadMissionMapOutcome()'
    abilityLevel = 0
    if subject.hasAbility[DownloadMissionMapAbility]:
        abilityLevel = subject.abilities[DownloadMissionMapAbility] * 4
    if subject.hasAbility[MapDownloadingAbility]:
        abilityLevel += subject.abilities[MapDownloadingAbility]
    print 'ability = ', abilityLevel, 'missionDifficulty = ', missionDifficulty
    if not abilityLevel:
        return 0
    roll = random.randrange(100)
    mapAccess = abilityLevel + roll
    missionDifficulty = missionDifficulty + 50
    outcome = mapAccess - missionDifficulty
    print 'roll = ', roll, 'outcome = ', outcome
    if outcome < 0:
        print 'download mission map failed'
        discovery.abilityMessageToCaster(ST.ID_ABILITY_DOWNLOAD_MISSION_MAP_FAILED, DownloadMissionMapAbility, subject.locator, 0, 0)
        return 0
    accessFlags = 1
    if outcome >= 10:
        accessFlags += 2
    if outcome >= 20:
        accessFlags += 4
    if outcome >= 30:
        accessFlags += 8
    if outcome >= 40:
        accessFlags += 16
    if outcome >= 50:
        accessFlags += 32
    print 'download mission map success'
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_GENERIC_ABILITY_UPGRADE, 0)
    discovery.abilityMessageToCaster(ST.ID_ABILITY_DOWNLOAD_MISSION_MAP_SUCCESS, DownloadMissionMapAbility, subject.locator, 0, 0)
    return accessFlags
    return


def ComputeHackConstructOutcome(subject):
    print 'RS - ComputeHackConstructOutcome()'
    abilityLevel = subject.abilities[HackConstructKeyAbility] * 4
    print 'ability = ', abilityLevel
    if abilityLevel is None:
        discovery.clientSystemMessage(subject.locator, ST.ID_HARDLINE_HACK_CONSTRUCT_ABILITY_FAIL, constants.Chat.CT_SYS_IMPORTANT)
        return 0
    print 'RS - Hack construct ability check succeeded!'
    return abilityLevel
    return


def GoUpALevel(subject, newLevel):
    oldMaxHealth = subject.MaxHealth
    subject.AbilityInv.recalculateMaxHealth()
    subject.Health += subject.MaxHealth - oldMaxHealth
    oldMaxIS = subject.InnerStrengthMax
    subject.AbilityInv.recalculateInnerstrength()
    subject.InnerStrengthAvailable += subject.InnerStrengthMax - oldMaxIS
    discovery.clientSystemMessage(subject.locator, ST.ID_LEVEL_UP_FEEDBACK, constants.Chat.CT_SYS_IMPORTANT)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_LEVEL_UP, 0)
    if newLevel == 3:
        discovery.sendModalMessage(subject.locator, ST.ID_LEVEL_UP_FEEDBACK_THREE, 0)
    if newLevel == 8:
        discovery.sendModalMessage(subject.locator, ST.ID_LEVEL_UP_FEEDBACK_EIGHT, 0)
    if newLevel == 9:
        discovery.sendModalMessage(subject.locator, ST.ID_LEVEL_UP_FEEDBACK_NINE, 0)
    if newLevel == 10:
        discovery.sendModalMessage(subject.locator, ST.ID_LEVEL_UP_FEEDBACK_TEN, 0)
    if newLevel == 15:
        discovery.sendModalMessage(subject.locator, ST.ID_LEVEL_UP_FEEDBACK_FIFTEEN, 0)
    return


def VendorBuyback(itemTypeID):
    if constants.Build.debug:
        print 'VendorBuyback() with ', itemTypeID
    if itemTypeID in NoBuyBackList:
        return int(1)
    if itemTypeID & -2147483648:
        abilitySellFullPriceList = {PowerShotAbility: 1, PreciseBlowAbility: 1, DetectVulnerabilityAbility: 1, EnergizedAttacksAbility: 1, PunishingBlowsAbility: 1, HyperStrengthAbility: 1, IgnorePainAbility: 1, TechniqueAbility: 1, SweepTheFloorAbility: 1, SpinningFlowerAbility: 1, ZenMasterAbility: 1, PurityAbility: 1, BalanceAbility: 1, DisarmTrapsAbility: 1, ImpartInvisibilityAbility: 1}
        abilityCodeData = itemTypeID & -1024
        level = itemTypeID & 1023
        buyback = discovery.getGameObjectPropValue(abilityCodeData, 'VendorPrice')
        if buyback is None:
            buyback = DEFAULT_VENDOR_PRICE
        else:
            for i in range(1, level):
                buyback += (i + 1) * (i + 1) * 200

            if abilitySellFullPriceList.has_key((abilityCodeData & 4193280) >> 10):
                return int(buyback)
            else:
                return int(buyback / 10.0)
    else:
        buyback = discovery.getGameObjectPropValue(itemTypeID, 'VendorPrice')
    if buyback is None:
        buyback = DEFAULT_VENDOR_PRICE
    return int(buyback / 10.0)
    return


def RecycleInfo(itemTypeID):
    if not itemTypeID:
        return 0
    if itemTypeID in NoBuyBackList:
        return 0
    buyback = discovery.getGameObjectPropValue(itemTypeID, 'VendorPrice')
    if buyback is None:
        buyback = DEFAULT_VENDOR_PRICE
    return int(buyback / 20.0)
    return


def RunVendorList(table, subject):
    loot = rewardselection.DeterminePopulateReward('GrabEverythingRewardRule', table, subject.PlayerCharacter)
    if constants.Build.debug:
        print 'Requested a Vendor using table ', table
        print 'Received ', loot
    return
    return


def RunMissionRewards(encodedReward, subject):
    rewards = rewardselection.Decode_Reward(encodedReward)
    loot = rewardselection.Apply_Rewards(rewards, subject.PlayerCharacter)
    if constants.Build.debug:
        print 'Received a mission reward of ', encodedReward
        print 'Received ', loot
    return
    return


def DebugReward(rule, table, roll, loot):
    print 'Computed loot using ', rule
    print '  on the table ', table
    print '  with the value of', roll
    print '  Selected reward and received ', loot
    return


def RunReward(subject, table, rule, args):
    if consolevar.GreysGiveNoLoot and subject.CharacterBase and subject.CharacterBase.getKillerConsideredGrey():
        return
    if subject.CharacterBase and subject.CharacterBase.getDeathReason() == constants.DeathReason.MISSION:
        return
    if rule is None:
        rule = 'DefaultRewardRule'
    _obj = subject.Container
    if _obj is None:
        _obj = subject.DataNode
    loot = rewardselection.DeterminePopulateReward(rule, table, _obj)
    if constants.Build.debug:
        DebugReward(rule, table, rewardselection.Lastroll, loot)
    return


def RunPocketChange(subject):
    if consolevar.GreysGiveNoLoot and subject.CharacterBase and subject.CharacterBase.getKillerConsideredGrey():
        return
    if subject.CharacterBase and subject.CharacterBase.getDeathReason() == constants.DeathReason.MISSION:
        return
    if constants.Construct.IsTutorialConstruct:
        return
    if subject.Level is not None and subject.Level > 0:
        info_amount = int(subject.Level * 5 * random.randrange(100) * consolevar.AI_PocketChangeScale)
        subject.Container.addInfo(info_amount)
        if constants.Build.debug:
            print 'Received %d info' % info_amount
    return


def RunCollectorPrizeList(table, subject):
    loot = rewardselection.DeterminePopulateReward('EverythingButNoDropNoWearRewardRule', table, subject.PlayerCharacter)
    if constants.Build.debug:
        print 'Requested a Collector Prize using table ', table
        print 'Received ', loot
    return
    return


def RunCollectorReqList(table, subject):
    loot = rewardselection.DeterminePopulateReward('GrabEverythingRewardRule', table, subject.PlayerCharacter)
    if constants.Build.debug:
        print 'Requested a Collector Requirement using table ', table
        print 'Received ', loot
    return
    return


def ReloadRewardSelection():
    rewardselection.ReloadRewardSelection()
    return


rewardselection = RewardSelection()
import re, os, sys
GoodItems = {}
BadItems = {}

def ParseItem(item, tablename):
    if item in GoodItems:
        return
    if item in BadItems:
        files = BadItems[item]
        files += ', ' + tablename
        BadItems[item] = files
        return
    type_info = discovery.getTypeInfo(int(item))
    if type_info.has_key('id'):
        GoodItems[item] = 1
    else:
        BadItems[item] = tablename
    return


def ParseTable(table, tablename):
    for block in table:
        for row in block:
            if row == 'entry':
                entries = block[row]
                for entry in entries:
                    if entry.has_key('reward'):
                        reward = entry['reward']
                        if reward.has_key('item'):
                            ParseItem(reward['item'], tablename)

    return


def TableImport(tablename):
    try:
        package = __import__('RewardTables.' + tablename)
        module = getattr(package, tablename)
        classobj = getattr(module, tablename)
        ParseTable(classobj.table, tablename)
    except:
        if constants.Build.debug:
            print 'Could not parse ', tablename
        discovery.errorPrint("REWARDVALIDATION: Could not parse table '%s'" % tablename)

    return


def RewardCheck():
    ignoredfiles = [
     '__init__.py', 'rewardselection.py', 'RewardTable.py', 'rewardtableutils.py']
    print 'Starting Reward Check'
    TablesDirectory = 'RewardTables'
    basepath = None
    for pathname in sys.path:
        if 'Rewards' in pathname:
            basepath = pathname

    if basepath is None:
        print "Couldn't find reward path"
        return
    basepath += '\\RewardTables'
    matchtable = re.compile('.*\\.py$')
    for (root, dirs, files) in os.walk(basepath):
        for filename in files:
            match = matchtable.match(filename)
            if match:
                if filename not in ignoredfiles:
                    tablename = re.sub('(.*)\\.py$', '\\1', filename)
                    TableImport(tablename)

    for item in BadItems:
        if constants.Build.debug:
            print 'Invalid item %s was found in the table(s) %s' % (item, BadItems[item])
        discovery.errorPrint('REWARDVALIDATION: Invalid item %s was found in the table(s) %s' % (item, BadItems[item]))

    return


if consolevar.ValidateRewardTables:
    RewardCheck()
level_exp_reqs = {1: 500, 2: 4500, 3: 16000, 4: 39000, 5: 77500, 6: 135500, 7: 217000, 8: 326000, 9: 466500, 10: 642500, 11: 797500, 12: 967500, 13: 1152500, 14: 1352500, 15: 1567500, 16: 1797500, 17: 2042500, 18: 2302500, 19: 2577500, 20: 2867500, 21: 3172500, 22: 3492500, 23: 3827500, 24: 4177500, 25: 4542500, 26: 4922500, 27: 5317500, 28: 5727500, 29: 6152500, 30: 6592500, 31: 7047500, 32: 7517500, 33: 8002500, 34: 8502500, 35: 9017500, 36: 9547500, 37: 10092500, 38: 10652500, 39: 11227500, 40: 11817500, 41: 12422500, 42: 13042500, 43: 13677500, 44: 14327500, 45: 14992500, 46: 15672500, 47: 16367500, 48: 17077500, 49: 17802500, 50: 17802500}

def GoldenTicketProc(subject, invitem):
    subject.PlayerCharacter.addInformation(100000, constants.RST.RECYCLE)
    subject.MissionTeam.startSpecificMission(10)
    return SUCCESS
    return


def ExperienceBoostHalfLevel(subject, invitem):
    level = subject.PlayerCharacter.getLevel()
    if level > 50:
        level = 50
    elif level < 1:
        level = 1
    subject.PlayerCharacter.addExperience(level_exp_reqs[level] / 5, constants.RST.GOLDEN_TICKET)
    return SUCCESS
    return


def ExperienceBoostQuarterLevel(subject, invitem):
    level = subject.PlayerCharacter.getLevel()
    if level > 50:
        level = 50
    elif level < 1:
        level = 1
    subject.PlayerCharacter.addExperience(level_exp_reqs[level] / 10, constants.RST.GOLDEN_TICKET)
    return SUCCESS
    return


def GenericOrgEventAbilityRule(subject, invitem):
    if invitem.GoldenTicketOrgPoint == constants.OrgPointType.OrgPointTypeNeoFragments or invitem.GoldenTicketOrgPoint == constants.OrgPointType.OrgPointTypeEvent2Clues:
        discovery.sendInvokeOrgEventMessage(subject.locator, invitem.GoldenTicketOrgPoint, subject.OrganizationID, GetReputationFromOrg(subject, subject.OrganizationID), subject.Level, invitem.type, subject.CrewID, subject.FactionID)
    return FAILURE
    return


def GoldenTicketPlayAnimation(subject, invitem):
    subject.CharMvt.playNamedScript(Stance_Stand, invitem.GoldenTicketAnimation, 0)
    return SUCCESS
    return


def PlayCakeAnimations(subject, invitem):
    if subject.IsInCombat:
        discovery.clientSystemMessage(subject.locator, ST.ID_EMOTE_ERROR_IN_COMBAT, constants.Chat.CT_SYS_IMPORTANT)
        return FAILURE
    if (subject.Stance == Stance_Stand or subject.Stance == Stance_Aggro) and subject.Action == Action_Idle:
        animName = 'Stand_Emote_MeroCake%d' % random.randrange(1, 4)
        subject.CharMvt.playInterNamedScript(Stance_Stand, animName, 0)
        return SUCCESS
    else:
        discovery.clientSystemMessage(subject.locator, ST.ID_EMOTE_ERROR_STANDING, constants.Chat.CT_SYS_IMPORTANT)
        return FAILURE
    return


def GetReputationFromOrg(subject, org):
    rep = None
    if org == constants.Organization.Zion:
        rep = subject.ReputationZionMilitary
    if org == constants.Organization.Machines:
        rep = subject.ReputationMachines
    if org == constants.Organization.Merovingian:
        rep = subject.ReputationMerovingian
    if org == constants.Organization.Cypherites:
        rep = subject.ReputationCypherites
    if org == constants.Organization.Niobe:
        rep = subject.ReputationNiobe
    if org == constants.Organization.PluribusNeo:
        rep = subject.ReputationPluribusNeo
    if rep is None:
        return 0
    return rep
    return


def GoldenTicketApplyAbility(subject, invitem):
    subject.AbilityInv.reapplyAbility(invitem.GoldenTicketAbilityID)
    return FAILURE
    return


def GoldenTicketSpawnSequence(subject, invitem):
    if invitem.ReqCharLevel is not None:
        if subject.Level < invitem.ReqCharLevel:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_GOLDEN_TICKET_PREREQ_NOT_MET, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    if invitem.GoldenTicketConstructID is not None:
        constructID = consolevar.ConID
        if constructID is None or constructID != invitem.GoldenTicketConstructID:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_PANDORA_BOX_FAIL_CONSTRUCT, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    x = 0
    try:
        item = getattr(invitem, 'GoldenTicketMissionRestrictedItem%i' % x)
        while item > 0:
            if subject.Inventory.hasItem(item):
                msg = ST.ID_OBJ_GOLDEN_TICKET_HAS_RESTRICTED_ITEM
                if invitem.GoldenTicketMissionItemFail is not None:
                    msg = invitem.GoldenTicketMissionItemFail
                discovery.clientSystemMessage(subject.locator, msg, constants.Chat.CT_SYS_IMPORTANT)
                return FAILURE
            x += 1
            item = getattr(invitem, 'GoldenTicketMissionRestrictedItem%i' % x)

    except:
        pass

    if invitem.GoldenTicketHardlineMinRange is not None:
        obj = rsmethods.getClosestObjectOfType(592, subject.Position)
        if obj is not None and (obj.Position.x - subject.Position.x) ** 2 + (obj.Position.z - subject.Position.z) ** 2 < invitem.GoldenTicketHardlineMinRange ** 2:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_PANDORA_BOX_FAIL_HARDLINE, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
        obj = rsmethods.getClosestObjectOfType(2400, subject.Position)
        if obj is not None and (obj.Position.x - subject.Position.x) ** 2 + (obj.Position.z - subject.Position.z) ** 2 < invitem.GoldenTicketHardlineMinRange ** 2:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_PANDORA_BOX_FAIL_HARDLINE, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    if invitem.GoldenTicketSignalBoosterMinRange is not None:
        obj = rsmethods.getClosestObjectOfType(2409, subject.Position)
        if obj is not None and (obj.Position.x - subject.Position.x) ** 2 + (obj.Position.z - subject.Position.z) ** 2 < invitem.GoldenTicketSignalBoosterMinRange ** 2:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_PANDORA_BOX_FAIL_SIGNAL_BOOSTER, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    if invitem.GoldenTicketOutdoorOnly is not None and invitem.GoldenTicketOutdoorOnly and subject.CharMvt.isInside():
        discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_PANDORA_BOX_FAIL_INDOORS, constants.Chat.CT_SYS_IMPORTANT)
        return FAILURE
    if invitem.GoldenTicketBreakExclusive is not None and invitem.GoldenTicketBreakExclusive:
        if subject.AbilityInv.CurExclusiveAbility != InvalidAbility:
            subject.AbilityInv.deactivateAbility(subject.AbilityInv.CurExclusiveAbility)
    if invitem.GoldenTicketRecastShadow is not None and invitem.GoldenTicketRecastShadow > 0:
        subject.AbilityInv.addRecastTimer(MasterShadowActiveAbility, invitem.GoldenTicketRecastShadow)
        subject.AbilityInv.addRecastTimer(DisguiseGrantAbility, invitem.GoldenTicketRecastShadow)
        subject.AbilityInv.addRecastTimer(SneakGrantAbility, invitem.GoldenTicketRecastShadow)
        subject.AbilityInv.addRecastTimer(InvisibilityGrantAbility, invitem.GoldenTicketRecastShadow)
        subject.AbilityInv.addRecastTimer(VanishAbility, invitem.GoldenTicketRecastShadow)
        subject.AbilityInv.addRecastTimer(ConcealmentGrantAbility, invitem.GoldenTicketRecastShadow)
        subject.AbilityInv.addRecastTimer(EscapeandEvasionAbility, invitem.GoldenTicketRecastShadow)
    if subject.CharMvt.getStance() != Stance_Stand or subject.CharMvt.getAction() != Action_Idle:
        discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_PANDORA_BOX_FAIL_NOT_STAND, constants.Chat.CT_SYS_IMPORTANT)
        return FAILURE
    if invitem.GoldenTicketSpawnItemID is not None:
        position = subject.Position
        position.y -= subject.HalfExtents.y
        discovery.SpawnObject(invitem.GoldenTicketSpawnItemID, consolevar.ConID, position)
    subject.PlayerCharacter.spawnSequence(invitem.GoldenTicketSequenceID)
    return SUCCESS
    return


def GoldenTicketStartMission(subject, invitem):
    if invitem.ReqCharLevel is not None:
        if subject.Level < invitem.ReqCharLevel:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_GOLDEN_TICKET_PREREQ_NOT_MET, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    if invitem.GoldenTicketConstructID is not None:
        constructID = subject.CharMvt.getCurrentConstructID()
        if constructID is None or constructID != invitem.GoldenTicketConstructID:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_GOLDEN_TICKET_FAIL_CONSTRUCT, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    if invitem.GoldenTicketMinZionRep is not None:
        ZionRep = subject.ReputationZionMilitary
        if ZionRep is None or ZionRep < invitem.GoldenTicketMinZionRep:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_GOLDEN_TICKET_FAIL_REPUTATION, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    if invitem.GoldenTicketMinMervRep is not None:
        MervRep = subject.ReputationMerovingian
        if MervRep is None or MervRep < invitem.GoldenTicketMinMervRep:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_GOLDEN_TICKET_FAIL_REPUTATION, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    if invitem.GoldenTicketMinMachineRep is not None:
        MachineRep = subject.ReputationMachines
        if MachineRep is None or MachineRep < invitem.GoldenTicketMinMachineRep:
            discovery.clientSystemMessage(subject.locator, ST.ID_OBJ_GOLDEN_TICKET_FAIL_REPUTATION, constants.Chat.CT_SYS_IMPORTANT)
            return FAILURE
    x = 0
    try:
        item = getattr(invitem, 'GoldenTicketMissionRestrictedItem%i' % x)
        while item > 0:
            if subject.Inventory.hasItem(item):
                msg = ST.ID_OBJ_GOLDEN_TICKET_HAS_RESTRICTED_ITEM
                if invitem.GoldenTicketMissionItemFail is not None:
                    msg = invitem.GoldenTicketMissionItemFail
                discovery.clientSystemMessage(subject.locator, msg, constants.Chat.CT_SYS_IMPORTANT)
                return FAILURE
            x += 1
            item = getattr(invitem, 'GoldenTicketMissionRestrictedItem%i' % x)

    except:
        pass

    MissionID = -1
    Difficulty = 1
    AutoStart = 0
    SkipChecks = 0
    ExtraArgs = ''
    if invitem.GoldenTicketMissionID is not None:
        MissionID = invitem.GoldenTicketMissionID
    if invitem.GoldenTicketMissionDifficulty is not None:
        Difficulty = invitem.GoldenTicketMissionDifficulty
    if invitem.GoldenTicketMissionStart is not None:
        AutoStart = invitem.GoldenTicketMissionStart
    if invitem.GoldenTicketMissionSkipChecks is not None:
        SkipChecks = invitem.GoldenTicketMissionSkipChecks
    if invitem.GoldenTicketMissionArgs is not None:
        ExtraArgs = invitem.GoldenTicketMissionArgs
    subject.MissionTeam.startSpecificMission(MissionID, Difficulty, AutoStart, SkipChecks, invitem, ExtraArgs)
    return FAILURE
    return


def GoldenTicketRespec(subject, invitem):
    if subject.IsDead:
        ability.utility.SendAbilityOutputToCaster(ST.ID_GOLDEN_TICKET_FAIL_WHILE_DEAD, CorruptCode1Ability, subject.locator, subject.locator, 0)
        return FAILURE
    if subject.AbilityInv.resetAttributes():
        discovery.playEffect(subject.locator, subject.locator, 301989893, 0)
        discovery.clientSystemMessage(subject.locator, ST.ID_RESPEC_MESSAGE, constants.Chat.CT_SYS_IMPORTANT)
        return SUCCESS
    else:
        discovery.clientSystemMessage(subject.locator, ST.ID_RESPEC_MESSAGE_FAIL, constants.Chat.CT_SYS_IMPORTANT)
        return FAILURE
    return


def GoldenTicketDecayingCode(subject, invitem):
    if subject.IsDead:
        ability.utility.SendAbilityOutputToCaster(ST.ID_GOLDEN_TICKET_FAIL_WHILE_DEAD, CorruptCode1Ability, subject.locator, subject.locator, 0)
        return FAILURE
    if subject.AbilityInv.hasTempMod(CorruptCode1Ability, CombatTacticsAbility) or subject.AbilityInv.hasTempMod(CorruptCode2Ability, CombatTacticsAbility):
        stringID = discovery.getGameObjectPropValue(1 << 31 | CorruptCode1Ability << 10, 'InfoID')
        ability.utility.SendAbilityOutputToCaster(ST.ID_ABILITY_CAST_FAILURE_LOWER_BUFF_STACKING_SELF, CorruptCode1Ability, subject.locator, subject.locator, int(stringID))
        return FAILURE
    else:
        subject.AbilityInv.addRecastTimer(invitem.type, 120.0, 1)
        if random.randrange(100) < 50:
            subject.AbilityInv.reapplyAbility(CorruptCode1Ability)
        else:
            subject.AbilityInv.reapplyAbility(CorruptCode2Ability)
        return SUCCESS
    return


def GoldenTicketAntioverrideProto01(subject, invitem):
    if subject.IsDead:
        ability.utility.SendAbilityOutputToCaster(ST.ID_GOLDEN_TICKET_FAIL_WHILE_DEAD, AntioverrideProto01AccAbility, subject.locator, subject.locator, 0)
        return FAILURE
    if subject.AbilityInv.hasTempMod(AntioverrideProto01AccAbility) or subject.AbilityInv.hasTempMod(AntioverrideProto01DecAbility) or subject.AbilityInv.hasTempMod(AntioverrideProto01RunAbility):
        ability.utility.SendAbilityOutputToCaster(ST.ID_ABILITY_CAST_FAILURE_SAME_BUFF_STACKING_SELF, AntioverrideProto01AccAbility, subject.locator, subject.locator, 0)
        return FAILURE
    else:
        subject.AbilityInv.addRecastTimer(invitem.type, 300.0, 1)
        if random.randrange(99) < 34:
            subject.AbilityInv.reapplyAbility(AntioverrideProto01AccAbility)
        elif random.randrange(99) < 67:
            subject.AbilityInv.reapplyAbility(AntioverrideProto01DecAbility)
        else:
            subject.AbilityInv.reapplyAbility(AntioverrideProto01RunAbility)
        return SUCCESS
    return


def GoldenTicketOpenURL(subject, invitem):
    hidebuttons = 0
    relativepath = 0
    height = 0
    width = 0
    restrictmovesize = 0
    if invitem.HideBackForwardButtons is not None:
        hidebuttons = invitem.HideBackForwardButtons
    if invitem.LocalRelativePath is not None:
        relativepath = invitem.LocalRelativePath
    if invitem.PreferredHeight is not None:
        height = invitem.PreferredHeight
    if invitem.PreferredWidth is not None:
        width = invitem.PreferredWidth
    if invitem.RestrictMoveResize is not None:
        restrictmovesize = invitem.RestrictMoveResize
    subject.PlayerCharacter.openURL(invitem.Address, hidebuttons, relativepath, height, width, restrictmovesize)
    if invitem.GoldenTicketURLConsume is not None:
        if invitem.GoldenTicketURLConsume == 1:
            return SUCCESS
    return FAILURE
    return


RSICalls = {'SetBodyType': 'subject.Inventory.setBodyType(invitem.GoldenTicketRSIID)', 'SetHeadType': 'subject.Inventory.setHeadType(invitem.GoldenTicketRSIID)', 'SetHairType': 'subject.Inventory.setHairType(invitem.GoldenTicketRSIID)', 'SetHairColor': 'subject.Inventory.setHairColor(invitem.GoldenTicketRSIID)', 'SetFacialHairType': 'subject.Inventory.setFacialHairType(invitem.GoldenTicketRSIID)', 'SetFacialHairColor': 'subject.Inventory.setFacialHairColor(invitem.GoldenTicketRSIID)', 'SetSkinTone': 'subject.Inventory.setSkinTone(invitem.GoldenTicketRSIID)', 'SetTattooType': 'subject.Inventory.setTattooType(invitem.GoldenTicketRSIID)'}

def GoldenTicketRSIProc(subject, invitem):
    if invitem.GoldenTicketMaleOnly and not subject.Inventory.isMale():
        return FAILURE
    if invitem.GoldenTicketFemaleOnly and subject.Inventory.isMale():
        return FAILURE
    exec RSICalls[invitem.GoldenTicketRSICall]
    subject.Inventory.commitRSIToDatabase()
    return SUCCESS
    return


def LookupCID(pvp_id):
    cid_base_id = constants.Construct.ConstructIDBase
    metr_list = (
     consolevar.PVPWorldFileName1, consolevar.PVPWorldFileName2, consolevar.PVPWorldFileName3, consolevar.PVPWorldFileName4, consolevar.PVPWorldFileName5, consolevar.PVPWorldFileName6, consolevar.PVPWorldFileName7, consolevar.PVPWorldFileName8, consolevar.PVPWorldFileName9, consolevar.PVPWorldFileName10)
    cid_data = (
     -1, -1)
    if pvp_id <= len(metr_list) and pvp_id > 0:
        cid_data = (
         metr_list[pvp_id - 1], cid_base_id + pvp_id - 1)
    return cid_data
    return


def LookupConstructData(metr_filename, org_id):
    crp = 'resource/worlds/final_world/constructs/archive/'
    north = constants.Construct.NorthDegrees
    northeast = constants.Construct.NorthEastDegrees
    east = constants.Construct.EastDegrees
    south = constants.Construct.SouthDegrees
    west = constants.Construct.WestDegrees
    zion = constants.Organization.Zion
    machines = constants.Organization.Machines
    mero = constants.Organization.Merovingian
    niobe = constants.Organization.Niobe
    cypher = constants.Organization.Cypherites
    neo = constants.Organization.PluribusNeo
    construct_data_lookup = {(-1, zion): ((-500, 2100, 500), north), (-1, machines): ((-500, 2100, 500), north), (-1, mero): ((-500, 2100, 500), north), (-1, niobe): ((-500, 2100, 500), north), (-1, cypher): ((-500, 2100, 500), north), (-1, neo): ((-500, 2100, 500), north), (crp + 'archive01/archive01.metr', zion): ((-4000, 95, -46000), north), (crp + 'archive01/archive01.metr', machines): ((23000, 95, 5000), west), (crp + 'archive01/archive01.metr', mero): ((-43000, 1095, 5000), east), (crp + 'archive01/archive01.metr', niobe): ((23000, 1095, -17000), west), (crp + 'archive01/archive01.metr', cypher): ((-16000, 95, 33000), south), (crp + 'archive01/archive01.metr', neo): ((-43000, 95, -17000), east), (crp + 'archive02/archive02.metr', zion): ((-3000, -105, 12000), north), (crp + 'archive02/archive02.metr', machines): ((-3000, -105, 40000), south), (crp + 'archive02/archive02.metr', mero): ((16000, -105, 28000), west), (crp + 'archive02/archive02.metr', niobe): ((11000, -105, 14000), north), (crp + 'archive02/archive02.metr', cypher): ((11000, -105, 40000), south), (crp + 'archive02/archive02.metr', neo): ((-12000, -105, 26000), east), (crp + 'archive_yuki/archive_yuki.metr', zion): ((15500, 295, 15500), west), (crp + 'archive_yuki/archive_yuki.metr', machines): ((-25500, 295, 35500), south), (crp + 'archive_yuki/archive_yuki.metr', mero): ((-24500, 295, -5500), north), (crp + 'archive_yuki/archive_yuki.metr', niobe): ((4500, 295, 35500), south), (crp + 'archive_yuki/archive_yuki.metr', cypher): ((-35500, 295, 14500), east), (crp + 'archive_yuki/archive_yuki.metr', neo): ((5500, 295, -5500), north), (crp + 'archive_sakura/archive_sakura.metr', zion): ((15500, 295, 15500), west), (crp + 'archive_sakura/archive_sakura.metr', machines): ((-25500, 295, 35500), south), (crp + 'archive_sakura/archive_sakura.metr', mero): ((-24500, 295, -5500), north), (crp + 'archive_sakura/archive_sakura.metr', niobe): ((4500, 295, 35500), south), (crp + 'archive_sakura/archive_sakura.metr', cypher): ((-35500, 295, 14500), east), (crp + 'archive_sakura/archive_sakura.metr', neo): ((5500, 295, -5500), north), (crp + 'archive_ashencourte/archive_ashencourte.metr', zion): ((-18500, 95, -7500), east), (crp + 'archive_ashencourte/archive_ashencourte.metr', machines): ((18000, 95, -6000), west), (crp + 'archive_ashencourte/archive_ashencourte.metr', mero): ((5000, 95, 15000), west), (crp + 'archive_ashencourte/archive_ashencourte.metr', niobe): ((-18000, 95, 6000), east), (crp + 'archive_ashencourte/archive_ashencourte.metr', cypher): ((18500, 95, 7500), west), (crp + 'archive_ashencourte/archive_ashencourte.metr', neo): ((-5000, 95, -15000), northeast), (crp + 'archive_widowsmoor/archive_widowsmoor.metr', zion): ((-18500, 95, -7500), east), (crp + 'archive_widowsmoor/archive_widowsmoor.metr', machines): ((18000, 95, -6000), west), (crp + 'archive_widowsmoor/archive_widowsmoor.metr', mero): ((5000, 95, 15000), west), (crp + 'archive_widowsmoor/archive_widowsmoor.metr', niobe): ((-18000, 95, 6000), east), (crp + 'archive_widowsmoor/archive_widowsmoor.metr', cypher): ((18500, 95, 7500), west), (crp + 'archive_widowsmoor/archive_widowsmoor.metr', neo): ((-5000, 95, -15000), northeast)}
    construct_data = (
     (
      -1, -1, -1), -1)
    if (
     metr_filename, org_id) in construct_data_lookup:
        construct_data = construct_data_lookup[(metr_filename, org_id)]
    return construct_data
    return


def LookupTeleportLocationData(pvp_id, org_id):
    if constants.build.Debug:
        print 'Looking for PvPID ', pvp_id, '...'
    cid_data = LookupCID(pvp_id)
    construct_data = LookupConstructData(cid_data[0], org_id)
    if constants.build.Debug:
        print 'Found location (%s,%s,%s) in %s!' % (construct_data[0][0], construct_data[0][1], construct_data[0][2], cid_data[0])
    locationData = None
    if construct_data:
        locationData = (
         cid_data[1], (construct_data[0][0], construct_data[0][1], construct_data[0][2]), construct_data[1])
    return locationData
    return


def FormatObjectProperties(gameobj):
    props = gameobj.getProps()
    props.sort()
    d = [
     '************* START Server version of object *************']
    for prop in props:
        if not '/' in prop:
            d.append(prop + ': ' + str(getattr(gameobj, prop)))

    d.append('************* END Server version of object *************')
    return d
    return


testSequences = (48, 49)

def DataNodeSequenceTest(playerObj, accessLevel):
    playerObj.PlayerCharacter.spawnSequence(random.choice(testSequences))
    return


LowSequences = (156, 157, 158, 159, 160, 161, 162, 163, 164)

def DataNodeSequenceLow(playerObj, accessLevel):
    playerObj.PlayerCharacter.spawnSequence(random.choice(LowSequences))
    return


MidSequences = (165, 166, 167, 168, 169)

def DataNodeSequenceMid(playerObj, accessLevel):
    if accessLevel < 30 or random.randrange(100) < 40:
        return DataNodeSequenceLow(playerObj, accessLevel)
    playerObj.PlayerCharacter.spawnSequence(random.choice(MidSequences))
    return


HighSequences = (170, 171, 172, 173, 174, 175, 176, 177, 118)

def DataNodeSequenceHigh(playerObj, accessLevel):
    if accessLevel < 35 or random.randrange(100) < 50:
        return DataNodeSequenceMid(playerObj, accessLevel)
    playerObj.PlayerCharacter.spawnSequence(random.choice(HighSequences))
    return


def DataNodeSequenceHighPlus(playerObj, accessLevel):
    if random.randrange(100) < consolevar.DataNodeHighAgentChance:
        if consolevar.ConID == 17:
            playerObj.PlayerCharacter.spawnAgent(0)
        else:
            playerObj.PlayerCharacter.spawnAgent(1)
    else:
        DataNodeSequenceHigh(playerObj, accessLevel)
    return


def RecipeTicketProc(subject, invitem):
    index = 0
    val = discovery.getGameObjectPropValue(invitem.type, 'GoldenTicketTaughtRecipe%d' % index)
    while val is not None:
        index += 1
        subject.Inventory.learnRecipe(val)
        val = discovery.getGameObjectPropValue(invitem.type, 'GoldenTicketTaughtRecipe%d' % index)

    return SUCCESS
    return


def ConvertToItemProc(subject, invitem):
    newItemID = invitem.ConvertToItem
    count = 1
    if invitem.ConvertToItemCount is not None:
        count = invitem.ConvertToItemCount
    subject.Inventory.rmvItem(invitem.slot)
    for i in range(count):
        subject.Inventory.addItem(newItemID, 0)

    return


def LuggableRecipeAndFragProc(subject, invitem):
    RecipeTicketProc(subject, invitem)
    ConvertToItemProc(subject, invitem)
    return FAILURE
    return


NoBuyBackList = (
 0, 0)
start_msg = '-- Loaded RSHooks '
discovery.outputDebugString(start_msg)
discovery.serverPrint(start_msg)

def ModuleError(errobj, errdata, errtraceback):
    print str(errobj), str(errdata), str(errtraceback)
    return


return

# okay decompiling work/monolith_pyc\rshooks.pyc
