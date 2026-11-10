# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\soldier\old_sm_aikido.py
# Compiled at: 2006-03-30 16:29:38
import traceback, obj, ability.utility as Utility
from ability.defines import *
AIKIDO_COUNTER_THROW_DAMAGE = 75
AIKIDO_SPINNING_PIGEON_DAMAGE = 90
AIKIDO_AERIAL_TAKEDOWN_DAMAGE = 300
AIKIDO_PUNCH_REVERSAL_CATCH_SLAM_DAMAGE = 325
AIKIDO_SWORD_ATTACK_DAMAGE = 250
AIKIDO_ARM_BAR_DAMAGE = 100
AIKIDO_TOMO_NAGE_DAMAGE = 350
AIKIDO_MAKI_ONASHI_DAMAGE = 215
AIKIDO_BEND_LIKE_REED_DAMAGE = 15
AIKIDO_SPINNING_PIGEON_POWERLESS_TIME = 15
AIKIDO_PUNCH_REVERSAL_CATCH_SLAM_ENRAGE_TIME = 30
AIKIDO_IRON_GUARD_BLOCK_BOOST = 50
AIKIDO_IRON_GUARD_BLOCK_EFFECT_DURATION = 60
AIKIDO_TOMO_NAGE_STUN_DURATION = 30
AIKIDO_COUNTER_THROW_CONFUSE_DURATION = 10
AIKIDO_ARM_BAR_ENRAGE_DURATION = 15
AIKIDO_SWORD_ATTACK_CONFUSE_DURATION = 25
AIKIDO_COUNTER_THROW_POWER_POINT_BOOST = 1
AIKIDO_SWORD_ATTACK_SPEED_POINT_LOSS = 2
AIKIDO_PERFECT_SELF_DEFENSE_POINT_BOOST = 2
AIKIDO_ARM_BAR_POWER_POINT_LOSS = 2
AIKIDO_IRON_GUARD_DEFENSE_SETTING_BOOST = 50
AIKIDO_BEND_LIKE_REED_POWER_POINT_LOSS = 2

def AikidoAerialTakedown_DirectObject(subject, msg):
    Utility.outputAbilityDebug('AikidoAerialTakedown: %d damage dealt' % AIKIDO_AERIAL_TAKEDOWN_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, AIKIDO_AERIAL_TAKEDOWN_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, AikidoAerialTakedownAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def AikidoCounterThrow_DirectObject(subject, msg):
    Utility.outputAbilityDebug('AikidoCounterThrow: %d damage dealt' % AIKIDO_COUNTER_THROW_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, AIKIDO_COUNTER_THROW_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, AikidoCounterThrowAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def AikidoArmBar_DirectObject(subject, msg):
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, AIKIDO_ARM_BAR_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, AikidoArmBarAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def AikidoIronGuard_Subject(subject, msg):
    Utility.outputAbilityDebug('AikidoIronGuard: %d to Defense tactical setting' % AIKIDO_IRON_GUARD_BLOCK_BOOST)
    value = subject.AbilityInv.createSpecificMod(MOD_AFFECTS_DEFENSEMOD, MOD_BLOCK, AIKIDO_IRON_GUARD_BLOCK_BOOST)
    value2 = subject.AbilityInv.createSpecificMod(MOD_AFFECTS_ATTACKMOD, MOD_BLOCK, AIKIDO_IRON_GUARD_BLOCK_BOOST)
    subject.AbilityInv.addTempMod(AikidoIronGuardAbility, CombatModifierAbility, value, AIKIDO_IRON_GUARD_BLOCK_EFFECT_DURATION, 0, 0)
    subject.AbilityInv.addTempMod(AikidoIronGuardAbility, CombatModifierAbility, value2, AIKIDO_IRON_GUARD_BLOCK_EFFECT_DURATION, 0, 0)
    return


def AikidoTomoNage_DirectObject(subject, msg):
    Utility.outputAbilityDebug('AikidoTomoNage: %d damage dealt' % AIKIDO_TOMO_NAGE_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, AIKIDO_TOMO_NAGE_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, AikidoTomoNageAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def AikidoMakiOnashi_DirectObject(subject, msg):
    Utility.outputAbilityDebug('AikidoMakiOnashi: %d damage dealt' % AIKIDO_MAKI_ONASHI_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, AIKIDO_MAKI_ONASHI_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, AikidoMakiOnashiAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def AikidoPunchReversalCatchSlam_DirectObject(subject, msg):
    Utility.outputAbilityDebug('AikidoPunchReversalCatchSlam: %d damage dealt' % AIKIDO_PUNCH_REVERSAL_CATCH_SLAM_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, AIKIDO_PUNCH_REVERSAL_CATCH_SLAM_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, AikidoPunchReversalCatchSlamAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def AikidoSpinningClayPigeon_DirectObject(subject, msg):
    Utility.outputAbilityDebug('AikidoCSpinningClayPigeon: %d damage dealt' % AIKIDO_SPINNING_PIGEON_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, AIKIDO_SPINNING_PIGEON_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, AikidoSpinningClayPigeonAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    subject.AbilityInv.setDeactivateDelay(AikidoSpinningClayPigeonAbility, AIKIDO_SPINNING_PIGEON_POWERLESS_TIME)
    return


def AikidoSpinningClayPigeon_Deactivate(subject):
    Utility.outputAbilityDebug('AikidoSpinningClayPigeonAbility: powerless disabling')
    return


def AikidoSwordAttack_DirectObject(subject, msg):
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, AIKIDO_SWORD_ATTACK_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, AikidoSwordAttackAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


return

# okay decompiling work/monolith_pyc\ability\soldier\old_sm_aikido.pyc
