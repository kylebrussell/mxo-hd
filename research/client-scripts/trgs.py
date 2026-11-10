# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\trgs.py
# Compiled at: 2008-10-15 03:01:28
import traceback, RewardSelection
from whrandom import randint
import getopt

def exceptionCB(obj, data, tb):
    try:
        discovery.errorPrint('****************************************')
        discovery.errorPrint('Rule Exception')
        discovery.errorPrint(`obj`)
        discovery.errorPrint(`data`)
        discovery.errorPrint('** traceback **')
        if tb:
            stackTrace = traceback.extract_tb(tb)
            stackTrace.reverse()
            for tuple in stackTrace:
                discovery.errorPrint('File: %s (%d) Function: %s' % tuple[:3])
                discovery.errorPrint(tuple[3])

        discovery.errorPrint('****************************************')
        traceback.print_exc()
    except:
        traceback.print_exc()

    return


def ComputeHackConstructOutcome(abilityLevel, constructDefense):
    print 'TRGS - ComputeHackConstructOutcome()'
    print 'abilityLevel = ', abilityLevel, 'firewall defense = ', constructDefense
    if abilityLevel is None:
        return 0
    if abilityLevel > constructDefense:
        print 'TRGS - Hack construct succeeded!'
        return 1
    print 'TRGS - Hack construct failed.'
    return 0
    return


class FakeGameObject:
    __module__ = __name__

    def __init__(self, props):
        if props is None:
            return
        for prop in props:
            self.__dict__[prop] = props[prop]

        return


def MakeAndEncodeReward(rule='DefaultRewardRule', table='Table1', dict=None, args=None):
    fgo = FakeGameObject(dict)
    loot = rewardselection.DetermineEncodedReward(rule, table, args, fgo)
    return loot
    return


def MakeRewardRoll(rule='DefaultRewardRule', table='Table1', dict=None, args=None):
    fgo = FakeGameObject(dict)
    loot = rewardselection.DetermineReward(rule, table, args, fgo)
    return loot
    return


def GetRewardItemID(reward):
    itemID = 0
    try:
        item = rewardselection.Decode_Reward(reward)
        itemID = int(rewardselection.Get_Item(item[0]))
    except:
        pass

    return itemID
    return


def ReloadRewardSelection():
    rewardselection.ReloadRewardSelection()
    return


def FindMissionModule(missionid):
    classname = 'Mission' + str(missionid)
    if not Missions.has_key(classname):
        try:
            package = __import__('Missions.' + classname)
            module = getattr(package, classname)
            classobj = getattr(module, classname)
            Missions[classname] = classobj
        except:
            Missions[classname] = None

    if Missions[classname] is None:
        return None
    else:
        return Missions[classname]()
    return


def ParseMissionArgs(missionargs):
    try:
        autostart = 0
        difficulty = -1
        sectors = []
        areas = []
        permutations = []
        missionargs = missionargs.split()
        (optlist, args) = getopt.getopt(missionargs, 'ad:m:')
        for (opt, arg) in optlist:
            if opt == '-a':
                autostart = 1
            elif opt == '-d':
                difficulty = int(arg)
            elif opt == '-m':
                phaselist = arg.split(':')
                for phase in phaselist:
                    parsed = phase.split('.')
                    if len(parsed) == 2:
                        sectors.append(int(parsed[0]))
                        areas.append(int(parsed[1]))
                    elif len(parsed) == 3:
                        sectors.append(int(parsed[0]))
                        areas.append(int(parsed[1]))
                        permutations.append(int(parsed[2]))
                    else:
                        return None

        if len(sectors) != len(areas):
            return None
        return (autostart, difficulty, sectors, areas, permutations)
    except:
        return None

    return


