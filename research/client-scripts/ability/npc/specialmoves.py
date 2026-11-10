# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\npc\specialmoves.py
# Compiled at: 2009-02-11 02:01:37
import random, traceback, obj, stringtable_client as StringTable, ability.utility as Utility, ability.effects as EFFECTS, ltfxmap as FX, ability.hacker.virii_defines as VIRII
from ability.defines import *
import combat_defines as CD, ability.hacker.misc as HACKER_MISC, NPCTypes as NPCs
POUNCE_DAMAGE = 250
TWINRAZOR_DAMAGE = 300
TWINRAZOR2_DAMAGE = 300
BLOODDRINK_DAMAGE = 100
BLOODDRINK_PRONE_DAMAGE_MULTIPLIER = 2
NPCBOOSTUNDERLINGS_BOOST_VALUE = 3
NPCBOOSTUNDERLINGS_TIME = 10.0
NPCGENERICBOOST_FX = FX.FX_CHARACTER_UPGRADE_CAST
NPCBOOSTUNDERLINGS_FX = NPCGENERICBOOST_FX
NPCBLOODDRINK_DRINKER_FX = FX.FX_CHARACTER_HEALTH_REGENERATION
NPCBLOODDRINK_VICTIM_FX = FX.FX_CHARACTER_HEALTH_DRAIN
NPCBOOST_FX = NPCGENERICBOOST_FX

def NPCTwinRazor_DirectObject(subject, msg):
    Utility.outputAbilityDebug('NPCTwinRazor: %d damage dealt' % TWINRAZOR_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, TWINRAZOR_DAMAGE)
    return


def NPCTwinRazor2_DirectObject(subject, msg):
    Utility.outputAbilityDebug('NPCTwinRazor2: %d damage dealt' % TWINRAZOR2_DAMAGE)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, TWINRAZOR2_DAMAGE)
    return


def NPCPhantomPasswall_DirectObject(subject, msg):
    Utility.outputAbilityDebug('NPCPhantomPasswall: Object')
    HACKER_MISC.ShadowObject_Subject(subject, msg)
    return


def NPCPhantomPasswall_Test(sentence):
    Utility.outputAbilityDebug('NPCPhantomPasswall: test')
    sentence.result = SUCCESS
    return


NPCPhantomPasswall_Test.depAttr = '\n'

def NPCBoostUnderlings_DirectObject(subject, msg):
    Utility.outputAbilityDebug('NPCBoostUnderlings: DirectObject')
    type = 0
    if Utility.IsAnNPC(subject):
        type = subject.AI.getCaptainUnderlingType()
        Utility.outputAbilityDebug('NPCBoostUnderlings: type = %d, AItype= %d' % (msg.result, type))
    else:
        return
    if msg.result == type:
        Utility.outputAbilityDebug('NPCBoostUnderlings: BOOSTIN!')
        subject.AbilityInv.addTempModToAllActive(NPCBoostUnderlingsAbility, NPCBOOSTUNDERLINGS_BOOST_VALUE, NPCBOOSTUNDERLINGS_TIME, 0, 1)
    return


def NPCBoostUnderlings_Subject(subject, msg):
    Utility.outputAbilityDebug('NPCBoostUnderlings Subject')
    radius = 400
    loc = subject.Position
    if not Utility.IsAnNPC(subject):
        return
    type = subject.AI.getCaptainUnderlingType()
    subject.AbilityInv.applyAbilityToArea(NPCBoostUnderlingsAbility, type, loc.x, loc.y, loc.z, radius)
    return


NPCBoostUnderlings_Subject.depAttr = '\n'

