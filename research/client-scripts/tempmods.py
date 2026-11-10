# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\tempmods.py
# Compiled at: 2009-02-11 02:01:37
from ability.defines import *
import ltfxmap as FX, combat_defines as CD, ability.effects as Effects, ability.utility as Utility, stringtable_client as StringTable, random, time
dispelExemptAbilities = {DeathEffectAbility: 1, SittingEffectAbility: 1, WeaponReqsNotMetEffectAbility: 1, CombatEffectAbility: 1, NPCShowInterlockImmuneAbility: 1, Event2_0FlyVirusImmunityAbility: 1, ControlImmunityTimerAbility: 1, KarateAbility: 1, SelfDefenseAbility: 1, HackerCombatStyleAbility: 1, AikidoAbility: 1, HandgunsAbility: 1, KungFuAbility: 1, RiflesAbility: 1, SubmachinegunsAbility: 1}
dispelExemptItems = {47114: 1, 47115: 1, 42693: 1, 42696: 1, 38118: 1, 38119: 1}

def outputTempModDebug(msg):
    if consolevar.AbilityDebugPrint > 1:
        discovery.outputDebugString(msg)
        discovery.serverPrint(msg)
    return


CONST_REGEN_FX = FX.FX_NPC_REGEN
VIRUSCRASH_FX = FX.FX_CHARACTER_HEALTH_DRAIN
CONFUSE_COMBAT_MOD = -10
GENERICSTUN_ATTACK_MOD = -20
VIRUSCRASH_CORRODE_VALUE = 2
FLIT_GUN_DAMAGE = 20
VIRUSCRASH_CORRODE_CHANCEVALUE = 100
MOBIUS_IMMUNITY_CHANCE = 1
CRYPTOS_CHARACTER = 42974

def AbilityDeactivateCallback(tempMod, subject):
    abilToDeactivate = tempMod.ParentAbility
    py_fun = subject.AbilityInv.getDeactivationFunction(abilToDeactivate)
    if py_fun is not None:
        py_fun(subject)
    return


def KillNPCCallback(tempMod, subject):
    subject.CharMvt.killSelf()
    return


def disableFiringInMeleePenInit(tempMod, subject):
    subject.Interlock.disableFiringIntoMeleePenalty(1)
    Utility.outputAbilityDebug('Disabling Firing into Melee Penalty')
    return


def disableFiringInMeleePenTerm(tempMod, subject):
    subject.Interlock.disableFiringIntoMeleePenalty(0)
    Utility.outputAbilityDebug('Enabling Firing into Melee Penalty')
    return


def disableFiringMeleeRangePenInit(tempMod, subject):
    subject.Interlock.disableFiringAtMeleeRangePenalty(1)
    Utility.outputAbilityDebug('Disabling Firing At Melee Range Penelty')
    return


def disableFiringMeleeRangePenTerm(tempMod, subject):
    subject.Interlock.disableFiringAtMeleeRangePenalty(0)
    Utility.outputAbilityDebug('Enabling Firing At Melee Range Penelty')
    return


def UpdateCombatStyle(tempMod, subject):
    subject.AbilityInv.updateCombatStyle()
    return


def disableMultiOnOnePenaltyInit(tempMod, subject):
    subject.Interlock.disableMultiOnOnePenalty(1)
    Utility.outputAbilityDebug('Disabling Multi On One Penalty.')
    return


def disableMultiOnOnePenaltyTerm(tempMod, subject):
    subject.Interlock.disableMultiOnOnePenalty(0)
    Utility.outputAbilityDebug('Enabling Multi On One Penalty.')
    return


def Concealment_RemoveConcealmentStealth(tempMod, subject):
    Utility.outputAbilityDebug('Removing concealments stealth')
    subject.CharMvt.removeDissemblingType(kSneak)
    return


def GenericConstHealthRegenEvent(tempMod, subject):
    health = subject.AbilityInv.Health
    max_health = subject.AbilityInv.MaxHealth
    boost = tempMod.Value
    outputTempModDebug('GenericConstHealthRegenEvent: health, %d' % health)
    outputTempModDebug('GenericConstHealthRegenEvent: maxhealth, %d' % max_health)
    if health < max_health:
        health = health + boost
        if health > max_health:
            boost = max_health - subject.AbilityInv.Health
            outputTempModDebug('GenericConstHealthRegenEvent: healed for %d ' % boost)
            subject.Interlock.giveHealth(subject.locator, boost)
        else:
            outputTempModDebug('GenericConstHealthRegenEvent: healed for %d ' % boost)
            subject.Interlock.giveHealth(subject.locator, boost)
        outputTempModDebug('GenericConstHealthRegenEvent: boost, %d' % boost)
    return


def GenericConstHealthRegenEventWithReturn(tempMod, subject):
    health = subject.AbilityInv.Health
    max_health = subject.AbilityInv.MaxHealth
    boost = tempMod.Value
    outputTempModDebug('GenericConstHealthRegenEvent: health, %d' % health)
    outputTempModDebug('GenericConstHealthRegenEvent: maxhealth, %d' % max_health)
    if health < max_health:
        health = health + boost
        if health > max_health:
            boost = max_health - subject.AbilityInv.Health
            outputTempModDebug('GenericConstHealthRegenEvent: healed for %d ' % boost)
            subject.Interlock.giveHealth(subject.locator, boost)
        else:
            outputTempModDebug('GenericConstHealthRegenEvent: healed for %d ' % boost)
            subject.Interlock.giveHealth(subject.locator, boost)
        outputTempModDebug('GenericConstHealthRegenEvent: boost, %d' % boost)
        return boost
    return 0
    return


def DisruptConnectionTerm(tempMod, subject):
    subject.CharMvt.removeDissemblingType(kDisruptedConnection)
    return


def MessageCloudInit(tempMod, subject):
    outputTempModDebug('Message Cloud Debuff Init ')
    outputTempModDebug(str(subject.AbilityInv.SelectionRangeDebuff))
    subject.AbilityInv.SelectionRangeDebuff = float(tempMod.Value)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_MESSAGE_CLOUD_MIDDLE, 0)
    outputTempModDebug(str(subject.AbilityInv.SelectionRangeDebuff))
    outputTempModDebug('--------------------------')
    return


def MessageCloudTerm(tempMod, subject):
    outputTempModDebug('Message Cloud Debuff Term ')
    subject.AbilityInv.SelectionRangeDebuff = 0.0
    outputTempModDebug(str(subject.AbilityInv.SelectionRangeDebuff))
    outputTempModDebug('--------------------------')
    return


def MessageCloudPulse(tempMod, subject):
    outputTempModDebug('Message Cloud Pulse ')
    pos = subject.Model.Position
    radius = 1200
    subject.Abilities.applyAbilityToArea(MessageCloudAbility, 10, pos.x, pos.y, pos.z, radius)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_MESSAGE_CLOUD_MIDDLE, 0)
    return


MessageCloudPulse.Interval = 1.0

def DetectionPulse(tempMod, subject):
    outputTempModDebug('TempMod Pulse ')
    if subject.AbilityInv.hasTempMod(tempMod.ParentAbility, tempMod.ParentAbility):
        return
    procs = (
     '', '', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, tempMod.ParentAbility, 1, 0, 1)
    pos = subject.Position
    radius = 1200
    res = random.randrange(100) + subject.abilities[tempMod.ParentAbility] * 4
    subject.AbilityInv.applyAbilityToArea(tempMod.ParentAbility, res, pos.x, pos.y, pos.z, radius)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_WEAPON_CONCEALMENT_COUNTERMEASURE, 0)
    return


def killSimulacra(tempMod, subject):
    subject.SimulacraMaster.killSimulacra(tempMod.Value)
    return


def NegativeConditionSweepPulse(tempMod, subject):
    temp_mods = subject.AbilityInv.getTempMods()
    from time import ctime
    Utility.outputAbilityDebug('Sweep: %s' % ctime())
    parentAbils = {}
    if tempMod.ParentAbility == Antibiotic1Ability or tempMod.ParentAbility == Antibiotic2Ability or tempMod.ParentAbility == Antibiotic3Ability or tempMod.ParentAbility == Antibiotic4Ability or tempMod.ParentAbility == Antibiotic5Ability:
        if subject.hasAbility[ZombieVirus2BaseAbility] or subject.hasAbility[ZombieVirus3BaseAbility]:
            subject.AbilityInv.removeAbilityGrant(ZombieVirus2BaseAbility)
            subject.AbilityInv.removeAbilityGrant(ZombieVirus3BaseAbility)
            subject.AbilityInv.removeAbilityGrant(ZombieVirus2BiteAbility)
            subject.AbilityInv.removeAbilityGrant(ZombieVirus3BiteAbility)
            subject.AbilityInv.removeAbilityGrant(NPCKilledOnlyByHolyWaterAbility)
            subject.AbilityInv.deactivateAbility(RSICaptureAbility)
            subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
            subject.AbilityInv.deactivateAbility(RSICapExecAbility)
            subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno89Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno90Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno91Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno92Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno93Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno94Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
            subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
            subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
            subject.AbilityInv.deactivateAbility(RSICapValfAbility)
            subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
            subject.CharMvt.revertToRSI()
    for temp_mod in temp_mods:
        if (temp_mod.IsAbility and abilitylib.isADebuffAbility(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility) or temp_mod.IsItem and not dispelExemptItems.has_key(temp_mod.ParentAbility)) and temp_mod.IsEquippingEffect == 0:
            parentAbils = GetParentAbils(subject, parentAbils, temp_mod)

    for temp_mod in parentAbils.values():
        if (temp_mod.IsAbility and abilitylib.isADebuffAbility(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility) or temp_mod.IsItem and not dispelExemptItems.has_key(temp_mod.ParentAbility)) and temp_mod.IsEquippingEffect == 0:
            roll = random.randrange(100)
            Utility.outputAbilityDebug('NegativeConditionSweepPulse: Is debuff %d and rolling chance was %d' % (temp_mod.ParentAbility, roll))
            if roll < tempMod.Value:
                Utility.outputAbilityDebug('Removing Debuff %s ' % abilitylib.getAbilityName(temp_mod.ParentAbility))
                myMods = subject.AbilityInv.getTempMods(temp_mod.ParentAbility, InvalidAbility)
                if not tempMod.IsItem:
                    if temp_mod.IsItem:
                        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_REMOVE_DEBUFF_ITEM, tempMod.ParentAbility, subject.locator, subject.locator, temp_mod.ParentAbility)
                    else:
                        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_REMOVE_DEBUFF_ABILITY, tempMod.ParentAbility, subject.locator, subject.locator, 1 << 31 | temp_mod.ParentAbility << 10)
                    for child_mod in myMods:
                        subject.AbilityInv.removeTempMod(child_mod.ParentAbility, child_mod.TargetAbility)

                discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_ANTIVIRAL_SWEEP, 0)

    return


def NegativeConditionSweepToggle(tempMod, subject):
    temp_mods = subject.AbilityInv.getTempMods()
    from time import ctime
    parentAbils = {}
    for temp_mod in temp_mods:
        if (temp_mod.IsAbility and abilitylib.isADebuffAbility(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility) or temp_mod.IsItem and not dispelExemptItems.has_key(temp_mod.ParentAbility)) and temp_mod.IsEquippingEffect == 0:
            parentAbils = GetParentAbils(subject, parentAbils, temp_mod)

    for temp_mod in parentAbils.values():
        if (temp_mod.IsAbility and abilitylib.isADebuffAbility(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility) or temp_mod.IsItem and not dispelExemptItems.has_key(temp_mod.ParentAbility)) and temp_mod.IsEquippingEffect == 0:
            roll = random.randrange(100)
            if roll < tempMod.Value:
                myMods = subject.AbilityInv.getTempMods(temp_mod.ParentAbility, InvalidAbility)
                if not tempMod.IsItem:
                    for child_mod in myMods:
                        subject.AbilityInv.removeTempMod(child_mod.ParentAbility, child_mod.TargetAbility)

    return


def NegativeConditionSweepInstant(tempMod, subject):
    temp_mods = subject.AbilityInv.getTempMods()
    from time import ctime
    parentAbils = {}
    for temp_mod in temp_mods:
        if (temp_mod.IsAbility and abilitylib.isADebuffAbility(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility) or temp_mod.IsItem and not dispelExemptItems.has_key(temp_mod.ParentAbility)) and temp_mod.IsEquippingEffect == 0:
            parentAbils = GetParentAbils(subject, parentAbils, temp_mod)

    for temp_mod in parentAbils.values():
        if (temp_mod.IsAbility and abilitylib.isADebuffAbility(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility) or temp_mod.IsItem and not dispelExemptItems.has_key(temp_mod.ParentAbility)) and temp_mod.IsEquippingEffect == 0:
            myMods = subject.AbilityInv.getTempMods(temp_mod.ParentAbility, InvalidAbility)
            if not tempMod.IsItem:
                for child_mod in myMods:
                    subject.AbilityInv.removeTempMod(child_mod.ParentAbility, child_mod.TargetAbility)

    return


def GetParentAbils(subject, parentAbils, tempMod, takeBuffs=0):
    if tempMod.ParentAbility == 0 or tempMod.ParentAbility == InvalidAbility:
        Utility.outputAbilityDebug('Adding%d, %d' % (tempMod.ParentAbility, tempMod.TargetAbility))
        parentAbils[tempMod.ParentAbility] = tempMod
        return parentAbils
    parentMods = subject.AbilityInv.getTempMods(InvalidAbility, tempMod.ParentAbility)
    numMods = len(parentMods)
    if numMods == 0:
        if tempMod.IsItem:
            parentAbils[tempMod.ParentAbility + InvalidAbility] = tempMod
        else:
            parentAbils[tempMod.ParentAbility] = tempMod
        Utility.outputAbilityDebug('Adding3 %d, %d' % (tempMod.ParentAbility, tempMod.TargetAbility))
    else:
        for parent_mod in parentMods:
            Utility.outputAbilityDebug('FOR%d, %d' % (parent_mod.ParentAbility, parent_mod.TargetAbility))
            if parent_mod.IsItem:
                Utility.outputAbilityDebug('IsItem')
                if abilitylib.isADebuffAbility(parent_mod.TargetAbility) or parent_mod.Value < 0 and not takeBuffs:
                    Utility.outputAbilityDebug('Adding2 %d, %d, %d' % (parent_mod.ParentAbility, parent_mod.TargetAbility, parent_mod.ParentAbility + InvalidAbility))
                    parentAbils[parent_mod.ParentAbility + InvalidAbility] = parent_mod
                    return parentAbils
                elif abilitylib.isABuffAbility(parent_mod.TargetAbility) or parent_mod.Value > 0 and takeBuffs:
                    Utility.outputAbilityDebug('Adding2 %d, %d, %d' % (parent_mod.ParentAbility, parent_mod.TargetAbility, parent_mod.ParentAbility + InvalidAbility))
                    parentAbils[parent_mod.ParentAbility + InvalidAbility] = parent_mod
                    return parentAbils
            else:
                parentAbils = GetParentAbils(subject, parentAbils, parent_mod)

    return parentAbils
    return


