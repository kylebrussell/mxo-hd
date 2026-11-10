# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\soldier\old_sm_karate.py
# Compiled at: 2006-03-30 16:29:38
import traceback, obj, ability.utility as Utility
from ability.defines import *
import stringtable_client as StringTable
KUNGFU_DIMMAK_STRIKE_DAMAGE = 10
KARATE_GUARD_BREAKER_DAMAGE = 50
KARATE_SWIRLING_KI_SUMMON_DAMAGE = 75
KARATE_JUMP_SIDEKICK_DAMAGE = 215
KARATE_KI_CHARGED_REVERSE_PUNCH_DAMAGE = 300
KARATE_MACHINEGUN_KICK_DAMAGE = 325
KARATE_POWER_SWEEP_TO_KI_CHARGED_PUNCH_DAMAGE = 150
KARATE_SKY_HIGH_SIDE_KICK_DAMAGE = 250
KARATE_BERSERKER_MODS_DURATION = 15
KARATE_GUARD_BREAKER_BLOCK_BOOST_DURATION = 15
KARATE_SKY_HIGH_SIDE_KICK_STUN_DURATION = 15
KARATE_POWER_SWEEP_TO_KI_CHARGED_PUNCH_BLOCK_BOOST_DURATION = 12
KARATE_SWIRLING_KI_SUMMON_DURATION = 10
KARATE_BERSERKER_ATTACK_MOD = 50
KARATE_BERSERKER_DEFENSE_MOD = 50
KARATE_BERSERKER_DAMAGE_BONUS_MOD = 30
KARATE_BERSERKER_LIFE_COST = 60
KARATE_GUARD_BREAKER_BLOCK_BOOST = -50
KARATE_POWER_SWEEP_TO_KI_CHARGED_PUNCH_BLOCK_BOOST = -50
KARATE_SWIRLING_KI_SUMMON_SPEED_EFFECT_MOD = 1
KARATE_SWIRLING_KI_SUMMON_SPEED_DEFENSE = 2

