# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\interlock\combat_utility.py
# Compiled at: 2007-11-13 02:02:54
import random, traceback, obj, combat_defines as CD, math

def outputToAll(msg):
    discovery.outputDebugString(msg)
    discovery.serverPrint(msg)
    return


def outputCombatDebugMessage(debug_test, warning_level):
    if consolevar.CombatVerbosity is None:
        return
    if consolevar.CombatVerbosity >= warning_level:
        outputToAll(debug_test)
    return


def outputCombatDebugMessageAll(debug_test, warning_level, obj1=None, obj2=None):
    outputCombatDebugMessage(debug_test, warning_level)
    if consolevar.SendClientCombatData >= warning_level:
        if obj1 is not None and obj1.locator is None:
            print 'obj1.locator is None'
        if obj1 is not None and obj1.locator is not None:
            discovery.clientSystemMessage2(obj1.locator, debug_test, constants.Chat.CT_SYS_DEBUG)
        if obj2 is not None and obj2.locator is not None:
            discovery.clientSystemMessage2(obj2.locator, debug_test, constants.Chat.CT_SYS_DEBUG)
    return


def outputCombatDebugMessageOnMatch(debug_test, warning_level):
    if consolevar.CombatVerbosity == warning_level:
        outputToAll(debug_test)
    return


def isAbilityMartialArt(ability):
    if ability == AikidoAbility:
        return True
    elif ability == KarateAbility:
        return True
    elif ability == KungFuAbility:
        return True
    else:
        return False
    return


def isPreferredTactical(s1, s2):
    if s2 == constants.combat.SPEED:
        return s1 == constants.combat.POWER
    elif s2 == constants.combat.POWER:
        return s1 == constants.combat.DEFENSE
    elif s2 == constants.combat.DEFENSE:
        return s1 == constants.combat.SPEED
    elif s2 == constants.combat.BLOCK:
        return s1 != constants.combat.BLOCK
    return


def isPreferredFinishingTactical(attacker, defender):
    if attacker == constants.combat.DEFENSE:
        return defender == constants.combat.POWER
    if attacker == constants.combat.SPEED:
        return defender == constants.combat.DEFENSE
    if attacker == constants.combat.POWER:
        return defender == constants.combat.BLOCK
    return


def isUsingWeapon(equippedItem):
    if equippedItem == constants.combat.RANGED_DOUBLE_PISTOL:
        outputCombatDebugMessage('weapon is dual pistol: %d ' % equippedItem, 20)
        return True
    elif equippedItem == constants.combat.RANGED_SINGLE_PISTOL:
        outputCombatDebugMessage('weapon is pistol: %d ' % equippedItem, 20)
        return True
    elif equippedItem == constants.combat.RANGED_SHOTGUN:
        outputCombatDebugMessage('weapon is shotgun: %d ' % equippedItem, 20)
        return True
    elif equippedItem == constants.combat.RANGED_MACHINE_GUN:
        outputCombatDebugMessage('weapon is machine gun: %d ' % equippedItem, 20)
        return True
    elif equippedItem == constants.combat.RANGED_RIFLE:
        outputCombatDebugMessage('weapon is rifle: %d ' % equippedItem, 20)
        return True
    elif equippedItem == constants.combat.RANGED_DBL_MACHINE_GUN:
        outputCombatDebugMessage('weapon is dual submahine gun: %d ' % equippedItem, 20)
        return True
    if equippedItem == 0:
        outputCombatDebugMessage('weapon type invalid: %d ' % equippedItem, 20)
    else:
        outputCombatDebugMessage('weapon type not found: %d ' % equippedItem, 20)
    return False
    return


def isEscapeAbility(ability):
    if ability == InvalidAbility or ability == NoneAbility:
        return False
    if abilitylib.isEscapeAbility(ability) == 1:
        return True
    return False
    return


def isSpecialMove(ability):
    if ability == InvalidAbility or ability == NoneAbility:
        return False
    if isEscapeAbility(ability) == True:
        return False
    return True
    return


def isPlayerWithdrawing(player):
    if player.isAttemptingWithdraw:
        return True
    if isEscapeAbility(player.requestedSpecialMove) == True:
        return True
    return False
    return


