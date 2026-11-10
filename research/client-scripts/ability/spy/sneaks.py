# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\spy\sneaks.py
# Compiled at: 2007-11-13 02:02:54
import random, traceback, obj, ltfxmap as FX
from ability.defines import *
import missionvalidate as mv, combatvalidate as cv, ability.utility as Utility, stringtable_client as StringTable, generic_free_attacks as freeattacks, tempmods as TM
SNEAKATKSPINBACKSWEEP_DAMAGE = 75
SNEAKATKDROPKICK_DAMAGE = 150
SNEAKATKSUPLEX_DAMAGE = 300
SNEAKATKSPINTHROW_DAMAGE = 300
SNEAKATKPUNT_DAMAGE = 600
SNEAKATKTOPSYTURVY_DAMAGE = 775
SNEAKATKFREEDOM2JOINTS_DAMAGE = 900
SNEAKATKDIVINGFRANK_DAMAGE = 725
SNEAKATKBULLDOG_DAMAGE = 950
SNEAKATKREVERSEBULLDOG_DAMAGE = 1200

def SneakGrant_Test(sentence):
    sentence.result = SUCCESS
    if sentence.subject.AbilityInv.hasTempMod(SneakFreeDisableShieldAbility, ISDrainTargetAbility) or Utility.InitialSneakTest(sentence.subject):
        discovery.clientSystemMessage(sentence.subject.locator, StringTable.ID_CLIENT_ABILITY_SNEAKER_CANNOT_SNEAK)
        sentence.result = FAILURE
    return


SneakGrant_Test.depAttr = '\n'

def ConcealmentGrant_DirectObject(subject, msg):
    Utility.outputAbilityDebug('Concealment activated on self')
    subject.CharMvt.addDissemblingType(kSneak)
    procs = (
     '', 'Concealment_RemoveConcealmentStealth', 'ConcealUpdate', '', '')
    subject.AbilityInv.addTempModProcs(procs, ConcealmentGrantAbility, ConcealmentGrantAbility, 0, 0, 0)
    Utility.turnOffAuras(subject)
    return


def ConcealmentGrant_Deactivate(subject):
    TM.Concealment_RemoveConcealmentStealth(subject, subject)
    subject.AbilityInv.removeTempMod(ConcealmentGrantAbility, ConcealmentGrantAbility)
    subject.AbilityInv.removeStateMod(ConcealedAbility)
    subject.AbilityInv.removeStateMod(StealthStateAbility)
    return


def ConcealmentGrant_Test(sentence):
    sentence.result = SUCCESS
    if Utility.InitialSneakTest(sentence.subject):
        discovery.clientSystemMessage(sentence.subject.locator, StringTable.ID_CLIENT_ABILITY_SNEAKER_CANNOT_CONCEAL)
        sentence.result = FAILURE
    return


ConcealmentGrant_Test.depAttr = '\n'

def EscapeandEvasion_DirectObject(subject, msg):
    Utility.outputAbilityDebug('Escape and Evasion activated on self.')
    subject.CharMvt.activateSneak()
    procs = (
     '', 'EscapeAndEvasionTerm', 'SneakUpdate', '', '')
    subject.AbilityInv.addTempModProcs(procs, EscapeandEvasionAbility, EscapeandEvasionAbility, 0, 0, 0)
    Utility.turnOffAuras(subject)
    return


def EscapeandEvasion_Deactivate(subject):
    subject.CharMvt.deactivateSneak()
    subject.AbilityInv.removeTempMod(EscapeandEvasionAbility, EscapeandEvasionAbility)
    return


def EscapeandEvasion_Test(sentence):
    sentence.result = SUCCESS
    if Utility.InitialSneakTest(sentence.subject):
        discovery.clientSystemMessage(sentence.subject.locator, StringTable.ID_CLIENT_ABILITY_SNEAKER_CANNOT_ESCAPE_AND_EVADE)
        sentence.result = FAILURE
    return


EscapeandEvasion_Test.depAttr = '\n'

def BaseSneakAttackTestFn(sentence, sneak_attack_ability):
    Utility.outputAbilityDebug('BaseSneakAttackTestFn: Test')
    sentence.result = SUCCESS
    return


BaseSneakAttackDepAttr = '\ndirectObject.hasAbility[SneakAttackDetectionAbility]\ndirectObject.abilities[SneakAttackDetectionAbility]\ndirectObject.Position\ndirectObject.Orientation\ndirectObject.IsInCombat\ndirectObject.locator\n'

def SneakAttackSpinBack_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackSpinBack: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackSpinBackAbility)
    return


SneakAttackSpinBack_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackDropKick_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackDropKick: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackDropKickAbility)
    return


SneakAttackDropKick_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackPunt_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackPunt: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackPuntAbility)
    return


SneakAttackPunt_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackSpinThrow_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackSpinThrow: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackSpinThrowAbility)
    return


SneakAttackSpinThrow_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackSuplex_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackSuplex: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackSuplexAbility)
    return


SneakAttackSuplex_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackTopsyTurvy_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackTopsyTurvy: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackTopsyTurvyAbility)
    return


SneakAttackTopsyTurvy_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackDivingFrank_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackDivingFrank: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackDivingFrankAbility)
    return


SneakAttackDivingFrank_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackBullDog_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackBullDog: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackBullDogAbility)
    return


SneakAttackBullDog_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackRevBullDog_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackRevBullDog: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackRevBullDogAbility)
    return


SneakAttackRevBullDog_Test.depAttr = BaseSneakAttackDepAttr

def SneakAttackFreeJoints_Test(sentence):
    Utility.outputAbilityDebug('SneakAttackFreeJoints: Test')
    BaseSneakAttackTestFn(sentence, SneakAttackFreeJointsAbility)
    return


SneakAttackFreeJoints_Test.depAttr = BaseSneakAttackDepAttr

def SneakFreePrototype_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


def SneakFreeHarryingAttack_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


def SneakFreeDistractingBlow_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


def SneakFreeInfuriatingAssault_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


def SneakFreeLegThrow_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


def SneakFreeDazzlingThrow_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


def StaggeringThrow_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


def SneakFreeWeakeningThrow_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


def SneakFreeWardingBlow_Subject(subject, msg):
    subject.CharMvt.playScriptWithTarget(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow, msg.directObjectLocator)
    return


return

# okay decompiling work/monolith_pyc\ability\spy\sneaks.pyc
