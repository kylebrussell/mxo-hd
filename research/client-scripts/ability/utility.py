# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\utility.py
# Compiled at: 2006-03-30 16:29:38
import random, traceback, obj
from ability.defines import *
from combatvalidate import CanFight
import stringtable_client as StringTable, math

def InterlockTurns(value):
    """ability mgr considers negative time values are interlock turns."""
    return -value
    return


def d100():
    return random.range(100)
    return


def outputToAll(msg):
    discovery.outputDebugString(msg)
    discovery.serverPrint(msg)
    return


def outputAbilityDebug(msg):
    if consolevar.AbilityDebugPrint > 1:
        discovery.outputDebugString(msg)
        discovery.serverPrint(msg)
    return


def setAbilityCombatID(sentence, combatID):
    combatIDMasked = combatID & 255
    sentence.result_data |= combatIDMasked
    return


def setAbilityDataValue(sentence, value):
    result_data = sentence.result_data
    combatID = result_data & 255
    result_data = value
    result_data <<= 8
    result_data |= combatID
    sentence.result_data = result_data
    return


def getAbilityName(abil):
    return abilitylib.getAbilityName(abil)
    return


def evalContest(A, aMod, D, dMod):
    """
    This is the basic contest of ability check. This covers combat
    (attacker versus defender) as well as non-combat (ability versus
    difficulty) situations.

    Attacker's Value: A+aMod+d100
    Defender's Value: D+dMod+50

    Attacker wins if his value is higher.    
    """
    discovery.serverPrint('A ' + `A` + '  aMod ' + `aMod` + '  D ' + `D` + '  dMod ' + `dMod`)
    roll = random.randrange(100)
    discovery.serverPrint('roll ' + `roll`)
    av = A + aMod + roll
    dv = D + dMod + 50
    discovery.serverPrint("Attacker's Value: (A+aMod+roll) " + `av`)
    discovery.serverPrint("Defender's Value: (D+dMod+50  ) " + `dv`)
    delta = av - dv
    discovery.serverPrint('delta ' + `delta`)
    return delta
    return


def flat_distance_within(pos1, pos2, test_dist):
    x_diff = pos1.x - pos2.x
    z_diff = pos1.z - pos2.z
    return x_diff * x_diff + z_diff * z_diff < test_dist * test_dist
    return


def SafeVal(value):
    if value is None:
        return 0
    return value
    return


def GetAbilityLevel(player, ability):
    if player.hasAbility[ability]:
        return SafeVal(player.abilities[ability])
    else:
        return -10000
    return


def GetHPAL(player):
    if player.hasAbility[AwakenedAbility]:
        return (
         AwakenedAbility, GetAbilityLevel(player, AwakenedAbility))
    return (
     AwakenedAbility, 0)
    return


def Get_HPAL_Level(player):
    (ability, level) = GetHPAL(player)
    return level
    return


def GetToolAbility(sentence):
    type = sentence.indirectObject.type
    type_info = discovery.getTypeInfo(type)
    if type_info.has_key('ReqAbilityID'):
        return type_info['ReqAbilityID']
    else:
        discovery.serverPrint('Tool used with no ability.')
        discovery.clientSystemMessage(sentence.subject.locator, StringTable.ID_ABILITY_TOOL_FAIL, constants.Chat.CT_SYS_IMPORTANT)
        return None
    return


def HasToolForAbility(sentence, ability):
    """ checks if there is a tool appropriate for ability in inventory, returns True or False"""
    if sentence.subject.AI is not None:
        return SUCCESS
    tool = sentence.subject.Inventory.findToolForAbility(ability)
    if tool is not None:
        outputToAll('Tool found in inventory with required ability')
        return SUCCESS
    else:
        outputToAll('No tool found in inventory with required ability')
        return FAILURE
    return


def IsProgramLauncherEquipped(subject):
    if IsAnNPC(subject):
        return SUCCESS
    if subject.Inventory.hasEquippedHackerItem():
        return SUCCESS
    else:
        discovery.serverPrint('   Program Launcher is not equipped!')
        discovery.clientSystemMessage(subject.locator, StringTable.ID_ABILITY_TOOL_FAIL, constants.Chat.CT_SYS_IMPORTANT)
        return FAILURE
    return


def GetRandomListEntry(list):
    return list[GetRandomListSlot(list)]
    return


