# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\clientrules.py
# Compiled at: 2006-03-30 16:29:38
import random, traceback, missionvalidate as mv, combatvalidate as cv, followvalidate as fv, stringtable_client as ST

def exceptionCB(obj, data, tb):
    try:
        discovery.errorPrint('****************************************')
        discovery.errorPrint('Rule Exception')
        discovery.errorPrint(`obj`)
        discovery.errorPrint(`data`)
        discovery.errorPrint('** traceback **')
        if tb:
            stackTrace = traceback.extract_tb(tb)
            stackTrace.reverse()
            for tuple in stackTrace:
                discovery.errorPrint('File: %s (%d) Function: %s' % tuple[:3])
                discovery.errorPrint(tuple[3])

        discovery.errorPrint('****************************************')
    except:
        pass

    return


def debugTest():
    discovery.outputDebugString('ODS: Rules System Functioning')
    discovery.consolePrint('CS: Rules System Functioning')
    return


def Get(possible, default):
    if possible is None:
        return default
    return possible
    return


def Success(sentence):
    """
    You can use this for any always-successful verification
    """
    sentence.result = SUCCESS
    return


Success.ID = 1
Success.depAttr = ''

def Failure(sentence):
    """
    You can use this for any always-failed verification
    """
    sentence.result = FAILURE
    return


Failure.ID = 0
Failure.depAttr = ''

def TurnLightOn(sentence):
    """
    This rule is called whenever a light switch is turned on
    """
    sentence.result = SUCCESS
    return


TurnLightOn.ID = 9000
TurnLightOn.depAttr = ''

def TurnLightOff(sentence):
    """
    This rule is called whenever a light switch is turned off
    """
    sentence.result = SUCCESS
    return


TurnLightOff.ID = 9001
TurnLightOff.depAttr = ''

def ElevShowPanelUI(sentence):
    """
    This rule is called whenever an elevator is used
    """
    sentence.result = SUCCESS
    return


ElevShowPanelUI.ID = 9002
ElevShowPanelUI.depAttr = ''

def DecompileAbilityTest(sentence):
    """
    This rule checks for the presence of the decompile ability.
    """
    abil = sentence.subject.abilities[DecompileAbility] * 4
    if abil > 0:
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


DecompileAbilityTest.ID = 6000
DecompileAbilityTest.depAttr = ''

def GetItemsMatchingType(sentence, type):
    return sentence.subject.Inventory.getBackpackItems(type)
    return


def GetMissionItems(sentence):
    return sentence.subject.Inventory.getMissionItems(sentence.subject.MissionKey)
    return


def HasMissionKeyItem(sentence, type=0):
    if type != 0:
        item_list = GetItemsMatchingType(sentence, type)
    else:
        item_list = GetMissionItems(sentence)
    if len(item_list) == 0:
        return FAILURE
    for eachItem in item_list:
        if mv.ValidMissionKeyMatch(eachItem.MissionKey, sentence.directObject.MissionKey):
            return SUCCESS

    discovery.consolePrint('The key is not for this mission.')
    return FAILURE
    return


HasMissionKeyItem.depAttr = '\ndirectObject.MissionKey\n'

def HasToolForAbility(sentence, ability):
    """ checks if there is a tool appropriate for ability in inventory, returns True or False"""
    if sentence.subject.AI is not None:
        return SUCCESS
    tool = sentence.subject.Inventory.findToolForAbility(ability)
    if tool is not None:
        return SUCCESS
    else:
        return FAILURE
    return


def ComputerInfect(sentence):
    """
    Succeed if we have a computer virus which
    matches our mission key.
    """
    if not mv.ValidMissionKeyBaseMatch(sentence.subject.MissionKey, sentence.directObject.MissionKey):
        return
    if HasMissionKeyItem(sentence, discovery.getGameObjectType('Computer Virus')):
        sentence.result = SUCCESS
        return
    return


ComputerInfect.ID = 1003
ComputerInfect.depAttr = HasMissionKeyItem.depAttr
ComputerInfect.depAttr += '\nindirectObject.instData\nindirectObject.type\n'