def UpgradeSweepPulse(tempMod, subject):
    temp_mods = subject.AbilityInv.getTempMods()
    Utility.outputAbilityDebug(' Buff Sweeper %d ' % tempMod.Value)
    parentAbils = {}
    for temp_mod in temp_mods:
        if temp_mod.IsAbility and abilitylib.isABuffAbility(temp_mod.ParentAbility) and temp_mod.IsEquippingEffect == 0 and abilitylib.canRemoveBuff(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility):
            parentAbils = GetParentAbils(subject, parentAbils, temp_mod, 1)

    for temp_mod in parentAbils.values():
        if random.randrange(100) < tempMod.Value:
            Utility.outputAbilityDebug('Removing Buff %s ' % abilitylib.getAbilityName(temp_mod.ParentAbility))
            myMods = subject.AbilityInv.getTempMods(temp_mod.ParentAbility, InvalidAbility)
            if not tempMod.IsItem:
                if temp_mod.IsItem:
                    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_COUNTER_UPGRADE_1_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, temp_mod.ParentAbility)
                    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_COUNTER_UPGRADE_1_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, temp_mod.ParentAbility)
                else:
                    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_COUNTER_UPGRADE_1_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, 1 << 31 | temp_mod.ParentAbility << 10)
                    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_COUNTER_UPGRADE_1_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, 1 << 31 | temp_mod.ParentAbility << 10)
                for child_mod in myMods:
                    subject.AbilityInv.removeTempMod(child_mod.ParentAbility, child_mod.TargetAbility)

                Utility.outputAbilityDebug(' Toggling off %s' % abilitylib.getAbilityName(temp_mod.ParentAbility))
                subject.AbilityInv.deactivateAbility(temp_mod.ParentAbility)

    return


def RemoveRandomBuff(tempMod, subject):
    temp_mods = subject.AbilityInv.getTempMods()
    Utility.outputAbilityDebug(' Scanning all buffs ')
    _buffs = []
    parentAbils = {}
    for temp_mod in temp_mods:
        if temp_mod.IsAbility and abilitylib.isABuffAbility(temp_mod.ParentAbility) and temp_mod.IsEquippingEffect == 0 and abilitylib.canRemoveBuff(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility):
            parentAbils = GetParentAbils(subject, parentAbils, temp_mod, 1)

    for temp_mod in parentAbils.values():
        if temp_mod.IsAbility and abilitylib.isABuffAbility(temp_mod.ParentAbility) and temp_mod.IsEquippingEffect == 0 and abilitylib.canRemoveBuff(temp_mod.ParentAbility) and not dispelExemptAbilities.has_key(temp_mod.ParentAbility):
            _buffs.append(temp_mod)

    _num = len(_buffs)
    Utility.outputAbilityDebug(' Buffs found: %d ' % _num)
    if _num > 1:
        _target = random.randrange(0, _num - 1)
    else:
        _target = 0
    if _num > 0:
        Utility.outputAbilityDebug(' Removing: %s %d %d %d %d ' % (abilitylib.getAbilityName(_buffs[_target].ParentAbility), _buffs[_target].ParentAbility, _buffs[_target].ParentAbility << 10, 1 << 31, 1 << 31 | _buffs[_target].ParentAbility << 10))
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_COUNTER_UPGRADE_1_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, 1 << 31 | _buffs[_target].ParentAbility << 10)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_COUNTER_UPGRADE_1_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, 1 << 31 | _buffs[_target].ParentAbility << 10)
        myMods = subject.AbilityInv.getTempMods(_buffs[_target].ParentAbility, InvalidAbility)
        subject.AbilityInv.removeTempMod(_buffs[_target].ParentAbility, _buffs[_target].TargetAbility)
        for child_mod in myMods:
            subject.AbilityInv.removeTempMod(child_mod.ParentAbility, child_mod.TargetAbility)

        Utility.outputAbilityDebug(' Toggling off %s' % abilitylib.getAbilityName(_buffs[_target].ParentAbility))
        subject.AbilityInv.deactivateAbility(_buffs[_target].ParentAbility)
        Utility.outputAbilityDebug(' Removed: %s %d %d %d %d successfully' % (abilitylib.getAbilityName(_buffs[_target].ParentAbility), _buffs[_target].ParentAbility, _buffs[_target].ParentAbility << 10, 1 << 31, 1 << 31 | _buffs[_target].ParentAbility << 10))
    else:
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_CANCEL_UPGRADE_1_FAIL_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_CANCEL_UPGRADE_1_FAIL_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
    return


def RemoveAllBuffs(tempMod, subject):
    temp_mods = subject.AbilityInv.getTempMods()
    Utility.outputAbilityDebug(' Scanning for all buffs ')
    buffDict = {}
    for temp_mod in temp_mods:
        if temp_mod.IsAbility and abilitylib.isABuffAbility(temp_mod.ParentAbility) and temp_mod.IsEquippingEffect == 0 and not dispelExemptAbilities.has_key(temp_mod.ParentAbility):
            Utility.outputAbilityDebug(' Removing: %s ' % abilitylib.getAbilityName(temp_mod.ParentAbility))
            buffDict[temp_mod.ParentAbility] = 1
            subject.AbilityInv.removeTempMod(temp_mod.ParentAbility, temp_mod.TargetAbility)

    for buff in buffDict:
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_COUNTER_UPGRADE_1_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, 1 << 31 | buff << 10)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_COUNTER_UPGRADE_1_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, 1 << 31 | buff << 10)
        if abilitylib.isToggleAbility(buff):
            Utility.outputAbilityDebug(' Toggling off ')
            subject.AbilityInv.deactivateAbility(buff)

    return


def ImmortalityInit(tempMod, subject):
    Utility.outputAbilityDebug('ImmortalityInit: Enabling Immortality. ')
    subject.Interlock.cantDie(1)
    return


def ImmortalityTerm(tempMod, subject):
    Utility.outputAbilityDebug('ImmortalityInit: Disabling Immortality. ')
    subject.Interlock.cantDie(0)
    return


def CantForceCombatInit(tempMod, subject):
    Utility.outputAbilityDebug('CantForceCombatInit: Disabled force combat. ')
    subject.Interlock.setAllowedToForceCombat(0)
    return


def CantForceCombatTerm(tempMod, subject):
    Utility.outputAbilityDebug('CantForceCombatTerm: Enabled force combat. ')
    subject.Interlock.setAllowedToForceCombat(1)
    return


def CantFreeAttackInit(tempMod, subject):
    Utility.outputAbilityDebug('CantFreeAttackInit: Disabled free attacks. ')
    subject.Interlock.setAllowedToFreeAttack(0)
    return


def CantFreeAttackTerm(tempMod, subject):
    Utility.outputAbilityDebug('CantFreeAttackTerm: Enabled free attacks. ')
    subject.Interlock.setAllowedToFreeAttack(1)
    return


def CantBeForceIntoCombatInit(tempMod, subject):
    Utility.outputAbilityDebug('CantBeForceIntoCombatInit: Disabled chance to get into combat. ')
    subject.Interlock.setAllowedToBeForceIntoCombat(0)
    return


def CantBeForceIntoCombatTerm(tempMod, subject):
    Utility.outputAbilityDebug('CantBeForceIntoCombatTerm: Enabled chance to get into combat. ')
    subject.Interlock.setAllowedToBeForceIntoCombat(1)
    return


def CantBeFreeAttackedInit(tempMod, subject):
    Utility.outputAbilityDebug('CantBeFreeAttackedInit: Disabled chance to get free attack. ')
    subject.Interlock.setAllowedToBeFreeAttacked(0)
    return


def CantBeFreeAttackedTerm(tempMod, subject):
    Utility.outputAbilityDebug('CantBeFreeAttackedTerm: EEnabled chance to get free attacked. ')
    subject.Interlock.setAllowedToBeFreeAttacked(1)
    return


def SneakInit(tempMod, subject):
    subject.CharMvt.activateSneak()
    Utility.turnOffAuras(subject)
    if subject.AbilityInv.CurExclusiveAbility != tempMod.ParentAbility:
        subject.AbilityInv.CurExclusiveAbility = tempMod.ParentAbility
    return


def SneakTerm(tempMod, subject):
    subject.CharMvt.deactivateSneak()
    return


def SneakUpdate(tempMod, subject):
    subject.CharMvt.activateSneak()
    if Utility.SneakTest(subject):
        subject.AbilityInv.turnOffAbility(tempMod.ParentAbility, 1, 1, 1)
    return


def ConcealUpdate(tempMod, subject):
    subject.CharMvt.playScript(Stance_Invis, subject.CharMvt.Action)
    if Utility.SneakTest(subject):
        subject.AbilityInv.turnOffAbility(tempMod.ParentAbility, 1, 1, 1)
    return


def NPCBoostUnderlingsUpdate(tempMod, subject):
    Utility.outputAbilityDebug('Boosting underlings. ')
    radius = 400
    loc = subject.Position
    if not Utility.IsAnNPC(subject):
        return
    type = subject.AI.getCaptainUnderlingType()
    subject.AbilityInv.applyAbilityToArea(NPCBoostUnderlingsAbility, type, loc.x, loc.y, loc.z, radius)
    return


NPCBoostUnderlingsUpdate.Interval = 10.0

def GenericDisableInit(tempMod, subject):
    subject.AbilityInv.disableAbility(tempMod.TargetAbility)
    return


def GenericDisableTerm(tempMod, subject):
    subject.AbilityInv.enableAbility(tempMod.TargetAbility)
    return


def packVal(hi, lo):
    return hi << 16 | lo
    return


def unpackVal(v):
    return (
     v >> 16, v & 65535)
    return


def BufferOverflowUpdate(tempMod, subject):
    subject.AbilityInv.removeFromBuffBuffer(tempMod.ParentAbility)
    maxBufSize = subject.AbilityInv.getBuffBufferMax()
    curBufSize = subject.AbilityInv.getBuffBuffer()
    gap = maxBufSize - curBufSize
    if gap >= tempMod.Value:
        subject.AbilityInv.addToBuffBuffer(tempMod.Value, tempMod.ParentAbility)
    else:
        subject.AbilityInv.addToBuffBuffer(gap, tempMod.ParentAbility)
    return


def BufferOverflowInit(tempMod, subject):
    subject.AbilityInv.addToBuffBuffer(0, tempMod.ParentAbility)
    BufferOverflowUpdate(tempMod, subject)
    return


def BufferOverflowTerm(tempMod, subject):
    subject.AbilityInv.removeFromBuffBuffer(tempMod.ParentAbility)
    return


def BufferOverflowEvent(tempMod, subject):
    val = unpackVal(tempMod.Value)
    if 0 == val[0]:
        tempMod.CancelThisEvent()
        return
    BufferOverflowUpdate(tempMod, subject)
    return


def EvadeCombatInit(tempMod, subject):
    outputTempModDebug('EvadeCombatInit:   ')
    subject.Interlock.setDefenseModeToPassive()
    subject.Interlock.createEvadeShield()
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_COMBAT_EVASION_ON, tempMod.ParentAbility, subject.Locator, 0, 0)
    return


def EvadeCombatTerm(tempMod, subject):
    outputTempModDebug('EvadeCombatTerm: ')
    subject.Interlock.setDefenseModeToAutoDefend()
    if subject.Interlock.hasEvadeShield():
        subject.Interlock.destroyEvadeShield()
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_COMBAT_EVASION_OFF, tempMod.ParentAbility, subject.Locator, 0, 0)
    return


def EvadeCombatEvent(tempMod, subject):
    outputTempModDebug('EvadeCombatEvent: taking %d IS' % tempMod.Value)
    if subject.AbilityInv.InnerStrengthAvailable < tempMod.Value:
        subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
        return
    if tempMod.Value:
        subject.AbilityInv.takeInnerStrength(tempMod.Value)
    return


def ShadowObjectTerm(tempMod, subject):
    Utility.outputAbilityDebug('ShadowObjectTerm: Deactivate')
    subject.Interlock.setAllowedToUseTimedUsables(1)
    subject.Interlock.setEvadeState(0)
    subject.CharMvt.removeDissemblingType(kShadowObject)
    subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_SHADOW_OBJECT_RAMP_DOWN, 0)
    return


def ShadowObjectInit(tempMod, subject):
    Utility.outputAbilityDebug('ShadowObjectInit: Activate')
    subject.CharMvt.addDissemblingType(kShadowObject)
    subject.Interlock.setEvadeState(1)
    subject.Interlock.setAllowedToUseTimedUsables(0)
    Utility.turnOffAuras(subject)
    return


def DisguiseTerm(tempMod, subject):
    Utility.outputAbilityDebug('DisquiseTerm: Deactivate')
    subject.CharMvt.removeDissemblingType(kDisguised)
    if subject.hasAbility[ZombieVirus2BaseAbility]:
        zmod = subject.AbilityInv.getTempMods(ZombieVirus2BaseAbility, NullTargetAbility)
        if zmod[0].Value == 0:
            model = 1476398198
        else:
            model = 1476398197
        subject.CharMvt.setModel(model)
        subject.AbilityInv.enableTitles(1)
        return
    if subject.hasAbility[SmithVirus1BaseAbility] or subject.hasAbility[SmithVirus2BaseAbility]:
        model = discovery.getGameObjectPropValue(1 << 31 | SmithVirus1BaseAbility << 10, 'ModelOverride')
        if model is not None:
            subject.CharMvt.setModel(model)
        subject.AbilityInv.enableTitles(1)
        return
    if subject.hasAbility[ZombieVirus3BaseAbility]:
        zmod = subject.AbilityInv.getTempMods(ZombieVirus3BaseAbility, NullTargetAbility)
        if zmod[0].Value == 0:
            model = 1476398198
        else:
            model = 1476398197
        subject.CharMvt.setModel(model)
        subject.AbilityInv.enableTitles(1)
        return
    subject.CharMvt.revertToRSI()
    procs = ('', 'PlayScript', '', '')
    subject.AbilityInv.enableTitles(1)
    return


def LocateDataNodeTerm(tempMod, subject):
    Utility.outputAbilityDebug('LocateDataNodeTerm: Deactivate')
    subject.AbilityInv.deactivateLocateDataNode()
    subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
    return


def VisLocDataNode(tempMod, subject):
    subject.AbilityInv.visualLocateDataNode(tempMod.Value, tempMod.ParentAbility, subject.hasAbility[AdvancedDataNodeGogglesAbility])
    return


def AudLocDataNode(tempMod, subject):
    subject.AbilityInv.audioLocateDataNode(tempMod.Value)
    return


def GhostStrikeInit(tempMod, subject):
    Utility.outputAbilityDebug('GhostStrikeInit: Activate')
    subject.CharMvt.addDissemblingType(kInvisible)
    procs = ('InvisibilityInit', 'InvisibilityTerm', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, InvisibilityGrantAbility, NullTargetAbility, tempMod.Value, 0, 1)
    return


def InvisibilityInit(tempMod, subject):
    subject.CharMvt.playScript(Stance_Invis, subject.CharMvt.Action)
    Utility.turnOffAuras(subject)
    return


def InvisibilityTerm(tempMod, subject):
    Utility.outputAbilityDebug('Invisibility_Termination: Deactivate')
    subject.CharMvt.removeDissemblingType(kInvisible)
    subject.CharMvt.playScript(Stance_Stand, subject.CharMvt.Action)
    return


def PowerlessInit(tempMod, subject):
    Utility.outputAbilityDebug('PowerlessInit: Subject no longer Powerless!')
    subject.Interlock.setAllowedToPerformSpecialMoves(0)
    subject.Interlock.setAllowedToUseAbilities(0)
    return