def GetRandomListSlot(list):
    return random.randrange(0, len(list))
    return


def GetEquippedWeapon(subject):
    if IsAnNPC(subject):
        return
    else:
        return subject.Inventory.getEquippedWeaponItem()
    return


def GetEquippedHackerItem(subject):
    if IsAnNPC(subject):
        return
    else:
        return subject.Inventory.getEquippedHackerItem()
    return


def SetItem(subject, item, slot):
    if IsAnNPC(subject) and slot == 109:
        subject.setWeapon(item.type)
        return
    else:
        subject.Inventory.setItem(item, slot)
    return


def GetRandEquipedItemFromInventory(subject):
    if subject.Inventory:
        validItemSlots = subject.Inventory.getEquippedItems()
        if not validItemSlots:
            return validItemSlots
        return GetRandomListEntry(validItemSlots)
    else:
        outputAbilityDebug('GetRandEquipedItemFromInventory: No inventory on target.')
        return subject.Inventory
    return


def IsACharacter(subject):
    if subject.CharMvt:
        return True
    else:
        return False
    return


def IsAPlayer(subject):
    if IsACharacter(subject):
        if subject.Inventory:
            return True
    return False
    return


def IsAnNPC(subject):
    if IsAPlayer(subject):
        return False
    return True
    return


def IsAnItem(subject):
    return IsACharacter(subject) == False
    return


def PerformSpecialMove(subject, defenderLocator, ability, success, level=0, item=None):
    result = subject.Interlock.specialAttack(defenderLocator, ability, success, level, item)
    outputAbilityDebug('Special Move Perf: %d ' % result)
    return result
    return


BaseCombatSpecialMoveDepAttr = '\ndirectObject.locator\n'

def SendAbilityOutputToCaster(msgID, ability, caster, target, extraData=()):
    discovery.abilityMessageToCaster(msgID, ability, caster, target, extraData)
    return


def SendAbilityOutputToTarget(msgID, ability, caster, target, extraData=()):
    discovery.abilityMessageToTarget(msgID, ability, caster, target, extraData)
    return


def SendAbilityOutputToAll(msgID, ability, caster, target, extraData=()):
    discovery.abilityMessageToAll(msgID, ability, caster, target, extraData)
    return


def SendAbilityOutputToCasterSentence(msgID, ability, sentence, extraData=()):
    discovery.abilityMessageToCaster(msgID, ability, sentence.subject.locator, sentence.directObject.locator, extraData)
    return


def SendAbilityOutputToTargetSentence(msgID, ability, sentence, extraData=()):
    discovery.abilityMessageToTarget(msgID, ability, sentence.subject.locator, sentence.directObject.locator, extraData)
    return


def SendAbilityOutputToAllSentence(msgID, ability, sentence, extraData=()):
    discovery.abilityMessageToAll(msgID, ability, sentence.subject.locator, sentence.directObject.locator, extraData)
    return


def SendAbilityOutputToCasterMsg(msgID, ability, msg, extraData=()):
    discovery.abilityMessageToCaster(msgID, ability, msg.subjectLocator, msg.directObjectLocator, extraData)
    return


def SendAbilityOutputToTargetMsg(msgID, ability, msg, extraData=()):
    discovery.abilityMessageToTarget(msgID, ability, msg.subjectLocator, msg.directObjectLocator, extraData)
    return


def SendAbilityOutputToAllMsg(msgID, ability, msg, extraData=()):
    discovery.abilityMessageToAll(msgID, ability, msg.subjectLocator, msg.directObjectLocator, extraData)
    return


def SendDestoryItemOutputToAllMsg(abilityID, subject, directobject, itemID, send_message=True):
    if send_message == False:
        return
    SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_ITEM_DESTORY, abilityID, subject, directobject, itemID)
    return


def SendGiveHealthResultToAll(abilityID, subject, directobject, value, send_message=True):
    if send_message == False:
        return
    SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_GIVE_HEALTH, abilityID, subject, directobject, value)
    return


def SendTakeDamageResultToAll(result, abilityID, subject, directobject, value, send_message=True):
    if send_message == False:
        return
    if result == DEFLECTED or result == FAILURE:
        SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_VIRUS_DEFLECTED, abilityID, subject, directobject, value)
    else:
        SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_TAKE_DAMAGE, abilityID, subject, directobject, value)
    return