def NPCDetection_DirectObject(subject, msg):
    Utility.outputAbilityDebug('NPCDetection DirectObject')
    if subject.CharMvt is None:
        return
    res = msg.result
    abillvl = 0
    if subject.CharMvt.hasDissemblingType(kDisguised):
        if subject.hasAbility[DisguiseDurationAbility]:
            abillvl += subject.abilities[DisguiseDurationAbility] * 8
        if subject.hasAbility[DisguiseAbility]:
            abillvl += subject.abilities[DisguiseAbility] * 4
        if res > abillvl + 50:
            Utility.outputAbilityDebug('Overcame Disguise %d vs %d' % (res, abillvl + 50))
            subject.CharMvt.deactivateDisguise()
            subject.AbilityInv.deactivateAbility(DisguiseAbility)
            discovery.playEffect(subject.locator, subject.locator, FX.FX_WEAPON_CONCEALMENT_COUNTER_PLAYER, 0)
        else:
            Utility.outputAbilityDebug('Failed to Overcome Disguise %d vs %d' % (res, abillvl + 50))
    elif subject.CharMvt.hasDissemblingType(kInvisible):
        if subject.hasAbility[InvisDurationAbility]:
            abillvl += subject.abilities[InvisDurationAbility] * 8
        if subject.hasAbility[InvisibilityAbility]:
            abillvl += subject.abilities[InvisibilityAbility] * 4
        if res > abillvl + 50:
            Utility.outputAbilityDebug('Overcame Invisibility %d vs %d' % (res, abillvl + 50))
            subject.CharMvt.removeDissemblingType(kInvisible)
            subject.AbilityInv.deactivateAbility(InvisibilityAbility)
            subject.AbilityInv.deactivateAbility(InvisibilityImpartedAbility)
            discovery.playEffect(subject.locator, subject.locator, FX.FX_WEAPON_CONCEALMENT_COUNTER_PLAYER, 0)
        else:
            Utility.outputAbilityDebug('Failed to Overcome Invisibility %d vs %d' % (res, abillvl + 50))
    return


def NPCDetection_Test(sentence):
    Utility.outputAbilityDebug('NPCDetection Test')
    sentence.result = random.randrange(100) + sentence.subject.abilities[NPCDetectionAbility] * 4
    return


NPCDetection_Test.depAttr = '\n'

def NPCUnDisguise_DirectObject(subject, msg):
    subject.CharMvt.revertToRSI()
    subject.CharMvt.removeDissemblingType(kAdminDisguise)
    return


def NPCDisguise_DirectObject(subject, msg):
    Utility.outputAbilityDebug('NPCDisguise DO')
    subject.CharMvt.setModel(msg.indirectObjectItem.Disguise)
    subject.CharMvt.addDissemblingType(kAdminDisguise)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, DisguiseAbility, msg)
    return


def HalloweenMask_DirectObject(subject, msg):
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
    Utility.outputAbilityDebug('HalloweenMask DO')
    disguiseID = msg.indirectObjectItem.Disguise
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    subject.AbilityInv.addTempMod(HalloweenMaskAbility, NullTargetAbility, disguiseID, 5)
    discovery.sendInvokeOrgEventMessage(subject.locator, constants.OrgPointType.OrgPointTypeHalloween, 0, 0, subject.Level, 0, 0, 0)
    return


def HolidayMask_DirectObject(subject, msg):
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
    Utility.outputAbilityDebug('HalloweenMask DO')
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, HolidayMaskAbility, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, HolidayMaskAbility, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapture_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICaptureAbility, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICaptureAbility, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno83_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    subject.AbilityInv.deactivateAbility(RSICapExecAbility)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno83Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno83Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno84_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    subject.AbilityInv.deactivateAbility(RSICapExecAbility)
    subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno84Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno84Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno85_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    subject.AbilityInv.deactivateAbility(RSICapExecAbility)
    subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno85Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno85Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno86_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    subject.AbilityInv.deactivateAbility(RSICapExecAbility)
    subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno86Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno86Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno87_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    subject.AbilityInv.deactivateAbility(RSICapExecAbility)
    subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno87Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno87Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno88_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    subject.AbilityInv.deactivateAbility(RSICapExecAbility)
    subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno88Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno88Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno89_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
    subject.AbilityInv.deactivateAbility(RSICapExecAbility)
    subject.AbilityInv.deactivateAbility(RSICapKuno83Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno84Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno85Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno86Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno87Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno88Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno89Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno89Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno90_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno90Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno90Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno91_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno91Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno91Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno92_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno92Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno92Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno93_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno93Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno93Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno94_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapKuno95Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
    subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno94Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno94Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno95_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapKuno96Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
    subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno95Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno95Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno96_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapKuno97Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
    subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno96Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno96Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno97_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapKuno98Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
    subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno97Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno97Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno98_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapKuno99Ability)
    subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
    subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno98Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno98Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno99_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapKuno100Ability)
    subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno99Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno99Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapKuno100_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapSSRAbility)
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapKuno100Ability, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapKuno100Ability, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapElite_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapEliteAbility, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapEliteAbility, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapExec_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
    subject.AbilityInv.deactivateAbility(RSICaptureAbility)
    subject.AbilityInv.deactivateAbility(RSICapEliteAbility)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapExecAbility, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapExecAbility, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapSSR_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapValfAbility)
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapSSRAbility, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapSSRAbility, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapValf_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    subject.AbilityInv.deactivateAbility(RSICapWrightAbility)
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapValfAbility, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapValfAbility, msg.subjectLocator, msg.subjectLocator, 0)
    return


