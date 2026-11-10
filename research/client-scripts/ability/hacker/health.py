# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\hacker\health.py
# Compiled at: 2008-02-21 04:32:03
import random, traceback, obj, missionvalidate as mv, combatvalidate as cv, stringtable_client as StringTable, ability.utility as Utility
from ability.defines import *
import ltfxmap as FX
RequestTime = 60

def RestoreRSI_DirectObject(subject, msg):
    duration = 120
    subject.PlayerCharacter.resuscitate()
    subject.AbilityInv.takeInnerStrength(subject.AbilityInv.InnerStrengthMax - 1)
    subject.AbilityInv.Health = 1
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_RESTORE_RSI, 0)
    subject.CharMvt.playScript(Stance_Stand, Action_Resurrect, RestoreRSIAbility)
    Utility.SendAbilityOutputToTargetMsg(StringTable.ID_CLIENT_ABILITY_REZ_DIRECTOBJECT, RestoreRSIAbility, msg)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_REZ_SUBJECT, RestoreRSIAbility, msg)
    if subject.AbilityInv.isPvP():
        discovery.sendApplyAbilityToSelf(msg.subjectLocator, 1208)
    return


def RestoreRSI_Test(sentence):
    sentence.result = SUCCESS
    return


RestoreRSI_Test.depAttr = '\n'

def RevitalizeRSI_DirectObject(subject, msg):
    subject.PlayerCharacter.resuscitate()
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_RESTORE_RSI, 0)
    subject.CharMvt.playScript(Stance_Stand, Action_Resurrect, RevitalizeRSIAbility)
    subject.AbilityInv.Health = subject.AbilityInv.MaxHealth
    subject.AbilityInv.takeInnerStrength(-subject.AbilityInv.InnerStrengthMax)
    Utility.SendAbilityOutputToTargetMsg(StringTable.ID_CLIENT_ABILITY_REZ_DIRECTOBJECT, RevitalizeRSIAbility, msg)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_REZ_SUBJECT, RevitalizeRSIAbility, msg)
    if subject.AbilityInv.isPvP():
        discovery.sendApplyAbilityToSelf(msg.subjectLocator, 1208)
    return


def RejuvenateRSI_DirectObject(subject, msg):
    subject.PlayerCharacter.resuscitate()
    subject.AbilityInv.Health = subject.AbilityInv.MaxHealth
    subject.AbilityInv.takeInnerStrength(-subject.AbilityInv.InnerStrengthMax)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_RESTORE_RSI, 0)
    subject.CharMvt.playScript(Stance_Stand, Action_Resurrect, RejuvenateRSIAbility)
    Utility.SendAbilityOutputToTargetMsg(StringTable.ID_CLIENT_ABILITY_REZ_DIRECTOBJECT, RejuvenateRSIAbility, msg)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_REZ_SUBJECT, RejuvenateRSIAbility, msg)
    if subject.AbilityInv.isPvP():
        discovery.sendApplyAbilityToSelf(msg.subjectLocator, 1208)
    return


def ReviveRSI_DirectObject(subject, msg):
    subject.PlayerCharacter.resuscitate()
    subject.AbilityInv.Health = subject.AbilityInv.MaxHealth
    subject.AbilityInv.takeInnerStrength(-subject.AbilityInv.InnerStrengthMax)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_RESTORE_RSI, 0)
    subject.CharMvt.playScript(Stance_Stand, Action_Resurrect, ReviveRSIAbility)
    Utility.SendAbilityOutputToTargetMsg(StringTable.ID_CLIENT_ABILITY_REZ_DIRECTOBJECT, ReviveRSIAbility, msg)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_REZ_SUBJECT, ReviveRSIAbility, msg)
    if subject.AbilityInv.isPvP():
        discovery.sendApplyAbilityToSelf(msg.subjectLocator, 1208)
    return


def RenewRSI_DirectObject(subject, msg):
    subject.PlayerCharacter.resuscitate()
    subject.AbilityInv.Health = subject.AbilityInv.MaxHealth
    subject.AbilityInv.takeInnerStrength(-subject.AbilityInv.InnerStrengthMax)
    discovery.playEffect(subject.locator, subject.locator, FX.FX_CHARACTER_RESTORE_RSI, 0)
    subject.CharMvt.playScript(Stance_Stand, Action_Resurrect, ReviveRSIAbility)
    Utility.SendAbilityOutputToTargetMsg(StringTable.ID_CLIENT_ABILITY_REZ_DIRECTOBJECT, RenewRSIAbility, msg)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_REZ_SUBJECT, RenewRSIAbility, msg)
    if subject.AbilityInv.isPvP():
        discovery.sendApplyAbilityToSelf(msg.subjectLocator, 1208)
    return


from time import ctime
print 'Reloading Health ', ctime()
return

# okay decompiling work/monolith_pyc\ability\hacker\health.pyc
