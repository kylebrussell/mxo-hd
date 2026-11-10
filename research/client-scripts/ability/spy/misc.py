# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\spy\misc.py
# Compiled at: 2009-02-11 02:01:37
import random, traceback, obj, missionvalidate as mv, combatvalidate as cv, stringtable_client as StringTable, ability.utility as Utility, ltfxmap as FX, generic_free_attacks as gfa
from ability.defines import *
FAILURE_MASK = 16777216
CRITICAL_FAILURE_MASK = 33554432
HAMSTRING_COMBAT_TACTICS_MOD = -25
HAMSTRING_SLOWNESS_MOD = 0.75
HAMSTRING_INTERLOCK_FORCE_MOD = -0.25
HAMSTRING_DURATION = 30
ROOTING_DURATION = 60
MEZZING_DURATION = 60
HAMSTRING_FX = FX.FX_CHARACTER_HAMSTRING
ROOTING_FX = FX.FX_CHARACTER_ROOTING_ATTACK
FIND_WEAKNESS_FX = FX.FX_NPC_REGEN

def GhostStrike_Subject(subject, msg):
    Utility.outputAbilityDebug('GhostStrike_Subject')
    if random.randint(1, 10) <= 8:
        Utility.outputAbilityDebug('GhostStrike_Subject - Reapply Invis')
        subject.AbilityInv.addTempMod(GhostStrikeAbility, ISDrainTargetAbility, msg.result, 0)
        subject.AbilityInv.reapplyAbility(InvisibilityGrantAbility)
    else:
        Utility.outputAbilityDebug('GhostStrike_Subject - NO Reapply Invis')
    return


def GhostStrike_Test(sentence):
    if sentence.subject.AbilityInv.hasTempMod(InvisibilityGrantAbility, NullTargetAbility) or sentence.subject.AbilityInv.hasTempMod(InvisibilityImpartedAbility, NullTargetAbility):
        gfa.AbilityThrowFreeAttack(sentence)
        if sentence.result != SUCCESS:
            sentence.result = FAILURE
            return
        sentence.result = SUCCESS
        sentence.subject.CharMvt.playScript(Stance_Aggro, Action_FreeAttackWeaponSMKnifeThrow)
        temp_mods = sentence.subject.AbilityInv.getTempMods(InvisibilityGrantAbility, NullTargetAbility)
        duration = 0
        for temp_mod in temp_mods:
            if temp_mod.TimeLeft > duration:
                duration = temp_mod.TimeLeft

        temp_mods = sentence.subject.AbilityInv.getTempMods(InvisibilityImpartedAbility, NullTargetAbility)
        for temp_mod in temp_mods:
            if temp_mod.TimeLeft > duration:
                duration = temp_mod.TimeLeft

        sentence.result = duration
        Utility.outputAbilityDebug('GhostStrike_Test - %d sec' % duration)
    else:
        sentence.result = FAILURE
        refund = sentence.subject.AbilityInv.getInnerStrengthCost(GhostStrikeAbility)
        procs = ('', 'RefundIS', '', '', '')
        sentence.subject.AbilityInv.addTempModProcs(procs, GhostStrikeAbility, NullTargetAbility, 0.1, 0, 0, refund)
        Utility.outputAbilityDebug('GhostStrike_Test: Refunding %d IS' % refund)
    return


GhostStrike_Test.depAttr = '\ndirectObject.hasAbility[CurrentTacticAbility]\ndirectObject.hasAbility[ThrowDefenseTacticsAbility]\ndirectObject.abilities[ThrowDefenseTacticsAbility]\ndirectObject.abilities[ConsistencyAbility]\ndirectObject.locator\n'

def LineUpTheShot_Subject(subject, msg):
    Utility.outputAbilityDebug('LineUpTheShot_Subject')
    if msg.result == 1:
        subject.AbilityInv.reapplyAbility(ConcealmentGrantAbility)
    return


def LineUpTheShot_Test(sentence):
    if sentence.subject.AbilityInv.hasTempMod(ConcealmentGrantAbility, ConcealmentGrantAbility):
        sentence.result = 1
    else:
        sentence.result = 2
    return


LineUpTheShot_Test.depAttr = '\n'

