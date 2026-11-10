# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\soldier\old_sm_pistol.py
# Compiled at: 2006-03-30 16:29:38
import traceback, obj, ability.utility as Utility
from ability.defines import *
PISTOL_AERIAL_DAMAGE = 250
PISTOL_BARRAGE_DAMAGE = 215
PISTOL_BLINDING_SHOT_DAMAGE = 75
PISTOL_DASH_DAMAGE = 325
PISTOL_DISARMING_SHOT_DAMAGE = 50
PISTOL_EVASIVE_DAMAGE = 90
PISTOL_POINT_BLANK_DAMAGE = 300
PISTOL_SLIDE_DAMAGE = 100
PISTOL_EXECUTION_DAMAGE = 350
PISTOL_EVASIVE_BLOCK_BOOST_DURATION = 12
PISTOL_BLINDING_SHOT_BLINDNESS_EFFECT_DURATION = 12
PISTOL_AERIAL_EFFECT_DURATION = 10
PISTOL_EVASIVE_BLOCK_BOOST = 50

def PistolAerial_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolAerial: confused for %d seconds and %d damage dealt' % (PISTOL_AERIAL_EFFECT_DURATION, PISTOL_AERIAL_DAMAGE))
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_AERIAL_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolAerialAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def PistolBarrage_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolBarrage: %d damage dealt' % PISTOL_BARRAGE_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_BARRAGE_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolBarrageAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def PistolBlindingShot_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolBlindingShot: blinded for %d seconds and %d damage dealt' % (PISTOL_BLINDING_SHOT_BLINDNESS_EFFECT_DURATION, PISTOL_BLINDING_SHOT_DAMAGE))
    SE.EnableBlindness(subject, msg.subjectLocator, PistolBlindingShotAbility, PISTOL_BLINDING_SHOT_BLINDNESS_EFFECT_DURATION, 0)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_BLINDING_SHOT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolBlindingShotAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def PistolDash_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolDash: %d damage dealt' % PISTOL_DASH_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_DASH_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolDashAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def PistolDisarmingShot_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolDisarmingShot: weapon unequipped and %d damage dealt' % PISTOL_DISARMING_SHOT_DAMAGE)
    subject.Interlock.unequipWeapon(msg.subjectLocator)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_DISARMING_SHOT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolDisarmingShotAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def PistolEvasive_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolEvasive: %d damage dealt' % PISTOL_EVASIVE_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_EVASIVE_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolEvasiveAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def PistolEvasive_Subject(subject, msg):
    Utility.outputAbilityDebug('PistolEvasive: %d to Defense rolls for %d seconds' % (PISTOL_EVASIVE_BLOCK_BOOST, PISTOL_EVASIVE_BLOCK_BOOST_DURATION))
    value = subject.AbilityInv.createSpecificMod(MOD_AFFECTS_DEFENSEMOD, MOD_BLOCK, PISTOL_EVASIVE_BLOCK_BOOST)
    value2 = subject.AbilityInv.createSpecificMod(MOD_AFFECTS_ATTACKMOD, MOD_BLOCK, PISTOL_EVASIVE_BLOCK_BOOST)
    subject.AbilityInv.addTempMod(PistolEvasiveAbility, CombatModifierAbility, value, PISTOL_EVASIVE_BLOCK_BOOST_DURATION, 0, 0)
    subject.AbilityInv.addTempMod(PistolEvasiveAbility, CombatModifierAbility, value2, PISTOL_EVASIVE_BLOCK_BOOST_DURATION, 0, 0)
    return


def PistolPointBlank_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolPointBlank: %d damage dealt' % PISTOL_POINT_BLANK_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_POINT_BLANK_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolPointBlankAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def PistolSlide_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolSlide: %d damage dealt' % PISTOL_SLIDE_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_SLIDE_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolSlideAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def PistolExecution_DirectObject(subject, msg):
    Utility.outputAbilityDebug('PistolExecution: %d damage dealt' % PISTOL_EXECUTION_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, PISTOL_EXECUTION_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, PistolExecutionAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


return

# okay decompiling work/monolith_pyc\ability\soldier\old_sm_pistol.pyc
