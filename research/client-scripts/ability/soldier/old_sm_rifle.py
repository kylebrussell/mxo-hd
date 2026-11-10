# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\soldier\old_sm_rifle.py
# Compiled at: 2006-03-30 16:29:38
import traceback, obj, ability.utility as Utility
RIFLE_BODY_SHOT_DAMAGE = 50
RIFLE_CRIPPLING_SHOT_DAMAGE = 300
RIFLE_DEADLY_SHOT_DAMAGE = 325
RIFLE_DISABLING_SHOT_DAMAGE = 90
RIFLE_IMMOBILIZING_SHOT_DAMAGE = 75
RIFLE_RIFLE_BUTT_DAMAGE = 215
RIFLE_THREE_ROUND_BURST_DAMAGE = 100
RIFLE_WOUNDING_SHOT_DAMAGE = 250
RIFLE_WOUNDING_SHOT_WOUND_DAMAGE_PER_5_SECONDS = 50
RIFLE_IMMOBILIZING_SHOT_STUN_DURATION = 15
RIFLE_DISABLING_SHOT_ABILITY_MOD_DURATION = 15
RIFLE_WOUNDING_SHOT_WOUND_WOUNDING_EFFECT_DURATION = 15
RIFLE_RIFLE_BUTT_CONFUSE_EFFECT_DURATION = 10
RIFLE_CRIPPLING_SHOT_ABILITY_MOD_DURATION = 20
RIFLE_DISABLING_SHOT_ABILITY_MOD = -25
RIFLE_CRIPPLING_SHOT_ABILITY_MOD = -50

def RifleBodyShot_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RifleBodyShot: %d damage dealt' % RIFLE_BODY_SHOT_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, RIFLE_BODY_SHOT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, RifleBodyShotAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def RifleCripplingShot_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RifleCripplingShot: %d damage dealt' % (RIFLE_CRIPPLING_SHOT_ABILITY_MOD, RIFLE_CRIPPLING_SHOT_DAMAGE))
    subject.AbilityInv.addTempModToAllActive(RifleCripplingShotAbility, RIFLE_CRIPPLING_SHOT_ABILITY_MOD, RIFLE_CRIPPLING_SHOT_ABILITY_MOD_DURATION, 0)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, RIFLE_CRIPPLING_SHOT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, RifleCripplingShotAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def RifleDeadlyShot_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RifleDeadlyShot: %d damage dealt' % RIFLE_DEADLY_SHOT_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, RIFLE_DEADLY_SHOT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, RifleDeadlyShotAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def RifleDisablingShot_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RifleDisablingShot: %d to all abilities and %d damage dealt' % (RIFLE_DISABLING_SHOT_ABILITY_MOD, RIFLE_DISABLING_SHOT_DAMAGE))
    subject.AbilityInv.addTempModToAllActive(RifleDisablingShotAbility, RIFLE_DISABLING_SHOT_ABILITY_MOD, RIFLE_DISABLING_SHOT_ABILITY_MOD_DURATION, 0)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, RIFLE_DISABLING_SHOT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, RifleDisablingShotAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def RifleImmobilizingShot_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RifleImmobilizingShot: stunned for %d seconds and %d damage dealt' % (RIFLE_IMMOBILIZING_SHOT_STUN_DURATION, RIFLE_DEADLY_SHOT_DAMAGE))
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, RIFLE_IMMOBILIZING_SHOT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, RifleImmobilizingShotAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def RifleRifleButt_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RifleRifleButt: confused for %d seconds and %d damage dealt' % (RIFLE_RIFLE_BUTT_CONFUSE_EFFECT_DURATION, RIFLE_RIFLE_BUTT_DAMAGE))
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, RIFLE_RIFLE_BUTT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, RifleRifleButtAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def RifleThreeRoundBurst_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RifleThreeRoundBurst: %d damage dealt' % RIFLE_THREE_ROUND_BURST_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, RIFLE_THREE_ROUND_BURST_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, RifleThreeRoundBurstAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def RifleWoundingShot_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RifleWoundingShot: wounded for %d seconds and %d damage dealt' % (RIFLE_WOUNDING_SHOT_WOUND_WOUNDING_EFFECT_DURATION, RIFLE_WOUNDING_SHOT_DAMAGE))
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, RIFLE_WOUNDING_SHOT_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, RifleWoundingShotAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


return

# okay decompiling work/monolith_pyc\ability\soldier\old_sm_rifle.pyc