def PowerlessTerm(tempMod, subject):
    Utility.outputAbilityDebug('PowerlessTerm: Subject no longer Powerless!')
    subject.Interlock.setAllowedToPerformSpecialMoves(1)
    subject.Interlock.setAllowedToUseAbilities(1)
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_STATE_DISABLE_TARGET, 0, 0, subject.locator, StringTable.ID_STATE_POWERLESS)
    return


def StunInit(tempMod, subject):
    Utility.outputAbilityDebug('EnableStun: Subject stunned ')
    StunMezCommonInit(tempMod, subject)
    return


def StunTerm(tempMod, subject):
    Utility.outputAbilityDebug('StunTerm: Subject unstunned!')
    caster = 0
    abilityID = 0
    StunMezCommonTerm(tempMod, subject)
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_STATE_DISABLE_TARGET, abilityID, caster, subject.locator, StringTable.ID_STATE_STUNNED)
    return


def EnrageInit(tempMod, subject):
    Utility.outputAbilityDebug('EnrageInit: Subject enrage for ')
    subject.Interlock.setAllowedToWithdrawFlag(0)
    subject.Interlock.setAllowedToChangeTargets(0)
    subject.Interlock.setAllowedToISBlock(0)
    return


def GenericEnrageUpdate(tempMod, subject):
    if Utility.IsAPlayer(subject):
        return
    outputTempModDebug('GenericEnrage: Update')
    if subject.Interlock.isInCombat:
        outputTempModDebug('GenericEnrage: Update (NPC is in combat already)')
        return
    victim = Utility.GetClosestEnemyInSight(subject, 1000)
    if victim is not None:
        subject.Interlock.attack(victim.locator, 1)
    else:
        outputTempModDebug('GenericEnrage: Update (no victim found)')
    return


def EnrageTerm(tempMod, subject):
    Utility.outputAbilityDebug('EnrageTerm: Subject unenrage!')
    subject.Interlock.setAllowedToWithdrawFlag(1)
    subject.Interlock.setAllowedToChangeTargets(1)
    subject.Interlock.setAllowedToISBlock(1)
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_STATE_DISABLE_TARGET, 0, 0, subject.locator, StringTable.ID_STATE_ENRAGED)
    return


def BlindnessInit(tempMod, subject):
    Utility.outputAbilityDebug('BlindnessInit: Target blinded!')
    return


def BlindnessTerm(tempMod, subject):
    Utility.outputAbilityDebug('BlindnessTerm: subject unblinded!')
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_STATE_DISABLE_TARGET, 0, 0, subject.locator, StringTable.ID_STATE_BLIND)
    return


def RootInit(tempMod, subject):
    Utility.outputAbilityDebug('RootInit: Subject rooted!')
    return


def RootTerm(tempMod, subject):
    Utility.outputAbilityDebug('RootTerm: Subject is no longer rooted!')
    subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_STATE_DISABLE_TARGET, 0, 0, subject.locator, StringTable.ID_STATE_ROOTING)
    return


def PlayRootFX(tempMod, subject):
    tempMod.Value = 671091179
    PlayFX(tempMod, subject)
    return


def PlayEnergizedFX(tempMod, subject):
    if subject.Interlock.isInCombat:
        tempMod.Value = 671090444
        PlayFX(tempMod, subject)
    return


def PlayPreciseFX(tempMod, subject):
    if subject.Interlock.isInCombat:
        tempMod.Value = 671090431
        PlayFX(tempMod, subject)
    return


def PlayAdrenalineFX(tempMod, subject):
    if subject.Interlock.isInCombat:
        discovery.playEffect(subject.locator, subject.locator, 671091276, 0)
    else:
        subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
    return


def PlayPreciseFX(tempMod, subject):
    if subject.Interlock.isInCombat:
        tempMod.Value = 671090431
        PlayFX(tempMod, subject)
    return


def PlayAdrenalineFX(tempMod, subject):
    if subject.Interlock.isInCombat:
        discovery.playEffect(subject.locator, subject.locator, 671091276, 0)
    else:
        subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
    return


def WoundingInit(tempMod, subject):
    Utility.outputAbilityDebug('EnableWounding: Subject wounded !')
    return


def WoundingEventNoKill(tempMod, subject):
    outputTempModDebug("WoundedCallBack: Wounded takes %d health, but won't die" % tempMod.Value)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_BLEEDING_LOOP, 0)
    return


def WoundingTerm(tempMod, subject):
    Utility.outputAbilityDebug('WoundingTerm: Subject no longer wounded !')
    return


def ImpairConnectionInit(tempMod, subject):
    Utility.outputAbilityDebug('ImpairConnectionInit: Turning on dissembling type!')
    subject.CharMvt.addDissemblingType(kDisruptedConnection)
    return


def ImpairConnectionTerm(tempMod, subject):
    Utility.outputAbilityDebug('ImpairConnectionInit: Turning off dissembling type !')
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_STATE_DISABLE_IMPAIR_CONNECTION_TARGET, 0, 0, subject.locator, 0)
    subject.CharMvt.removeDissemblingType(kDisruptedConnection)
    return


def StunMezCommonInit(tempMod, subject):
    subject.Interlock.setAllowedToPerformSpecialMoves(0)
    subject.Interlock.setAllowedToUseItems(0)
    subject.Interlock.setAllowedToUseWeapons(0)
    subject.Interlock.setAllowedToFreeAttack(0)
    subject.Interlock.setAllowedToForceCombat(0)
    subject.Interlock.setAllowedToUseAbilities(0)
    subject.Interlock.setAllowedToUseUsables(0)
    return


def StunMezCommonTerm(tempMod, subject):
    subject.Interlock.setAllowedToPerformSpecialMoves(1)
    subject.Interlock.setAllowedToUseItems(1)
    subject.Interlock.setAllowedToUseWeapons(1)
    subject.Interlock.setAllowedToFreeAttack(1)
    subject.Interlock.setAllowedToForceCombat(1)
    subject.Interlock.setAllowedToUseAbilities(1)
    subject.Interlock.setAllowedToUseUsables(1)
    return


def MezzingInit(tempMod, subject):
    Utility.outputAbilityDebug('EnableMezzed: Subject mezzed !')
    StunMezCommonInit(tempMod, subject)
    return


def MezzingTerm(tempMod, subject):
    Utility.outputAbilityDebug('DisableMezzed: Subject is no longer mezzed!')
    StunMezCommonTerm(tempMod, subject)
    subject.AbilityInv.addTempMod(MezAbility, MovementSpeedAbility, 0, 2)
    temp_mods = subject.AbilityInv.getTempMods(4096, MezAbility)
    for temp_mod in temp_mods:
        Utility.outputAbilityDebug('MezzingTerm: Removing temp mod from abil %d' % temp_mod.ParentAbility)
        subject.AbilityInv.removeTempMod(temp_mod.ParentAbility, temp_mod.TargetAbility)

    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_STATE_DISABLE_TARGET, 0, 0, subject.locator, StringTable.ID_STATE_MEZZED)
    return


def AddMezState(tempMod, subject):
    Utility.outputAbilityDebug('AddMezState: Attempting to mez target!')
    subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, MezAbility, 0, 0, float(tempMod.Value))
    return


def ConfuseInit(tempMod, subject):
    Utility.outputAbilityDebug('ConfuseInit: Subject confused !')
    subject.Interlock.setAllowedToWithdrawFlag(0)
    subject.Interlock.setAllowedToChangeTacticalSetting(0)
    subject.Interlock.setAllowedToPerformSpecialMoves(0)
    return


def ConfuseUpdate(tempMod, subject):
    outputTempModDebug('GenericConfuse: Update')
    subject.Interlock.randomizeQueue()
    return


def ConfuseTerm(tempMod, subject):
    Utility.outputAbilityDebug('ConfuseTerm: Subject unconfused!')
    subject.Interlock.setAllowedToWithdrawFlag(1)
    subject.Interlock.setAllowedToChangeTacticalSetting(1)
    subject.Interlock.setAllowedToPerformSpecialMoves(1)
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_STATE_DISABLE_TARGET, 0, 0, subject.locator, StringTable.ID_STATE_CONFUSED)
    return


def ViralDOT(tempMod, subject):
    duration = tempMod.Duration
    if duration == 0:
        duration = 1
    discovery.playEffect(subject.locator, subject.locator, VIRUSCRASH_FX, 0)
    updateInterval = tempMod.UpdateInterval
    dps = float(tempMod.Value) / duration
    curDamage = int(dps * updateInterval + 0.5)
    Utility.outputAbilityDebug('DOT: damage = ' + `curDamage` + '.')
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, curDamage, tempMod.ParentAbility, Viral, tempMod.IsAbility, tempMod.TargetAbility)
    return


def PhysicalDOT(tempMod, subject):
    duration = tempMod.Duration
    if duration == 0:
        duration = 1
    discovery.playEffect(subject.locator, subject.locator, VIRUSCRASH_FX, 0)
    updateInterval = tempMod.UpdateInterval
    dps = float(tempMod.Value) / duration
    curDamage = int(dps * updateInterval + 0.5)
    Utility.outputAbilityDebug('DOT: damage = ' + `curDamage` + '.')
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, curDamage, tempMod.ParentAbility, Melee, tempMod.IsAbility, tempMod.TargetAbility)
    return


def RangedDOT(tempMod, subject):
    duration = tempMod.Duration
    if duration == 0:
        duration = 1
    discovery.playEffect(subject.locator, subject.locator, VIRUSCRASH_FX, 0)
    updateInterval = tempMod.UpdateInterval
    dps = float(tempMod.Value) / duration
    curDamage = int(dps * updateInterval + 0.5)
    Utility.outputAbilityDebug('DOT: damage = ' + `curDamage` + '.')
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, curDamage, tempMod.ParentAbility, Ranged, tempMod.IsAbility, tempMod.TargetAbility)
    return


def ThrownDOT(tempMod, subject):
    duration = tempMod.Duration
    if duration == 0:
        duration = 1
    discovery.playEffect(subject.locator, subject.locator, VIRUSCRASH_FX, 0)
    updateInterval = tempMod.UpdateInterval
    dps = float(tempMod.Value) / duration
    curDamage = int(dps * updateInterval + 0.5)
    Utility.outputAbilityDebug('DOT: damage = ' + `curDamage` + '.')
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, curDamage, tempMod.ParentAbility, Throw, tempMod.IsAbility, tempMod.TargetAbility)
    return


def StealthDOT(tempMod, subject):
    duration = tempMod.Duration
    if duration == 0:
        duration = 1
    discovery.playEffect(subject.locator, subject.locator, VIRUSCRASH_FX, 0)
    updateInterval = tempMod.UpdateInterval
    dps = float(tempMod.Value) / duration
    curDamage = int(dps * updateInterval + 0.5)
    Utility.outputAbilityDebug('DOT: damage = ' + `curDamage` + '.')
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, curDamage, tempMod.ParentAbility, Stealth, tempMod.IsAbility, tempMod.TargetAbility)
    return


def NeutralDOT(tempMod, subject):
    duration = tempMod.Duration
    if duration == 0:
        duration = 1
    discovery.playEffect(subject.locator, subject.locator, VIRUSCRASH_FX, 0)
    updateInterval = tempMod.UpdateInterval
    dps = float(tempMod.Value) / duration
    curDamage = int(dps * updateInterval + 0.5)
    Utility.outputAbilityDebug('DOT: damage = ' + `curDamage` + ' Value = ' + `(tempMod.Value)` + '.')
    Utility.outputAbilityDebug('DOT: dps = ' + `dps` + ' interval = ' + `updateInterval` + ' duration = ' + `(tempMod.Duration)`)
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, curDamage, tempMod.ParentAbility, Neutral, tempMod.IsAbility, tempMod.TargetAbility)
    return


def HOT(tempMod, subject):
    health = subject.AbilityInv.Health
    max_health = subject.AbilityInv.MaxHealth
    updateInterval = tempMod.UpdateInterval
    dps = float(tempMod.Value) / tempMod.Duration
    boost = int(dps * updateInterval + 0.5)
    Utility.outputAbilityDebug('HOT: heal = ' + `boost` + '.')
    outputTempModDebug('HOT: health, %d' % health)
    if health < max_health:
        newHealth = health + boost
        if newHealth > max_health:
            newBoost = max_health - newHealth
            outputTempModDebug('HOT: healed for %d ' % newBoost)
            subject.AbilityInv.dealAbilityDamage(tempMod.Source, newBoost, tempMod.ParentAbility, NeutralHeal, tempMod.IsAbility)
        else:
            outputTempModDebug('HOT: healed for %d ' % boost)
            subject.AbilityInv.dealAbilityDamage(tempMod.Source, boost, tempMod.ParentAbility, NeutralHeal, tempMod.IsAbility)
    return


def ISDrainWithAbilityDeactivation(tempMod, subject):
    if subject.AbilityInv.InnerStrengthAvailable < tempMod.Value:
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_ISDRAIN_DEACTIVATE, tempMod.ParentAbility, tempMod.Source, subject.locator, tempMod.Value - subject.AbilityInv.InnerStrengthAvailable)
        subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
        return
    if tempMod.Value:
        outputTempModDebug('ISDrainWithAbilityDeactivation: taking %d IS' % tempMod.Value)
        subject.AbilityInv.takeInnerStrength(tempMod.Value)
    return


def ISDrain(tempMod, subject):
    if subject.AbilityInv.InnerStrengthAvailable < tempMod.Value:
        return
    if tempMod.Value:
        outputTempModDebug('ISDrain: taking %d IS' % tempMod.Value)
        subject.AbilityInv.takeInnerStrength(tempMod.Value)
    return


def UpdateTapDataNode(tempMod, subject):
    outputTempModDebug('Update Tap Data Node : ' + `(tempMod.Value)`)
    subject.AbilityInv.updateTapDataNode(tempMod.Value)
    return


def TermTapDataNode(tempMod, subject):
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TAP_DATA_NODE_DEACTIVATION, TapDataNodeAbility, subject.locator, 0)
    return


def DisarmedInit(tempMod, subject):
    subject.Interlock.setAllowedToUseWeapons(0)
    return


def DisarmedTerm(tempMod, subject):
    subject.Interlock.setAllowedToUseWeapons(1)
    return


def OnHealthChangeDeactivateAbility(tempMod, subject, damageTaken):
    deactiveSubjectMods = 1
    deactiveDirectObjectMods = 1
    deactiveEquipingMods = 1
    outputTempModDebug('OnHealthChangeDeactivateAbility : ')
    subject.AbilityInv.turnOffAbility(tempMod.ParentAbility, deactiveSubjectMods, deactiveDirectObjectMods, deactiveEquipingMods)
    return


def OnHealthReduceDeactivateAbility(tempMod, subject, damageTaken):
    deactiveSubjectMods = 1
    deactiveDirectObjectMods = 1
    deactiveEquipingMods = 1
    outputTempModDebug('OnHealthChangeDeactivateAbility %d: ' % damageTaken)
    if damageTaken > 0 and subject.Health != subject.MaxHealth:
        subject.AbilityInv.turnOffAbility(tempMod.ParentAbility, deactiveSubjectMods, deactiveDirectObjectMods, deactiveEquipingMods)
    return