def ObjectCheckForSixthSense(object, udata):
    relationship = udata.neutral
    if object.IsDead is not None and object.IsDead:
        return
    stealthLevel = 0
    if object.hasAbility[StealthAbility]:
        stealthLevel = object.abilities[StealthAbility]
    if object.CharMvt.isFriendly(udata.locator):
        Utility.outputAbilityDebug("Friendly, don't show friends.")
        relationship = udata.friend
        return
    elif object.CharMvt.isEnemy(udata.locator):
        Utility.outputAbilityDebug('Enemy')
        relationship = udata.foe
    else:
        Utility.outputAbilityDebug('Neutral')
        relationship = udata.neutral
    discovery.clientSystemMessage(object.locator, StringTable.ID_ABILITY_SIXTH_SENSE_SCAN)
    detectorRoll = udata.roll + udata.ability_level
    stealtherRoll = 0
    if stealthLevel == 0:
        stealtherRoll = stealthLevel + 50
    Utility.outputAbilityDebug('stealther.stealthLevel: %d' % stealthLevel)
    Utility.outputAbilityDebug('detector.roll: %d  detector.sixthSenseLevel: %d' % (udata.roll, udata.ability_level))
    Utility.outputAbilityDebug('detector(%d) > stealther(%d)? ' % (detectorRoll, stealtherRoll))
    if detectorRoll > stealtherRoll:
        udata.detected_list.append((object, relationship))
        discovery.playEffect(udata.locator, udata.locator, FX.FX_CHARACTER_SIXTH_SENSE_TARGET, 0)
        Utility.SendAbilityOutputToTarget(StringTable.ID_ABILITY_SIXTH_SENSE_DISCOVERY, SixthSenseAbility, udata.locator, object.locator, 0)
    else:
        Utility.SendAbilityOutputToTarget(StringTable.ID_ABILITY_SIXTH_SENSE_RESIST, SixthSenseAbility, udata.locator, object.locator, 0)
    return


def SixthSense_Subject(subject, msg):

    class _USERDATA:
        __module__ = __name__

    udata = _USERDATA()
    udata.neutral = constants.SixthSense.neutral
    udata.friend = constants.SixthSense.friend
    udata.foe = constants.SixthSense.foe
    udata.ability_level = subject.abilities[SixthSensingAbility]
    udata.detected_list = []
    udata.roll = random.randrange(100)
    udata.MissionKey = subject.MissionKey
    udata.FactionID = subject.FactionID
    udata.locator = subject.locator
    radius = 1500
    components = (
     constants.Components.CharMovement,)
    objects_in_rad = subject.PlayerCharacter.getObjectsInRadius(radius, components)
    Utility.outputAbilityDebug('SixthSense - Objects in radius: %d ' % len(objects_in_rad))
    for object in objects_in_rad:
        if object != subject:
            ObjectCheckForSixthSense(object, udata)

    subject.PlayerCharacter.sendPlayerPositions(udata.detected_list)
    return


def SixthSense_Test(sentence):
    Utility.outputAbilityDebug('Testing SixthSense.')
    discovery.playEffect(sentence.subject.locator, sentence.subject.locator, FX.FX_CHARACTER_SIXTH_SENSE_CAST, 0)
    sentence.result = SUCCESS
    return


SixthSense_Test.depAttr = '\n'
TAPINFOBANK_MAX_INFO_TAPPED = 50

def TapInfoBank_DirectObject(subject, msg):
    Utility.outputAbilityDebug('tapping : %d ' % msg.result)
    subject.PlayerCharacter.addInformation(-msg.result)
    return


def TapInfoBank_Subject(subject, msg):
    subject.PlayerCharacter.addInformation(msg.result)
    return


def TapInfoBank_Test(sentence):
    Utility.outputAbilityDebug('Tapinfo bank')
    draw_cap = 50
    difficulty_level = sentence.directObject.Level * 2
    attack_result = sentence.subject.abilities[TapInfoBankAbility] + random.randrange(0, 100)
    difference = attack_result - difficulty_level
    succeeded_in_performing = True
    subj_info = sentence.directObject.Information
    if difference > 0:
        info_to_tap = difference
        if info_to_tap > TAPINFOBANK_MAX_INFO_TAPPED:
            info_to_tap = TAPINFOBANK_MAX_INFO_TAPPED
        if subj_info < info_to_tap:
            info_to_tap = subj_info
        print 'Tapping ', info_to_tap
        sentence.result = info_to_tap
        sentence.result_data = sentence.subject.Interlock.specialAttack(sentence.directObject.locator, TapInfoBankAbility, True)
    else:
        sentence.result = 0
        succeeded_in_performing = False
        sentence.result_data = sentence.subject.Interlock.specialAttack(sentence.directObject.locator, TapInfoBankAbility, False)
    return
    return