def ComputerHack(sentence):
    """
    Hack client rule always succeeds if this is the player's mission;
    server rule does more validation.
    """
    sentence.result = FAILURE
    if not mv.ValidMissionKeyBaseMatch(sentence.subject.MissionKey, sentence.directObject.MissionKey):
        return
    sentence.result = SUCCESS
    return
    if sentence.subject.abilities[HackerAbility]:
        sentence.result = SUCCESS
        return
    return


ComputerHack.ID = 1002
ComputerHack.depAttr = HasMissionKeyItem.depAttr
ComputerHack.depAttr += '\nindirectObject.instData\nindirectObject.type\n'

def TrapDisarm(sentence):
    """
    This rule checks for the presence of the DisarmTraps ability.
    """
    TrapIsArmed = sentence.directObject.TrapArmed
    TrapIsVisible = sentence.directObject.TrapVisible
    abil = 4 * sentence.subject.abilities[DisarmTrapsAbility]
    sentence.result = FAILURE
    if TrapIsVisible:
        if TrapIsArmed:
            if abil > 0:
                sentence.result = SUCCESS
    return


TrapDisarm.ID = 4010
TrapDisarm.depAttr = '\ndirectObject.TrapArmed\ndirectObject.TrapVisible\n'

def SecurityPBXDisable(sentence):
    """
    This rule always returns true. The server rule performs validation.
    """
    sentence.result = SUCCESS
    return


SecurityPBXDisable.ID = 1012
SecurityPBXDisable.depAttr = '\nindirectObject.type\n'

def DoorPickLock(sentence):
    """
    Pick Lock will always appear, except in the tutorial
    """
    if clientshell.getTutorialState() != clientshell.eTutState_Invalid:
        sentence.result = FAILURE
        return
    sentence.result = SUCCESS
    return


DoorPickLock.ID = 4000
DoorPickLock.depAttr = '\nindirectObject.Quality\nindirectObject.ReqAbilityID\ndirectObject.Difficulty\ndirectObject.locator\n'

def GetOnLadder(sentence):
    """
    This rule allows for checks before a player can get on a ladder.
    """
    sentence.result = FAILURE
    if sentence.subject.Stance != Stance_Stand and sentence.subject.Stance != Stance_Aggro and sentence.subject.Stance != Stance_Sneak and sentence.subject.Stance != Stance_Invis:
        return
    if sentence.directObject.Ladder is None:
        return
    heightdiff = sentence.subject.Position.y - sentence.directObject.Position.y
    if heightdiff > 0:
        sentence.directObject.Ladder.checkNavMeshTop()
        if not sentence.directObject.Ladder.HasValidTopPosition:
            discovery.consolePrint('HasValidTopPosition = %d' % sentence.directObject.Ladder.HasValidTopPosition)
            return
    else:
        sentence.directObject.Ladder.checkNavMeshBottom()
        if not sentence.directObject.Ladder.HasValidBottomPosition:
            return
    if sentence.subject.CharMvt is None:
        return
    if sentence.subject.CharMvt.isInside():
        return
    sentence.result = SUCCESS
    return


GetOnLadder.ID = 4100
GetOnLadder.depAttr = '\n'

def DoorLockTest(sentence):
    """
    This rule returns success if the player has the key, otherwise failure
    """
    sentence.result = FAILURE
    if not mv.ValidMissionKeyBaseMatch(sentence.subject.MissionKey, sentence.directObject.MissionKey):
        return
    if HasMissionKeyItem(sentence):
        sentence.result = SUCCESS
        return
    return


DoorLockTest.ID = 4003
DoorLockTest.depAttr = HasMissionKeyItem.depAttr
DoorLockTest.depAttr += '\nindirectObject.instData\ndirectObject.MissionKey\n'

def CardKeyTest(sentence):
    """
    This rule simply returns success
    """
    if not mv.ValidMissionKeyBaseMatch(sentence.subject.MissionKey, sentence.directObject.MissionKey):
        return
    if HasMissionKeyItem(sentence, discovery.getGameObjectType('CardKey')):
        sentence.result = SUCCESS
        return
    return


