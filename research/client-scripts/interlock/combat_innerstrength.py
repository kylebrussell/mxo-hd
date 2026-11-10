# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\interlock\combat_innerstrength.py
# Compiled at: 2006-03-30 16:29:39
import random, traceback, obj, combat_defines as CD, math, combat_utility as CU

def calcInnerStrengthWithSelfDefense(tactical_setting):
    if tactical_setting == constants.combat.POWER:
        return -20
    elif tactical_setting == constants.combat.SPEED:
        return -10
    elif tactical_setting == constants.combat.DEFENSE:
        return -10
    elif tactical_setting == constants.combat.BLOCK:
        return 10
    return 0
    return


def calcInnerStrengthWithMartialArt(tactical_setting):
    if tactical_setting == constants.combat.POWER:
        return -20
    elif tactical_setting == constants.combat.SPEED:
        return -10
    elif tactical_setting == constants.combat.DEFENSE:
        return -5
    elif tactical_setting == constants.combat.BLOCK:
        return 10
    return 0
    return


def calcInnerStrengthWithGun(tactical_setting):
    if tactical_setting == constants.combat.POWER:
        return -20
    elif tactical_setting == constants.combat.SPEED:
        return -10
    elif tactical_setting == constants.combat.DEFENSE:
        return -10
    elif tactical_setting == constants.combat.BLOCK:
        return 10
    return 0
    return


def calcInnerStrengthForWinner(tactic):
    innerstrength_amount = 0
    if CU.isUsingWeapon(tactic.itemID):
        innerstrength_amount += calcInnerStrengthWithGun(tactic.tacticalSetting)
    elif CU.isAbilityMartialArt(tactic.martialArt):
        innerstrength_amount += calcInnerStrengthWithMartialArt(tactic.tacticalSetting)
    else:
        innerstrength_amount += calcInnerStrengthWithSelfDefense(tactic.tacticalSetting)
    return innerstrength_amount
    return


def calcInnerStrengthForLoser(tactic):
    innerstrength_amount = 0
    if CU.isUsingWeapon(tactic.itemID):
        innerstrength_amount += calcInnerStrengthWithGun(tactic.tacticalSetting)
    elif CU.isAbilityMartialArt(tactic.martialArt):
        innerstrength_amount += calcInnerStrengthWithMartialArt(tactic.tacticalSetting)
    else:
        innerstrength_amount += calcInnerStrengthWithSelfDefense(tactic.tacticalSetting)
    return innerstrength_amount
    return


return

# okay decompiling work/monolith_pyc\interlock\combat_innerstrength.pyc