TapInfoBank_Test.depAttr = '\ndirectObject.locator\ndirectObject.Level\ndirectObject.Information\nindirectObject.type\n'
_DISRUPT_BOOTED_FLAG = 1 << 31
CombatAbilityList = (
 KarateAbility, KungFuAbility, AikidoAbility, HandgunsAbility, SubmachinegunsAbility, RiflesAbility, AwakenedAbility)

def TapDataNode_Subject(subject, msg):
    Utility.outputAbilityDebug('TapDataNode_Subject: ')
    subject.AbilityInv.activateTapDataNode(msg.indirectObjectItem)
    return


LocateDataNode_Duration = 0.0
LocateDataNode_IntervalTime = 1.0
LocateDataNode_tmpMod = {}
LocateDataNode_tmpMod['Procs'] = (
 '', 'LocateDataNodeTerm', 'VisLocDataNode', '', '')
LocateDataNode_tmpMod['Target'] = LocateDataNodeAbility
LocateDataNode_tmpMod['Parent'] = LocateDataNodeAbility
LocateDataNode_tmpMod['Interval'] = LocateDataNode_IntervalTime

def LocateDataNode_DirectObject(subject, msg):
    Utility.outputAbilityDebug('LocateDataNode_Subject: ')
    abilityUsed = LocateDataNodeSkillAbility
    Utility.outputAbilityDebug('  Using LocateDataNodeAbility')
    range = int(msg.indirectObjectItem.Range * 100)
    print 'Locate DataNode Range : ', range
    subject.AbilityInv.activateLocateDataNode(range, msg.indirectObjectItem)
    if msg.indirectObjectItem.Type == 0:
        range = range
        tmpMod = LocateDataNode_tmpMod.copy()
        tmpMod['Duration'] = 10.0
        tmpMod['Procs'] = ('', 'LocateDataNodeTerm', '', '', '')
        subject.AbilityInv.addTempModProcs2(tmpMod)
        subject.AbilityInv.visualLocateDataNode(range, LocateDataNodeAbility)
        if subject.hasAbility[AdvancedDataTapAbility]:
            subject.AbilityInv.requestLuggableLocation()
    else:
        LocateDataNode_tmpMod['Value'] = range
        subject.AbilityInv.addTempModProcs2(LocateDataNode_tmpMod)
        Utility.outputAbilityDebug(' DataNodeLocate: goggles; %d' % subject.hasAbility[AdvancedDataNodeGogglesAbility])
        subject.AbilityInv.visualLocateDataNode(range, LocateDataNodeAbility, subject.hasAbility[AdvancedDataNodeGogglesAbility])
    return


Disguise_TempMod = {}
Disguise_TempMod['Procs'] = (
 '', 'DisguiseTerm', '', '', '')
Disguise_TempMod['Parent'] = DisguiseGrantAbility
Disguise_TempMod['Target'] = NullTargetAbility

def DisguiseGrant_DirectObject(subject, msg):
    subject.AbilityInv.deactivateAbility(HolidayMaskAbility)
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
    Utility.outputAbilityDebug('DisguiseGrant DirectObject')
    subject.CharMvt.setModel(msg.indirectObjectItem.Disguise)
    subject.CharMvt.addDissemblingType(kDisguised)
    subject.AbilityInv.enableTitles(0)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_DISGUISE_ACTIVATION, DisguiseGrantAbility, msg)
    duration = 30.0 + msg.result
    subject.AbilityInv.addStateMod(subject.locator, DisguiseGrantAbility, StealthStateAbility, 0, 0, float(duration))
    tmp = Disguise_TempMod.copy()
    tmp['Duration'] = duration
    subject.AbilityInv.addTempModProcs2(tmp)
    Utility.turnOffAuras(subject)
    return


def DisguiseGrant_Test(sentence):
    Utility.outputAbilityDebug('DisguiseGrant Test ')
    angle = 60.0
    abil_lvl = 1 + sentence.subject.abilities[DisguiseGrantAbility] / 2
    if sentence.subject.hasAbility[DisguiseDurationAbility]:
        abil_lvl += sentence.subject.abilities[DisguiseDurationAbility]
    sentence.result = abil_lvl
    opponents = sentence.subject.PlayerCharacter.getEnemiesInRadius(800)
    if len(opponents) == 0:
        return 0
    for opponent in opponents:
        if opponent.locator != sentence.subject.locator:
            (inFrust, dist) = Utility.isObjWithinFrust(opponent, sentence.subject, angle)
            if inFrust:
                if physics.clearLine(opponent.Position, sentence.subject.Position):
                    Utility.outputAbilityDebug("Disguise_Test: Can't disguise!")
                    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_CANT_DISGUISE_NEAR_ENEMIES, 0, sentence.subject.locator, opponent.locator, 0)
                    sentence.result = FAILURE

    return