CardKeyTest.ID = 4005
CardKeyTest.depAttr = HasMissionKeyItem.depAttr
CardKeyTest.depAttr += '\nindirectObject.instData\n'

def GenericKeyTest(sentence):
    """
    This rule returns success if the player has the correct key
    otherwise returns failure
    """
    sentence.result = FAILURE
    if not mv.ValidMissionKeyBaseMatch(sentence.subject.MissionKey, sentence.directObject.MissionKey):
        return
    if HasMissionKeyItem(sentence):
        sentence.result = SUCCESS
        return
    return


GenericKeyTest.ID = 4006
GenericKeyTest.depAttr = HasMissionKeyItem.depAttr
GenericKeyTest.depAttr += '\nindirectObject.instData\ndirectObject.MissionKey\n'

def DoorOpenGeneric(sentence):
    """
    This rule enforces that a door that has been locked to a specific
    mission team may only be opened by them.
    """
    playerkey = sentence.subject.MissionKey
    if playerkey != mv.NON_ASSIGNABLE_MISSIONKEY and not mv.ValidMissionKeyBaseMatch(sentence.directObject.MissionKey, playerkey):
        return
    sentence.result = SUCCESS
    return


DoorOpenGeneric.ID = 4004
DoorOpenGeneric.depAttr = '\ndirectObject.MissionKey\n'

def StanceSiteEnter(sentence):
    """
    This rule should be used as a general stance site rule.
    """
    sentence.result = FAILURE
    if not sentence.directObject.SiteInUse:
        if sentence.subject.Stance == 0:
            sentence.result = SUCCESS
    return


StanceSiteEnter.ID = 4110
StanceSiteEnter.depAttr = '\ndirectObject.SiteInUse\n'

def RequestCombat(sentence):
    """
    This rule should become quite complex as it tries to figure
    out if anyone can fight anything.
    """
    if cv.CanFight(sentence.subject, sentence.directObject):
        sentence.result = SUCCESS
    return


RequestCombat.ID = 2001
RequestCombat.depAttr = '\ndirectObject.IsDead\ndirectObject.FactionID\ndirectObject.NavPolyID\n'

def CanFollowClient(subject, directObject):
    if mv.ValidMissionKeyBaseMatch(subject.MissionTeam.MissionKey, directObject.MissionElement.MissionKey):
        return True
    return False
    return


def CanStopFollowClient(directObject):
    if consolevar.PlayerID == directObject.AI.MasterCharacterID:
        return True
    return False
    return


def FollowMeNPC(sentence):
    """
    This rule is called whenever an amenable NPC is told to follow
    """
    if CanFollowClient(sentence.subject, sentence.directObject):
        sentence.result = SUCCESS
    return


FollowMeNPC.ID = 9004
FollowMeNPC.depAttr = '\ndirectObject.SimulacraSlave.OwnerCharacterID\ndirectObject.MissionElement.MissionKey\n'

def StopFollowingMeNPC(sentence):
    """
    This rule is called whenever an obsequious NPC is told to stop following
    """
    if CanStopFollowClient(sentence.directObject):
        sentence.result = SUCCESS
    return


StopFollowingMeNPC.ID = 9005
StopFollowingMeNPC.depAttr = '\ndirectObject.AI.MasterCharacterID\n'

def IsYourSimulacrum(sentence):
    """
    This rule is called to check if a simulacrum is yours
    """
    if sentence.directObject.SimulacraSlave.OwnerCharacterID == consolevar.PlayerID:
        sentence.result = SUCCESS
    return


IsYourSimulacrum.ID = 9006
IsYourSimulacrum.depAttr = '\ndirectObject.SimulacraSlave.OwnerCharacterID\n'

def MissionTeamInvite(sentence):
    sentence.result = SUCCESS
    return


MissionTeamInvite.ID = 10010
MissionTeamInvite.depAttr = ''