def GetPropertyFromAbility(ability_id, property_name):
    type_id = discovery.abilityIDToGameObjectID(ability_id)
    return discovery.getGameObjectPropValue(type_id, property_name)
    return


def GetAbilityInfoID(ability_id):
    return GetPropertyFromAbility(ability_id, 'InfoID')
    return


def AbilityRound(value):
    newValue = int(value + 0.5)
    if newValue == 0:
        newValue = 1
    return newValue
    return


def AbilityFourCCFix(val):
    return val & 2147483647
    return


def ToFourCC(val):
    t = [
     0, 0, 0, 0]
    t[0] = chr(val & 255)
    t[1] = chr((val & 65280) >> 8)
    t[2] = chr((val & 16711680) >> 16)
    t[3] = chr((val & -16777216) >> 24)
    return t
    return


def normalize(vec):
    vlen = math.sqrt(vec.x ** 2 + vec.y ** 2 + vec.z ** 2)
    vec.x = vec.x / vlen
    vec.y = vec.y / vlen
    vec.z = vec.z / vlen
    return vlen
    return


def lengthSquared(vec):
    vlen = vec.x ** 2 + vec.y ** 2 + vec.z ** 2
    return vlen
    return


def length(vec):
    vlen = math.sqrt(lengthSquared(vect))
    return vlen
    return


def dot(a, b):
    return a.x * b.x + a.y * b.y + a.z * b.z
    return


def vec_sub(a, b):

    class temp_vec:
        __module__ = __name__

    temp = temp_vec()
    temp.x = a.x - b.x
    temp.y = a.y - b.y
    temp.z = a.z - b.z
    return temp
    return


def isObjWithinFrust(basis_obj, target_obj, width):
    basis_pos = basis_obj.Position
    basis_dir = basis_obj.Orientation.BasisVector_Z
    target_pos = target_obj.Position
    vec_to_target = vec_sub(target_pos, basis_pos)
    len_to_target = normalize(vec_to_target)
    orientation_offset = dot(vec_to_target, basis_dir)
    if orientation_offset < 0:
        return (False, len_to_target)
    else:
        frust_width = width / 2
        angle = math.acos(orientation_offset) * (180 / math.pi)
        if angle < width:
            return (
             True, len_to_target)
        else:
            return (
             False, len_to_target)
    return


def GetSneakLevel(subject):
    sneak_lvl = subject.abilities[SneakGrantAbility]
    return sneak_lvl
    return


def SneakTest(subject):
    angle = 60.0
    max_dist = 2000.0
    min_dist = 300.0
    max_sneak_lvl = 50.0
    subjectPos = subject.Position
    components = (
     constants.Components.PlayerCharacter,)
    sneak_lvl = float(GetSneakLevel(subject))
    if sneak_lvl > max_sneak_lvl:
        sneak_lvl = max_sneak_lvl
    dist = min_dist + (max_dist - max_dist * (sneak_lvl / max_sneak_lvl))
    outputAbilityDebug('Sneaker detection range %f meters ' % (dist / 100.0))
    opponents = subject.PlayerCharacter.getEnemiesInRadius(dist)
    if len(opponents) == 0:
        return 0
    noiseLevel = subject.CharMvt.NoiseLevel
    stealthLevel = subject.CharMvt.StealthLevel
    for opponent in opponents:
        if opponent.locator != subject.locator and CanFight(subject, opponent):
            (inFrust, dist) = isObjWithinFrust(opponent, subject, angle)
            if inFrust:
                if physics.clearLine(opponent.Position, subject.Position):
                    stealthAwareness = opponent.CharMvt.StealthAwareness
                    outputAbilityDebug('SA(%i) v.s SL(%i) NL(%i) ' % (stealthAwareness, stealthLevel, noiseLevel))
                    if abilitylib.sneakTest(opponent.CharMvt.StealthAwareness, stealthLevel, noiseLevel, dist):
                        outputAbilityDebug('Sneaker Popped!')
                        SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_REVEALER_SNEAKER_REVEALED, 0, opponent.locator, subject.locator, 0)
                        SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_SNEAKER_SNEAKER_REVEALED, 0, opponent.locator, subject.locator, 0)
                        return 1

    return 0
    return