DisguiseGrant_Test.depAttr = '\nindirectObject.type\n'
INVISIBILITY_BASE_TIME = 30.0

def InvisibilityGrant_DirectObject(subject, msg):
    Utility.outputAbilityDebug('InvisibilityGrant: DirectObject')
    subject.CharMvt.addDissemblingType(kInvisible)
    duration = INVISIBILITY_BASE_TIME + subject.abilities[InvisibilityGrantAbility] * 2
    if subject.hasAbility[InvisDurationAbility]:
        duration += subject.abilities[InvisDurationAbility]
    if msg.result != SUCCESS:
        Utility.outputAbilityDebug('Invisibility (Ghost Strike): Value %d' % msg.result)
        duration = msg.result
        subject.AbilityInv.removeTempMod(GhostStrikeAbility, ISDrainTargetAbility)
    Utility.outputAbilityDebug('Invisibility: Check deactivation in %f seconds.' % duration)
    procs = (
     'InvisibilityInit', 'InvisibilityTerm', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, InvisibilityGrantAbility, NullTargetAbility, duration, 0, 1)
    subject.AbilityInv.addTempMod(InvisibilityGrantAbility, MovementSpeedAbility, -300, duration, 0, 1)
    subject.AbilityInv.addStateMod(subject.locator, InvisibilityGrantAbility, StealthStateAbility, 0, 0, float(duration))
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_IMPROVED_INVISIBILITY_ACTIVATION, InvisibilityGrantAbility, msg)
    Utility.turnOffAuras(subject)
    return


def InvisibilityGrant_Deactivate(subject):
    subject.AbilityInv.removeTempMod(InvisibilityGrantAbility, NullTargetAbility)
    subject.AbilityInv.removeTempMod(InvisibilityGrantAbility, MovementSpeedAbility)
    Utility.outputAbilityDebug('InvisibilityGrant_Termination: Deactivate')
    return


def InvisibilityGrant_Test(sentence):
    Utility.outputAbilityDebug('InvisibilityGrant Test ')
    angle = 60.0
    abil_lvl = sentence.subject.abilities[InvisibilityGrantAbility]
    sentence.result = SUCCESS
    if sentence.subject.AbilityInv.hasTempMod(VanishAbility, ISDrainTargetAbility):
        Utility.outputAbilityDebug('Vanish Prox Override')
        return 0
    temp_mods = sentence.subject.AbilityInv.getTempMods(GhostStrikeAbility, ISDrainTargetAbility)
    for temp_mod in temp_mods:
        Utility.outputAbilityDebug('Ghost Strike override')
        sentence.result = temp_mod.Value
        return 0

    opponents = sentence.subject.PlayerCharacter.getEnemiesInRadius(800)
    if len(opponents) == 0:
        return 0
    for opponent in opponents:
        if opponent.locator != sentence.subject.locator:
            if opponent.AbilityInv is not None and opponent.AbilityInv.hasTempMod(BlindAbility):
                continue
            (inFrust, dist) = Utility.isObjWithinFrust(opponent, sentence.subject, angle)
            if inFrust:
                if physics.clearLine(opponent.Position, sentence.subject.Position):
                    Utility.outputAbilityDebug("InvisibilityGrant_Test: Can't become invisibility!")
                    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_CANT_INVISIBLE_NEAR_ENEMIES, 0, sentence.subject.locator, opponent.locator, 0)
                    sentence.result = FAILURE
                    return 0

    return


InvisibilityGrant_Test.depAttr = '\nindirectObject.type\n'

def ImpartInvisibility_Test(sentence):
    Utility.outputAbilityDebug('ImpartInvisibility: test')
    if sentence.subject == sentence.directObject:
        sentence.result = FAILURE
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_IMPART_INVIS_FAIL, 0, sentence.subject.locator, 0, 0)
    else:
        sentence.result = SUCCESS
    return


ImpartInvisibility_Test.depAttr = '\n'
INVISIBILITY_BASE_TIME = 30.0

def InvisibilityImparted_DirectObject(subject, msg):
    Utility.outputAbilityDebug('Invisibility: Subject')
    subject.CharMvt.addDissemblingType(kInvisible)
    duration = 120
    Utility.outputAbilityDebug('Invisibility: Check deactivation in %f seconds.' % duration)
    procs = (
     '', 'InvisibilityTerm', '', '', '')
    subject.AbilityInv.addTempModProcs(procs, InvisibilityImpartedAbility, NullTargetAbility, duration, 0, 1)
    Utility.turnOffAuras(subject)
    return