def RSICapWright_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMaskAbility)
    subject.AbilityInv.deactivateAbility(HalloweenMask2Ability)
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
    disguiseID = msg.indirectObjectItem.Disguise
    subject.CharMvt.setModel(disguiseID)
    if msg.indirectObjectItem.Disguise > 2147483647:
        disguiseID = 2147483647 - disguiseID
    procs = (
     '', 'HalloweenMaskOff', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, RSICapWrightAbility, NullTargetAbility, 0, 0, 0, disguiseID)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, RSICapWrightAbility, msg.subjectLocator, msg.subjectLocator, 0)
    return


def NPCDisguise_Deactivate(subject):
    subject.CharMvt.removeDissemblingType(kAdminDisguise)
    return


def NPCChainFlame_Test(sentence):
    Utility.outputAbilityDebug('NPCChainFlame_Test')
    sentence.result = FAILURE
    if not sentence.directObject.hasAbility[NPCChainFlameAbility]:
        sentence.result = SUCCESS
    return


NPCChainFlame_Test.depAttr = '\ndirectObject.hasAbility[NPCChainFlameAbility]\n'

def NPCChainFlameTrigger_Test(sentence):
    Utility.outputAbilityDebug('NPCChainFlameTrigger_Test')
    sentence.result = FAILURE
    if sentence.subject != sentence.directObject:
        if not sentence.directObject.hasAbility[NPCChainFlameAbility]:
            sentence.result = SUCCESS
    return


NPCChainFlameTrigger_Test.depAttr = '\ndirectObject.hasAbility[NPCChainFlameAbility]\n'

def NPCChainFlameEffect_Test(sentence):
    Utility.outputAbilityDebug('NPCChainFlameEffect_Test')
    if sentence.subject.AbilityInv.hasTempMod(NPCChainFlameEffectAbility, NullTargetAbility):
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


NPCChainFlameEffect_Test.depAttr = '\n'

def Event2_0FlyVirusEffect1_DirectObject(subject, msg):
    Utility.outputAbilityDebug('Event2_0FlyVirusEffect1_DirectObject')
    temp_mods = subject.AbilityInv.getTempMods(Event2_0FlyVirusEffect2Ability, InvalidAbility)
    for temp_mod in temp_mods:
        subject.AbilityInv.removeTempMod(temp_mod.ParentAbility, temp_mod.TargetAbility)

    return


def Event2_0FlyVirusEffect1_Test(sentence):
    Utility.outputAbilityDebug('Event2_0FlyVirusEffect1_Test')
    if sentence.directObject.hasAbility[Event2_0FlyVirusAbility] or sentence.directObject.hasAbility[Event2_0GMFlyVirusAbility] or sentence.directObject.hasAbility[NPCAbility] or sentence.subject == sentence.directObject:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


Event2_0FlyVirusEffect1_Test.depAttr = '\ndirectObject.hasAbility[Event2_0FlyVirusAbility]\ndirectObject.hasAbility[Event2_0GMFlyVirusAbility]\ndirectObject.hasAbility[NPCAbility]\n'

def Event2_0FlyVirusEffect2_DirectObject(subject, msg):
    Utility.outputAbilityDebug('Event2_0FlyVirusEffect2_DirectObject')
    temp_mods = subject.AbilityInv.getTempMods(Event2_0FlyVirusEffect1Ability, InvalidAbility)
    for temp_mod in temp_mods:
        subject.AbilityInv.removeTempMod(temp_mod.ParentAbility, temp_mod.TargetAbility)

    return