def isPlayerCombatProne(player):
    if player.proneState & constants.combat_prone.COMBAT_PRONE_MASK:
        outputCombatDebugMessage('player %d combat prone 0x%x 0x%x' % (player.slot, player.proneState, CD.ABILITY_CONTROLLED_MASK), 5)
        return True
    outputCombatDebugMessage('player %d not combat prone 0x%x 0x%x' % (player.slot, player.proneState, CD.ABILITY_CONTROLLED_MASK), 5)
    return False
    return


def isPlayerBeingGanged(player):
    return player.opponentCount > consolevar.CombatShortMoveCount
    return


def determineBulletTime(attacker, defender, result, special_move=False):
    roll = random.randrange(100)
    outputCombatDebugMessage('bullet time determine: %d' % defender.combatantType, 7)
    if defender.combatantType & constants.combat.BOSS:
        outputCombatDebugMessage('BOSS: %d' % defender.combatantType, 7)
        if roll < 60:
            result.bullet_time = 1
    if defender.combatantType & constants.combat.PVPCAPTAIN:
        outputCombatDebugMessage('PVPCAPTAIN: %d' % defender.combatantType, 7)
        if roll < 60:
            result.bullet_time = 1
    if defender.combatantType & constants.combat.MISSIONTARGET:
        outputCombatDebugMessage('MISSIONTARGET: %d' % defender.combatantType, 7)
        if roll < 60:
            result.bullet_time = 1
    if defender.combatantType & constants.combat.PLAYER and attacker.combatantType & constants.combat.PLAYER:
        outputCombatDebugMessage('PLAYERs: %d' % defender.combatantType, 7)
        if roll < 75:
            result.bullet_time = 1
    if roll < 10:
        outputCombatDebugMessage('normal: %d' % defender.combatantType, 7)
        result.bullet_time = 1
    outputCombatDebugMessage('bullet time enabled(%d): %d' % (result.bullet_time, roll), 7)
    return


def finishingMoveAllowed(attacker, defender, result, checkDefender):
    dmger = attacker
    vic = defender
    dmgDone = result.loserDamage
    if checkDefender:
        dmger = defender
        vic = attacker
        dmgDone = result.winnerDamage
    if result.distance != constants.combat.SHORT_RANGE:
        return False
    if isPlayerCombatProne(vic):
        return False
    if dmgDone < vic.health:
        return False
    determineBulletTime(dmger, vic, result)
    return True
    return


def killingMoveAllowed(winner, loser, result):
    if result.loserDamage >= loser.health:
        determineBulletTime(winner, loser, result)
        return True
    return False
    return


def possibleDisarmMove(winner, loser, result):
    if result.outcomeFlag != constants.combat.ATTACKER_WINS:
        return False
    if isUsingWeapon(loser.equippedItemType) and winner.opponentCount > consolevar.CombatShortMoveCount:
        return result.distance == constants.combat.SHORT_RANGE
    return False
    return


def shortMoveAllowed(winner, loser, result):
    if result.outcomeFlag == constants.combat.DRAW or result.outcomeFlag == constants.combat.ATTACKER_WINS or result.outcomeFlag == constants.combat.FINISHING_MOVE:
        outputCombatDebugMessage('short move w:%d l:%d count:%d?' % (winner.opponentCount, loser.opponentCount, consolevar.CombatShortMoveCount), 2)
        if winner.opponentCount <= consolevar.CombatShortMoveCount and loser.opponentCount <= consolevar.CombatShortMoveCount:
            outputCombatDebugMessage('combatants not in multi-on-one', 2)
            return False
        if possibleDisarmMove(winner, loser, result):
            return False
        outputCombatDebugMessage('at short range? %d' % (result.distance == constants.combat.SHORT_RANGE), 2)
        if isUsingWeapon(winner.equippedItemType):
            if winner.opponentCount <= consolevar.CombatShortMoveCount:
                return False
            else:
                return True
        else:
            return True
    else:
        return False
    return


def safeAbVal(possible):
    if possible is None:
        return 0
    return possible
    return


return

# okay decompiling work/monolith_pyc\interlock\combat_utility.pyc