def HandleInvokeOrgEvent(locator, event=constants.OrgPointType.OrgPointTypeInvalid, org=constants.Organization.Invalid, reputation=0, level=0, faction=0, crew=0):
    if event == constants.OrgPointType.OrgPointTypeNeoFragments:
        ability = HandleNeoCorpseEvents(event, org)
        if ability is not None:
            discovery.sendApplyAbilityToSelf(locator, ability, level, level)
    if event == constants.OrgPointType.OrgPointTypeEvent2Clues:
        ability = HandleEvent2Events(event, org)
        if ability is not None:
            discovery.sendApplyAbilityToSelf(locator, ability, level, level)
    if event == constants.OrgPointType.OrgPointTypeHalloween and worldevent.isEventActive('2008 Halloween Event Begins') and not worldevent.isEventActive('2008 Halloween Event Ends'):
        discovery.sendApplyAbilityToSelf(locator, 1004, level, level)
    return


def HandleNeoCorpseEvents(event, org):
    if worldevent is None:
        return
    if not worldevent.isEventActive('Neo Corpse Begins'):
        return
    if worldevent.isEventActive('Neo Corpse Buff Ends'):
        return
    bIsAfterCollection = worldevent.isEventActive('Neo Corpse Ends')
    normalizedPoints = worldevent.getNormalizedOrgPoints(event)
    normalizedPoints = [_[1] for i in range(len(normalizedPoints))]
    normalizedPoints.sort()
    normalizedPoints.reverse()
    orgOrder = [_[1] for pair in normalizedPoints]
    orgPlace = min(orgOrder.index(org), 2)
    if bIsAfterCollection:
        if orgPlace == 0:
            return constants.Ability.Event1_1Neo5Ability
        else:
            return constants.Ability.Event1_1Neo4Ability
    else:
        aAbility = [
         constants.Ability.Event1_1Neo3Ability, constants.Ability.Event1_1Neo2Ability, constants.Ability.Event1_1Neo1Ability]
        return aAbility[orgPlace]
    return


def HandleEvent2Events(event, org):
    if worldevent is None:
        return
    if not worldevent.isEventActive('Flymen - Buff Stage 1'):
        return
    if worldevent.isEventActive('Flymen - Buff Ends'):
        return
    bIsAfterCollection = worldevent.isEventActive('Flymen - Buff Stage 2')
    normalizedPoints = worldevent.getNormalizedOrgPoints(event)
    normalizedPoints = [_[1] for i in range(len(normalizedPoints))]
    normalizedPoints.sort()
    normalizedPoints.reverse()
    orgOrder = [_[1] for pair in normalizedPoints]
    orgPlace = min(orgOrder.index(org), 2)
    orgNum = max(min(org, 3), 1) - 1
    if bIsAfterCollection:
        aAbility = [
         [
          constants.Ability.Event2_0Zion2Ability, constants.Ability.Event2_0Machine2Ability, constants.Ability.Event2_0Merv2Ability], [constants.Ability.Event2_0Zion4Ability, constants.Ability.Event2_0Machine4Ability, constants.Ability.Event2_0Merv4Ability]]
        if orgPlace == 0:
            return aAbility[1][orgNum]
        else:
            return aAbility[0][orgNum]
    else:
        aAbility = [
         [
          constants.Ability.Event2_0Zion3Ability, constants.Ability.Event2_0Machine3Ability, constants.Ability.Event2_0Merv3Ability], [constants.Ability.Event2_0Zion2Ability, constants.Ability.Event2_0Machine2Ability, constants.Ability.Event2_0Merv2Ability], [constants.Ability.Event2_0Zion1Ability, constants.Ability.Event2_0Machine1Ability, constants.Ability.Event2_0Merv1Ability]]
        return aAbility[orgPlace][orgNum]
    return


rewardselection = RewardSelection.RewardSelection()
Missions = {}
start_msg = '-- Loaded TRGS.py'
discovery.outputDebugString(start_msg)
discovery.serverPrint(start_msg)
return

# okay decompiling work/monolith_pyc\trgs.pyc