def InvisibilityImparted_Deactivate(subject):
    subject.AbilityInv.removeTempMod(InvisibilityImpartedAbility, NullTargetAbility)
    Utility.outputAbilityDebug('Invisibility_Termination: Deactivate')
    subject.CharMvt.removeDissemblingType(kInvisible)
    return


def InvisibilityImparted_Test(sentence):
    if sentence.subject.AbilityInv.hasTempMod(InvisibilityImpartedAbility, NullTargetAbility) or sentence.subject.AbilityInv.hasTempMod(InvisibilityGrantAbility, NullTargetAbility):
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


InvisibilityImparted_Test.depAttr = '\n'
DETECTTRAPS_TRAP_DETECTED_FX = FX.FX_OBJECT_SELECT_TARGET_SELECT_CHAR

def DetectTraps_DirectObject(subject, msg):
    Utility.outputAbilityDebug('Executing DetectTraps')
    if subject.Trap is None:
        return
    tool_type = msg.result
    if subject.Trap.TrapArmed:
        abil_lvl = subject.abilities[DisarmTrapsAbility]
        trap_detect_bonus = discovery.getGameObjectPropValue(tool_type, 'TrapDetectBonus')
        trap_detect_mod = subject.Trap.DetectDifficulty
        trap_detect_roll = Utility.evalContest(abil_lvl, trap_detect_bonus, 0, trap_detect_mod)
        if trap_detect_roll > 0:
            Utility.outputAbilityDebug('Trapped object type %s detected!' % subject.type)
            subject.DetectTrapFX = DETECTTRAPS_TRAP_DETECTED_FX
            Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TRAP_DETECTED, DetectTrapsAbility, msg.subjectLocator, msg.directObjectLocator)
            return
        Utility.outputAbilityDebug('Trapped object type %s not detected!' % subject.type)
    Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TRAP_NOT_DETECTED, DetectTrapsAbility, msg.subjectLocator, msg.directObjectLocator)
    return


def DetectTraps_Test(sentence):
    sentence.result = sentence.indirectObject.type
    return
    return


DetectTraps_Test.depAttr = '\nindirectObject.type\n'

def DisarmTraps_DirectObject(directObject, msg):
    tool_type = msg.result
    if tool_type & CRITICAL_FAILURE_MASK:
        directObject.DmgVol.applyDamage(msg.subjectLocator)
        directObject.StateMachine.setState('TriggeredState')
    else:
        directObject.StateMachine.setState('DisarmedState')
    return


def DisarmTraps_Test(sentence):
    Utility.outputAbilityDebug('Testing for disarming trap!')
    if not sentence.directObject.Trappable:
        Utility.outputAbilityDebug('Object cannot be disarmed!')
        sentence.result = 0
        return
    if not sentence.directObject.TrapArmed:
        Utility.outputAbilityDebug('Object is not trapped!')
        sentence.result = 0
        return
    abil_lvl = sentence.subject.abilities[DisarmTrapsAbility]
    disarm_bonus = discovery.getGameObjectPropValue(sentence.indirectObject.type, 'TrapDisarmBonus')
    disarm_mod = sentence.directObject.DisarmDifficulty
    trap_disarm_roll = Utility.evalContest(abil_lvl, disarm_bonus, 0, disarm_mod)
    trap_visible = sentence.directObject.DetectTrapFX
    if not trap_visible:
        Utility.outputAbilityDebug('Critically failed to disarm trap, need to detect first!')
        sentence.result = sentence.indirectObject.type ^ CRITICAL_FAILURE_MASK
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_TRAP_DISARM_CRITICAL_FAILURE, DisarmTrapsAbility, sentence)
        return
    if trap_disarm_roll > 0:
        Utility.outputAbilityDebug('Trap is disarmed!')
        sentence.result = sentence.indirectObject.type
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_TRAP_DISARMED, DisarmTrapsAbility, sentence)
        return
    elif trap_disarm_roll <= 0 and trap_disarm_roll > -25:
        Utility.outputAbilityDebug('Failed to disarm trap!')
        sentence.result = 0
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_TRAP_DISARM_FAILURE, DisarmTrapsAbility, sentence)
        return
    else:
        Utility.outputAbilityDebug('Critically failed to disarm trap!')
        sentence.result = sentence.indirectObject.type ^ CRITICAL_FAILURE_MASK
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_TRAP_DISARM_CRITICAL_FAILURE, DisarmTrapsAbility, sentence)
        return
    return