def OnHealthReduceDeactivateChance(tempMod, subject, damageTaken):
    deactiveSubjectMods = 1
    deactiveDirectObjectMods = 1
    deactiveEquipingMods = 1
    roll = random.randrange(0, 100)
    outputTempModDebug('OnHealthChangeDeactivateAbility %d damage, %d chance, %d roll: ' % (damageTaken, tempMod.Value, roll))
    if tempMod.Value > roll and damageTaken > 0 and subject.Health != subject.MaxHealth:
        subject.AbilityInv.turnOffAbility(tempMod.ParentAbility, deactiveSubjectMods, deactiveDirectObjectMods, deactiveEquipingMods)
    if tempMod.Value < 0:
        tempMod.Value = -tempMod.Value
    return


def OnHealthReduceHealProc(tempMod, subject, damageTaken):
    GenericConstHealthRegenEvent(tempMod, subject)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_HACKER_HEALTH_TARGETBASE, 0)
    return


def OnHealthReduceHealProcChance50(tempMod, subject, damageTaken):
    if 50 > random.randrange(0, 100):
        healed = GenericConstHealthRegenEventWithReturn(tempMod, subject)
        discovery.playEffect(subject.locator, subject.locator, FX.FX_HACKER_HEALTH_TARGETBASE, 0)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_HEAL_SELF, tempMod.ParentAbility, subject.locator, subject.locator, healed)
    return


def SpeedingInit(tempMod, subject):
    Utility.outputAbilityDebug('SpeedingInit: Turning on speed')
    subject.CharMvt.addDissemblingType(kSpeeding)
    return


def SpeedingTerm(tempMod, subject):
    Utility.outputAbilityDebug('SpeedingTerm: Turning off speed')
    subject.CharMvt.removeDissemblingType(kSpeeding)
    return


def GenericCrashTerm(tempMod, subject):
    subject.AbilityInv.removeTempMod(CrashStateAbility, ConditionStatesAbility)
    if tempMod.TimeLeft <= 0:
        outputTempModDebug('GenericCrashTerm: Player crashes to last hardline!')
        if subject.Inventory:
            items = subject.Inventory.getEquippedItems()
            for item in items:
                roll = random.randrange(1, 100)
                Utility.outputAbilityDebug('GenericCrashTerm: Corrode chance roll = ' + `roll` + '.')
                if roll > VIRUSCRASH_CORRODE_CHANCEVALUE:
                    Utility.outputAbilityDebug('GenericCrashTerm: ' + `VIRUSCRASH_CORRODE_CHANCEVALUE` + '% corrode chance failed.')
                    continue
                Utility.outputAbilityDebug('GenericCrashTerm: Corroding item(' + `item` + ').')
                result = Effects.CorrodeItem(item, VIRUSCRASH_CORRODE_VALUE)
                discovery.playEffect(subject.locator, subject.locator, VIRUSCRASH_FX, 0)
                if result == True:
                    subject.Inventory.setItem(item, item.slot)

            subject.CharMvt.teleportToHardline()
        else:
            subject.CharMvt.killSelf()
    else:
        outputTempModDebug('GenericCrashTerm: Crash aborted somehow!')
    if subject.AbilityInv.hasTempMod(CrashStateAbility, ConditionStatesAbility):
        subject.AbilityInv.removeTempMod(CrashStateAbility, ConditionStatesAbility)
    return


def PersonalFirewallInit(tempMod, subject):
    Utility.outputAbilityDebug('PersonalFirewall: Armor bonus of %d.' % tempMod.Value)
    subject.CharMvt.addDissemblingType(kArmor)
    return


def PersonalFirewallTerm(tempMod, subject):
    Utility.outputAbilityDebug('PersonalFirewall: Armor bonus of %d.' % tempMod.Value)
    subject.CharMvt.removeDissemblingType(kArmor)
    return


def PVPFlagFlipperInit(tempMod, subject):
    Utility.outputAbilityDebug('PVPFlagFlipper: flipping flag.')
    subject.AbilityInv.flipPVPFlag(not subject.AbilityInv.isPvP())
    return


def DeathEffectUpdate(tempMod, subject):
    decr = 3.0
    val = tempMod.Value
    tempMod.Value = val + decr
    return


def SmokeBombInit(tempMod, subject):
    Utility.outputAbilityDebug('SmokeBombInit')
    if not subject.Inventory.hasItem(460):
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_SMOKE_BOMB_MISSING_ITEM, tempMod.ParentAbility, subject.locator, subject.locator, 0)
        return
    if not subject.AbilityInv.hasTempMod(VanishAbility, ISDrainTargetAbility):
        procs = (
         '', 'VanishInit', '', '')
        subject.AbilityInv.addTempModProcs(procs, VanishAbility, ISDrainTargetAbility, 0.2)
    return


def VanishInit(tempMod, subject):
    Utility.outputAbilityDebug('Vanish init')
    if not subject.Inventory.hasItem(460):
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_SMOKE_BOMB_MISSING_ITEM, tempMod.ParentAbility, subject.locator, subject.locator, 0)
        return
    subject.AbilityInv.addTempMod(VanishAbility, ISDrainTargetAbility, 0, 0.2)
    subject.AbilityInv.reapplyAbility(InvisibilityGrantAbility)
    return


def ImpartInvisibilityInit(tempMod, subject):
    Utility.outputAbilityDebug('Impart Invisibility init')
    if subject.Interlock.IsInCombat:
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_FAIL_TARGET_IN_INTERLOCK, InvisibilityImpartedAbility, tempMod.Source, subject.locator, 0)
        return
    elif subject.AbilityInv.hasTempMod(InvisibilityGrantAbility, NullTargetAbility) or subject.AbilityInv.hasTempMod(InvisibilityImpartedAbility, NullTargetAbility):
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_IMPART_INVISIBILITY_STACK_CASTER, InvisibilityImpartedAbility, tempMod.Source, subject.locator, 0)
        return
    else:
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_IMPART_INVISIBILITY_CASTER, InvisibilityImpartedAbility, tempMod.Source, subject.locator, 0)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_IMPART_INVISIBILITY_TARGET, InvisibilityImpartedAbility, tempMod.Source, subject.locator, 0)
        subject.AbilityInv.reapplyAbility(InvisibilityImpartedAbility)
    return


def ImpartInvisibilityTerm(tempMod, subject):
    Utility.outputAbilityDebug('Impart Invisibility term')
    subject.CharMvt.removeDissemblingType(kInvisible)
    return


def ImpartBlurInit(tempMod, subject):
    Utility.outputAbilityDebug('Impart blur init')
    subject.CharMvt.addDissemblingType(kBlur)
    return


def ImpartBlurTerm(tempMod, subject):
    Utility.outputAbilityDebug('Impart blur term')
    subject.CharMvt.removeDissemblingType(kBlur)
    return


def EnergizedAttacksInit(tempMod, subject):
    Utility.outputAbilityDebug('Energized Attacks init')
    subject.Interlock.updateTacticalSetting(constants.combat.TT_ENERGIZED)
    return


def EnergizedAttacksTerm(tempMod, subject):
    Utility.outputAbilityDebug('Energized Attacks term')
    subject.Interlock.updateTacticalSetting(constants.combat.TT_INVALID)
    return


def PerciseBlowInit(tempMod, subject):
    Utility.outputAbilityDebug('Percise Blow init')
    subject.Interlock.updateTacticalSetting(constants.combat.TT_PRECISE)
    return


def PerciseBlowTerm(tempMod, subject):
    Utility.outputAbilityDebug('Percise Blow term')
    subject.Interlock.updateTacticalSetting(constants.combat.TT_INVALID)
    return


def EnableRootWithChance(tempMod, subject):
    Utility.outputAbilityDebug('EnableRootWithChance ')
    procs = ('', 'SendGenericTermString', '', 'OnHealthReduceDeactivateChance')
    procs2 = ('', '', '', '')
    subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, RootedAbility, 0, 0, float(tempMod.Duration))
    subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, 0, tempMod.Duration, 0, 0, -tempMod.Value)
    return


def SendGenericTermString(tempMod, subject):
    Utility.outputAbilityDebug('Send Generic Term String ')
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TERM, tempMod.ParentAbility, subject.Locator, subject.Locator, 1 << 31 | tempMod.ParentAbility << 10)
    return


def InterruptRoot(tempMod, subject):
    Utility.outputAbilityDebug('Root Interrupted ')
    subject.AbilityInv.removeTempMod(tempMod.ParentAbility, tempMode.TargetTarget)
    return


def AddRecastTimer(tempMod, subject):
    Utility.outputAbilityDebug('Add Recast Timer %d ' % tempMod.Value)
    subject.AbilityInv.addRecastTimer(tempMod.ParentAbility, tempMod.Value)
    return


def AddItemRecastTimer(tempMod, subject):
    Utility.outputAbilityDebug('Add Item Recast Timer %d ' % tempMod.Value)
    subject.AbilityInv.addRecastTimer(tempMod.ParentAbility, tempMod.Value, 1)
    return


def AddSpecifiedItemRecastTimer(tempMod, subject):
    thisParent = tempMod.ParentAbility
    targetItem = Utility.GetPropertyFromAbility(thisParent, 'ItemToRecast')
    if not targetItem is None:
        Utility.outputAbilityDebug('Add Specified Item Recast Timer %d, %d' % (targetItem, tempMod.Value))
        subject.AbilityInv.addRecastTimer(targetItem, tempMod.Value, 1)
    else:
        Utility.outputAbilityDebug('AddSpecifiedItemRecastTimer Error: targetItem not found!')
    return


def DeactivateThisAbility(tempMod, subject):
    subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
    return


def SetEvadeCombatTimer(tempMod, subject):
    Utility.outputAbilityDebug('SetEvadeCombatTimer 30 ')
    subject.AbilityInv.addRecastTimer(tempMod.ParentAbility, 30.0)
    return


def TauntAggro(tempMod, subject):
    Utility.outputAbilityDebug('Taunting...')
    if not Utility.IsAnNPC(subject):
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_CANNOT_TAUNT_HUMANS, TauntAbility, tempMod.Source, subject.locator)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_TAUNT_TARGET, TauntAbility, tempMod.Source, subject.locator)
        return
    if subject.AI:
        subject.AI.setMostHated(tempMod.Source)
        Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_TAUNT_CASTER, TauntAbility, tempMod.Source, subject.locator)
    return


def CapValueAt30(tempMod, subject):
    if tempMod.Value > 30:
        tempMod.Value = 30
    return


def DoNothing(tempMod, subject):
    return


def PlayFX(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, tempMod.Value, 0)
    return


def ApplyAuraEffect(tempMod, subject):
    Utility.outputAbilityDebug('Applying Aura: %d' % tempMod.Value)
    if subject.AbilityInv.Health > 0:
        subject.AbilityInv.reapplyAbility(tempMod.Value)
    return


def ApplyAuraEffectHealth(tempMod, subject, change):
    if subject.AbilityInv.Health > 0:
        subject.AbilityInv.reapplyAbility(tempMod.Value)
    return


def ObjApplyAuraEffect(tempMod, subject):
    Utility.outputAbilityDebug('Applying Object Aura: %d' % tempMod.Value)
    subject.AbilityInv.reapplyAbility(tempMod.Value)
    return


def ApplyAuraEffectOnHealthChange(tempMod, subject, damageTaken):
    subject.AbilityInv.reapplyAbility(tempMod.Value)
    return


def ApplyNextAuraEffectHealthChance(tempMod, subject, damageTaken):
    roll = random.randrange(0, 100)
    if roll < tempMod.Value:
        subject.AbilityInv.reapplyAbility(tempMod.ParentAbility + 1)
    return


def PlayScript(tempMod, subject):
    Utility.outputAbilityDebug('PlayScript %d' % tempMod.Value)
    subject.CharMvt.playScript(subject.CharMvt.Stance, tempMod.Value)
    return


def PlayStanceScript(tempMod, subject):
    Utility.outputAbilityDebug('PlayStanceScript ')
    stance = discovery.getGameObjectPropValue(1 << 31 | tempMod.ParentAbility << 10, 'PlayStanceScriptStance')
    action = discovery.getGameObjectPropValue(1 << 31 | tempMod.ParentAbility << 10, 'PlayStanceScriptAction')
    subject.CharMvt.playScript(int(stance), int(action))
    return


def PlayHyperStrengthFX(tempMod, subject):
    Utility.outputAbilityDebug('PlayHyperStrengthFX')
    discovery.playEffect(subject.locator, subject.locator, 671091159, 0)
    return


def HealthDrainPercent(tempMod, subject):
    percent = tempMod.Value
    max_health = subject.AbilityInv.MaxHealth
    damage = int(percent / 100.0 * max_health)
    outputTempModDebug('HealthDrainPercent: maxhealth %d, percent %d, damage %d' % (max_health, percent, damage))
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, damage, tempMod.ParentAbility, Neutral, tempMod.IsAbility)
    return


def ChainFlameEffectFunc(tempMod, subject):
    HealthDrainPercent(tempMod, subject)
    discovery.playEffect(subject.locator, subject.locator, 671091290, 0)
    procs = ('', 'PlayChainFlameFX', '', '')
    subject.AbilityInv.addTempModProcs(procs, NPCChainFlameEffectAbility, ISDrainTargetAbility, 1, 1)
    return


def PlayChainFlameFX(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 671091290, 0)
    return


def FlyVirusFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 3724542070L, 0)
    return


def FlyStormFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 2952790085L, 0)
    return


def CodeEyesFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 134217736, 0)
    return


def TwinGhostFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108884, 0)
    return


def BlueLampFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 2952790089L, 0)
    return


def RedEyeFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108891, 0)
    return


def SeraphicVisionFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108896, 0)
    return


def EliteCommMFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108888, 0)
    return


def EliteCommFFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108927, 0)
    return


def TacticBoostFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108932, 0)
    return


def WarSwordFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108950, 0)
    return


def WarSwordLargeFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108949, 0)
    return


def OGAccelerationFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108966, 0)
    return


def OGDecelerationFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108967, 0)
    return


def SmithFirewallFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108901, 0)
    return


def OverrideOrbFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108994, 0)
    return


def SSRGumFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108996, 0)
    return


def ISBoosterFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108998, 0)
    return


def NinjaScarfFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109030, 0)
    return


def BlueShiftFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109032, 0)
    return


def AndvarFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109045, 0)
    return


def OGSignalJammingFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109049, 0)
    return


def AntioverrideProto01Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109052, 0)
    return


def AntioverrideDecFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109059, 0)
    return


def AntioverrideRunFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109060, 0)
    return


def AntioverrideSigFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109061, 0)
    return


def OverrideImageFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109062, 0)
    return


def RSICaptureKuno83Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108999, 0)
    return


def RSICaptureKuno84Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109000, 0)
    return


def RSICaptureKuno85Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109000, 0)
    return


def RSICaptureKuno86Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109001, 0)
    return


def RSICaptureKuno87Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109001, 0)
    return


def RSICaptureKuno88Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108999, 0)
    return


def RSICaptureKuno89Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108999, 0)
    return


def RSICaptureKuno90Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109000, 0)
    return


def RSICaptureKuno91Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109001, 0)
    return


def RSICaptureKuno92Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109002, 0)
    return


def RSICaptureKuno93Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109000, 0)
    return


