# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\general.py
# Compiled at: 2009-02-11 02:01:37
import random, traceback, obj, missionvalidate as mv, combatvalidate as cv, stringtable_client as StringTable, ability.utility as Utility
from ability.defines import *
import ability.effects as EFFECTS, ltfxmap as FX, ltfxmap as FX
WOODENSTAKE_DAMAGE = 10
SILVERBULLET_DAMAGE = 10
HOLYWATER_DAMAGE = 10
WOODENSTAKE_DAMAGE_MULT = 10
SILVERBULLET_DAMAGE_MULT = 10
HOLYWATER_DAMAGE_MULT = 10
WOODENSTAKE_PLUS_DAMAGE_MULT = 75
SILVERBULLET_PLUS_DAMAGE_MULT = 75
HOLYWATER_PLUS_DAMAGE_MULT = 75
EMP_BLAST_NO_DAMAGE = 0
EMP_BLAST_DAMAGE = 5000

def Detection_DirectObject(subject, msg):
    Utility.outputAbilityDebug('Detection DirectObject')
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
            subject.AbilityInv.deactivateAbility(DisguiseGrantAbility)
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


def Detection_Test(sentence):
    Utility.outputAbilityDebug('Detection Test')
    sentence.result = random.randrange(100) + sentence.subject.abilities[DetectAbility]
    return


Detection_Test.depAttr = '\n'

def WoodenStakeAttack_DirectObject(subject, msg):
    damage = WOODENSTAKE_DAMAGE
    if subject.hasAbility[NPCKilledOnlyByWoodAbility]:
        damage *= WOODENSTAKE_DAMAGE_MULT
    Utility.outputAbilityDebug('WoodenStakeAttack: %d damage dealt' % damage)
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, damage, WoodenStakeAttackAbility, Melee, True)
    return


def WoodenStakePlusAttack_DirectObject(subject, msg):
    damage = WOODENSTAKE_DAMAGE
    if subject.hasAbility[NPCKilledOnlyByWoodAbility]:
        damage *= WOODENSTAKE_PLUS_DAMAGE_MULT
    Utility.outputAbilityDebug('WoodenStakePlusAttack: %d damage dealt' % damage)
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, damage, WoodenStakePlusAttackAbility, Melee, True)
    return


def SilverBulletAttack_DirectObject(subject, msg):
    damage = SILVERBULLET_DAMAGE
    if subject.hasAbility[NPCKilledOnlyBySilverAbility]:
        damage *= SILVERBULLET_DAMAGE_MULT
    Utility.outputAbilityDebug('SilverBulletAttack: %d damage dealt' % damage)
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, damage, SilverBulletAttackAbility, Ranged, True)
    return


def SilverBulletPlusAttack_DirectObject(subject, msg):
    damage = SILVERBULLET_DAMAGE
    if subject.hasAbility[NPCKilledOnlyBySilverAbility]:
        damage *= SILVERBULLET_PLUS_DAMAGE_MULT
    Utility.outputAbilityDebug('SilverBulletPlusAttack: %d damage dealt' % damage)
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, damage, SilverBulletPlusAttackAbility, Ranged, True)
    return


def HolyWaterAttack_DirectObject(subject, msg):
    damage = HOLYWATER_DAMAGE
    Utility.outputAbilityDebug('HolyWaterAttack: %d damage' % damage)
    if subject.hasAbility[NPCKilledOnlyByHolyWaterAbility]:
        damage *= HOLYWATER_DAMAGE_MULT
    Utility.outputAbilityDebug('HolyWaterAttack: %d damage dealt' % damage)
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, damage, HolyWaterAttackAbility, Melee, True)
    if (subject.hasAbility[ZombieVirus2BaseAbility] or subject.hasAbility[ZombieVirus3BaseAbility]) and subject.PlayerCharacter is not None:
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