DisarmTraps_Test.depAttr = '\ndirectObject.DetectTrapFX\ndirectObject.TrapArmed\ndirectObject.TrapVisible\ndirectObject.DisarmDifficulty\ndirectObject.Trappable\nindirectObject.type\n'

def SetTraps_DirectObject(directObject, msg):
    trap_type = msg.result
    is_fail = 0
    is_crit_fail = 0
    if trap_type & CRITICAL_FAILURE_MASK:
        is_crit_fail = 1
        trap_type = trap_type ^ CRITICAL_FAILURE_MASK
    elif trap_type & FAILURE_MASK:
        is_fail = 1
        trap_type = trap_type ^ FAILURE_MASK
    if is_fail and not directObject.TrapArmed:
        return
    elif is_fail and directObject.TrapArmed:
        directObject.DmgVol.applyDamage(msg.subjectLocator)
        directObject.StateMachine.setState('TriggeredState')
        return
    if is_crit_fail and not directObject.TrapArmed:
        directObject.DmgVol.applyDamage(msg.subjectLocator)
        directObject.StateMachine.setState('TriggeredState')
        return
    elif is_crit_fail and directObject.TrapArmed:
        directObject.DmgVol.applyDamage(msg.subjectLocator)
        directObject.StateMachine.setState('TriggeredState')
    damage_volume = discovery.getGameObjectPropValue(trap_type, 'TrapDamageVol')
    detect_mod = discovery.getGameObjectPropValue(trap_type, 'TrapDetectMod')
    disarm_mod = discovery.getGameObjectPropValue(trap_type, 'TrapDisarmMod')
    damage_mod = discovery.getGameObjectPropValue(trap_type, 'TrapDamageMod')
    trap_fx = discovery.getGameObjectPropValue(trap_type, 'TrapKitFX')
    if directObject.StateMachine.setState('TrappedState'):
        directObject.DmgVol.DamageMin = damage_mod * 2 + 15
        directObject.DmgVol.DamageMax = damage_mod * 3 + 30
        directObject.DisarmDifficulty = disarm_mod
        directObject.DetectDifficulty = detect_mod
        directObject.TrapFX = trap_fx
        if damage_volume.x and damage_volume.y and damage_volume.z:
            directObject.DmgVol.DamageBox.x = damage_volume.x
            directObject.DmgVol.DamageBox.y = damage_volume.y
            directObject.DmgVol.DamageBox.z = damage_volume.z
        if is_crit_fail:
            directObject.DmgVol.applyDamage(msg.subjectLocator)
            directObject.StateMachine.setState('TriggeredState')
            return
        Utility.outputAbilityDebug('Trap is set!')
    else:
        Utility.outputAbilityDebug('ERROR PUTTING OBJECT INTO TRAPPED STATE')
    return


def SetTraps_Test(sentence):
    Utility.outputAbilityDebug('Testing for placing trap!')
    if not sentence.directObject.Trappable:
        Utility.outputAbilityDebug('Object cannot be trapped!')
        sentence.result = FAILURE
        return
    abil_lvl = sentence.subject.abilities[SetTrapsAbility]
    trap_set_difficulty = discovery.getGameObjectPropValue(sentence.indirectObject.type, 'TrapArmMod')
    trap_set_roll = Utility.evalContest(abil_lvl, 0, 0, trap_set_difficulty)
    if trap_set_roll > 0:
        if not sentence.directObject.TrapArmed:
            Utility.outputAbilityDebug('Trap set successfully!')
            sentence.result = sentence.indirectObject.type
            Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TRAP_SET, SetTrapsAbility, sentence.subject.locator, 0, sentence.directObject.type)
            return
        else:
            Utility.outputAbilityDebug('Could not trap, object was already trapped!')
            sentence.result = FAILURE
            return
    elif trap_set_roll <= 0 and trap_set_roll >= -25:
        Utility.outputAbilityDebug('Failed to set trap!')
        sentence.result = sentence.indirectObject.type ^ FAILURE_MASK
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TRAP_SET_FAILURE, SetTrapsAbility, sentence.subject.locator, 0, sentence.directObject.type)
        return
    else:
        Utility.outputAbilityDebug('Critically failed to set trap!')
        sentence.result = sentence.indirectObject.type ^ CRITICAL_FAILURE_MASK
        Utility.SendAbilityOutputToCaster(StringTable.ID_CLIENT_ABILITY_TRAP_SET_CRITICAL_FAILURE, SetTrapsAbility, sentence.subject.locator, 0, sentence.directObject.type)
        return
    return


