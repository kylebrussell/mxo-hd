# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\hacker\misc.py
# Compiled at: 2008-02-21 04:32:03
import random, traceback, obj, missionvalidate as mv, combatvalidate as cv, ability.utility as Utility, stringtable_client as StringTable, ltfxmap as FX
from ability.defines import *
NEGATIVE_COND_TIME = 30
NEGATIVE_COND_1_REMOVE_PERCENTS = 25
NEGATIVE_COND_2_REMOVE_PERCENTS = 50
NEGATIVE_COND_3_REMOVE_PERCENTS = 75
RECALL_TEAM_FX = FX.FX_CHARACTER_RECALL_MISSION_TEAM_PHASE_OUT
RECALL_TEAMMEMBER_FX = FX.FX_CHARACTER_RECALL_TEAM_MEMBER_PHASE_OUT
RECALL_TEAM_CASTER_FX = FX.FX_CHARACTER_RECALL_MISSION_TEAM_CAST
RECALL_TEAM_MEMBER_CASTER_FX = FX.FX_CHARACTER_RECALL_TEAM_MEMBER_CAST

def RecallTeamMember_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RecallTeamMember_DirectObject: Recalling team member')
    Utility.outputAbilityDebug('Result: %d, current construct: %d' % (msg.result, subject.CharMvt.getCurrentConstructID()))
    if msg.result != consolevar.ConID or subject.CharMvt.isInLoadingArea():
        Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_RECALL_ACROSS_CONSTRUCTS, RecallTeamMemberAbility, msg)
        return
    if subject.Interlock.IsInCombat:
        Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_TELEPORTING_INCOMBAT, RecallTeamMemberAbility, msg)
        return
    if subject.IsDead:
        Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_TELEPORTING_DEAD, RecallTeamMemberAbility, msg)
        return
    discovery.playEffect(subject.locator, subject.locator, RECALL_TEAMMEMBER_FX, 0)
    subject.CharMvt.realTeleport(msg.result, msg.location)
    return


def RecallTeamMember_Subject(subject, msg):
    Utility.outputAbilityDebug('RecallTeamMember_Subject: Recalling team member')
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_TELEPORTING_CASTER, RecallTeamMemberAbility, msg)
    Utility.SendAbilityOutputToTargetMsg(StringTable.ID_CLIENT_TELEPORTING_TARGET, RecallTeamMemberAbility, msg)
    return


def RecallTeamMember_Test(sentence):
    Utility.outputAbilityDebug('RecallTeamMember_test')
    if sentence.subject.locator == sentence.directObject.locator:
        Utility.outputAbilityDebug("Can't recall self")
        return
    sentence.result = consolevar.ConID
    Utility.outputAbilityDebug('Result: %d, current construct: %d' % (sentence.result, sentence.subject.CharMvt.getCurrentConstructID()))
    return


RecallTeamMember_Test.depAttr = '\n'

def RecallMissionTeam_DirectObject(subject, msg):
    Utility.outputAbilityDebug('RecallTeam_DirectObject: Recalling team member')
    if subject.locator == msg.subjectLocator:
        return
    RecallTeamMember_DirectObject(subject, msg)
    return


def RecallMissionTeam_Test(sentence):
    Utility.outputAbilityDebug('RecallMissionTeam_Test')
    sentence.result = consolevar.ConID
    return


RecallMissionTeam_Test.depAttr = '\n'

def EngageFoes_DirectObject(subject, msg):
    if not Utility.IsAnNPC(subject):
        return
    if subject.AI:
        subject.AI.setMostHated(msg.subjectLocator)
    return


def DownloadMissionMap_Deactivate(subject):
    subject.MissionTeam.disableMissionMap()
    return


return

# okay decompiling work/monolith_pyc\ability\hacker\misc.pyc