def RSICaptureKuno94Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108999, 0)
    return


def RSICaptureKuno95Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108999, 0)
    return


def RSICaptureKuno96Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109002, 0)
    return


def RSICaptureKuno97Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109001, 0)
    return


def RSICaptureKuno98Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108999, 0)
    return


def RSICaptureKuno99Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109001, 0)
    return


def RSICaptureKuno100Func(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109007, 0)
    return


def RSICaptureSSRFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108996, 0)
    return


def RSICaptureValfFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109045, 0)
    return


def RSICaptureEliteFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67108888, 0)
    return


def RSICaptureWrightFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109043, 0)
    return


def RSICaptureExecFunc(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 67109064, 0)
    return


def AddControlImmunityTimer(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility):
        subject.AbilityInv.addTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility, 0, tempMod.TimeLeft)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_CONTROL_IMMUNITY_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, tempMod.TimeLeft)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_CONTROL_IMMUNITY_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, tempMod.TimeLeft)
    return


def AddControlImmunityTimerVal(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility):
        subject.AbilityInv.addTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility, 0, tempMod.Value)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_CONTROL_IMMUNITY_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, tempMod.Value)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_CONTROL_IMMUNITY_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, tempMod.Value)
    return


def ApplyStunEffect(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility):
        Utility.outputAbilityDebug('Applying stun effect')
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_MACHINEGUN_PUNCH_SUBJECT, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_MACHINEGUN_PUNCH_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, StunAbility, 0, 0, float(tempMod.Value))
    else:
        Utility.outputAbilityDebug('Stun stacking detected')
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_CAST_FAILURE_LOWER_BUFF_STACKING_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
    return


def ApplyStunWithTimer(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility):
        Utility.outputAbilityDebug('Applying stun effect')
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_MACHINEGUN_PUNCH_SUBJECT, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_MACHINEGUN_PUNCH_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, StunAbility, 0, 0, float(tempMod.Value))
        subject.AbilityInv.addTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility, 0, tempMod.Value)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_CONTROL_IMMUNITY_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, tempMod.Value)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_CONTROL_IMMUNITY_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, tempMod.Value)
    else:
        Utility.outputAbilityDebug('Stun stacking detected')
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_CAST_FAILURE_LOWER_BUFF_STACKING_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
    return


def ApplyStunWithCodedTimer(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility):
        stun_time = tempMod.Value >> 8
        immunity_time = tempMod.Value & 255
        Utility.outputAbilityDebug('Applying stun effect %dsec stun %dsec immunity' % (stun_time, immunity_time))
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_MACHINEGUN_PUNCH_SUBJECT, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, StunAbility, 0, 0, stun_time)
        subject.AbilityInv.addTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility, 0, immunity_time)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_CONTROL_IMMUNITY_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, immunity_time)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_CONTROL_IMMUNITY_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, immunity_time)
    else:
        Utility.outputAbilityDebug('Stun stacking detected')
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_CAST_FAILURE_LOWER_BUFF_STACKING_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
    return


def ApplyMezEffect(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility):
        Utility.outputAbilityDebug('Applying mez effect')
        subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, MezAbility, 0, 0, float(tempMod.Value))
    else:
        Utility.outputAbilityDebug('Mez stacking detected')
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_CAST_FAILURE_LOWER_BUFF_STACKING_CASTER, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
    return


def HyperSpeedInit(tempMod, subject):
    Utility.outputAbilityDebug('HyperSpeedInit: Turning on HyperSpeed')
    subject.CharMvt.addDissemblingType(kHyperSpeed)
    return


def HyperSpeedTerm(tempMod, subject):
    Utility.outputAbilityDebug('HyperSpeedTerm: Turning off HyperSpeed')
    subject.CharMvt.removeDissemblingType(kHyperSpeed)
    return


def HyperDodgeInit(tempMod, subject):
    Utility.outputAbilityDebug('HyperDodgeInit: Turning on HyperDodge')
    subject.CharMvt.addDissemblingType(kHyperDodge)
    return


def HyperDodgeTerm(tempMod, subject):
    Utility.outputAbilityDebug('HyperDodgeTerm: Turning off HyperDodge')
    subject.CharMvt.removeDissemblingType(kHyperDodge)
    return


def ShowNeoRSI(tempMod, subject):
    Utility.outputAbilityDebug('ShowNeoRSI: Turning on NeoRSI')
    subject.CharMvt.addDissemblingType(kNeoRSI)
    return


def HideNeoRSI(tempMod, subject):
    Utility.outputAbilityDebug('HideNeoRSI: Turning off NeoRSI')
    subject.CharMvt.removeDissemblingType(kNeoRSI)
    return


def EvadeStateFlipper(tempMod, subject):
    Utility.outputAbilityDebug('EvadeStateFlipper: Changing Evade State')
    if subject.Interlock.getEvadeState():
        subject.Interlock.setEvadeState(0)
    else:
        subject.Interlock.setEvadeState(1)
    return


def ReduceMaxHealthPercent(tempMod, subject):
    penalty = -int(subject.AbilityInv.MaxHealth * 0.03)
    Utility.outputAbilityDebug('ReduceMaxHealthPercent: Reducing %d' % penalty)
    if not subject.AbilityInv.hasTempMod(tempMod.ParentAbility, MaxHealthAbility):
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_FLY_VIRUS_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, penalty)
    subject.AbilityInv.addTempMod(tempMod.ParentAbility, MaxHealthAbility, penalty, 5)
    return


def EnableSusceptibility(tempMod, subject):
    Utility.outputAbilityDebug('EnableSusceptibility: %d' % tempMod.Value)
    subject.AbilityInv.enableSusceptibilityTo(tempMod.Value, 1)
    return


def DisableSusceptibility(tempMod, subject):
    Utility.outputAbilityDebug('DisableSusceptibility: %d' % tempMod.Value)
    subject.AbilityInv.enableSusceptibilityTo(tempMod.Value, 0)
    return


def EnableImmunity(tempMod, subject):
    Utility.outputAbilityDebug('EnableImmunity: %d' % tempMod.Value)
    subject.AbilityInv.enableImmunityTo(tempMod.Value, 1)
    return


def DisableImmunity(tempMod, subject):
    Utility.outputAbilityDebug('DisableImmunity: %d' % tempMod.Value)
    subject.AbilityInv.enableImmunityTo(tempMod.Value, 0)
    return


def EnableAccImmunity(tempMod, subject):
    subject.AbilityInv.enableImmunityTo(OGAccelerateEffectAbility, 1)
    subject.AbilityInv.enableImmunityTo(OGLOSAccelerateEffectAbility, 1)
    return


def DisableAccImmunity(tempMod, subject):
    subject.AbilityInv.enableImmunityTo(OGAccelerateEffectAbility, 0)
    subject.AbilityInv.enableImmunityTo(OGLOSAccelerateEffectAbility, 0)
    return


def EnableDecImmunity(tempMod, subject):
    subject.AbilityInv.enableImmunityTo(OGDecelerateEffectAbility, 1)
    subject.AbilityInv.enableImmunityTo(OGLOSDecelerateEffectAbility, 1)
    subject.AbilityInv.enableImmunityTo(NPCWrightDecelerateEffectAbility, 1)
    return


def DisableDecAccImmunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(AntioverrideProto01DecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideDecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGIndefAccelAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGDecelImmunityAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGLOSAccelerateEffectAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(OGLOSDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(NPCWrightDecelerateEffectAbility, 0)
    return


def DisableDecLOSAccImmunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(OGAccelerateEffectAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideDecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGIndefAccelAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideProto01DecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGDecelImmunityAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(OGLOSDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(NPCWrightDecelerateEffectAbility, 0)
    return


def DisableDecImmImmunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(OGAccelerateEffectAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideDecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGIndefAccelAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideProto01DecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGLOSAccelerateEffectAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(OGLOSDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(NPCWrightDecelerateEffectAbility, 0)
    return


def DisableDecIndImmunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(AntioverrideProto01DecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideDecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGAccelerateEffectAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGDecelImmunityAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGLOSAccelerateEffectAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(OGLOSDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(NPCWrightDecelerateEffectAbility, 0)
    return


def DisableDecAntiImmunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(OGAccelerateEffectAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideDecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGIndefAccelAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGDecelImmunityAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGLOSAccelerateEffectAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(OGLOSDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(NPCWrightDecelerateEffectAbility, 0)
    return


def DisableDecAnti2Immunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(OGAccelerateEffectAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGIndefAccelAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGDecelImmunityAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideProto01DecAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(OGLOSAccelerateEffectAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(OGLOSDecelerateEffectAbility, 0)
        subject.AbilityInv.enableImmunityTo(NPCWrightDecelerateEffectAbility, 0)
    return


def EnableRunImmunity(tempMod, subject):
    subject.AbilityInv.enableImmunityTo(OGRunEffectAbility, 1)
    return


def DisableRunImmImmunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(AntioverrideProto01RunAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideRunAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGRunEffectAbility, 0)
    return


def DisableRunAntiImmunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(OGRunImmunityAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideRunAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGRunEffectAbility, 0)
    return


def DisableRunAnti2Immunity(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(OGRunImmunityAbility, ISDrainTargetAbility) and not subject.AbilityInv.hasTempMod(AntioverrideProto01RunAbility, ISDrainTargetAbility):
        subject.AbilityInv.enableImmunityTo(OGRunEffectAbility, 0)
    return


def EnableSigImmunity(tempMod, subject):
    subject.AbilityInv.enableImmunityTo(OGSignalJammingEffectAbility, 1)
    subject.AbilityInv.enableImmunityTo(NPCWrightSignalJammingEffectAbility, 1)
    return


def DisableSigAnti2Immunity(tempMod, subject):
    subject.AbilityInv.enableImmunityTo(OGSignalJammingEffectAbility, 0)
    subject.AbilityInv.enableImmunityTo(NPCWrightSignalJammingEffectAbility, 0)
    return


def EnableFlyImmunity(tempMod, subject):
    Utility.outputAbilityDebug('EnableImmunityWithOutput: %d' % tempMod.Value)
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_FLY_IMMUNITY_CASTER, Event2_0FlyVirusEffect1Ability, tempMod.Source, subject.locator, 0)
    subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect1Ability, 1)
    subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect2Ability, 1)
    subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusAbility, 1)
    subject.AbilityInv.enableImmunityTo(Event2_0GMFlyVirusAbility, 1)
    subject.AbilityInv.enableImmunityTo(AssassinMask2EffectAbility, 1)
    return


def DisableFlyImmunity(tempMod, subject):
    Utility.outputAbilityDebug('DisableFlyImmunity: %d' % tempMod.Value)
    Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_FLY_IMMUNITY_CASTER_END, Event2_0FlyVirusEffect1Ability, tempMod.Source, subject.locator, 0)
    subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect1Ability, 0)
    subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect2Ability, 0)
    subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusAbility, 0)
    subject.AbilityInv.enableImmunityTo(Event2_0GMFlyVirusAbility, 0)
    subject.AbilityInv.enableImmunityTo(AssassinMask2EffectAbility, 0)
    return


def DisableFlyImmunityPill(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(47004, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47005, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47006, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47007, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47008, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47009, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(48177, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(48178, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(Event2_0FlyVirusImmunityAbility, NullTargetAbility):
        Utility.outputAbilityDebug('DisableFlyImmunity: %d' % tempMod.Value)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_FLY_IMMUNITY_CASTER_END, Event2_0FlyVirusEffect1Ability, tempMod.Source, subject.locator, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect1Ability, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect2Ability, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusAbility, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0GMFlyVirusAbility, 0)
        subject.AbilityInv.enableImmunityTo(AssassinMask2EffectAbility, 0)
    return


def DisableFlyImmunityGear(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(CorruptPatchersPillAbility, HealthRegenRateAbility) and not subject.AbilityInv.hasTempMod(Event2_0FlyVirusImmunityAbility, NullTargetAbility):
        Utility.outputAbilityDebug('DisableFlyImmunity: %d' % tempMod.Value)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_FLY_IMMUNITY_CASTER_END, Event2_0FlyVirusEffect1Ability, tempMod.Source, subject.locator, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect1Ability, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect2Ability, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusAbility, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0GMFlyVirusAbility, 0)
        subject.AbilityInv.enableImmunityTo(AssassinMask2EffectAbility, 0)
    return


def DisableFlyImmunityAb(tempMod, subject):
    if not subject.AbilityInv.hasTempMod(47004, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47005, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47006, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47007, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47008, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(47009, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(48177, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(48178, RangedDamageResistanceAbility) and not subject.AbilityInv.hasTempMod(CorruptPatchersPillAbility, HealthRegenRateAbility):
        Utility.outputAbilityDebug('DisableFlyImmunity: %d' % tempMod.Value)
        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_FLY_IMMUNITY_CASTER_END, Event2_0FlyVirusEffect1Ability, tempMod.Source, subject.locator, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect1Ability, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusEffect2Ability, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0FlyVirusAbility, 0)
        subject.AbilityInv.enableImmunityTo(Event2_0GMFlyVirusAbility, 0)
        subject.AbilityInv.enableImmunityTo(AssassinMask2EffectAbility, 0)
    return


def ApplyHiddenAuraEffect(tempMod, subject):
    subject.AbilityInv.reapplyAbility(tempMod.Value)
    return


def ApplyState(tempMod, subject):
    subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, tempMod.Value, 0, 0, 0)
    return


def ApplyStateOnEnd(tempMod, subject):
    if tempMod.TimeLeft <= 0:
        subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, tempMod.Value, 0, 0, 0)
    return


def DoWaveOfCorruption(tempMod, subject):
    Utility.outputAbilityDebug('DoWaveOfCorruption, %d, %d' % (Action_Abil_WaveofCorruption_A, Action_MaxActions))
    procs = ('', 'ApplyAuraEffect', '', '')
    subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, NullTargetAbility, 1.0, 0, 0, 900)
    subject.CharMvt.playScript(Stance_Aggro, Action_Abil_WaveofCorruption_A)
    return


def ClearDisarmState(tempMod, subject):
    if subject.AbilityInv.hasTempMod(CombatDisarmedAbility, DisarmedAbility):
        Utility.SendAbilityOutputToTarget(StringTable.ID_OBJ_PANDORA3_3SHIRT_SUBJECT, tempMod.ParentAbility, subject.locator, subject.locator, 0)
        subject.AbilityInv.removeTempMod(CombatDisarmedAbility, DisarmedAbility)
    return


def OnDamageAddTwo(tempMod, subject, damageTaken):
    Utility.outputAbilityDebug('OnDamageAddTwo %d' % damageTaken)
    if damageTaken > 0 and tempMod.Value < 25:
        tempMod.Value += 2
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TOUGHNESS_BOOST_SELF, tempMod.ParentAbility, subject.locator, subject.locator, tempMod.Value)
    return


def OnDamageAddThree(tempMod, subject, damageTaken):
    Utility.outputAbilityDebug('OnDamageAddTwo %d' % damageTaken)
    if damageTaken > 0 and tempMod.Value < 26:
        tempMod.Value += 3
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TOUGHNESS_BOOST_SELF, tempMod.ParentAbility, subject.locator, subject.locator, tempMod.Value)
    return


def RestoreDoubleIS(tempMod, subject):
    Utility.outputAbilityDebug('RestoreDoubleIS %d' % tempMod.Value)
    restoreVal = 0
    if tempMod.Value * -2 < -100:
        restoreVal = -100
    else:
        restoreVal = tempMod.Value * -2
    subject.AbilityInv.takeInnerStrength(restoreVal)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_RESTORE_INNER_STRENGTH_SELF, tempMod.ParentAbility, subject.locator, subject.locator, -restoreVal)
    return


def MobiusCodeEffect(tempMod, subject):
    max_health = subject.AbilityInv.MaxHealth
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, max_health * 0.1, tempMod.ParentAbility, NeutralHeal, tempMod.IsAbility)
    if random.randrange(100) < MOBIUS_IMMUNITY_CHANCE:
        procs = ('MobiusCodeInit', 'MobiusCodeTerm', '', '')
        subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, tempMod.ParentAbility, 10, 0, 1)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_IMMUNE_TO_ALL_SELF, tempMod.ParentAbility, subject.locator, subject.locator, 0)
        discovery.playEffect(subject.locator, subject.locator, FX.FX_HEALING_GIFT_HEALING_INVUL, 0)
    else:
        discovery.playEffect(subject.locator, subject.locator, FX.FX_HEALING_GIFT_HEALING_NOINVUL, 0)
    return


def MobiusCodeInit(tempMod, subject):
    CantBeForceIntoCombatInit(tempMod, subject)
    CantBeFreeAttackedInit(tempMod, subject)
    subject.AbilityInv.enableSusceptibilityTo(MobiusCodeAbility, 1)
    return


def MobiusCodeTerm(tempMod, subject):
    CantBeForceIntoCombatTerm(tempMod, subject)
    CantBeFreeAttackedTerm(tempMod, subject)
    subject.AbilityInv.enableSusceptibilityTo(MobiusCodeAbility, 0)
    return


def AntiStunPillEffect(tempMod, subject):
    temp_mods = subject.AbilityInv.getTempMods()
    for temp_mod in temp_mods:
        if temp_mod.TargetAbility == StunAbility or temp_mod.TargetAbility == MezAbility:
            subject.AbilityInv.removeTempMod(temp_mod.ParentAbility, temp_mod.TargetAbility)
        if temp_mod.ParentAbility == ControlImmunityTimerAbility:
            time = temp_mod.TimeLeft / 2
            if time >= 1:
                subject.AbilityInv.addTempMod(ControlImmunityTimerAbility, ISDrainTargetAbility, 0, time)

    return


def AssassinMaskProc(tempMod, subject):
    num = random.randrange(1400)
    if num < 200:
        subject.AbilityInv.reapplyAbility(CorruptingFeastAbility)
    elif num < 400:
        subject.AbilityInv.reapplyAbility(CorruptingShroudAbility)
    elif num < 600:
        subject.AbilityInv.reapplyAbility(CorruptionPulseAbility)
    elif num < 800:
        subject.AbilityInv.reapplyAbility(CorruptionWaveAbility)
    elif num < 1000:
        subject.AbilityInv.reapplyAbility(SignalCorruptionAbility)
    elif num < 1200:
        subject.AbilityInv.reapplyAbility(CorruptingFuryAbility)
    else:
        subject.AbilityInv.reapplyAbility(CorruptingCalmAbility)
    num = random.randrange(10) * 0.01 + 0.01
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, subject.AbilityInv.MaxHealth * num, tempMod.ParentAbility, Neutral, tempMod.IsAbility)
    return