SetTraps_Test.depAttr = '\ndirectObject.TrapArmed\ndirectObject.Trappable\nindirectObject.TrapArmMod\nindirectObject.type\n'

def PacketSniffer_DirectObject(directObject, msg):
    trap_type = msg.result
    is_fail = 0
    is_crit_fail = 0
    if trap_type & CRITICAL_FAILURE_MASK:
        is_crit_fail = 1
        trap_type = trap_type ^ CRITICAL_FAILURE_MASK
    elif trap_type & FAILURE_MASK:
        is_fail = 1
        trap_type = trap_type ^ FAILURE_MASK
    if is_fail and directObject.TrapArmed:
        directObject.DmgVol.applyDamage(msg.subjectLocator)
        directObject.StateMachine.setState('TriggeredState')
        return
    if is_crit_fail and directObject.TrapArmed:
        directObject.DmgVol.applyDamage(msg.subjectLocator)
        directObject.StateMachine.setState('TriggeredState')
    damage_volume = discovery.getGameObjectPropValue(trap_type, 'TrapDamageVol')
    detect_mod = discovery.getGameObjectPropValue(trap_type, 'TrapDetectMod')
    disarm_mod = discovery.getGameObjectPropValue(trap_type, 'TrapDisarmMod')
    damage_mod = discovery.getGameObjectPropValue(trap_type, 'TrapDamageMod')
    trap_fx = discovery.getGameObjectPropValue(trap_type, 'TrapKitFX')
    print trap_fx
    if directObject.StateMachine.setState('TrappedState'):
        directObject.DmgVol.DamageMin = damage_mod * 2 + 15
        directObject.DmgVol.DamageMax = damage_mod * 3 + 30
        directObject.DisarmDifficulty = disarm_mod
        directObject.DetectDifficulty = detect_mod
        directObject.TrapFX = trap_fx
        print directObject.TrapFX
        if damage_volume.x and damage_volume.y and damage_volume.z:
            directObject.DmgVol.DamageBox.x = damage_volume.x
            directObject.DmgVol.DamageBox.y = damage_volume.y
            directObject.DmgVol.DamageBox.z = damage_volume.z
        if is_crit_fail:
            directObject.DmgVol.applyDamage(msg.subjectLocator)
            directObject.StateMachine.setState('TriggeredState')
            return
        Utility.outputAbilityDebug('Trap is set!')
    else:
        Utility.outputAbilityDebug('ERROR PUTTING OBJECT INTO TRAPPED STATE')
    return


def PacketSniffer_Test(sentence):
    Utility.outputAbilityDebug('Testing for placing trap!')
    if Utility.GetToolAbility(sentence) != SetTrapsAbility:
        Utility.outputAbilityDebug('Tool required to set a trap!')
        sentence.result = FAILURE
        return
    if not sentence.subject.hasAbility[SetTrapsAbility]:
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_NO_ABILITY_EQUIPPED, SetTrapsAbility, sentence)
        sentence.result = FAILURE
        return
    if not sentence.directObject.Trappable:
        Utility.outputAbilityDebug('Object cannot be trapped!')
        sentence.result = FAILURE
        return
    if sentence.directObject.TrapArmed:
        Utility.outputAbilityDebug('Object is already trapped!')
    abil_lvl = sentence.subject.abilities[SetTrapsAbility]
    trap_set_difficulty = discovery.getGameObjectPropValue(sentence.indirectObject.type, 'TrapArmMod')
    trap_set_roll = Utility.evalContest(abil_lvl, 0, 0, trap_set_difficulty)
    if trap_set_roll > 0:
        Utility.outputAbilityDebug('Trap set successfully!')
        sentence.result = sentence.indirectObject.type
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_TRAP_SET, SetTrapsAbility, sentence)
        return
    elif trap_set_roll <= 0 and trap_set_roll >= -25:
        Utility.outputAbilityDebug('Failed to set trap!')
        sentence.result = sentence.indirectObject.type ^ FAILURE_MASK
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_TRAP_SET_FAILURE, SetTrapsAbility, sentence)
        return
    else:
        Utility.outputAbilityDebug('Critically failed to set trap!')
        sentence.result = sentence.indirectObject.type ^ CRITICAL_FAILURE_MASK
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_TRAP_SET_CRITICAL_FAILURE, SetTrapsAbility, sentence)
        return
    return


PacketSniffer_Test.depAttr = '\ndirectObject.TrapArmed\ndirectObject.Trappable\nindirectObject.TrapArmMod\nindirectObject.type\n'