def Event2_0FlyVirusEffect2_Test(sentence):
    Utility.outputAbilityDebug('Event2_0FlyVirusEffect2_Test')
    if sentence.directObject.hasAbility[Event2_0FlyVirusAbility] or sentence.directObject.hasAbility[Event2_0GMFlyVirusAbility] or sentence.directObject.hasAbility[NPCAbility] or sentence.subject == sentence.directObject:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


Event2_0FlyVirusEffect2_Test.depAttr = '\ndirectObject.hasAbility[Event2_0FlyVirusAbility]\ndirectObject.hasAbility[Event2_0GMFlyVirusAbility]\ndirectObject.hasAbility[NPCAbility]\n'

def Event2_0FlymanKillItem_DirectObject(subject, msg):
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, msg.result, Event2_0FlymanKillItemAbility, Neutral, True)
    return


def Event2_0FlymanKillItem_Test(sentence):
    Utility.outputAbilityDebug('Event2_0FlymanKillItem_Test')
    if sentence.directObject.hasAbility[Event2_0FlymanSusceptibilityAbility]:
        sentence.result = 140
    elif sentence.directObject.hasAbility[Event2_0FlyVirusAbility]:
        sentence.result = 100
    else:
        sentence.result = 10
    return


Event2_0FlymanKillItem_Test.depAttr = '\ndirectObject.hasAbility[Event2_0FlymanSusceptibilityAbility]\ndirectObject.hasAbility[Event2_0FlyVirusAbility]\n'

def Event2_0FlymanKillItem2_DirectObject(subject, msg):
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, msg.result, Event2_0FlymanKillItem2Ability, Neutral, True)
    return


def Event2_0FlymanKillItem2_Test(sentence):
    Utility.outputAbilityDebug('Event2_0FlymanKillItem2_Test')
    if sentence.directObject.hasAbility[Event2_0FlymanSusceptibilityAbility]:
        sentence.result = 1400
    elif sentence.directObject.hasAbility[Event2_0FlyVirusAbility]:
        sentence.result = 100
    else:
        sentence.result = 10
    return


Event2_0FlymanKillItem2_Test.depAttr = '\ndirectObject.hasAbility[Event2_0FlymanSusceptibilityAbility]\ndirectObject.hasAbility[Event2_0FlyVirusAbility]\n'

def Event2_0FlymanTransformPulse_Test(sentence):
    if sentence.subject.AbilityInv.hasTempMod(Event2_0FlymanTransformEffectAbility, EventSlot1Ability) or sentence.subject.AbilityInv.hasTempMod(Event2_0FlymanTransformEffectAbility, NullTargetAbility):
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


Event2_0FlymanTransformPulse_Test.depAttr = '\n'

def Event2_0FlymanTransformEffect_Test(sentence):
    if sentence.subject.AbilityInv.hasTempMod(Event2_0FlymanTransformEffectAbility, EventSlot1Ability) or sentence.subject.AbilityInv.hasTempMod(Event2_0FlymanTransformEffectAbility, NullTargetAbility):
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


Event2_0FlymanTransformEffect_Test.depAttr = '\n'

def Event2_5KillCodeEffect_Test(sentence):
    Utility.outputAbilityDebug('Event2_5KillCodeEffect_Test')
    if sentence.directObject.hasAbility[NPCAbility]:
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


Event2_5KillCodeEffect_Test.depAttr = '\ndirectObject.hasAbility[NPCAbility]\n'

def Event2_5KillCode_Test(sentence):
    Utility.outputAbilityDebug('Event2_5KillCode_Test')
    if sentence.subject.AbilityInv.hasTempMod(Event2_5KillCodeTriggerAbility, 0):
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


Event2_5KillCode_Test.depAttr = '\n'

def ZombieVirusAoE_Test(sentence):
    if sentence.directObject.hasAbility[ZombieVirusBaseAbility] or sentence.directObject.hasAbility[ZombieVirusGrantAbility]:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


