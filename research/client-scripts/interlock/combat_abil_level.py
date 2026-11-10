# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\interlock\combat_abil_level.py
# Compiled at: 2006-03-30 16:29:39
import random, traceback, obj, combat_defines as CD, math, combat_utility as CU, combat_innerstrength as CI

def getTacticalInitiativeBonus(tacticalSetting):
    tactical_initiatives = [
     20, 10, 0]
    if tacticalSetting >= constants.combat.DEFENSE and tacticalSetting <= constants.combat.SPEED:
        return tactical_initiatives[tacticalSetting]
    return 0
    return


_MartialArtList = [KarateAbility, KungFuAbility, AikidoAbility]

def abilityLevel(player, ability):
    if player.hasAbility[ability]:
        return CU.safeAbVal(player.abilities[ability])
    else:
        return -10000
    return


def getBaseAbilityLevel(player):
    if player.hasAbility[AwakenedAbility]:
        return (
         AwakenedAbility, abilityLevel(player, AwakenedAbility))
    return (
     AwakenedAbility, 0)
    return


def GetMasteryLevel(player, ability):
    if ability == InvalidAbility:
        return 0
    return 0
    return


def GetItemAbilityLevel(equippedItemID, equippedItem, player):
    use_ability = InvalidAbility
    use_ability_level = 0
    if equippedItem == constants.combat.RANGED_SINGLE_PISTOL:
        if player.hasAbility[HandgunsAbility]:
            use_ability = HandgunsAbility
    elif equippedItem == constants.combat.RANGED_DOUBLE_PISTOL:
        if player.hasAbility[HandgunsAbility]:
            use_ability = HandgunsAbility
    elif equippedItem == constants.combat.RANGED_RIFLE or equippedItem == constants.combat.RANGED_SHOTGUN:
        if player.hasAbility[RiflesAbility]:
            use_ability = RiflesAbility
    elif equippedItem == constants.combat.RANGED_MACHINE_GUN:
        if player.hasAbility[SubmachinegunsAbility]:
            use_ability = SubmachinegunsAbility
    if use_ability != InvalidAbility:
        use_ability_level = player.abilities[use_ability]
    use_ability_level += GetMasteryLevel(player, use_ability)
    return (
     use_ability, use_ability_level)
    return


def SelectItemAbilityLevel(equippedItemID, equippedItem, player, result):
    (use_ability, use_ability_level) = GetItemAbilityLevel(equippedItemID, equippedItem, player)
    result.itemAbility = use_ability
    result.itemAbilityLevel = use_ability_level
    result.itemID = equippedItemID
    result.itemType = equippedItem
    return


def SelectCombatAbilityLevel(tacticalSetting, requestedMartialArt, player, result):
    result.initiative = player.Level + getTacticalInitiativeBonus(tacticalSetting)
    return


def isUsingWeapon(equippedItem):
    if equippedItem == constants.combat.RANGED_DOUBLE_PISTOL:
        CU.outputCombatDebugMessage('weapon is dual pistol: %d ' % equippedItem, 10)
        return True
    elif equippedItem == constants.combat.RANGED_SINGLE_PISTOL:
        CU.outputCombatDebugMessage('weapon is pistol: %d ' % equippedItem, 10)
        return True
    elif equippedItem == constants.combat.RANGED_SHOTGUN:
        CU.outputCombatDebugMessage('weapon is shotgun: %d ' % equippedItem, 10)
        return True
    elif equippedItem == constants.combat.RANGED_MACHINE_GUN:
        CU.outputCombatDebugMessage('weapon is machine gun: %d ' % equippedItem, 10)
        return True
    elif equippedItem == constants.combat.RANGED_RIFLE:
        CU.outputCombatDebugMessage('weapon is rifle: %d ' % equippedItem, 10)
        return True
    elif equippedItem == constants.combat.RANGED_DBL_MACHINE_GUN:
        CU.outputCombatDebugMessage('weapon is dual submahine gun: %d ' % equippedItem, 10)
        return True
    if equippedItem == 0:
        CU.outputCombatDebugMessage('weapon type invalid: %d ' % equippedItem, 10)
    else:
        CU.outputCombatDebugMessage('weapon type not found: %d ' % equippedItem, 10)
    return False
    return


_PRIMARY_TACTICAL_SETTING_SYNERGY_MOD = 25
_SECONDARY_TACTICAL_SETTING_SYNERGY_MOD = 15

def getMartialArtAbilityMod(ability, tacticalSetting):
    return 0
    return


def getItemAbilityMod(itemAbility, tacticalSetting):
    return 0
    return


def SetCombatTargetSettings(player, desiredSettings, result):
    SelectCombatAbilityLevel(desiredSettings.tacticalSetting, desiredSettings.martialArt, player, result)
    SelectItemAbilityLevel(desiredSettings.equippedItemID, desiredSettings.equippedItem, player, result)
    if desiredSettings.specialMove != InvalidAbility:
        if player.hasAbility[desiredSettings.specialMove]:
            result.specialMove = desiredSettings.specialMove
            result.specialMoveLevel = player.abilities[desiredSettings.specialMove]
    result.tacticalSetting = desiredSettings.tacticalSetting
    return


def GetFreeAttackAbility(player, equippedItemID, equippedItem, itemAbility, itemAbilityLevel):
    (itemAbility, itemAbilityLevel) = GetItemAbilityLevel(player, equippedItemID, equippedItem)
    return


return

# okay decompiling work/monolith_pyc\interlock\combat_abil_level.pyc