def HardlineHackControlNodeStage1(sentence):
    """
    Return SUCCESS if the player is an attacker and can attack this control node.
    """
    if sentence.subject.FactionID != sentence.directObject.FactionID:
        sentence.result = SUCCESS
    return


HardlineHackControlNodeStage1.ID = 10025
HardlineHackControlNodeStage1.depAttr = '\n'

def HardlineHackControlNodeStage2(sentence):
    """
    Return SUCCESS if the player can hack this control node.
    """
    if sentence.subject.FactionID != sentence.directObject.FactionID:
        sentence.result = SUCCESS
    return


HardlineHackControlNodeStage2.ID = 10026
HardlineHackControlNodeStage2.depAttr = '\n'

def HardlineHackControlNodeStage3(sentence):
    """
    Return SUCCESS if the player can hack this control node.
    """
    if sentence.subject.FactionID != sentence.directObject.FactionID:
        sentence.result = SUCCESS
    return


HardlineHackControlNodeStage3.ID = 10027
HardlineHackControlNodeStage3.depAttr = '\n'

def HardlineHackControlNodeClaimConstruct(sentence):
    """
    Return SUCCESS if the player can hack this control node.
    """
    if sentence.subject.FactionID != sentence.directObject.FactionID:
        sentence.result = SUCCESS
    return


HardlineHackControlNodeClaimConstruct.ID = 10028
HardlineHackControlNodeClaimConstruct.depAttr = '\n'

def HardlineEnterConstruct(sentence):
    """
    Return SUCCESS if the player can enter constructs via this hardline.
    """
    if sentence.subject.FactionID == 0:
        sentence.result = FAILURE
        return
    if not faction.hasConstructs():
        sentence.result = FAILURE
        return
    sentence.result = SUCCESS
    return


HardlineEnterConstruct.ID = 10023
HardlineEnterConstruct.depAttr = '\n'

def HardlineHackConstruct(sentence):
    """
    Return SUCCESS if the player can hack via this hardline.
    """
    if sentence.subject.FactionFlags & 16:
        sentence.result = FAILURE
        return
    if Get(sentence.subject.abilities[HackConstructKeyAbility], 0) == 0:
        sentence.result = FAILURE
        return
    if sentence.subject.FactionID == 0:
        sentence.result = FAILURE
        return
    if consolevar.HackConstructInfoCost > sentence.subject.Information:
        sentence.result = FAILURE
        return
    if not faction.isAtWarWithAnyone():
        sentence.result = FAILURE
        return
    sentence.result = SUCCESS
    return


HardlineHackConstruct.ID = 10024
HardlineHackConstruct.depAttr = '\n'

def TutorialExitPtJackIntoTheMatrix(sentence):
    """
    Return SUCCESS if the player can exit the tutorial
    """
    if clientshell.getTutorialState() != clientshell.eTutState_PostStatus and constants.Build.final:
        sentence.result = FAILURE
        return
    sentence.result = SUCCESS
    return


TutorialExitPtJackIntoTheMatrix.ID = 10037
TutorialExitPtJackIntoTheMatrix.depAttr = '\n'

def ConstructConfigure(sentence):
    """
    Return SUCCESS if the player can configure constructs via control node.
    """
    if sentence.subject.FactionID == sentence.directObject.FactionID:
        sentence.result = SUCCESS
    return


ConstructConfigure.ID = 10032
ConstructConfigure.depAttr = '\n'

def TagAccessNode(sentence):
    """
    Return SUCCESS if the player can tag an Access Node.
    """
    if accessnode.isValid(sentence.directObject.SoftBoundaryID) or not accessnode.canTagNode(sentence.directObject.SoftBoundaryID):
        sentence.result = FAILURE
        return
    sentence.result = SUCCESS
    return


TagAccessNode.ID = 10033
TagAccessNode.depAttr = '\n'

def HardlineExit(sentence):
    """
    Return SUCCESS if the player can exit via this hardline.
    Currently just always returns success.
    """
    if hardlineMgr.isKnown(sentence.directObject.Hardline.getID()):
        sentence.result = SUCCESS
        return
    sentence.result = FAILURE
    return