def RootingAttack_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RootingAttack_DirectObject  : ')
    SE.EnableRooting(subject, msg.subjectLocator, RootingAttackAbility, ROOTING_DURATION)
    discovery.playEffect(subject.locator, subject.locator, ROOTING_FX, 0)
    return


def RootingAttack_Subject(subject, msg):
    Utility.outputAbilityDebug('RootingAttack_Subject  :  ')
    subject.CharMvt.playScript(Stance_Stand, Action_ThrowKnife, RootingAttackAbility)
    return


def RootingAttack_Test(sentence):
    sentence.result = SUCCESS
    return


RootingAttack_Test.depAttr = '\ndirectObject.locator\n'

def MezzingAttack_DirectObject(subject, msg):
    Utility.outputAbilityDebug('MezzingAttack_DirectObject  : ')
    SE.EnableMezzing(subject, msg.subjectLocator, MezzingAttackAbility, MEZZING_DURATION)
    discovery.playEffect(subject.locator, subject.locator, ROOTING_FX, 0)
    return


def MezzingAttack_Subject(subject, msg):
    Utility.outputAbilityDebug('RootingAttack_Subject  :  ')
    subject.CharMvt.playScript(Stance_Stand, Action_ThrowKnife, MezzingAttackAbility)
    return


def MezzingAttack_Test(sentence):
    sentence.result = SUCCESS
    return


MezzingAttack_Test.depAttr = '\ndirectObject.locator\n'

def Hamstring_DirectObject(subject, msg):
    Utility.outputAbilityDebug('Hamstring_DirectObject  : ')
    SE.EnableSlowness(subject, msg.subjectLocator, HamstringAbility, HAMSTRING_SLOWNESS_MOD, HAMSTRING_DURATION, 0)
    subject.AbilityInv.addTempMod(HamstringAbility, AttackAbility, HAMSTRING_COMBAT_TACTICS_MOD, HAMSTRING_SLOWNESS_MOD, 0, 0)
    discovery.playEffect(subject.locator, subject.locator, HAMSTRING_FX, 0)
    return


def Hamstring_Subject(subject, msg):
    Utility.outputAbilityDebug('Hamstring_Subject  : ')
    subject.CharMvt.playScript(Stance_Stand, Action_ThrowKnife, HamstringAbility)
    return


def Hamstring_Test(sentence):
    sentence.result = SUCCESS
    defenderDif = 0
    toolBonus = 0
    attackerRoll = 1
    successfull = True
    if attackerRoll < defenderDif:
        sentence.result = SUCCESS
    return
    return


Hamstring_Test.depAttr = '\ndirectObject.locator\n'

def SneakFreeDisableShield_Test(sentence):
    Utility.outputAbilityDebug('SneakFreeDisableShield_Test')
    gfa.AbilityThrowFreeAttack(sentence)
    if sentence.result != SUCCESS:
        return
    if sentence.subject.AbilityInv.hasTempMod(InvisibilityGrantAbility, NullTargetAbility) or sentence.subject.AbilityInv.hasTempMod(InvisibilityImpartedAbility, NullTargetAbility):
        temp_mods = sentence.subject.AbilityInv.getTempMods(InvisibilityGrantAbility, NullTargetAbility)
        duration = 0
        for temp_mod in temp_mods:
            if temp_mod.TimeLeft > duration:
                duration = temp_mod.TimeLeft

        temp_mods = sentence.subject.AbilityInv.getTempMods(InvisibilityImpartedAbility, NullTargetAbility)
        for temp_mod in temp_mods:
            if temp_mod.TimeLeft > duration:
                duration = temp_mod.TimeLeft

        procs = (
         '', 'DisableShieldRestealth', '', '', '')
        sentence.subject.AbilityInv.addTempModProcs(procs, SneakFreeDisableShieldAbility, ISDrainTargetAbility, 1.5, 0, 0, duration)
    else:
        procs = (
         '', 'DisableShieldRestealth', '', '', '')
        sentence.subject.AbilityInv.addTempModProcs(procs, SneakFreeDisableShieldAbility, ISDrainTargetAbility, 1.5, 0, 0, -1)
    Utility.outputAbilityDebug('SneakFreeDisableShield_Test - res %d' % sentence.result)
    return


SneakFreeDisableShield_Test.depAttr = '\ndirectObject.hasAbility[CurrentTacticAbility]\ndirectObject.hasAbility[ThrowDefenseTacticsAbility]\ndirectObject.abilities[ThrowDefenseTacticsAbility]\ndirectObject.abilities[ConsistencyAbility]\ndirectObject.locator\n'
return

# okay decompiling work/monolith_pyc\ability\spy\misc.pyc
