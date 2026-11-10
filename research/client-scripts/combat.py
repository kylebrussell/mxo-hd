# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\combat.py
# Compiled at: 2006-03-30 16:29:38
import random, traceback, obj, combat_defines as CD, math, interlock.combat_abil_level as CombatAbilLevel, interlock.combat_modifiers as CombatMods, interlock.combat_innerstrength as CombatIS, interlock.combat_resolution as CombatRes, interlock.combat_utility as CU, interlock.combat_free_attacks as CombatFA, combat_calculations

def SetCombatTargetSettings(player, desiredSettings, result):
    CombatAbilLevel.SetCombatTargetSettings(player, desiredSettings, result)
    return


def SetFreeAttackAbility(player, equippedItemID, equippedItem, itemAbility, itemAbilityLevel):
    CombatAbilLevel.GetFreeAttackAbility(player, equippedItemID, equippedItem, itemAbility, itemAbilityLevel)
    return


def IsATactic(tacticalSettings):
    if tacticalSettings.elementType == constants.combat.QT_NORMAL or tacticalSettings.elementType == constants.combat.QT_SPECIAL_TACTIC:
        return True
    return False
    return


def GetTacticalPointsGained(player, tacticalSettings):
    if not IsATactic(tacticalSettings):
        return 0
    if tacticalSettings.tacticalSetting == constants.combat.BLOCK:
        return 0
    if tacticalSettings.tacticType == constants.combat.TT_INVALID:
        return 1
    elif tacticalSettings.tacticType == constants.combat.TT_PRECISE:
        return 1
    elif tacticalSettings.tacticType == constants.combat.TT_ENERGIZED:
        return 2
    return


def GetInnerStrengthUsed(player, tacticalSetting):
    inner_strength_used = 0
    if tacticalSetting == constants.combat.BLOCK:
        if consolevar.GainISFromBlocks:
            inner_strength_used = -10
        else:
            inner_strength_used = 0
    else:
        inner_strength_used = 0
    return inner_strength_used
    return


def DoProneStatesMatch(player_flags, move_flags):
    player_combat_flags = player_flags & CD.COMBAT_CONTROLLED_MASK
    if player_combat_flags == 0 and move_flags == 0:
        return True
    if player_combat_flags & move_flags > 0:
        return True
    return False
    return


def DetermineDominantProneType(flags):
    if flags & constants.combat_prone.LAYING_FACEDOWN:
        return constants.combat_prone.LAYING_FACEDOWN
    elif flags & constants.combat_prone.LAYING_FACEUP:
        return constants.combat_prone.LAYING_FACEUP
    elif flags & constants.combat_prone.VISUAL_FACEDOWN:
        return constants.combat_prone.VISUAL_FACEDOWN
    elif flags & constants.combat_prone.VISUAL_FACEUP:
        return constants.combat_prone.VISUAL_FACEUP
    elif flags & constants.combat_prone.I_LB_TRANSITION:
        return constants.combat_prone.I_LB_TRANSITION
    elif flags & constants.combat_prone.V_LB_TRANSITION:
        return constants.combat_prone.V_LB_TRANSITION
    elif flags & constants.combat_prone.BLIND:
        return constants.combat_prone.BLIND
    elif flags & constants.combat_prone.STUNNED:
        return constants.combat_prone.STUNNED
    elif flags & constants.combat_prone.CONFUSED:
        return constants.combat_prone.CONFUSED
    elif flags & constants.combat_prone.ENRAGED:
        return constants.combat_prone.ENRAGED
    return 0
    return


def HasProneStateExpired(flags, roundStarted, currentRound):
    CU.outputCombatDebugMessage('prone flags %d %d' % (flags, roundStarted), 2)
    if consolevar.CombatEnableProneAttacks == 0:
        CU.outputCombatDebugMessage('prone attacks not enabled %d' % consolevar.CombatEnableProneAttacks, 2)
        return True
    if flags >= constants.combat_prone.COMBAT_PRONE_MASK:
        CU.outputCombatDebugMessage('prone state flags outside of combat controlled %d >= %d' % (flags, int(constants.combat_prone.COMBAT_PRONE_MASK)), 2)
        return True
    if flags & CD.PRONE_RECOVERY_INSTANT:
        CU.outputCombatDebugMessage('instant prone recovery called for', 2)
        return True
    CU.outputCombatDebugMessage('round %d %d' % (currentRound, roundStarted), 2)
    if currentRound - roundStarted > 1:
        CU.outputCombatDebugMessage('prone state expired due to round %d %d' % (currentRound, roundStarted), 2)
        return True
    else:
        CU.outputCombatDebugMessage('it is not time for prone recovery', 2)
        return False
    CU.outputCombatDebugMessage('Ended prone recovery check ... should not be here', 2)
    return False
    return


def PerformFreeAttack(attacker, defender, result):
    if attacker.itemType != constants.combat.INVALID_WEAPON:
        CU.outputCombatDebugMessage('weapon free attack', 2)
        return CombatFA.PerformWeaponFreeAttack(attacker, defender, result)
    elif consolevar.MeleeFreeFire != 0:
        CU.outputCombatDebugMessage('melee free attack', 2)
        return CombatFA.PerformMeleeFreeAttack(attacker, defender, result)
    return False
    return


def ResolveCombat(player1, player2, result):
    CU.outputCombatDebugMessage('\n\n\n--start new combat resolution--', 3)
    attacker = CombatRes.determineAttacker(player1, player2, result.distance)
    if attacker is player1:
        defender = player2
    else:
        defender = player1
    CU.outputCombatDebugMessage('attacker: %d defender: %d' % (player1.slot, player2.slot), 3)
    (withdraw_processed, failed_to_withdraw) = CombatRes.determineResultOfWithdrawalRequest(attacker, defender, result)
    if withdraw_processed == True and failed_to_withdraw == False:
        CU.outputCombatDebugMessage('successfully withdrew ---\n\n', 3)
        return
    CombatRes.determineResultForStandardCombat(attacker, defender, failed_to_withdraw, result)
    CU.outputCombatDebugMessage('end new combat resolution ---', 3)
    return


return

# okay decompiling work/monolith_pyc\combat.pyc