HardlineExit.ID = 10020
HardlineExit.depAttr = '\ndirectObject.locator\n'

def HardlineUpload(sentence):
    """
    Return SUCCESS if the player can upload via this hardline.
    Currently just always returns success.
    """
    if hardlineMgr.isKnown(sentence.directObject.Hardline.getID()):
        sentence.result = SUCCESS
        return
    sentence.result = FAILURE
    return


HardlineUpload.ID = 10021
HardlineUpload.depAttr = '\ndirectObject.locator\n'

def HardlineExitToLoadingArea(sentence):
    """
    Return SUCCESS if the player can exit via this hardline.
    Currently just always returns success.
    """
    if hardlineMgr.isKnown(sentence.directObject.Hardline.getID()):
        sentence.result = SUCCESS
        return
    sentence.result = FAILURE
    return


HardlineExitToLoadingArea.ID = 10022
HardlineExitToLoadingArea.depAttr = '\ndirectObject.locator\n'

def HardlineSync(sentence):
    """
    Return SUCCESS if the player does not know this hardline.
    """
    if hardlineMgr.isKnown(sentence.directObject.Hardline.getID()):
        sentence.result = FAILURE
        return
    sentence.result = SUCCESS
    return


HardlineSync.ID = 10040
HardlineSync.depAttr = '\ndirectObject.locator\n'

def ContainerOpen(sentence):
    sentence.result = FAILURE
    if sentence.directObject.UseCount != 0 and (sentence.directObject.AI is None or sentence.directObject.Container.getLootState()) or sentence.directObject.UseTaker and sentence.directObject.UseTaker.Enabled:
        sentence.result = SUCCESS
    elif sentence.directObject.AI is None and mv.IsNonReservedMatchingKey(sentence.directObject.MissionKey, sentence.subject.MissionKey):
        sentence.result = SUCCESS
    return


ContainerOpen.ID = 1010
ContainerOpen.depAttr = '\n'

def Repair(sentence):
    """
    Just pass SUCCESS
    """
    sentence.result = SUCCESS
    return


Repair.ID = 1337
Repair.depAttr = '\n'

def GenericOpen(sentence):
    """
    Just pass SUCCESS
    """
    sentence.result = SUCCESS
    return


GenericOpen.ID = 1338
GenericOpen.depAttr = '\n'

def GenericClose(sentence):
    """
    Just pass SUCCESS
    """
    sentence.result = SUCCESS
    return


GenericClose.ID = 1339
GenericClose.depAttr = '\n'

def BombDefuseStep1(sentence):
    """
    This rule is called when determining if a player can initiate defusal step 1.
    """
    itemID = int(sentence.directObject.DefuseItemID1)
    if len(GetItemsMatchingType(sentence, itemID)) > 0:
        sentence.directObject.DetailsStringID = 0
        sentence.result = SUCCESS
        return
    sentence.directObject.DetailsStringID = ST.ID_OBJ_BOMB_NO_DEFUSE
    sentence.result = FAILURE
    return


BombDefuseStep1.ID = 3330
BombDefuseStep1.depAttr = '\ndirectObject.DefuseItemID1\ndirectObject.DetailsStringID\n'

def BombDefuseStep2(sentence):
    """
    This rule is called when determining if a player can initiate defusal step 2.
    """
    itemID = int(sentence.directObject.DefuseItemID2)
    if len(GetItemsMatchingType(sentence, itemID)) > 0:
        sentence.directObject.DetailsStringID = 0
        sentence.result = SUCCESS
        return
    sentence.directObject.DetailsStringID = ST.ID_OBJ_BOMB_NO_DEFUSE
    sentence.result = FAILURE
    return


BombDefuseStep2.ID = 3331
BombDefuseStep2.depAttr = '\ndirectObject.DefuseItemID2\ndirectObject.DetailsStringID\n'

