# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\emotes.py
# Compiled at: 2008-09-13 03:04:13
import traceback, stringtable_client as ST, movement as Movement

class Emote:
    __module__ = __name__

    def __init__(self, command_id, action_id):
        self.command_id = command_id
        self.isInteractive = False
        self.action_ids = {}
        self.action_ids[constants.Organization.Invalid] = action_id
        return


class EmoteDictionary:
    __module__ = __name__
    blank_emote = Emote(0, 0)

    def __init__(self):
        self.emotes = {}
        self.actions = [
         '']
        self.chatMsgKernel = {}
        return

    def addEmote(self, command_id, action, msg_kernel):
        if self.emotes.has_key(command_id):
            discovery.errorPrint('Attempted to use command id %x for "%s" but it is already used for "%s".' % (command_id, action, self.emotes[command_id].action))
            return
        new_action_id = self.createActionID(action)
        self.emotes[command_id] = Emote(command_id, new_action_id)
        self.chatMsgKernel[new_action_id] = msg_kernel
        return

    def addInteractiveEmote(self, command_id, query_action, accept_action, refuse_action, msg_kernel):
        if self.emotes.has_key(command_id):
            discovery.errorPrint('Attempted to use command id %x for "%s" but it is already used for "%s".' % (command_id, action, self.emotes[command_id].action))
            return
        new_emote = Emote(command_id, self.createActionID(query_action))
        new_emote.isInteractive = True
        new_emote.accept_action_id = self.createActionID(accept_action)
        new_emote.refuse_action_id = self.createActionID(refuse_action)
        new_emote.chatMsgKernel = msg_kernel
        self.emotes[command_id] = new_emote
        self.chatMsgKernel[new_emote.accept_action_id] = msg_kernel
        return

    def addZion(self, command_id, action):
        if not self.emotes.has_key(command_id):
            discovery.errorPrint('Attempted to add a Zion specialization to command id %x of "%s" before it has been defined.' % (command_id, action))
            return
        self.emotes[command_id].action_ids[constants.Organization.Zion] = self.createActionID(action)
        return

    def addMachines(self, command_id, action):
        if not self.emotes.has_key(command_id):
            discovery.errorPrint('Attempted to add a Machines specialization to command id %x of "%s" before it has been defined.' % (command_id, action))
            return
        self.emotes[command_id].action_ids[constants.Organization.Machines] = self.createActionID(action)
        return

    def addMerovingian(self, command_id, action):
        if not self.emotes.has_key(command_id):
            discovery.errorPrint('Attempted to add a Merovingian specialization to command id %x of "%s" before it has been defined.' % (command_id, action))
            return
        self.emotes[command_id].action_ids[constants.Organization.Merovingian] = self.createActionID(action)
        return

    def getCommandKeys(self):
        return self.emotes.keys()
        return

    def getActionList(self):
        return self.actions
        return

    def getActionID(self, command_id, organization):
        found_action_ids = self.emotes.get(command_id, self.blank_emote).action_ids
        if organization in found_action_ids:
            return found_action_ids[organization]
        return found_action_ids[constants.Organization.Invalid]
        return

    def isInteractive(self, command_id):
        return self.emotes.get(command_id, self.blank_emote).isInteractive
        return

    def getInteractiveAccept(self, command_id):
        emote = self.emotes.get(command_id, self.blank_emote)
        if emote.isInteractive:
            return emote.accept_action_id
        return 0
        return

    def getInteractiveRefuse(self, command_id):
        emote = self.emotes.get(command_id, self.blank_emote)
        if emote.isInteractive:
            return emote.refuse_action_id
        return 0
        return

    def internalGetChatMsg(self, msg_id_string, is_male):
        stringtable_dict = globals().get('ST').__dict__
        if is_male:
            return stringtable_dict.get(msg_id_string + '_MALE', stringtable_dict.get(msg_id_string, 0))
        return stringtable_dict.get(msg_id_string + '_FEMALE', stringtable_dict.get(msg_id_string, 0))
        return

    def getSubjectDirectMsg(self, action_id, is_male):
        if not self.chatMsgKernel.has_key(action_id):
            return 0
        return self.internalGetChatMsg('ID_EMOTETEXT_' + self.chatMsgKernel[action_id] + '_SUBJECT_DIRECTED', is_male)
        return

    def getSubjectIndirectMsg(self, action_id, is_male):
        if not self.chatMsgKernel.has_key(action_id):
            return 0
        return self.internalGetChatMsg('ID_EMOTETEXT_' + self.chatMsgKernel[action_id] + '_SUBJECT_INDIRECT', is_male)
        return

    def getObjectDirectMsg(self, action_id, is_male):
        if not self.chatMsgKernel.has_key(action_id):
            return 0
        return self.internalGetChatMsg('ID_EMOTETEXT_' + self.chatMsgKernel[action_id] + '_OBJECT_DIRECTED', is_male)
        return

    def getThirdPartyDirectMsg(self, action_id, is_male):
        if not self.chatMsgKernel.has_key(action_id):
            return 0
        return self.internalGetChatMsg('ID_EMOTETEXT_' + self.chatMsgKernel[action_id] + '_THIRDPARTY_DIRECTED', is_male)
        return

    def getThirdPartyIndirectMsg(self, action_id, is_male):
        if not self.chatMsgKernel.has_key(action_id):
            return 0
        return self.internalGetChatMsg('ID_EMOTETEXT_' + self.chatMsgKernel[action_id] + '_THIRDPARTY_INDIRECT', is_male)
        return

    def getObjectQueryMsg(self, command_id, is_male):
        emote = self.emotes.get(command_id, self.blank_emote)
        if not emote.isInteractive:
            return 0
        return self.internalGetChatMsg('ID_EMOTETEXT_' + emote.chatMsgKernel + '_OBJECT_QUERY', is_male)
        return

    def getSubjectAcceptMsg(self, command_id, is_male):
        emote = self.emotes.get(command_id, self.blank_emote)
        if not emote.isInteractive:
            return 0
        return self.internalGetChatMsg('ID_EMOTETEXT_' + emote.chatMsgKernel + '_SUBJECT_ACCEPT', is_male)
        return

    def getSubjectRefuseMsg(self, command_id, is_male):
        emote = self.emotes.get(command_id, self.blank_emote)
        if not emote.isInteractive:
            return 0
        return self.internalGetChatMsg('ID_EMOTETEXT_' + emote.chatMsgKernel + '_SUBJECT_REFUSE', is_male)
        return

    def createActionID(self, action):
        self.actions.append(action)
        return len(self.actions) - 1
        return