def HolyWaterPlusAttack_DirectObject(subject, msg):
    damage = HOLYWATER_DAMAGE
    Utility.outputAbilityDebug('HolyWaterPlusAttack: %d damage' % damage)
    if subject.hasAbility[NPCKilledOnlyByHolyWaterAbility]:
        damage *= HOLYWATER_PLUS_DAMAGE_MULT
    Utility.outputAbilityDebug('HolyWaterPlusAttack: %d damage dealt' % damage)
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, damage, HolyWaterPlusAttackAbility, Melee, True)
    if (subject.hasAbility[ZombieVirus2BaseAbility] or subject.hasAbility[ZombieVirus3BaseAbility]) and subject.PlayerCharacter is not None:
        subject.AbilityInv.removeAbilityGrant(ZombieVirus2BaseAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus3BaseAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus2BiteAbility)
        subject.AbilityInv.removeAbilityGrant(ZombieVirus3BiteAbility)
        subject.AbilityInv.removeAbilityGrant(NPCKilledOnlyByHolyWaterAbility)
        subject.CharMvt.revertToRSI()
    return


def EMPBlast_DirectObject(subject, msg):
    damage = EMP_BLAST_NO_DAMAGE
    Utility.outputAbilityDebug('EMPBlastAbility: %d damage' % damage)
    if subject.hasAbility[NPCEMPWeaknessAbility]:
        damage = EMP_BLAST_DAMAGE
    Utility.outputAbilityDebug('EMPBlastAbility: %d damage dealt' % damage)
    subject.AbilityInv.dealAbilityDamage(msg.subjectLocator, damage, EMPBlastAbility, Neutral, True)
    return


def PreciseBlow_Test(sentence):
    Utility.outputAbilityDebug('Precise Blow test')
    mods = sentence.subject.AbilityInv.getTempMods(EnergizedAttacksAbility)
    if len(mods) > 0:
        sentence.result = FAILURE
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_PRECISE_BLOW_FAIL, EnergizedAttacksAbility, sentence)
    else:
        sentence.result = SUCCESS
    return


PreciseBlow_Test.depAttr = '\n'

def EnergizedAttacks_Test(sentence):
    Utility.outputAbilityDebug('Energized Attacks test')
    mods = sentence.subject.AbilityInv.getTempMods(PreciseBlowAbility)
    if len(mods) > 0:
        sentence.result = FAILURE
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_ENERGIZED_ATTACKS_FAIL, EnergizedAttacksAbility, sentence)
    else:
        sentence.result = SUCCESS
    return


EnergizedAttacks_Test.depAttr = '\n'

def Jackout_DirectObject(subject, msg):
    subject.AbilityInv.jackoutTriggered()
    return


def ApplyAbilityToMissionTeamSubjectFunc(subject, msg):
    subject.AbilityInv.applyAbilityToMissionTeam(msg)
    return


def RSIConsumable_Test(sentence):
    Utility.outputAbilityDebug('RSIConsumable_Test %d' % sentence.indirectObject.type)
    if sentence.indirectObject.type > 0:
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


RSIConsumable_Test.depAttr = '\nindirectObject.type\n'
RSICalls = {'SetBodyType': 'subject.Inventory.setBodyType(msg.indirectObjectItem.NewRSIID)', 'SetHeadType': 'subject.Inventory.setHeadType(msg.indirectObjectItem.NewRSIID)', 'SetHairType': 'subject.Inventory.setHairType(msg.indirectObjectItem.NewRSIID)', 'SetHairColor': 'subject.Inventory.setHairColor(msg.indirectObjectItem.NewRSIID)', 'SetFacialHairType': 'subject.Inventory.setFacialHairType(msg.indirectObjectItem.NewRSIID)', 'SetFacialHairColor': 'subject.Inventory.setFacialHairColor(msg.indirectObjectItem.NewRSIID)', 'SetSkinTone': 'subject.Inventory.setSkinTone(msg.indirectObjectItem.NewRSIID)', 'SetTattooType': 'subject.Inventory.setTattooType(msg.indirectObjectItem.NewRSIID)'}

def RSIConsumable_DirectObject(subject, msg):
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_RSI_CONSUMABLE_SELF, RSIConsumableAbility, msg, int(msg.indirectObjectItem.InfoID))
    exec RSICalls[msg.indirectObjectItem.RSICall]
    subject.Inventory.commitRSIToDatabase()
    return


return

# okay decompiling work/monolith_pyc\ability\general.pyc
