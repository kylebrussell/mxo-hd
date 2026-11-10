# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\hacker\virii.py
# Compiled at: 2006-03-30 16:29:38
import random, traceback, obj, combat_defines as CD, missionvalidate as mv, combatvalidate as cv, stringtable_client as StringTable, ability.utility as Utility
from ability.utility import getAbilityName
from ability.defines import *
import ltfxmap as FX, ability.effects as EFFECTS, virii_defines as VIRII
AFFECTVISION1_TIME = VIRII.VIRII_LEVEL_1_DURATION
AFFECTVISION2_TIME = VIRII.VIRII_LEVEL_2_DURATION
AFFECTVISION3_TIME = VIRII.VIRII_LEVEL_3_DURATION
MOVEMENTDISRUPT1_TIME = VIRII.VIRII_LEVEL_1_DURATION
MOVEMENTDISRUPT2_TIME = VIRII.VIRII_LEVEL_2_DURATION
MOVEMENTDISRUPT3_TIME = VIRII.VIRII_LEVEL_2_DURATION
DISABLEABILITY1_TIME = 500
MOVEMENTDISRUPT1_EFFECT = 25
MOVEMENTDISRUPT2_EFFECT = 50
MOVEMENTDISRUPT3_EFFECT = 75

def DeflectBullets1Effect_DirectObject(subject, msg):
    Utility.outputAbilityDebug('DeflectBullets1: DirectObject')
    procs = (
     '', 'PlayFX', '', '')
    subject.AbilityInv.addTempModProcs(procs, DeflectBullets1Ability, DeflectBullets1Ability, 0.2, 0, 0, FX.FX_VIRUSCAST_SPLIT_DEFLECT_BULLETS_DEFLECTBULLETS1)
    return


def DeflectVirus1Effect_DirectObject(subject, msg):
    Utility.outputAbilityDebug('DeflectVirus1: DirectObject')
    procs = (
     '', 'PlayFX', '', '')
    subject.AbilityInv.addTempModProcs(procs, DeflectVirus1Ability, DeflectVirus1Ability, 0.2, 0, 0, FX.FX_VIRUSCAST_SPLIT_DEFLECT_VIRUS_DEFLECTVIRUS1)
    return


def GenericCorruption(directobject, chanceValue):
    item = (
     0, 0)
    destoryed_item_ID = 0
    if Utility.IsAnNPC(directobject):
        weapon = directobject.AI.getWeapon()
        if weapon:
            item = (
             directobject.AI.getWeapon(), 109)
        else:
            return (
             FAILURE, 0)
    else:
        itemList = []
        item = Utility.GetEquippedWeapon(directobject)
        if item:
            nSlot = 109
            itemList.append((item, nSlot))
        item = Utility.GetEquippedHackerItem(directobject)
        if item:
            nSlot = 110
            itemList.append((item, nSlot))
        if not itemList:
            Utility.outputAbilityDebug('GenericCorruption: Subject has no items to destroy.')
            return (FAILURE, 0)
        item = Utility.GetRandomListEntry(itemList)
    if Utility.IsAnNPC(directobject):
        destoryed_item_ID = item[0]
    else:
        destoryed_item_ID = item[0].type
    roll = random.randrange(1, 100)
    Utility.outputAbilityDebug('GenericCorruption: Corruption chance roll = ' + `roll` + '.')
    if roll > chanceValue:
        Utility.outputAbilityDebug("GenericCorruption: Corruption's " + `chanceValue` + ' failed on item(' + `(item[1])` + ').')
        return (DEFLECTED, destoryed_item_ID)
    destoryed = EFFECTS.DestroyItem(directobject, item[0], item[1])
    return (destoryed, destoryed_item_ID)
    return


return

# okay decompiling work/monolith_pyc\ability\hacker\virii.pyc