emotes = EmoteDictionary()

class Mood:
    __module__ = __name__

    def __init__(self, command_id, demeanor_id):
        self.command_id = command_id
        self.demeanor_id = demeanor_id
        return


class MoodDictionary:
    __module__ = __name__

    def __init__(self):
        self.mood_reqlevels = {}
        self.mood_ids = {}
        return

    def addMood(self, command_id, demeanor_id, min_level):
        if self.mood_ids.has_key(command_id):
            discovery.errorPrint('Attempted to use demeanor id %i for string-id %x but it is already used for %x.' % (demeanor_id, command_id, self.mood_ids[command_id].command_id))
            return
        self.mood_ids[command_id] = Mood(command_id, demeanor_id)
        self.mood_reqlevels[demeanor_id] = min_level
        return

    def getMoodRequiredLevel(self, demeanor_id):
        if not self.mood_reqlevels.has_key(demeanor_id):
            return 999999
        return self.mood_reqlevels[demeanor_id]
        return

    def getMoodValues(self):
        return self.mood_ids.values()
        return


moods = MoodDictionary()

def GetCommandKeys():
    return emotes.getCommandKeys()
    return


def GetActionList():
    return emotes.getActionList()
    return


def GetActionID(command_id, organization):
    return emotes.getActionID(command_id, organization)
    return


def IsInteractiveEmote(command_id):
    return emotes.isInteractive(command_id)
    return


def GetInteractiveAcceptID(command_id):
    return emotes.getInteractiveAccept(command_id)
    return


def GetInteractiveRefuseID(command_id):
    return emotes.getInteractiveRefuse(command_id)
    return


def GetObjectQueryMsgID(command_id, is_male):
    return emotes.getObjectQueryMsg(command_id, is_male)
    return


def GetSubjectAcceptMsgID(command_id, is_male):
    return emotes.getSubjectAcceptMsg(command_id, is_male)
    return


def GetSubjectRefuseMsgID(command_id, is_male):
    return emotes.getSubjectRefuseMsg(command_id, is_male)
    return


def IsInteractiveQueryAction(action_id):
    return emotes.isInteractiveQueryAction(action_id)
    return


def GetSubjectDirectMsgID(action_id, is_male):
    return emotes.getSubjectDirectMsg(action_id, is_male)
    return


