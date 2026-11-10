# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\interlock\combat_modifiers.py
# Compiled at: 2006-03-30 16:29:39
import random, traceback, obj, combat_defines as CD, math, combat_utility as CU
_COMBAT_PREFERRED_TACTICAL_BONUS = 0

def getTacticalMod(tacticalSetting, opponentTacticalSetting):
    if CU.isPreferredTactical(tacticalSetting, opponentTacticalSetting):
        return _COMBAT_PREFERRED_TACTICAL_BONUS
    else:
        return 0
    return


def getTacticSpecificArmorPiercingPercent(tacticalSetting):
    if tacticalSetting == constants.combat.POWER:
        return 0.5
    return 1.0
    return


def getBaseRangedValue(player):
    modifier = 0
    if player.equippedItemType == constants.combat.RANGED_SINGLE_PISTOL or player.equippedItemType == constants.combat.RANGED_DOUBLE_PISTOL:
        modifier = 5
    elif player.equippedItemType == constants.combat.RANGED_SHOTGUN:
        outputToAll('Shotgun currently not supported')
    elif player.equippedItemType == constants.combat.RANGED_MACHINE_GUN:
        modifier = 10
    elif player.equippedItemType == constants.combat.RANGED_RIFLE:
        modifier = 15
    return modifier
    return


def getRangedVsMeleeAttackModifier(attacker, defender, result):
    if CU.isUsingWeapon(attacker.equippedItemType) == False or CU.isUsingWeapon(defender.equippedItemType) == True:
        return 0
    base_modifier = 0
    if defender.opponentCount > 1:
        if attacker.allowFiringIntoMeleePenalty:
            base_modifier = -15
        else:
            CU.outputCombatDebugMessage('Firing into melee penalty negated due to ability', 3)
    modifier = getBaseRangedValue(attacker)
    if result.distance == constants.combat.SHORT_RANGE:
        if attacker.allowFiringAtMeleeRangePenalty == 0:
            CU.outputCombatDebugMessage('Firing at short range penalty negated due to ability', 3)
            return base_modifier
        modifier = modifier * -1
    elif defender.allowMeleeAtRangedPenalty == 0:
        CU.outputCombatDebugMessage('Melee at ranged penalty negated by ability', 3)
        return base_modifier
    return base_modifier + modifier
    return


def getRangedVsMeleeDefenseModifier(attacker, defender, result):
    if CU.isUsingWeapon(defender.equippedItemType) == False or CU.isUsingWeapon(attacker.equippedItemType) == True:
        return 0
    base_modifier = 0
    if attacker.opponentCount > 1:
        if defender.allowFiringIntoMeleePenalty:
            base_modifier = -15
        else:
            CU.outputCombatDebugMessage('Firing into melee penalty negated due to ability', 3)
    modifier = getBaseRangedValue(defender)
    if result.distance == constants.combat.SHORT_RANGE:
        if defender.allowFiringAtMeleeRangePenalty == 0:
            CU.outputCombatDebugMessage('Firing at short range penalty negated by ability', 3)
            return base_modifier
        modifier = modifier * -1
    elif attacker.allowMeleeAtRangedPenalty == 0:
        CU.outputCombatDebugMessage('Melee at ranged penalty negated by ability', 3)
        return base_modifier
    return base_modifier + modifier
    return


def getOpponentCountModifier(player):
    if player.opponentCount > consolevar.CombatMultiOnOneCount:
        if player.allowMultiOnOnePenalty:
            return player.opponentCount * -5
        else:
            CU.outputCombatDebugMessage('Multi-on-one penalty negated due to ability for player %d' % player.slot, 3)
            return 0
    else:
        return 0
    return


def getMultiOnOnePenalty(player):
    if player.opponentCount > consolevar.CombatMultiOnOneCount:
        if player.allowMultiOnOnePenalty:
            return player.opponentsFought * -5
        elif player.opponentsFought < 3:
            CU.outputCombatDebugMessage('Multi-on-one penalty negated due to ability for player %d' % player.slot, 3)
            return 0
        else:
            return (player.opponentsFought - 2) * -5
    else:
        return 0
    return


def getAttackerMods(attacker, defender, result):
    multiMod = getMultiOnOnePenalty(attacker)
    rangedMod = getRangedVsMeleeAttackModifier(attacker, defender, result)
    out_modifier = multiMod + rangedMod
    return out_modifier
    return


def getDefenderMods(attacker, defender, result):
    multiMod = getMultiOnOnePenalty(defender)
    rangedMod = getRangedVsMeleeDefenseModifier(attacker, defender, result)
    out_modifier = multiMod + rangedMod
    return out_modifier
    return


return

# okay decompiling work/monolith_pyc\interlock\combat_modifiers.pyc