def KarateBerserkerAttack_Subject(subject, msg):
    if msg.result == DEFLECTED:
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_NOT_ENOUGH_LIFE, KarateBerserkerAttackAbility, msg.subjectLocator, msg.directObjectLocator, 0)
        return
    subject.AbilityInv.addTempMod(KarateBerserkerAttackAbility, DefenseAbility, KARATE_BERSERKER_DEFENSE_MOD, KARATE_BERSERKER_MODS_DURATION, 0, 0)
    subject.AbilityInv.addTempMod(KarateBerserkerAttackAbility, AttackAbility, KARATE_BERSERKER_ATTACK_MOD, KARATE_BERSERKER_MODS_DURATION, 0, 0)
    subject.AbilityInv.addTempMod(KarateBerserkerAttackAbility, DamageBonus, KARATE_BERSERKER_DAMAGE_BONUS_MOD, KARATE_BERSERKER_MODS_DURATION, 0, 0)
    Utility.outputAbilityDebug('KarateBerserkerAttack: %d to Attack & %d to Defense rolls, %d BonusDamage  ,and -%d health.' % (KARATE_BERSERKER_ATTACK_MOD, KARATE_BERSERKER_DEFENSE_MOD, KARATE_BERSERKER_DAMAGE_BONUS_MOD, KARATE_BERSERKER_LIFE_COST))
    Utility.outputAbilityDebug('KarateBerserkerAttack: Health is currently %d' % subject.AbilityInv.Health)
    if subject.AbilityInv.Health <= KARATE_BERSERKER_LIFE_COST:
        damage = subject.Interlock.specialityDamageFromAbility(msg.subjectLocator, subject.AbilityInv.Health - 1)
        Utility.SendAbilityOutputToCaster(StringTable.ID_APPLY_DAMGE_TO_SELF, KarateBerserkerAttackAbility, msg.subjectLocator, msg.directObjectLocator, damage)
        Utility.outputAbilityDebug("KarateBerserkerAttack: Health set to 1 to ensure charater doesn't die. (Temp Solution)")
    else:
        damage = subject.Interlock.specialityDamageFromAbility(msg.subjectLocator, KARATE_BERSERKER_LIFE_COST)
        Utility.SendAbilityOutputToCaster(StringTable.ID_APPLY_DAMGE_TO_SELF, KarateBerserkerAttackAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def KarateBerserkerAttack_Test(sentence):
    sentence.result = SUCCESS
    if sentence.subject.AbilityInv.Health <= KARATE_BERSERKER_LIFE_COST:
        sentence.result = DEFLECTED
    return


KarateBerserkerAttack_Test.depAttr = Utility.BaseCombatSpecialMoveDepAttr

def KarateGuardBreaker_DirectObject(subject, msg):
    Utility.outputAbilityDebug('KarateGuardBreaker: %d to Defense tactical setting and %d damage dealt' % (KARATE_GUARD_BREAKER_BLOCK_BOOST, KARATE_GUARD_BREAKER_DAMAGE))
    value = subject.AbilityInv.createSpecificMod(MOD_AFFECTS_DEFENSEMOD, MOD_BLOCK, KARATE_GUARD_BREAKER_BLOCK_BOOST)
    value2 = subject.AbilityInv.createSpecificMod(MOD_AFFECTS_ATTACKMOD, MOD_BLOCK, KARATE_GUARD_BREAKER_BLOCK_BOOST)
    subject.AbilityInv.addTempMod(KarateGuardBreakerAbility, CombatModifierAbility, value, KARATE_GUARD_BREAKER_BLOCK_BOOST_DURATION, 0, 0)
    subject.AbilityInv.addTempMod(KarateGuardBreakerAbility, CombatModifierAbility, value2, KARATE_GUARD_BREAKER_BLOCK_BOOST_DURATION, 0, 0)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, KARATE_GUARD_BREAKER_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, KarateGuardBreakerAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def KarateSidekickCombo_DirectObject(subject, msg):
    Utility.outputAbilityDebug('KarateSidekickCombo: %d damage dealt' % KARATE_JUMP_SIDEKICK_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, KARATE_JUMP_SIDEKICK_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, KarateSidekickComboAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def KarateKiChargedReversePunch_DirectObject(subject, msg):
    Utility.outputAbilityDebug('KarateKiChargedReversePunch: %d damage dealt' % KARATE_KI_CHARGED_REVERSE_PUNCH_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, KARATE_KI_CHARGED_REVERSE_PUNCH_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, KarateKiChargedReversePunchAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def KarateMachinegunKick_DirectObject(subject, msg):
    Utility.outputAbilityDebug('KarateMachinegunKick: %d damage dealt' % KARATE_MACHINEGUN_KICK_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, KARATE_MACHINEGUN_KICK_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, KarateMachinegunKickAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def KaratePowerSweepToKiChargedPunch_DirectObject(subject, msg):
    value = subject.AbilityInv.createSpecificMod(MOD_AFFECTS_DEFENSEMOD, MOD_BLOCK, KARATE_POWER_SWEEP_TO_KI_CHARGED_PUNCH_BLOCK_BOOST)
    value2 = subject.AbilityInv.createSpecificMod(MOD_AFFECTS_ATTACKMOD, MOD_BLOCK, KARATE_POWER_SWEEP_TO_KI_CHARGED_PUNCH_BLOCK_BOOST)
    subject.AbilityInv.addTempMod(KaratePowerSweepToKiChargedPunchAbility, CombatModifierAbility, value, KARATE_POWER_SWEEP_TO_KI_CHARGED_PUNCH_BLOCK_BOOST_DURATION, 0, 0)
    subject.AbilityInv.addTempMod(KaratePowerSweepToKiChargedPunchAbility, CombatModifierAbility, value2, KARATE_POWER_SWEEP_TO_KI_CHARGED_PUNCH_BLOCK_BOOST_DURATION, 0, 0)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, KARATE_POWER_SWEEP_TO_KI_CHARGED_PUNCH_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, KaratePowerSweepToKiChargedPunchAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def KarateSkyHighSideKick_DirectObject(subject, msg):
    Utility.outputAbilityDebug('KarateSkyHighSideKick: stunned for %d seconds and %d damage dealt' % (KARATE_SKY_HIGH_SIDE_KICK_STUN_DURATION, KARATE_SKY_HIGH_SIDE_KICK_DAMAGE))
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, KARATE_SKY_HIGH_SIDE_KICK_DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, KarateSkyHighSideKickAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    return


def KarateSwirlingKiSummon_DirectObject(subject, msg):
    Utility.outputAbilityDebug('KarateSwirlingKiSummon: confused for %d seconds and %d damage dealt' % (KARATE_SWIRLING_KI_SUMMON_DURATION, KKARATE_SWIRLING_KI_SUMMON_DAMAGE))
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, KARATE_SWIRLING_KI_SUMMON__DAMAGE)
    Utility.SendTakeDamageResultToAll(SUCCESS, KarateSwirlingKiSummon, msg.subjectLocator, msg.directObjectLocator, damage)
    return


return

# okay decompiling work/monolith_pyc\ability\soldier\old_sm_karate.pyc