ZombieVirusAoE_Test.depAttr = '\ndirectObject.hasAbility[ZombieVirusBaseAbility]\ndirectObject.hasAbility[ZombieVirusGrantAbility]\n'

def ZombieVirusGranter_DirectObject(subject, msg):
    subject.AbilityInv.removeTempMod(ZombieVirusAoEAbility, NullTargetAbility)
    return


def ZombieVirus2Bite_Test(sentence):
    if sentence.directObject.hasAbility[ZombieVirus2BaseAbility] or sentence.directObject.hasAbility[ZombieVirus3BaseAbility]:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


ZombieVirus2Bite_Test.depAttr = '\ndirectObject.hasAbility[ZombieVirus2BaseAbility]\ndirectObject.hasAbility[ZombieVirus3BaseAbility]\n'

def ZombieVirus3Bite_Test(sentence):
    if sentence.directObject.hasAbility[ZombieVirus2BaseAbility] or sentence.directObject.hasAbility[ZombieVirus3BaseAbility]:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


ZombieVirus3Bite_Test.depAttr = '\ndirectObject.hasAbility[ZombieVirus2BaseAbility]\ndirectObject.hasAbility[ZombieVirus3BaseAbility]\n'

def SmithVirus1Attack_Test(sentence):
    if sentence.directObject.hasAbility[SmithVirus1BaseAbility] or sentence.directObject.hasAbility[SmithVirus2BaseAbility] or sentence.directObject.hasAbility[SmithVirusImmunityAbility]:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


SmithVirus1Attack_Test.depAttr = '\ndirectObject.hasAbility[SmithVirus1BaseAbility]\ndirectObject.hasAbility[SmithVirus2BaseAbility]\ndirectObject.hasAbility[SmithVirusImmunityAbility]\n'

def SmithVirus2Attack_Test(sentence):
    if sentence.directObject.hasAbility[SmithVirus1BaseAbility] or sentence.directObject.hasAbility[SmithVirus2BaseAbility] or sentence.directObject.hasAbility[SmithVirusImmunityAbility]:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


SmithVirus2Attack_Test.depAttr = '\ndirectObject.hasAbility[SmithVirus1BaseAbility]\ndirectObject.hasAbility[SmithVirus2BaseAbility]\ndirectObject.hasAbility[SmithVirusImmunityAbility]\n'

def SmithVirus1Base_Deactivate(subject):
    if subject.PlayerCharacter is not None:
        subject.CharMvt.revertToRSI()
    return


def SmithVirus2Base_Deactivate(subject):
    if subject.PlayerCharacter is not None:
        subject.CharMvt.revertToRSI()
    return


def ZombieVirus3Base_Deactivate(subject):
    if subject.PlayerCharacter is not None:
        subject.CharMvt.revertToRSI()
    return


def ZombieVirus2Base_Deactivate(subject):
    if subject.PlayerCharacter is not None:
        subject.CharMvt.revertToRSI()
    return


def OGTerminateEnemies_Test(sentence):
    if sentence.directObject.hasAbility[NPCPenaltyBaseAbility]:
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


OGTerminateEnemies_Test.depAttr = '\ndirectObject.hasAbility[NPCPenaltyBaseAbility]\n'

def OGTerminateFriendlies_Test(sentence):
    if sentence.directObject.hasAbility[NPCPenaltyBaseAbility]:
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


OGTerminateFriendlies_Test.depAttr = '\ndirectObject.hasAbility[NPCPenaltyBaseAbility]\n'

def OGLOSTerminateEnemies_Test(sentence):
    if sentence.directObject.hasAbility[NPCPenaltyBaseAbility]:
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


OGLOSTerminateEnemies_Test.depAttr = '\ndirectObject.hasAbility[NPCPenaltyBaseAbility]\n'

def OGLOSTerminateFriendlies_Test(sentence):
    if sentence.directObject.hasAbility[NPCPenaltyBaseAbility]:
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


OGLOSTerminateFriendlies_Test.depAttr = '\ndirectObject.hasAbility[NPCPenaltyBaseAbility]\n'
return

# okay decompiling work/monolith_pyc\ability\npc\specialmoves.pyc
