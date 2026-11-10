# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\soldier\old_sm_smg.py
# Compiled at: 2006-03-30 16:29:38
import traceback, obj, ability.utility as Utility
SMG_SUPPRESSION_FIRE_DAMAGE = 75
SMG_FULL_AUTO_DAMAGE = 250
SMG_FULL_AUTO_REDUX_DAMAGE = 300
SMG_HEAVING_VOLLEY_DAMAGE = 215
SMG_SLIDING_VOLLEY_DAMAGE = 100
SMG_CONTROLLED_BURST_DAMAGE = 90
SMG_SUPPRESSION_FIRE_POWERLESS_EFFECT_DURATION = 15

def SubmachineGunSuppressionFire_DirectObject(subject, msg):
    Utility.outputAbilityDebug('SubmachineGunSuppressionFire: %d damage dealt' % SMG_SUPPRESSION_FIRE_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, SMG_SUPPRESSION_FIRE_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, SubmachineGunSuppressionFireAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def SubmachineGunSuppressionFire_Subject(subject, msg):
    Utility.outputAbilityDebug('SubmachineGunSuppressionFire: Subject')
    return


def SubmachineGunCoveringFire_DirectObject(subject, msg):
    Utility.outputAbilityDebug('SubmachineGunCoveringFire: opponent forced to ground for 2 exchanges')
    return


def SubmachineGunFullAuto_DirectObject(subject, msg):
    Utility.outputAbilityDebug('SubmachineGunFullAuto: %d damage dealt' % SMG_FULL_AUTO_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, SMG_FULL_AUTO_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, SubmachineGunFullAutoAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def SubmachineGunFullAutoRedux_DirectObject(subject, msg):
    Utility.outputAbilityDebug('SubmachineGunFullAutoRedux: %d damage dealt' % SMG_FULL_AUTO_REDUX_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, SMG_FULL_AUTO_REDUX_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, SubmachineGunFullAutoReduxAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def SubmachineGunHeavingVolley_DirectObject(subject, msg):
    Utility.outputAbilityDebug('SubmachineGunHeavingVolley: %d damage dealt' % SMG_HEAVING_VOLLEY_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, SMG_HEAVING_VOLLEY_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, SubmachineGunHeavingVolleyAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def SubmachineGunSlidingVolley_DirectObject(subject, msg):
    Utility.outputAbilityDebug('SubmachineGunSlidingVolley: %d damage dealt' % SMG_SLIDING_VOLLEY_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, SMG_SLIDING_VOLLEY_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, SubmachineGunSlidingVolleyAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def SubmachineGunControlledBurst_DirectObject(subject, msg):
    Utility.outputAbilityDebug('SubmachineGunControlledBurst: %d damage dealt' % SMG_CONTROLLED_BURST_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, SMG_CONTROLLED_BURST_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, SubmachineGunControlledBurstAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


return

# okay decompiling work/monolith_pyc\ability\soldier\old_sm_smg.pyc