def DealFlitDamage(tempMod, subject):
    if subject.hasAbility[AssassinTransformAbility]:
        if random.randrange(100) < 2:
            subject.AbilityInv.dealAbilityDamage(tempMod.Source, FLIT_GUN_DAMAGE * 5, tempMod.ParentAbility, Neutral, tempMod.IsAbility)
        else:
            subject.AbilityInv.dealAbilityDamage(tempMod.Source, FLIT_GUN_DAMAGE, tempMod.ParentAbility, Neutral, tempMod.IsAbility)
    else:
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_FLIT_GUN_FAIL, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
    return


def DealNeutralDamagePercent(tempMod, subject):
    damage = subject.AbilityInv.MaxHealth * tempMod.Value / 100.0
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, damage, tempMod.ParentAbility, Neutral, tempMod.IsAbility)
    return


def Meatwad(tempMod, subject):
    subject.CharMvt.playNamedScript(Stance_Crumple, 'Stand_Crumple', 0)
    return


def PlayAssassinDeathFX(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 306590017, 0)
    return


def PlayUnlimitDeathFX(tempMod, subject):
    discovery.playEffect(subject.locator, subject.locator, 134217739, 0)
    return


def AddRecastTimerFromItem(tempMod, subject):
    abil = discovery.getGameObjectPropValue(tempMod.ParentAbility, 'RecastAbility')
    abil = (~(1 << 31) & abil) / 2 ** 10
    Utility.outputAbilityDebug('Add Item Recast Timer From Item %d %d ' % (abil, tempMod.Value))
    subject.AbilityInv.addRecastTimer(abil, tempMod.Value)
    return


def PlayActivationFX(tempMod, subject):
    AbilObjID = 1 << 31 | tempMod.ParentAbility << 10
    fxid = discovery.getGameObjectPropValue(AbilObjID, 'ActivationFX')
    Utility.outputAbilityDebug('PlayActivationFX %d %d' % (AbilObjID, fxid))
    discovery.playEffect(subject.locator, subject.locator, fxid, 0)
    return


def PlayItemUpdateFXIfInInterlock(tempMod, subject):
    if subject.Interlock.isInCombat:
        PlayItemUpdateFX(tempMod, subject)
    return


def PlayUpdateFXIfInInterlock(tempMod, subject):
    if subject.Interlock.isInCombat:
        PlayUpdateFX(tempMod, subject)
    return


def PlayItemUpdateFX(tempMod, subject):
    fxid = discovery.getGameObjectPropValue(tempMod.ParentAbility, 'UpdateFX')
    Utility.outputAbilityDebug('PlayItemUpdateFX %d %d' % (tempMod.ParentAbility, fxid))
    discovery.playEffect(subject.locator, subject.locator, fxid, 0)
    return


def PlayUpdateFX(tempMod, subject):
    AbilObjID = 1 << 31 | tempMod.ParentAbility << 10
    fxid = discovery.getGameObjectPropValue(AbilObjID, 'UpdateFX')
    Utility.outputAbilityDebug('PlayUpdateFX %d %d' % (AbilObjID, fxid))
    discovery.playEffect(subject.locator, subject.locator, fxid, 0)
    return


def PlayItemActivationFX(tempMod, subject):
    fxid = discovery.getGameObjectPropValue(tempMod.ParentAbility, 'ActivationFX')
    Utility.outputAbilityDebug('PlayActivationFX %d %d' % (tempMod.ParentAbility, fxid))
    discovery.playEffect(subject.locator, subject.locator, fxid, 0)
    return


def PlayItemActivationFXIfInInv(tempMod, subject):
    Utility.outputAbilityDebug('PlayItemActivationFXIfInInv')
    if subject.Inventory.hasItem(tempMod.ParentAbility):
        PlayItemActivationFX(tempMod, subject)
    return


def RestoreISValPlus15(tempMod, subject):
    restoreVal = -(tempMod.Value + 15)
    Utility.outputAbilityDebug('RestoreISValPlus15 %d' % tempMod.Value)
    subject.AbilityInv.takeInnerStrength(restoreVal)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_RESTORE_INNER_STRENGTH_SELF, tempMod.ParentAbility, subject.locator, subject.locator, -restoreVal)
    return


def ConcealInit(tempMod, subject):
    Utility.outputAbilityDebug('ConcealInit')
    if subject.AbilityInv.CurExclusiveAbility != tempMod.ParentAbility:
        Utility.outputAbilityDebug('Exclusive mismatch %d %d' % (subject.AbilityInv.CurExclusiveAbility, tempMod.ParentAbility))
        procs = ('', 'ConcealPanicEnd', '', '')
        subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, NullTargetAbility, 0.4, 0, 1)
        subject.CharMvt.playScript(Stance_Stand, subject.CharMvt.Action)
    else:
        subject.CharMvt.playScript(Stance_Invis, subject.CharMvt.Action)
    return


def ConcealPanicEnd(tempMod, subject):
    subject.AbilityInv.turnOffAbility(tempMod.ParentAbility, 1, 1, 1)
    return


def ConcealTerm(tempMod, subject):
    Utility.outputAbilityDebug('ConcealTerm')
    subject.CharMvt.deactivateSneak()
    subject.CharMvt.playScript(Stance_Stand, subject.CharMvt.Action)
    return


def HalloweenMaskOn(tempMod, subject):
    Utility.outputAbilityDebug('HalloweenMaskOn')
    mod = subject.AbilityInv.getTempMods(HalloweenMaskAbility, NullTargetAbility)
    if mod is not None and len(mod) == 1:
        disguiseID = mod[0].Value
        if disguiseID < 0:
            disguiseID = 2147483647 - disguiseID
        subject.AbilityInv.deactivateAbility(RSICaptureAbility)
        subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
        subject.AbilityInv.deactivateAbility(RSICapExecAbility)
        subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno89Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno90Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno91Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno92Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno93Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno94Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
        subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
        subject.AbilityInv.deactivateAbility(RSICapValfAbility)
        subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
        subject.CharMvt.setModel(disguiseID)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, tempMod.ParentAbility, subject.locator, subject.locator, 0)
        subject.AbilityInv.removeTempMod(HalloweenMaskAbility, NullTargetAbility)
    else:
        subject.AbilityInv.removeTempMod(HalloweenMask2Ability, NullTargetAbility)
    return


def HalloweenMaskOff(tempMod, subject):
    Utility.outputAbilityDebug('HalloweenMaskOff')
    if subject.hasAbility[ZombieVirus2BaseAbility]:
        Utility.outputAbilityDebug('HalloweenMaskOff2')
        zmod = subject.AbilityInv.getTempMods(ZombieVirus2BaseAbility, NullTargetAbility)
        if zmod[0].Value == 0:
            model = 1476398198
        else:
            model = 1476398197
        subject.CharMvt.setModel(model)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TERM, tempMod.ParentAbility, subject.locator, subject.locator, 0)
        return
    if subject.hasAbility[ZombieVirus3BaseAbility]:
        Utility.outputAbilityDebug('HalloweenMaskOff3')
        zmod = subject.AbilityInv.getTempMods(ZombieVirus3BaseAbility, NullTargetAbility)
        if zmod[0].Value == 0:
            model = 1476398198
        else:
            model = 1476398197
        subject.CharMvt.setModel(model)
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TERM, tempMod.ParentAbility, subject.locator, subject.locator, 0)
        return
    subject.CharMvt.revertToRSI()
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TERM, tempMod.ParentAbility, subject.locator, subject.locator, 0)
    return


def HalloweenMaskCheck(tempMod, subject):
    if subject.Description == 1476398198 or subject.Description == 1476398197 or subject.UseRSIDescription:
        subject.AbilityInv.deactivateAbility(tempMod.ParentAbility)
    return


def CalcCryptosTime(tempMod, subject):
    tm = time.localtime()
    targetTimeMin = subject.OpenOffset
    if subject is None:
        Utility.outputAbilityDebug('Subject none')
    elif subject.AbilityInv is None:
        Utility.outputAbilityDebug('AbilityInv none')
    duration = 0
    if tm[4] < targetTimeMin:
        duration = (targetTimeMin - tm[4]) * 60 + tm[3] % 2 * 3600
    else:
        duration = (targetTimeMin - tm[4]) * 60 + (tm[3] + 1) % 2 * 3600 + 3600
    Utility.outputAbilityDebug('CalcCryptosTime: %d' % duration)
    procs = (
     '', 'OpenCryptosBox', '', '', '')
    subject.PCAbilityInventory.addTempModProcs(procs, tempMod.ParentAbility, NullTargetAbility, duration)
    return


def OpenCryptosBox(tempMod, subject):
    if tempMod.TimeLeft <= 0:
        Utility.outputAbilityDebug('Opening Box')
        subject.StateMachine.setState('BoxOpening')
        position = subject.Position
        components = (constants.ComponentFactory.ELTGOModel, constants.ComponentFactory.ELTGOStateMachine)
        objects = physics.getObjectsInRadius(position.x, position.y, position.z, 500, components, 400.0)
        for object in objects:
            if object.CryptosSpawn is not None:
                Utility.outputAbilityDebug('CryptosSpawn')
                object.StateMachine.setState('CryptosSpawn')
                procs = ('', 'CloseCryptosBox', '', '', '')
                subject.PCAbilityInventory.addTempModProcs(procs, tempMod.ParentAbility, InvalidAbility, object.Model.SpeechDuration + 10)

    return


def CloseCryptosBox(tempMod, subject):
    if tempMod.TimeLeft <= 0:
        Utility.outputAbilityDebug('Closing Box')
        subject.StateMachine.setState('BoxClosing')
        position = subject.Position
        components = (constants.ComponentFactory.ELTGOModel, constants.ComponentFactory.ELTGOStateMachine)
        objects = physics.getObjectsInRadius(position.x, position.y, position.z, 500, components, 400.0)
        for object in objects:
            if object.CryptosTalkIdle is not None:
                Utility.outputAbilityDebug('CryptosTalkIdle')
                object.StateMachine.setState('CryptosTalkIdle')

    return


def EscapeAndEvasionTerm(tempMod, subject):
    Utility.outputAbilityDebug('EscapeAndEvasionTerm')
    subject.CharMvt.playScript(Stance_Stand, subject.CharMvt.Action)
    subject.CharMvt.deactivateSneak()
    deactiveSubjectMods = 1
    deactiveDirectObjectMods = 1
    deactiveEquipingMods = 1
    subject.AbilityInv.turnOffAbility(tempMod.ParentAbility, deactiveSubjectMods, deactiveDirectObjectMods, deactiveEquipingMods)
    return


def SnowballProc(tempMod, subject):
    Utility.outputAbilityDebug('SnowballProc')
    AbilObjID = 1 << 31 | tempMod.ParentAbility << 10
    goids = (
     discovery.getGameObjectPropValue(AbilObjID, 'FestiveObject1ID'), discovery.getGameObjectPropValue(AbilObjID, 'FestiveObject2ID'), discovery.getGameObjectPropValue(AbilObjID, 'FestiveObject3ID'), discovery.getGameObjectPropValue(AbilObjID, 'FestiveObject4ID'))
    foundItem = 0
    invalidId = 0
    for id in goids:
        if id is not None and id > 0:
            item = subject.Inventory.getItem(id)
            if item is not None:
                if item.slot == constants.Inventory.HatSlot:
                    if item.stability == 7:
                        subject.Inventory.rmvItem(item.slot)
                        Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_SNOWBALL_DESTROY_HAT, tempMod.ParentAbility, tempMod.Source, subject.locator, id)
                    elif Effects.CorrodeItem(item, -1):
                        subject.Inventory.setItem(item, item.slot)
                        foundItem += 1
        else:
            Utility.outputAbilityDebug('Invalid ID')

    Utility.outputAbilityDebug('Found %d items' % foundItem)
    return


