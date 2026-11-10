# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\effects.py
# Compiled at: 2006-03-30 16:29:38
import combat_defines as CD, ability.utility as Utility, ltfxmap as FX
from ability.defines import *
import stringtable_client as StringTable

def CorrodeItem(item, corrosionAmount):
    if item:
        stability = item.stability
        Utility.outputAbilityDebug('CorrodeItem: item has ' + `stability` + ' stability.')
        if stability == 0:
            Utility.outputAbilityDebug('CorrodeItem: Item is industructable.')
            return
        stability -= corrosionAmount
        item.stability = stability
        if stability <= 0 or stability > 7:
            Utility.outputAbilityDebug('CorrodeItem: Item has no stability left.')
        return True
    else:
        Utility.outputAbilityDebug('CorrodeItem: No item given to corrode.')
        return False
    return


def DestroyItem(subject, item, slot):
    if Utility.IsAPlayer(subject):
        Utility.outputAbilityDebug('DestroyItem: slot' + `slot` + "'s item.stability = " + `(item.stability)` + '.')
        if item.stability == 0:
            Utility.outputAbilityDebug('DestroyItem: Item is indestructable.')
            return DEFLECTED
        Utility.outputAbilityDebug("DestroyItem: Player's item destroyed.")
        subject.Inventory.rmvItem(slot)
    elif slot == 109:
        Utility.outputAbilityDebug("DestroyItem: NPC's weapon destroyed.")
        subject.AI.rmvWeapon()
    else:
        Utility.outputAbilityDebug("DestroyItem: NPC's doesn't have that slot.")
    return SUCCESS
    return


DISABLE_FX = FX.FX_CHARACTER_GENERIC_ABILITY_DOWNGRADE

def DisableDependents(subject, src_abil, duration):
    abil_list = abilitylib.getAbilityDependents(src_abil)
    Utility.outputAbilityDebug('DisableDependents:')
    if len(abil_list):
        for abil in abil_list:
            ab_name = Utility.getAbilityName(abil)
            Utility.outputAbilityDebug('%s ' % ab_name)
            if subject.hasAbility[abil]:
                Utility.outputAbilityDebug(' - disabling %s   ' % ab_name)
                procs = ('GenericDisableInit', 'GenericDisableTerm', '', '', '')
                subject.AbilityInv.addTempModProcs(procs, abilitylib.Disabled, abil, duration, 0, 0)

    return


def DisableAbility(subject, targetAbility, src, duration, casterLocator, srcIsObject, sendMessage=True):
    DisableDependents(subject, targetAbility, duration)
    ab_name = Utility.getAbilityName(targetAbility)
    Utility.outputAbilityDebug('DisableAbility: - disabling %s   ' % ab_name)
    procs = (
     'GenericDisableInit', 'GenericDisableTerm', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, abilitylib.Disabled, targetAbility, duration, 0, 0)
    type_id = 0
    if srcIsObject:
        type_id = targetAbility
    else:
        type_id = discovery.abilityIDToGameObjectID(targetAbility)
    discovery.playEffect(subject.locator, subject.locator, DISABLE_FX, 0)
    return


return

# okay decompiling work/monolith_pyc\ability\effects.pyc