def InitialSneakTest(subject):
    angle = 60.0
    max_dist = 2000.0
    min_dist = 300.0
    max_sneak_lvl = 50.0
    subjectPos = subject.Position
    components = (
     constants.Components.PlayerCharacter,)
    sneak_lvl = float(GetSneakLevel(subject))
    if sneak_lvl > max_sneak_lvl:
        sneak_lvl = max_sneak_lvl
    dist = min_dist + (max_dist - max_dist * (sneak_lvl / max_sneak_lvl))
    outputAbilityDebug('Sneaker detection range %f meters ' % (dist / 100.0))
    opponents = subject.PlayerCharacter.getEnemiesInRadius(dist)
    if len(opponents) == 0:
        return 0
    for opponent in opponents:
        if opponent.locator != subject.locator and CanFight(subject, opponent):
            (inFrust, dist) = isObjWithinFrust(opponent, subject, angle)
            if inFrust:
                if physics.clearLine(opponent.Position, subject.Position):
                    return 1

    return 0
    return


def GetClosestCharacter(subject, distance):
    subjectPos = subject.Position
    components = (
     constants.Components.CharMovement,)
    outputAbilityDebug('Finding all characters in %f meters ' % distance)
    characters = physics.getObjectsInRadius(subjectPos.x, subjectPos.y, subjectPos.z, distance, components)
    if len(characters) == 0:
        return 0
    validCharacters = []
    characterDists = []
    index = 0
    for character in characters:
        if character.locator != subject.locator and CanFight(subject, character):
            if physics.clearLine(character.Position, subject.Position):
                characterPos = character.Position
                vector = vec_sub(characterPos, subjectPos)
                distFrom = lengthSquared(vector)
                newEntry = (
                 distFrom, index)
                characterDists.append(newEntry)
        index += 1

    if len(characterDists) == 0:
        return None
    characterDists.sort()
    clostestCharacter = characterDists[0]
    clostestCharacterIndex = clostestCharacter[1]
    return characters[clostestCharacterIndex]
    return


def GetLOSFriendlies(subject, distance):
    subjectPos = subject.Position
    components = (
     constants.Components.CharMovement,)
    outputAbilityDebug('Finding all friendly characters in %f meters ' % distance)
    characters = physics.getObjectsInRadius(subjectPos.x, subjectPos.y, subjectPos.z, distance, components)
    if len(characters) == 0:
        return 0
    validCharacters = []
    distSquared = distance * distance
    for character in characters:
        if character.locator != subject.locator and CanFight(subject, character):
            if physics.clearLine(character.Position, subject.Position):
                characterPos = character.Position
                vector = vec_sub(characterPos, subjectPos)
                distFrom = lengthSquared(vector)
                if distFrom <= distSquared:
                    validCharacters.append(character)

    return validCharacters
    return


def GetClosestEnemyInSight(subject, distance):
    subjectPos = subject.Position
    components = (constants.Components.CharMovement,)
    enemies = physics.getObjectsInRadius(subjectPos.x, subjectPos.y, subjectPos.z, distance, components)
    if len(enemies) == 0:
        return None
    nearestEnemy = None
    nearestDistance = distance * distance
    for enemy in enemies:
        if subject.CharMvt.isEnemy(enemy.locator):
            outputAbilityDebug('GetClosestEnemyInSight: found a valid enemy')
            enemyPos = enemy.Position
            if physics.clearLine(subjectPos, enemyPos):
                vector = vec_sub(enemyPos, subjectPos)
                distFrom = lengthSquared(vector)
                if distFrom <= nearestDistance:
                    nearestEnemy = enemy
                    nearestDistance = distFrom

    return nearestEnemy
    return


def turnOffAuras(subject):
    AuraAbilities = [
     CombatAura1Ability, CombatAura2Ability, DeflectBullets1Ability, 
     DeflectVirus1Ability, Miasma1Ability, StaticField1Ability, CodeShaperAbility, 
     DaemonBoostAbility, DaemonBoost2Ability, ProxyTechnicianAbility]
    for abil in AuraAbilities:
        if subject.hasAbility[abil]:
            subject.AbilityInv.deactivateAbility(abil)

    return


def GetHitPointsForLevel(level):
    return (level - 1) * 3 + 100
    return


return

# okay decompiling work/monolith_pyc\ability\utility.pyc
