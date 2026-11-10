# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\followvalidate.py
# Compiled at: 2006-03-30 16:29:39
import random, traceback, obj, missionvalidate as mv

def CanFollow(subject, directObject):
    if mv.ValidMissionKeyBaseMatch(subject.MissionTeam.MissionKey, directObject.MissionKey):
        return True
    return False
    return


CanFollow.depAttr = '\ndirectObject.MissionKey\n'

def CanStopFollow(subject, directObject):
    if subject.PlayerCharacter.CharacterID == directObject.MasterCharacterID:
        return True
    return False
    return


CanStopFollow.depAttr = '\ndirectObject.MasterCharacterID\n'
return

# okay decompiling work/monolith_pyc\followvalidate.pyc