def DetonateProxyProc(tempMod, subject):
    Utility.outputAbilityDebug('DetonateProxyProc')
    maxhealth = subject.AbilityInv.MaxHealth
    currenthealth = subject.AbilityInv.Health
    totalBuffVal = subject.AbilityInv.getAbilityLevel(ViralDamageAbility)
    if currenthealth > maxhealth / 4:
        currenthealth = maxhealth / 4
    val = currenthealth / 1
    if val < maxhealth / 10:
        val = maxhealth / 10
    val -= totalBuffVal + 1
    Utility.outputAbilityDebug('DetonateProxyProc - Buff value %d' % val)
    procs = ('', 'DetonateProxyEnd', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, ViralDamageAbility, 1, 0, 0, int(val))
    if subject.AbilityInv.Health > 0:
        subject.AbilityInv.reapplyAbility(StopProxy1EffectAbility)
    return


def DetonateProxyEnd(tempMod, subject):
    subject.AbilityInv.dealAbilityDamage(tempMod.Source, 13000, tempMod.ParentAbility, Viral, 1)
    return


def ApplyPowerlessIfStaggered(tempMod, subject):
    if subject.AbilityInv.ConditionStateFlags & constants.combat.ST_STAGGERED:
        subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, PowerlessAbility, 0, 0, float(tempMod.Value))
    return


def ApplyStunEffectIfDazed(tempMod, subject):
    if subject.AbilityInv.ConditionStateFlags & constants.combat.ST_DAZED:
        ApplyStunWithTimer(tempMod, subject)
    return


def DealDamageIfOffBalance(tempMod, subject):
    if subject.AbilityInv.ConditionStateFlags & constants.combat.ST_OFFBALANCE:
        subject.AbilityInv.dealAbilityDamage(tempMod.Source, tempMod.Value, tempMod.ParentAbility, Throw, tempMod.IsAbility)
    return


def DisableEvasion(tempMod, subject):
    subject.AbilityInv.removeTempMod(EvadeCombatAbility, EvasionStateAbility)
    return


def EnableEvasion(tempMod, subject):
    subject.AbilityInv.reapplyAbility(EvadeCombatAbility)
    return


def DisableShieldRestealth(tempMod, subject):
    Utility.outputAbilityDebug('DisableShield Action %d Stance %d' % (subject.CharMvt.Action, subject.CharMvt.Stance))
    if subject.CharMvt.Stance == 8:
        return
    if tempMod.Value > 0:
        subject.AbilityInv.addTempMod(GhostStrikeAbility, ISDrainTargetAbility, tempMod.Value, 0)
        subject.AbilityInv.reapplyAbility(InvisibilityGrantAbility)
    else:
        subject.AbilityInv.reapplyAbility(SneakGrantAbility)
    return


def DesperationFunction(tempMod, subject):
    Utility.outputAbilityDebug('DesperationFunction: MaxH %d Health %d' % (subject.MaxHealth, subject.Health))
    infMod = 1.0 + subject.abilities[InitiativeBonusAbility] / 100.0
    if subject.Health > subject.MaxHealth / 2:
        tempMod.Value = infMod * 10
    else:
        tempMod.Value = infMod * (1.0 - subject.Health / (subject.MaxHealth * 1.0)) * 100.0
    return


def CalcSpotlightTime(tempMod, subject):
    tm = time.localtime()
    if subject is None:
        Utility.outputAbilityDebug('Subject none')
    elif subject.AbilityInv is None:
        Utility.outputAbilityDebug('AbilityInv none')
    Utility.outputAbilityDebug('time.localtime : %s' % tm)
    targetTimeYear = subject.TargetYear
    targetTimeMonth = subject.TargetMonth
    targetTimeDay = subject.TargetDay
    targetTimeHour = subject.TargetHour
    targetTimeMin = subject.TargetMin
    duration = 0
    actualTime = 0
    targetTime = 0
    if tm[0] != targetTimeYear:
        Utility.outputAbilityDebug('Year != %d, spotlight will not auto-setup' % targetTimeYear)
    elif tm[1] != targetTimeMonth:
        Utility.outputAbilityDebug('Month != %d' % targetTimeMonth)
        if tm[1] == targetTimeMonth - 1:
            Utility.outputAbilityDebug('Month = 1 month in advance')
            actualTime = tm[2] * 86400 + tm[3] * 3600 + tm[4] * 60 + tm[5]
            targetTime = 2678400 + targetTimeDay * 86400 + targetTimeHour * 3600 + targetTimeMin * 60
            if actualTime > targetTime:
                Utility.outputAbilityDebug('Actual Time: %d > Target Time: %d' % (actualTime, targetTime))
            else:
                duration = targetTime - actualTime
                Utility.outputAbilityDebug('CalcSpotlightTime: %d' % duration)
                procs = ('', 'ActivateSpotlight', '', '', '')
                subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, NullTargetAbility, duration)
        else:
            Utility.outputAbilityDebug('Month not usable, spotlight will not auto-setup')
    else:
        Utility.outputAbilityDebug('Month = same month')
        actualTime = tm[2] * 86400 + tm[3] * 3600 + tm[4] * 60 + tm[5]
        targetTime = targetTimeDay * 86400 + targetTimeHour * 3600 + targetTimeMin * 60
        if actualTime > targetTime:
            Utility.outputAbilityDebug('Actual Time: %d > Target Time: %d' % (actualTime, targetTime))
        else:
            duration = targetTime - actualTime
            Utility.outputAbilityDebug('CalcSpotlightTime: %d' % duration)
            procs = ('', 'ActivateSpotlight', '', '', '')
            subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, NullTargetAbility, duration)
    return


def ActivateSpotlight(tempMod, subject):
    if tempMod.TimeLeft <= 0:
        Utility.outputAbilityDebug('Activating Spotlight')
        subject.LightStateMachine.setState('SLightTrigger')
    return


def ObjApplySpotlightSpawn(tempMod, subject):
    srcObj = discovery.getObjectFromLocator(tempMod.Source)
    if srcObj is None:
        Utility.outputAbilityDebug('Spawning Default Sequence %d ' % tempMod.Value)
    else:
        Utility.outputAbilityDebug('Spawning Sequence %d ' % srcObj.AbilityInv.SpawnSequence)
        subject.PlayerCharacter.spawnSequence(srcObj.AbilityInv.SpawnSequence)
    return


def ObjApplyNPCSpawn(tempMod, subject):
    if subject.PlayerCharacter is not None:
        subject.PlayerCharacter.spawnSequence(tempMod.Value)
    return


def PVPFlagOnInit(tempMod, subject):
    Utility.outputAbilityDebug('PVPFlagFlipper: activating flag.')
    subject.AbilityInv.flipPVPFlag(1)
    return


def PVPFlagMsgOnInit(tempMod, subject):
    Utility.outputAbilityDebug('PVPFlagFlipper: activating flag.')
    if not subject.AbilityInv.isPvP():
        discovery.clientSystemMessage(subject.locator, StringTable.ID_ABILITY_PVPPULSE, constants.Chat.CT_SYS_DUNGEON_MASTER)
        subject.AbilityInv.flipPVPFlag(1)
    return


def PVPFlagMsgOnTimeInit(tempMod, subject):
    if not subject.AbilityInv.isPvP() and tempMod.TimeLeft <= 0:
        discovery.clientSystemMessage(subject.locator, StringTable.ID_ABILITY_PACZONE_LEAVE, constants.Chat.CT_SYS_DUNGEON_MASTER)
        subject.AbilityInv.updatePvP(1)
    return


def PVPFlagMsgOffInit(tempMod, subject):
    if subject.AbilityInv.isPvP():
        discovery.clientSystemMessage(subject.locator, StringTable.ID_ABILITY_PACZONE, constants.Chat.CT_SYS_DUNGEON_MASTER)
        subject.AbilityInv.flipPVPFlag(0)
    return


def RemoveSearchlightMods(tempMod, subject):
    if subject.AbilityInv.hasTempMod(SpotlightAuraAbility, 0):
        Utility.outputAbilityDebug('Removing Effect 1')
        subject.AbilityInv.removeTempMod(SpotlightAuraAbility, 0)
    if subject.AbilityInv.hasTempMod(SpotlightAuraAbility, NullTargetAbility):
        Utility.outputAbilityDebug('Removing Effect 2')
        subject.AbilityInv.removeTempMod(SpotlightAuraAbility, NullTargetAbility)
    if subject.AbilityInv.hasTempMod(SpotlightAuraAbility, ISDrainTargetAbility):
        Utility.outputAbilityDebug('Removing Effect 3')
        subject.AbilityInv.removeTempMod(SpotlightAuraAbility, ISDrainTargetAbility)
    return


def PlaySpotlightAuraFX(tempMod, subject):
    tempMod.Value = 2952790074L
    PlayFX(tempMod, subject)
    return


def TurnOffStealthAbils(tempMod, subject):
    Utility.outputAbilityDebug('TurnOffStealthAbils')
    subject.AbilityInv.turnOffAbility(SneakGrantAbility, 1, 1, 1)
    subject.AbilityInv.turnOffAbility(ConcealmentGrantAbility, 1, 1, 1)
    subject.AbilityInv.turnOffAbility(EscapeandEvasionAbility, 1, 1, 1)
    subject.AbilityInv.turnOffAbility(InvisibilityGrantAbility, 1, 1, 1)
    subject.AbilityInv.turnOffAbility(DisguiseGrantAbility, 1, 1, 1)
    return


def TestOrgAbility(tempMod, subject):
    Utility.outputAbilityDebug('Loading Org Ability')
    Utility.outputAbilityDebug('-- Player Org ID: %d' % subject.OrganizationID)
    thisParent = tempMod.ParentAbility
    targetOrg = Utility.GetPropertyFromAbility(thisParent, 'OrganizationID')
    if targetOrg < 1 or targetOrg > 3:
        Utility.outputAbilityDebug('-- ERROR: Invalid Target Org ID; doing nothing')
        return
    if subject.OrganizationID != targetOrg:
        discovery.clientSystemMessage(subject.locator, Utility.GetPropertyFromAbility(thisParent, 'StringWrongOrg'))
        subject.AbilityInv.removeAbilityFromAbility(thisParent)
        return
    if targetOrg == 1:
        targetRep = subject.ReputationZionMilitary
    if targetOrg == 2:
        targetRep = subject.ReputationMachines
    if targetOrg == 3:
        targetRep = subject.ReputationMerovingian
    Utility.outputAbilityDebug('-- Rep: %d' % targetRep)
    abRep = Utility.GetPropertyFromAbility(thisParent, 'MinRep')
    if targetRep < abRep:
        discovery.clientSystemMessage(subject.locator, Utility.GetPropertyFromAbility(thisParent, 'StringLowRep'))
        subject.AbilityInv.removeAbilityFromAbility(thisParent)
        return
    bBase = Utility.GetPropertyFromAbility(thisParent, 'BaseAbility')
    if not bBase:
        return
    enemyAb1 = (~(1 << 31) & Utility.GetPropertyFromAbility(thisParent, 'EnemyAbility1')) / 2 ** 10
    Utility.outputAbilityDebug('-- Enemy Ability 1 ID: %d' % enemyAb1)
    if subject.hasAbility[enemyAb1]:
        discovery.clientSystemMessage(subject.locator, Utility.GetPropertyFromAbility(thisParent, 'StringEnemy1'))
        subject.AbilityInv.removeAbilityFromAbility(enemyAb1)
    enemyAb2 = (~(1 << 31) & Utility.GetPropertyFromAbility(thisParent, 'EnemyAbility2')) / 2 ** 10
    Utility.outputAbilityDebug('-- Enemy Ability 2 ID: %d' % enemyAb2)
    if subject.hasAbility[enemyAb2]:
        discovery.clientSystemMessage(subject.locator, Utility.GetPropertyFromAbility(thisParent, 'StringEnemy2'))
        subject.AbilityInv.removeAbilityFromAbility(enemyAb2)
    return


def OnHealthReduceGiveISProc(tempMod, subject, damageTaken):
    if damageTaken < 0:
        return
    if subject.Health >= subject.MaxHealth:
        return
    restoreVal = -(damageTaken * tempMod.Value / 100)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_HACKER_HEALTH_TARGETBASE, 0)
    Utility.outputAbilityDebug('OnHealthReduceGiveISProc %d' % restoreVal)
    subject.AbilityInv.takeInnerStrength(restoreVal)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_RESTORE_INNER_STRENGTH_SELF, tempMod.ParentAbility, subject.locator, subject.locator, -restoreVal)
    return


def OnHealthReduceBurnISProc(tempMod, subject, damageTaken):
    burnVal = damageTaken * tempMod.Value / 100
    tempMod.Value = damageTaken
    GenericConstHealthRegenEvent(tempMod, subject)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_HACKER_HEALTH_TARGETBASE, 0)
    Utility.outputAbilityDebug('OnHealthReduceBurnISProc %d' % burnVal)
    subject.AbilityInv.takeInnerStrength(burnVal)
    return


def OnHealthReduceAddValueProc(tempMod, subject, damageTaken):
    if damageTaken < 0:
        return
    if subject.Health >= subject.MaxHealth:
        return
    bonusVal = damageTaken / 100
    tempMod.Value = tempMod.Value + bonusVal
    if tempMod.Value > 30:
        tempMod.Value = 30
    Utility.outputAbilityDebug('OnHealthReduceAddValueProc %d' % bonusVal)
    return


def MervISDrain(tempMod, subject):
    drainVal = tempMod.Value
    Utility.outputAbilityDebug('MervISDrain %d' % drainVal)
    subject.AbilityInv.takeInnerStrength(drainVal)
    return


def ApplyMachineEffectHealthChance(tempMod, subject, damageTaken):
    if damageTaken < 0:
        return
    if subject.Health >= subject.MaxHealth:
        return
    roll = random.randrange(0, 100)
    if roll < 5:
        subject.AbilityInv.reapplyAbility(MachineEEffectAbility)
    return


def ApplyMervEffectHealthChance(tempMod, subject, damageTaken):
    if damageTaken < 0:
        return
    if subject.Health >= subject.MaxHealth:
        return
    roll = random.randrange(0, 100)
    if roll < 5:
        subject.AbilityInv.reapplyAbility(MervEEffectAbility)
    return


def ApplyZionEffectHealthChance(tempMod, subject, damageTaken):
    if damageTaken < 0:
        return
    if subject.Health >= subject.MaxHealth:
        return
    roll = random.randrange(0, 100)
    if roll < 5:
        subject.AbilityInv.reapplyAbility(ZionEEffectAbility)
    return


def ZionRestoreIS(tempMod, subject):
    restoreVal = -tempMod.Value
    Utility.outputAbilityDebug('ZionRestoreIS %d' % tempMod.Value)
    subject.AbilityInv.takeInnerStrength(restoreVal)
    return