def BombDefuseStep3(sentence):
    """
    This rule is called when determining if a player can initiate defusal step 3.
    """
    itemID = int(sentence.directObject.DefuseItemID3)
    if len(GetItemsMatchingType(sentence, itemID)) > 0:
        sentence.directObject.DetailsStringID = 0
        sentence.result = SUCCESS
        return
    sentence.directObject.DetailsStringID = ST.ID_OBJ_BOMB_NO_DEFUSE
    sentence.result = FAILURE
    return


BombDefuseStep3.ID = 3332
BombDefuseStep3.depAttr = '\ndirectObject.DefuseItemID3\ndirectObject.DetailsStringID\n'

def BombDefuseStep4(sentence):
    """
    This rule is called when determining if a player can initiate defusal step 4.
    """
    itemID = int(sentence.directObject.DefuseItemID4)
    if len(GetItemsMatchingType(sentence, itemID)) > 0:
        sentence.directObject.DetailsStringID = 0
        sentence.result = SUCCESS
        return
    sentence.directObject.DetailsStringID = ST.ID_OBJ_BOMB_NO_DEFUSE
    sentence.result = FAILURE
    return


BombDefuseStep4.ID = 3333
BombDefuseStep4.depAttr = '\ndirectObject.DefuseItemID4\ndirectObject.DetailsStringID\n'

def BombDefuseStep5(sentence):
    """
    This rule is called when determining if a player can initiate defusal step 5.
    """
    itemID = int(sentence.directObject.DefuseItemID5)
    if len(GetItemsMatchingType(sentence, itemID)) > 0:
        sentence.directObject.DetailsStringID = 0
        sentence.result = SUCCESS
        return
    sentence.directObject.DetailsStringID = ST.ID_OBJ_BOMB_NO_DEFUSE
    sentence.result = FAILURE
    return


BombDefuseStep5.ID = 3334
BombDefuseStep5.depAttr = '\ndirectObject.DefuseItemID5\ndirectObject.DetailsStringID\n'

def IsAdmin(sentence):
    """
    This rule can be used to verify that the subject is an Admin
    """
    if discovery.clientIsAdmin():
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


IsAdmin.ID = 3336
IsAdmin.depAttr = '\n'

def SearchlightStep1(sentence):
    """
    This rule is called when determining if a player can initiate searchlight step 1.
    It always returns SUCCESS
    """
    sentence.result = SUCCESS
    return


SearchlightStep1.ID = 3337
SearchlightStep1.depAttr = '\n'

def SearchlightStep2(sentence):
    """
    This rule is called when determining if a player can initiate searchlight step 2.
    It always returns SUCCESS
    """
    sentence.result = SUCCESS
    return


SearchlightStep2.ID = 3338
SearchlightStep2.depAttr = '\n'

def SearchlightStep3(sentence):
    """
    This rule is called when determining if a player can initiate searchlight step 3.
    It always returns SUCCESS
    """
    sentence.result = SUCCESS
    return


SearchlightStep3.ID = 3339
SearchlightStep3.depAttr = '\n'

def SearchlightStep4(sentence):
    """
    This rule is called when determining if a player can initiate searchlight step 4.
    It always returns SUCCESS
    """
    sentence.result = SUCCESS
    return


SearchlightStep4.ID = 3340
SearchlightStep4.depAttr = '\n'

def SearchlightStep5(sentence):
    """
    This rule is called when determining if a player can initiate searchlight step 5.
    It always returns SUCCESS
    """
    sentence.result = SUCCESS
    return


SearchlightStep5.ID = 3341
SearchlightStep5.depAttr = '\n'

def SearchlightCleanup(sentence):
    """
    This rule just verifies that the subject is an Admin
    """
    if discovery.clientIsAdmin():
        sentence.result = SUCCESS
    else:
        sentence.result = FAILURE
    return


SearchlightCleanup.ID = 3342
SearchlightCleanup.depAttr = '\n'

def SearchlightStep6(sentence):
    """
    This rule is called when determining if a player can initiate searchlight step 5.
    It always returns SUCCESS
    """
    sentence.result = SUCCESS
    return


SearchlightStep6.ID = 3343
SearchlightStep6.depAttr = '\n'
return

# okay decompiling work/monolith_pyc\clientrules.pyc