def GetSubjectIndirectMsgID(action_id, is_male):
    return emotes.getSubjectIndirectMsg(action_id, is_male)
    return


def GetObjectDirectMsgID(action_id, is_male):
    return emotes.getObjectDirectMsg(action_id, is_male)
    return


def GetThirdPartyDirectMsgID(action_id, is_male):
    return emotes.getThirdPartyDirectMsg(action_id, is_male)
    return


def GetThirdPartyIndirectMsgID(action_id, is_male):
    return emotes.getThirdPartyIndirectMsg(action_id, is_male)
    return


def GetMoodValues():
    return moods.getMoodValues()
    return


def GetMoodRequiredLevel(demeanor_id):
    return moods.getMoodRequiredLevel(demeanor_id)
    return


emotes.addEmote(ST.ID_EMOTE_BECKON, 'beckon', 'BECKON')
emotes.addEmote(ST.ID_EMOTE_BIGWAVE, 'bigwave', 'BIGWAVE')
emotes.addEmote(ST.ID_EMOTE_BOW, 'bow', 'BOW')
emotes.addEmote(ST.ID_EMOTE_CLAP, 'clap', 'CLAP')
emotes.addEmote(ST.ID_EMOTE_CROSSARMS, 'crossarms', 'CROSSARMS')
emotes.addEmote(ST.ID_EMOTE_NOD, 'nod', 'NOD')
emotes.addEmote(ST.ID_EMOTE_YES, 'nod', 'YES')
emotes.addEmote(ST.ID_EMOTE_AGREE, 'nod', 'AGREE')
emotes.addEmote(ST.ID_EMOTE_ORANGUTAN, 'orangutan', 'ORANGUTAN')
emotes.addEmote(ST.ID_EMOTE_POINT, 'pointf', 'POINT')
emotes.addEmote(ST.ID_EMOTE_POINTBACK, 'pointb', 'POINTBACK')
emotes.addEmote(ST.ID_EMOTE_POINTLEFT, 'pointl', 'POINTLEFT')
emotes.addEmote(ST.ID_EMOTE_POINTRIGHT, 'pointr', 'POINTRIGHT')
emotes.addEmote(ST.ID_EMOTE_POINTUP, 'Pointup', 'POINTUP')
emotes.addEmote(ST.ID_EMOTE_POINTDOWN, 'Pointdown', 'POINTDOWN')
emotes.addEmote(ST.ID_EMOTE_SALUTE, 'salute', 'SALUTE')
emotes.addEmote(ST.ID_EMOTE_SHAKEHEAD, 'shakehead', 'SHAKEHEAD')
emotes.addEmote(ST.ID_EMOTE_NO, 'shakehead', 'NO')
emotes.addEmote(ST.ID_EMOTE_DISAGREE, 'shakehead', 'DISAGREE')
emotes.addEmote(ST.ID_EMOTE_STOMP, 'stompfoot', 'STOMP')
emotes.addEmote(ST.ID_EMOTE_TAPFOOT, 'tapfoot', 'TAPFOOT')
emotes.addEmote(ST.ID_EMOTE_WAVE, 'wave', 'WAVE')
emotes.addEmote(ST.ID_EMOTE_DANGERAREA, 'DangerArea', 'DANGERAREA')
emotes.addEmote(ST.ID_EMOTE_COMEFORWARD, 'Beckon', 'COMEFORWARD')
emotes.addEmote(ST.ID_EMOTE_ENEMYINSIGHT, 'EnemyInSight', 'ENEMY')
emotes.addEmote(ST.ID_EMOTE_ENEMY, 'EnemyInSight', 'ENEMY')
emotes.addEmote(ST.ID_EMOTE_HSDISPERSE, 'HSDisperse', 'HSDISPERSE')
emotes.addEmote(ST.ID_EMOTE_LOOKAROUND, 'LookAround', 'LOOKAROUND')
emotes.addEmote(ST.ID_EMOTE_COVER, 'TakeCover', 'COVER')
emotes.addEmote(ST.ID_EMOTE_TAKECOVER, 'TakeCover', 'COVER')
emotes.addEmote(ST.ID_EMOTE_MAPCHECK, 'MapCheck', 'MAPCHECK')
emotes.addEmote(ST.ID_EMOTE_ONEHANDEDHANDSTAND, 'Onehandedhandstand', 'ONEHANDEDHANDSTAND')
emotes.addEmote(ST.ID_EMOTE_GIGGLE, 'Giggle', 'GIGGLE')
emotes.addEmote(ST.ID_EMOTE_HANDSTAND, 'Handstand', 'HANDSTAND')
emotes.addEmote(ST.ID_EMOTE_HEARNO, 'coverears', 'HEARNO')
emotes.addEmote(ST.ID_EMOTE_SEENO, 'covereyes', 'SEENO')
emotes.addEmote(ST.ID_EMOTE_SPEAKNO, 'covermouth', 'SPEAKNO')
emotes.addEmote(ST.ID_EMOTE_COVEREARS, 'coverears', 'HEARNO')
emotes.addEmote(ST.ID_EMOTE_COVERMOUTH, 'covermouth', 'SPEAKNO')
emotes.addEmote(ST.ID_EMOTE_COVEREYES, 'covereyes', 'SEENO')
emotes.addEmote(ST.ID_EMOTE_BLOWKISS, 'Blowkiss', 'BLOWKISS')
emotes.addEmote(ST.ID_EMOTE_BLUSH, 'Blush', 'BLUSH')
emotes.addEmote(ST.ID_EMOTE_CHEER, 'Cheer', 'CHEER')
emotes.addEmote(ST.ID_EMOTE_CRACKKNUCKLES, 'Crackknuckles', 'CRACKKNUCKLES')
emotes.addEmote(ST.ID_EMOTE_KNUCKLES, 'Crackknuckles', 'CRACKKNUCKLES')
emotes.addEmote(ST.ID_EMOTE_CRY, 'Cry', 'CRY')
emotes.addEmote(ST.ID_EMOTE_CURTSEY, 'Curtsey', 'CURTSEY')
emotes.addEmote(ST.ID_EMOTE_FORMALBOW, 'Formalbow', 'FORMALBOW')
emotes.addEmote(ST.ID_EMOTE_FORMALCURTSEY, 'FormalCurtsey', 'FORMALCURTSEY')
emotes.addEmote(ST.ID_EMOTE_HEADBOW, 'Headbow', 'HEADBOW')
emotes.addEmote(ST.ID_EMOTE_BOWHEAD, 'Headbow', 'HEADBOW')
emotes.addEmote(ST.ID_EMOTE_INSULT, 'Insult', 'INSULT')
emotes.addEmote(ST.ID_EMOTE_SCREAM, 'Khan', 'KHAN')
emotes.addEmote(ST.ID_EMOTE_ANGUISH, 'Khan', 'KHAN')
emotes.addEmote(ST.ID_EMOTE_KARATEPOWER, 'Karatepower1', 'KARATEPOWER')
emotes.addEmote(ST.ID_EMOTE_KARATEPOWER2, 'Karatepower2', 'KARATEPOWER')
emotes.addEmote(ST.ID_EMOTE_KARATEPOWER3, 'Karatepower3', 'KARATEPOWER')
emotes.addEmote(ST.ID_EMOTE_KARATESPEED, 'Karatespeed1', 'KARATESPEED')
emotes.addEmote(ST.ID_EMOTE_KARATESPEED2, 'Karatespeed2', 'KARATESPEED')
emotes.addEmote(ST.ID_EMOTE_KARATESPEED3, 'Karatespeed3', 'KARATESPEED')
emotes.addEmote(ST.ID_EMOTE_KARATEDEFENSE, 'Karatedefense1', 'KARATEDEFENSE')
emotes.addEmote(ST.ID_EMOTE_KARATEDEFENSE2, 'Karatedefense2', 'KARATEDEFENSE')
emotes.addEmote(ST.ID_EMOTE_KARATEDEFENSE3, 'Karatedefense3', 'KARATEDEFENSE')
emotes.addEmote(ST.ID_EMOTE_KNEEL, 'Kneel', 'KNEEL')
emotes.addEmote(ST.ID_EMOTE_TAKEAKNEE, 'Kneel', 'TAKEAKNEE')
emotes.addEmote(ST.ID_EMOTE_KUNGFUDEFENSE, 'Kungfudefense1', 'KUNGFUDEFENSE')
emotes.addEmote(ST.ID_EMOTE_KUNGFUDEFENSE2, 'Kungfudefense2', 'KUNGFUDEFENSE')
emotes.addEmote(ST.ID_EMOTE_KUNGFUDEFENSE3, 'Kungfudefense3', 'KUNGFUDEFENSE')
emotes.addEmote(ST.ID_EMOTE_KUNGFUPOWER, 'Kungfupower1', 'KUNGFUPOWER')
emotes.addEmote(ST.ID_EMOTE_KUNGFUPOWER2, 'Kungfupower2', 'KUNGFUPOWER')
emotes.addEmote(ST.ID_EMOTE_KUNGFUPOWER3, 'Kungfupower3', 'KUNGFUPOWER')
emotes.addEmote(ST.ID_EMOTE_KUNGFUSPEED, 'Kungfuspeed1', 'KUNGFUSPEED')
emotes.addEmote(ST.ID_EMOTE_KUNGFUSPEED2, 'Kungfuspeed2', 'KUNGFUSPEED')
emotes.addEmote(ST.ID_EMOTE_KUNGFUSPEED3, 'Kungfuspeed3', 'KUNGFUSPEED')
emotes.addEmote(ST.ID_EMOTE_AIKIDODEFENSE, 'Aikidodefense1', 'AIKIDODEFENSE')
emotes.addEmote(ST.ID_EMOTE_AIKIDODEFENSE2, 'Aikidodefense2', 'AIKIDODEFENSE')
emotes.addEmote(ST.ID_EMOTE_AIKIDODEFENSE3, 'Aikidodefense3', 'AIKIDODEFENSE')
emotes.addEmote(ST.ID_EMOTE_AIKIDOPOWER, 'Aikidopower1', 'AIKIDOPOWER')
emotes.addEmote(ST.ID_EMOTE_AIKIDOPOWER2, 'Aikidopower2', 'AIKIDOPOWER')
emotes.addEmote(ST.ID_EMOTE_AIKIDOPOWER3, 'Aikidopower3', 'AIKIDOPOWER')
emotes.addEmote(ST.ID_EMOTE_AIKIDOSPEED, 'Aikidospeed1', 'AIKIDOSPEED')
emotes.addEmote(ST.ID_EMOTE_AIKIDOSPEED2, 'Aikidospeed2', 'AIKIDOSPEED')
emotes.addEmote(ST.ID_EMOTE_AIKIDOSPEED3, 'Aikidospeed3', 'AIKIDOSPEED')
emotes.addEmote(ST.ID_EMOTE_LAUGH, 'Laugh', 'LAUGH')
emotes.addEmote(ST.ID_EMOTE_RUDE, 'Insult', 'INSULT')
emotes.addEmote(ST.ID_EMOTE_LOSER, 'Loser', 'LOSER')
emotes.addEmote(ST.ID_EMOTE_BIGTROUBLE, 'BigTrouble', 'BIGTROUBLE')
emotes.addEmote(ST.ID_EMOTE_OKAY, 'Okay', 'OKAY')
emotes.addEmote(ST.ID_EMOTE_OK, 'Okay', 'OKAY')
emotes.addEmote(ST.ID_EMOTE_PEACE, 'Peace', 'PEACE')
emotes.addEmote(ST.ID_EMOTE_PULLHAIR, 'Pullhair', 'PULLHAIR')
emotes.addEmote(ST.ID_EMOTE_ROLLDICE, 'Rolldice', 'ROLLDICE')
emotes.addEmote(ST.ID_EMOTE_SARCASTICCLAP, 'Sarcasticclap', 'SARCASTICCLAP')
emotes.addEmote(ST.ID_EMOTE_GOLFCLAP, 'Sarcasticclap', 'SARCASTICCLAP')
emotes.addEmote(ST.ID_EMOTE_SCRATCHHEAD, 'Scratchhead', 'SCRATCHHEAD')
emotes.addEmote(ST.ID_EMOTE_SHRUG, 'Shrug', 'SHRUG')
emotes.addEmote(ST.ID_EMOTE_SOLID, 'Solid', 'SOLID')
emotes.addEmote(ST.ID_EMOTE_STRETCH, 'Stretch', 'STRETCH')
emotes.addEmote(ST.ID_EMOTE_SUCKITDOWN, 'suckitdown', 'SUCKITDOWN')
emotes.addEmote(ST.ID_EMOTE_SURRENDER, 'Surrender', 'SURRENDER')
emotes.addEmote(ST.ID_EMOTE_THUMBSUP, 'Thumbsup', 'THUMBSUP')
emotes.addEmote(ST.ID_EMOTE_TSKTSK, 'Tsktsk', 'TSKTSK')
emotes.addEmote(ST.ID_EMOTE_SHAMESHAME, 'Tsktsk', 'TSKTSK')
emotes.addEmote(ST.ID_EMOTE_TWOTHUMBSUP, 'Twothumbsup', 'TWOTHUMBSUP')
emotes.addEmote(ST.ID_EMOTE_PUKE, 'Vomit', 'VOMIT')
emotes.addEmote(ST.ID_EMOTE_VOMIT, 'Vomit', 'VOMIT')
emotes.addEmote(ST.ID_EMOTE_WHISTLE, 'Whistle', 'WHISTLE')
emotes.addEmote(ST.ID_EMOTE_GROVEL, 'Grovel', 'GROVEL')
emotes.addEmote(ST.ID_EMOTE_YAWN, 'Yawn', 'YAWN')
emotes.addEmote(ST.ID_EMOTE_PLEAD, 'Grovel', 'PLEAD')
emotes.addEmote(ST.ID_EMOTE_SHAKEFIST, 'Shakefist', 'SHAKEFIST')
emotes.addEmote(ST.ID_EMOTE_COOL, 'Cool', 'COOL')
emotes.addEmote(ST.ID_EMOTE_CRACKNECK, 'Neckcrack', 'CRACKNECK')
emotes.addEmote(ST.ID_EMOTE_HSASSEMBLE, 'HSAssemble', 'HSASSEMBLE')
emotes.addEmote(ST.ID_EMOTE_MOCKCRY, 'Mockcry', 'MOCKCRY')
emotes.addEmote(ST.ID_EMOTE_SLITTHROAT, 'Slitthroat', 'SLITTHROAT')
emotes.addEmote(ST.ID_EMOTE_POWERPOSE, 'Powerpose', 'POWERPOSE')
emotes.addEmote(ST.ID_EMOTE_THUMBSDOWN, 'Thumbsdown', 'THUMBSDOWN')
emotes.addEmote(ST.ID_EMOTE_TWOTHUMBSDOWN, 'Twothumbsdown', 'TWOTHUMBSDOWN')
emotes.addEmote(ST.ID_EMOTE_TAUNT, 'Taunt', 'TAUNT')
emotes.addEmote(ST.ID_EMOTE_MOVE, 'MoveOut', 'MOVEOUT')
emotes.addEmote(ST.ID_EMOTE_MOVEOUT, 'MoveOut', 'MOVEOUT')
emotes.addEmote(ST.ID_EMOTE_RDY, 'IAmReady', 'READY')
emotes.addEmote(ST.ID_EMOTE_READY, 'IAmReady', 'READY')
emotes.addEmote(ST.ID_EMOTE_IAMREADY, 'IAmReady', 'READY')
emotes.addEmote(ST.ID_EMOTE_STOP, 'Freeze', 'STOP')
emotes.addEmote(ST.ID_EMOTE_BIGCHEER, 'Extracheer', 'CHEER')
emotes.addEmote(ST.ID_EMOTE_WHOA, 'Whoa', 'WHOA')
emotes.addEmote(ST.ID_EMOTE_SUP, 'Sup', 'SUP')
emotes.addEmote(ST.ID_EMOTE_TALK1, 'Talk1', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALK2, 'Talk2', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALK3, 'Talk3', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKFORCEFUL, 'TalkForceful', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKANGRY, 'TalkAngry', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKEXCITED, 'TalkExcited', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKSCARED, 'TalkScared', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKCHUCKLE, 'TalkChuckle', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKHURT, 'TalkHurt', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKRELIEVED, 'TalkRelieved', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKNEGATIVE, 'TalkNegative', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKPUZZLED, 'TalkPuzzled', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKWHISPER, 'TalkWhisper', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKGROUP, 'TalkGroup', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKFLIRTATIOUS, 'TalkFlirtatious', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TALKAFFIRMATIVE, 'TalkAffirmative', 'TALK')
emotes.addEmote(ST.ID_EMOTE_TSUJ, 'TheTsuje', '')
emotes.addEmote(ST.ID_EMOTE_THEWAVE, 'TheWave', '')
emotes.addEmote(ST.ID_EMOTE_SNAKE, 'TheSnake', '')
emotes.addEmote(ST.ID_EMOTE_OVERHEAT, 'TheTsuje', '')
emotes.addEmote(ST.ID_EMOTE_EARPIECE, 'TouchEarPiece', '')
emotes.addEmote(ST.ID_EMOTE_TALKWHISPEROBVIOUS, 'TalkWhispO', 'TALK')
emotes.addEmote(ST.ID_EMOTE_BACKFLOP1, 'BackFlip', 'BACKFLOP')
emotes.addEmote(ST.ID_EMOTE_BACKFLOP, 'BackFlip', 'BACKFLOP')
emotes.addEmote(ST.ID_EMOTE_BACKFLOP2, 'BackFlop', 'BACKFLOP')
emotes.addEmote(ST.ID_EMOTE_BALLET, 'Ballet', '')
emotes.addEmote(ST.ID_EMOTE_BANG, 'BangBang', 'BANG')
emotes.addEmote(ST.ID_EMOTE_CUTITOUT, 'CutItOut', 'CUTITOUT')
emotes.addEmote(ST.ID_EMOTE_GIDDIYUP, 'Giddiyup', 'GIDDIYUP')
emotes.addEmote(ST.ID_EMOTE_HORNS, 'Horns', 'HORNS')
emotes.addEmote(ST.ID_EMOTE_MIMEWALL, 'MimeGlassWall', 'MIMEWALL')
emotes.addEmote(ST.ID_EMOTE_MIMEELBOW, 'MimeElbowHang', 'MIMEELBOW')
emotes.addEmote(ST.ID_EMOTE_MIMEROPE, 'PullRope', 'MIMEROPE')
emotes.addEmote(ST.ID_EMOTE_PICKNOSE, 'PickNose', 'PICKNOSE')
emotes.addEmote(ST.ID_EMOTE_DUH, 'SelfDuh', 'DUH')
emotes.addEmote(ST.ID_EMOTE_TIMEOUT, 'TimeOut', 'TIMEOUT')
emotes.addEmote(ST.ID_EMOTE_WHICHWAY, 'WhichWay', 'WHICHWAY')
emotes.addEmote(ST.ID_EMOTE_RUSSDANCE, 'RussDance', '')
emotes.addEmote(ST.ID_EMOTE_LEANWALLLOOP, 'LeanWallLoop', '')
emotes.addEmote(ST.ID_EMOTE_KICKDOOR, 'KickDoor', 'KICKDOOR')
emotes.addEmote(ST.ID_EMOTE_EXAMINE, 'Examine', 'EXAMINE')
emotes.addEmote(ST.ID_EMOTE_DRINKBOTTLE, 'DrinkBottle', 'DRINK')
emotes.addEmote(ST.ID_EMOTE_PICKUP, 'PickUp', 'PICKUP')
emotes.addEmote(ST.ID_EMOTE_TAKEPILL, 'TakePill', 'TAKEPILL')
emotes.addEmote(ST.ID_EMOTE_PLUGNOSE, 'PlugNose', 'PLUGNOSE')
emotes.addEmote(ST.ID_EMOTE_COUGH, 'Cough', 'COUGH')
emotes.addEmote(ST.ID_EMOTE_RIGHTON, 'RightOn', 'RIGHTON')
emotes.addEmote(ST.ID_EMOTE_SLEEP, 'Sleep', 'SLEEP')
emotes.addEmote(ST.ID_EMOTE_TIPHAT, 'TipHat', 'TIPHAT')
emotes.addEmote(ST.ID_EMOTE_CONFUSED, 'Confused', 'CONFUSED')
emotes.addEmote(ST.ID_EMOTE_DEAFENED, 'Deafened', 'DEAFENED')
emotes.addEmote(ST.ID_EMOTE_EYEDROPS, 'Eyedrops', 'EYEDROPS')
emotes.addEmote(ST.ID_EMOTE_PICKUPDESK, 'PickDesk', 'PICKUPDESK')
emotes.addEmote(ST.ID_EMOTE_POINTME, 'PointMe', 'POINTME')
emotes.addEmote(ST.ID_EMOTE_PUTDOWNDESK, 'PutDownDesk', 'PUTDOWNDESK')
emotes.addEmote(ST.ID_EMOTE_RUSH, 'Rush', 'RUSH')
emotes.addEmote(ST.ID_EMOTE_TALKDEPRESSED, 'TalkDepressed', '')
emotes.addEmote(ST.ID_EMOTE_THROWOBJECT, 'ThrowObject', 'THROWOBJECT')
emotes.addEmote(ST.ID_EMOTE_TOSS, 'Toss', 'TOSS')
emotes.addInteractiveEmote(ST.ID_EMOTE_SHAKEHANDS, 'Beckon', 'acceptshake', 'ShakeHead', 'SHAKEHANDS')
emotes.addInteractiveEmote(ST.ID_EMOTE_SLAP, 'Beckon', 'acceptslap', 'ShakeHead', 'SLAP')
emotes.addInteractiveEmote(ST.ID_EMOTE_HOLDHANDS, 'Beckon', 'acceptholdhands', 'ShakeHead', 'HOLDHANDS')
emotes.addInteractiveEmote(ST.ID_EMOTE_DOGSNIFF, 'Beckon', 'acceptdogsniff', 'ShakeHead', 'DOGSNIFF')
emotes.addInteractiveEmote(ST.ID_EMOTE_HUG, 'Beckon', 'Hug', 'ShakeHead', 'HUG')
emotes.addInteractiveEmote(ST.ID_EMOTE_WEDDINGKISS, 'Beckon', 'WeddingKiss', 'ShakeHead', 'WEDDINGKISS')
emotes.addInteractiveEmote(ST.ID_EMOTE_HOLDBOTHHANDS, 'Beckon', 'HoldBothHands', 'ShakeHead', 'HOLDHANDS')
emotes.addInteractiveEmote(ST.ID_EMOTE_KISSTHERING, 'Beckon', 'KissTheRing', 'ShakeHead', 'KISSTHERING')
emotes.addInteractiveEmote(ST.ID_EMOTE_MANHUG, 'Beckon', 'OneArmHug', 'ShakeHead', 'MANHUG')
emotes.addInteractiveEmote(ST.ID_EMOTE_POUND, 'Beckon', 'Pound', 'ShakeHead', 'POUND')
emotes.addInteractiveEmote(ST.ID_EMOTE_WEDDINGRING, 'Beckon', 'WeddingRing', 'ShakeHead', 'WEDDINGRING')
emotes.addInteractiveEmote(ST.ID_EMOTE_PROPOSE, 'Beckon', 'Propose', 'ShakeHead', 'PROPOSE')
emotes.addInteractiveEmote(ST.ID_EMOTE_DAP, 'Beckon', 'Dap', 'ShakeHead', 'DAP')
emotes.addInteractiveEmote(ST.ID_EMOTE_KISS, 'Beckon', 'Kiss', 'ShakeHead', 'KISS')
emotes.addInteractiveEmote(ST.ID_EMOTE_GIVECAKE, 'Beckon', 'GiveCake', 'ShakeHead', 'GIVECAKE')
emotes.addInteractiveEmote(ST.ID_EMOTE_BUMPBUTTS, 'Beckon', 'BumpButts', 'ShakeHead', 'BUMPBUTTS')
emotes.addInteractiveEmote(ST.ID_EMOTE_BANGBANG, 'Beckon', 'BangBang2Person', 'ShakeHead', 'BANGBANG')
moods.addMood(ST.ID_MOOD_OFF, Movement.Demeanor_None, 0)
moods.addMood(ST.ID_MOOD_NORMAL, Movement.Demeanor_None, 0)
moods.addMood(ST.ID_MOOD_DRUNK, Movement.Demeanor_Drunk, 3)
moods.addMood(ST.ID_MOOD_HURT, Movement.Demeanor_Hurt, 4)
moods.addMood(ST.ID_MOOD_SHY, Movement.Demeanor_Shy, 5)
moods.addMood(ST.ID_MOOD_FIDGETY, Movement.Demeanor_Fidgety, 6)
moods.addMood(ST.ID_MOOD_BOUNCY, Movement.Demeanor_Bouncy, 7)
moods.addMood(ST.ID_MOOD_CASUAL, Movement.Demeanor_Casual, 8)
moods.addMood(ST.ID_MOOD_CAUTIOUS, Movement.Demeanor_Cautious, 9)
moods.addMood(ST.ID_MOOD_STOIC, Movement.Demeanor_Stoic, 10)
moods.addMood(ST.ID_MOOD_SHIFTY, Movement.Demeanor_Shifty, 11)
moods.addMood(ST.ID_MOOD_AGGRESSIVE, Movement.Demeanor_Aggressive, 12)
moods.addMood(ST.ID_MOOD_REGAL, Movement.Demeanor_Regal, 13)
moods.addMood(ST.ID_MOOD_SLY, Movement.Demeanor_Sly, 14)
moods.addMood(ST.ID_MOOD_SULTRY, Movement.Demeanor_Sultry, 15)
moods.addMood(ST.ID_MOOD_COLD, Movement.Demeanor_Cold, 16)
moods.addMood(ST.ID_MOOD_COCKY, Movement.Demeanor_Cocky, 17)
moods.addMood(ST.ID_MOOD_UMBRELLA, Movement.Demeanor_Umbrella, 18)
moods.addMood(ST.ID_MOOD_SEXY, Movement.Demeanor_Sexy, 20)
return

# okay decompiling work/monolith_pyc\emotes.pyc