def ToolScanLoadout(tempMod, subject):
    if subject is None:
        Utility.outputAbilityDebug('Subject none')
        return
    elif subject.AbilityInv is None:
        Utility.outputAbilityDebug('AbilityInv none')
        return
    bFound = False
    bOrgFound = False
    bConFound = False
    bBuffFound = False
    mods = subject.AbilityInv.getTempMods()
    if mods is not None:
        for mod in mods:
            pAbil = mod.ParentAbility
            Utility.outputAbilityDebug('Parent Ability: %d' % pAbil)
            if pAbil is not None:
                if pAbil == ZionAAbility or pAbil == MachineAAbility or pAbil == MervAAbility:
                    bFound = True
                    bOrgFound = True
                elif pAbil == TacticBoost1Ability or pAbil == TacticBoost2Ability or pAbil == TacticBoost3Ability or pAbil == TacticBoost4Ability or pAbil == PillBolsterHealthAbility or pAbil == PillBolsterHealth1Ability or pAbil == PillBolsterHealth2Ability or pAbil == PillBolsterHealth3Ability or pAbil == PillBolsterHealth4Ability:
                    bFound = True
                    bConFound = True
                elif pAbil == BolsterHealth1Ability or pAbil == BolsterHealth2Ability or pAbil == BolsterHealth3Ability or pAbil == CombatEnhancement1Ability or pAbil == Efficiency1Ability or pAbil == EnhancedDodgeAbility or pAbil == FastHealing1Ability or pAbil == FastHealing2Ability or pAbil == ImprovedHackingAbility or pAbil == MovementAccelerator1Ability or pAbil == MovementAccelerator2Ability or pAbil == MovementAccelerator3Ability or pAbil == NegativeConditionSweep1Ability or pAbil == NegativeConditionSweep2Ability or pAbil == NetworkFirewall1Ability or pAbil == PersonalFirewall1Ability or pAbil == PersonalFirewall2Ability or pAbil == ResistCombat1Ability or pAbil == SweepParty1Ability or pAbil == SweepParty2Ability or pAbil == UpgradeHealthAbility or pAbil == UpgradeMasterBuffAbility:
                    bFound = True
                    bBuffFound = True

    else:
        Utility.outputAbilityDebug('No tempmods found')
    if bFound == True:
        if bOrgFound == True:
            Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_SCANNER_ORGFOUND, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        if bConFound == True:
            Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_SCANNER_CONFOUND, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        if bBuffFound == True:
            Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_SCANNER_BUFFFOUND, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
    else:
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_SCANNER_NONEFOUND, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
    return


def RandomCombatStateChance(tempMod, subject):
    roll = random.randrange(100)
    Utility.outputAbilityDebug('RandomCombatStateChance: %d %d' % (roll, tempMod.Value))
    if roll < tempMod.Value:
        states = [
         DazeAbility, OffBalanceAbility, StaggeredAbility]
        subject.AbilityInv.addStateMod(tempMod.Source, tempMod.ParentAbility, states[random.randrange(3)], 0, 0, float(tempMod.Value))
    return


def SetZombieModel(tempMod, subject):
    if tempMod.Value == 0:
        model = 1476398198
    else:
        model = 1476398197
    subject.CharMvt.setModel(model)
    if subject.AbilityInv.hasTempMod(HalloweenMaskAbility, NullTargetAbility) or subject.AbilityInv.hasTempMod(HalloweenMask2Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
        subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    if subject.AbilityInv.hasTempMod(RSICaptureAbility, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    if subject.AbilityInv.hasTempMod(RSICapKuno83Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno84Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno85Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno86Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno87Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno88Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno89Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno89Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno90Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno90Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno91Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno91Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno92Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno92Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno93Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno93Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno94Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno94Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno95Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno96Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno97Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno98Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno99Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
    if subject.AbilityInv.hasTempMod(RSICapKuno100Ability, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
    if subject.AbilityInv.hasTempMod(RSICapEliteAbility, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    if subject.AbilityInv.hasTempMod(RSICapExecAbility, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapExecAbility)
    if subject.AbilityInv.hasTempMod(RSICapSSRAbility, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    if subject.AbilityInv.hasTempMod(RSICapValfAbility, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    if subject.AbilityInv.hasTempMod(RSICapWrightAbility, NullTargetAbility):
        subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    if not subject.AbilityInv.hasTempMod(tempMod.ParentAbility, InvalidAbility):
        procs = ('SetZombieModel2', '', '', '', '')
        subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, InvalidAbility, 0, 0, 0, tempMod.Value)
        procs = (
         '', 'ZombieTerm2', '', 'ZombieHealth', '')
        subject.AbilityInv.addTempModProcs(procs, NullTargetAbility, InvalidAbility, 0, 0, 0, tempMod.Value)
    return


def SetZombieModel2(tempMod, subject):
    if tempMod.Value == 0:
        model = 1476398198
    else:
        model = 1476398197
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    subject.AbilityInv.deactivateAbility(RSICapExecAbility)
    subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno89Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno90Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno91Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno92Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno93Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno94Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
    subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    subject.CharMvt.setModel(model)
    return


def ZombieTerm2(tempMod, subject):
    if subject.PlayerCharacter is not None:
        subject.AbilityInv.removeAbilityGrant(ZombieVirus2BaseAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus3BaseAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus2BiteAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus3BiteAbility)
        subject.AbilityInv.removeAbilityGrant(NPCKilledOnlyByHolyWaterAbility)
        subject.AbilityInv.deactivateAbility(RSICaptureAbility)
        subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
        subject.AbilityInv.deactivateAbility(RSICapExecAbility)
        subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno89Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno90Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno91Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno92Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno93Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno94Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
        subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
        subject.AbilityInv.deactivateAbility(RSICapValfAbility)
        subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
        subject.CharMvt.revertToRSI()
    return


def ZombieHealth(tempMod, subject, change):
    if (subject.AbilityInv.Health <= 0 or subject.Interlock.IsDead) and subject.PlayerCharacter is not None:
        subject.AbilityInv.removeAbilityGrant(ZombieVirus2BaseAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus3BaseAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus2BiteAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus3BiteAbility)
        subject.AbilityInv.removeAbilityGrant(NPCKilledOnlyByHolyWaterAbility)
        subject.AbilityInv.deactivateAbility(RSICaptureAbility)
        subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
        subject.AbilityInv.deactivateAbility(RSICapExecAbility)
        subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno89Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno90Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno91Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno92Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno93Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno94Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
        subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
        subject.AbilityInv.deactivateAbility(RSICapValfAbility)
        subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
        subject.CharMvt.revertToRSI()
    return


def ZombieCheck(tempMod, subject):
    Utility.outputAbilityDebug('CZ: %d %d %d %d' % (subject.hasAbility[ZombieVirus2BiteAbility], subject.hasAbility[ZombieVirus3BiteAbility], subject.hasAbility[ZombieVirus2BaseAbility], subject.hasAbility[ZombieVirus3BaseAbility]))
    if subject.AbilityInv.Health <= 0 or subject.Interlock.IsDead:
        return
    if subject.hasAbility[ZombieVirus2BaseAbility]:
        subject.AbilityInv.deactivateAbility(RSICaptureAbility)
        subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
        subject.AbilityInv.deactivateAbility(RSICapExecAbility)
        subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno89Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno90Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno91Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno92Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno93Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno94Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
        subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
        subject.AbilityInv.deactivateAbility(RSICapValfAbility)
        subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus2BiteAbility)
        subject.AbilityInv.grantAbility(ZombieVirus2BiteAbility, 0)
        subject.AbilityInv.grantAbility(NPCKilledOnlyByHolyWaterAbility, 1)
    elif subject.hasAbility[ZombieVirus3BaseAbility]:
        subject.AbilityInv.deactivateAbility(RSICaptureAbility)
        subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
        subject.AbilityInv.deactivateAbility(RSICapExecAbility)
        subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno89Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno90Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno91Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno92Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno93Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno94Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
        subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
        subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
        subject.AbilityInv.deactivateAbility(RSICapValfAbility)
        subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus3BiteAbility)
        subject.AbilityInv.grantAbility(ZombieVirus3BiteAbility, 0)
        subject.AbilityInv.grantAbility(NPCKilledOnlyByHolyWaterAbility, 1)
    return


def AddItem(tempMod, subject):
    subject.Inventory.addItem(tempMod.Value)
    return


def SnowmanProc(tempMod, subject):
    if subject.Inventory.hasItem(46655) or subject.Inventory.hasItem(46656) or subject.Inventory.hasItem(46657):
        subject.Inventory.addItem(43009, 0)
        subject.Inventory.addItem(43010, 0)
        subject.Inventory.addItem(43011, 0)
        subject.Inventory.addItem(43012, 0)
        PVPFlagOnInit(tempMod, subject)
        discovery.clientSystemMessage(subject.locator, StringTable.ID_CLIENT_SNOWBALL_GENERATED, constants.Chat.CT_SYS_DUNGEON_MASTER)
    return


def RemovePrisonMod(tempMod, subject):
    subject.AbilityInv.deactivateAbility(PrisonMentalityAbility)
    subject.AbilityInv.deactivateAbility(PrisonMasteryAbility)
    return


def SetSmithVirusModel(tempMod, subject):
    model = discovery.getGameObjectPropValue(1 << 31 | tempMod.ParentAbility << 10, 'ModelOverride')
    if subject.UseRSIDescription != 0:
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_SMITH_VIRUS_ACTIVATE, tempMod.ParentAbility, subject.locator, subject.locator, 0)
    if model is not None:
        subject.CharMvt.setModel(model)
    if not subject.AbilityInv.hasTempMod(tempMod.ParentAbility, InvalidAbility):
        procs = ('SetEventModel', '', '', '', '')
        subject.AbilityInv.addTempModProcs(procs, tempMod.ParentAbility, InvalidAbility, 0, 0, 0, tempMod.Value)
        procs = (
         '', 'SmithVirusTerm', '', 'SmithVirusHealth', '')
        subject.AbilityInv.addTempModProcs(procs, NullTargetAbility, InvalidAbility, 0, 0, 0, tempMod.Value)
    return


def SetEventModel(tempMod, subject):
    model = discovery.getGameObjectPropValue(1 << 31 | tempMod.ParentAbility << 10, 'ModelOverride')
    if model is not None:
        subject.CharMvt.setModel(model)
    return


def SmithVirusHealth(tempMod, subject, change):
    if (subject.AbilityInv.Health <= 0 or subject.Interlock.IsDead) and subject.PlayerCharacter is not None:
        subject.AbilityInv.removeAbilityGrant(SmithVirus1BaseAbility)
        subject.AbilityInv.removeAbilityGrant(SmithVirus2BaseAbility)
        subject.AbilityInv.removeAbilityGrant(SmithVirus1AttackAbility)
        subject.AbilityInv.removeAbilityGrant(SmithVirus2AttackAbility)
    return


def SmithVirusCheck(tempMod, subject):
    Utility.outputAbilityDebug('SM: %d %d %d %d' % (subject.hasAbility[SmithVirus1BaseAbility], subject.hasAbility[SmithVirus2BaseAbility], subject.hasAbility[SmithVirus1AttackAbility], subject.hasAbility[SmithVirus2AttackAbility]))
    if subject.AbilityInv.Health <= 0 or subject.Interlock.IsDead:
        return
    if subject.hasAbility[SmithVirus1BaseAbility]:
        subject.AbilityInv.removeAbilityGrant(SmithVirus1AttackAbility)
        subject.AbilityInv.grantAbility(SmithVirus1AttackAbility, 0)
    elif subject.hasAbility[SmithVirus2BaseAbility]:
        subject.AbilityInv.removeAbilityGrant(SmithVirus2AttackAbility)
        subject.AbilityInv.grantAbility(SmithVirus2AttackAbility, 0)
    elif tempMod.ParentAbility == SmithVirus2AttackAbility:
        subject.AbilityInv.removeAbilityGrant(SmithVirus1BaseAbility)
        subject.AbilityInv.grantAbility(SmithVirus1BaseAbility, 0)
    else:
        subject.AbilityInv.removeAbilityGrant(SmithVirus2BaseAbility)
        subject.AbilityInv.grantAbility(SmithVirus2BaseAbility, 0)
    return


def SmithVirusTerm(tempMod, subject):
    Utility.outputAbilityDebug('SmithVirusTerm')
    if (subject.hasAbility[SmithVirus1BaseAbility] or subject.hasAbility[SmithVirus2BaseAbility]) and (tempMod.ParentAbility == SmithEndBrownieAbility or tempMod.ParentAbility == OracleAnniversaryCookieAbility) and subject.PlayerCharacter is not None:
        if subject.PlayerCharacter is not None:
            subject.CharMvt.revertToRSI()
        if tempMod.Source == subject.locator:
            Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_SMITH_END_SELF, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        else:
            Utility.SendAbilityOutputToTarget(StringTable.ID_CLIENT_ABILITY_SMITH_END_TARGET, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
            Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_SMITH_END_SUBJECT, tempMod.ParentAbility, tempMod.Source, subject.locator, 0)
        subject.AbilityInv.removeTempMod(SmithVirus1AttackAbility, NullTargetAbility)
        subject.AbilityInv.removeTempMod(SmithVirus2AttackAbility, NullTargetAbility)
    subject.AbilityInv.removeAbilityGrant(SmithVirus1BaseAbility)
    subject.AbilityInv.removeAbilityGrant(SmithVirus1AttackAbility)
    subject.AbilityInv.removeAbilityGrant(SmithVirus2BaseAbility)
    subject.AbilityInv.removeAbilityGrant(SmithVirus2AttackAbility)
    return


def DeactivateShadow(tempMod, subject):
    Utility.outputAbilityDebug('DeactivateShadow')
    if subject.AbilityInv is not None:
        if subject.AbilityInv.hasTempMod(MasterShadowActiveAbility, NullTargetAbility):
            Utility.outputAbilityDebug('  Deactivating Shadow')
            subject.AbilityInv.deactivateAbility(MasterShadowActiveAbility)
    return


def DeactivateShadowIfInInv(tempMod, subject):
    Utility.outputAbilityDebug('DeactivateShadowIfInInv %d' % tempMod.ParentAbility)
    if subject.Inventory.hasItem(tempMod.ParentAbility):
        DeactivateShadow(tempMod, subject)
    return


def PvPIfInInventory(tempMod, subject):
    Utility.outputAbilityDebug('PvPIfInInventory %d' % tempMod.ParentAbility)
    if subject.Inventory.hasItem(tempMod.ParentAbility):
        PVPFlagOnInit(tempMod, subject)
    return


def OverdriveTerm2(tempMod, subject):
    if subject.AbilityInv.ConditionStateFlags & constants.combat.ST_COMBAT_EFFECT:
        subject.AbilityInv.addStateMod(subject.locator, CombatEffectAbility, EnrageAbility, 0, 0, 0)
    return


def OverdriveTerm(tempMod, subject):
    if subject.AbilityInv.ConditionStateFlags & constants.combat.ST_COMBAT_EFFECT:
        procs = (
         '', 'OverdriveTerm2', '', '', '')
        subject.AbilityInv.addTempModProcs(procs, CombatEffectAbility, NullTargetAbility, 0.1)
    return


def CombatTerm(tempMod, subject):
    if subject.AbilityInv.hasTempMod(CombatEffectAbility, EnrageAbility):
        Utility.outputAbilityDebug('CombatTerm')
        subject.AbilityInv.removeTempMod(CombatEffectAbility, EnrageAbility)
    return


def WriteCodeActivateTerm(tempMod, subject):
    if subject.Stance == Stance_StandType:
        subject.CharMvt.playScript(Stance_Stand, Action_Idle)
    return


def RefundIS(tempMod, subject):
    subject.AbilityInv.takeInnerStrength(-tempMod.Value)
    return


if constants.Build.debug:
    discovery.serverPrint('TempMods Loaded : %s' % time.ctime())
return

# okay decompiling work/monolith_pyc\tempmods.pyc
