# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\interlock\combat_free_attacks.py
# Compiled at: 2006-05-17 12:54:51
import random, traceback, obj, combat_defines as CD, math, combat_utility as CU, combat_innerstrength as CI, combat_resolution as CR, generic_free_attacks as GenFA, combat_calculations as CC

def PerformWeaponFreeAttack(attacker, defender, result):
    CU.outputCombatDebugMessage('weapon free attack func()', 2)
    action = Action_Idle
    item = attacker.itemType
    result.attackerMod = 0
    result.defenderMod = 0
    atrTotal = result.attackerRoll
    defTotal = result.defenderRoll
    CU.outputCombatDebugMessage('A rolls %d D rolls %d' % (atrTotal, defTotal), 2)
    if defender.canBeFreeAttacked:
        attackerWon = atrTotal > defTotal
    else:
        attackerWon = False
    if attackerWon:
        damage_total = CC.GetFreeAttackDamage(attacker.damageBonus, attacker.damageInfluence, attacker.itemDamage, attacker.itemDamageRange, attacker.itemWeaponSpeed, attacker.isEnergizedTactic, attacker.isPreciseBlows, attacker.tacticSetting, 1)
        CU.outputCombatDebugMessage('PerformWeaponFreeAttack: damage = %d.' % damage_total, 2)
        defender_total_toughness_percent = CC.GetDamageReductionValue(attacker.characterLevel, defender.characterLevel, defender.toughness + defender.rangedToughness, damage_total)
        str = 'PerformWeaponFreeAttack: Defender Resistance = %g' % defender_total_toughness_percent
        CU.outputCombatDebugMessage(str, 2)
        result.damageAbsorbed = float(damage_total) * defender_total_toughness_percent
        result.damage = damage_total - result.damageAbsorbed
        CU.outputCombatDebugMessage('PerformWeaponFreeAttack: Damage Resisted= %d' % result.damageAbsorbed, 2)
        if result.damage < 0:
            CU.outputCombatDebugMessage('PerformWeaponFreeAttack: Damage < 0: %d' % result.damage, 2)
            result.damage = 1
        CU.outputCombatDebugMessage('PerformWeaponFreeAttack: Final Damage: %d' % result.damage, 2)
        result.resultCode = constants.combat.FA_ITEM_PERFORMED
    else:
        result.damage = 0
        result.resultCode = constants.combat.FA_ITEM_FAILED
    if item == constants.combat.RANGED_SINGLE_PISTOL:
        action = Action_FreeAttackWeaponPistol
    elif item == constants.combat.RANGED_DOUBLE_PISTOL:
        action = Action_FreeAttackWeaponDualPistol
    elif item == constants.combat.RANGED_DBL_MACHINE_GUN:
        action = Action_FreeAttackWeaponDualSubM
    elif item == constants.combat.RANGED_MACHINE_GUN:
        action = Action_FreeAttackWeaponSubM
    elif item == constants.combat.RANGED_RIFLE:
        action = Action_FreeAttackWeaponRifle
    else:
        result.resultCode = constants.combat.FA_ERROR
        return False
    if result.resultCode == constants.combat.FA_ITEM_PERFORMED:
        attacker.gameObject.CharMvt.playScriptWithTarget(Stance_Aggro, action, defender.locator, HandgunsAbility)
    else:
        attacker.gameObject.CharMvt.playScriptWithTarget(Stance_Aggro, action, defender.locator, InvalidAbility)
    return True
    return


def GetMeleeFreeAttackScript(martialArt):
    CU.outputCombatDebugMessage('martial art style %d.' % martialArt, 2)
    karateArray = (
     Action_FreeAttackKarate1, Action_FreeAttackKarate2, Action_FreeAttackKarate3)
    kungfuArray = (Action_FreeAttackWushu1, Action_FreeAttackWushu2, Action_FreeAttackWushu3)
    aikidoArray = (Action_FreeAttackAikido1, Action_FreeAttackAikido2, Action_FreeAttackAikido3)
    selfdefenseArray = (Action_FreeAttackSelfD1, Action_FreeAttackSelfD2, Action_FreeAttackSelfD3)
    if martialArt == KarateAbility:
        action = random.choice(karateArray)
        CU.outputCombatDebugMessage('picking karate melee attack %d.' % action, 2)
    elif martialArt == KungFuAbility:
        action = random.choice(kungfuArray)
        CU.outputCombatDebugMessage('picking kungfu melee attack %d.' % action, 2)
    elif martialArt == AikidoAbility:
        action = random.choice(aikidoArray)
        CU.outputCombatDebugMessage('picking aikidomelee attack %d.' % action, 2)
    else:
        action = random.choice(selfdefenseArray)
        CU.outputCombatDebugMessage('picking self-d melee attack %d.' % action, 2)
    return action
    return


def PerformMeleeFreeAttack(attacker, defender, result):
    item = 0
    result.attackerMod = 0
    result.defenderMod = 0
    atrTotal = result.attackerRoll
    defTotal = result.defenderRoll
    CU.outputCombatDebugMessage('A rolls %d D rolls %d' % (atrTotal, defTotal), 2)
    if defender.canBeFreeAttacked:
        attackerWon = atrTotal > defTotal
    else:
        attackerWon = False
    if attackerWon:
        damage_total = CC.GetFreeAttackDamageMelee(attacker.damageBonus, attacker.itemWeaponSpeed, attacker.isEnergizedTactic, attacker.isPreciseBlows, attacker.tacticSetting, 1)
        CU.outputCombatDebugMessage('PerformMeleeFreeAttack: damage = %d.' % damage_total, 2)
        defender_total_toughness_percent = CC.GetDamageReductionValue(attacker.characterLevel, defender.characterLevel, defender.toughness + defender.meleeToughness, damage_total)
        str = 'PerformMeleeFreeAttack: Defender Resistance = %g' % defender_total_toughness_percent
        CU.outputCombatDebugMessage(str, 2)
        result.damageAbsorbed = float(damage_total) * defender_total_toughness_percent
        result.damage = damage_total - result.damageAbsorbed
        CU.outputCombatDebugMessage('PerformMeleeFreeAttack: Damage Resisted= %d' % result.damageAbsorbed, 2)
        if result.damage < 0:
            CU.outputCombatDebugMessage('PerformMeleeFreeAttack: Damage < 0: %d' % result.damage, 2)
            result.damage = 1
        CU.outputCombatDebugMessage('PerformMeleeFreeAttack: Final Damage: %d' % result.damage, 2)
        result.resultCode = constants.combat.FA_MELEE_PERFORMED
    else:
        result.damage = 0
        result.resultCode = constants.combat.FA_MELEE_FAILED
    action = GetMeleeFreeAttackScript(attacker.specialAbility)
    CU.outputCombatDebugMessage('playing melee script %d.' % action, 2)
    if result.resultCode == constants.combat.FA_MELEE_PERFORMED:
        attacker.gameObject.CharMvt.playScriptWithTarget(Stance_Aggro, action, defender.locator, HandgunsAbility)
    else:
        attacker.gameObject.CharMvt.playScriptWithTarget(Stance_Aggro, action, defender.locator, InvalidAbility)
    return True
    return


return

# okay decompiling work/monolith_pyc\interlock\combat_free_attacks.pyc
