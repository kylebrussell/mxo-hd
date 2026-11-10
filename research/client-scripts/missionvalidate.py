# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\missionvalidate.py
# Compiled at: 2006-03-30 16:29:39
import random, traceback
EMPTY_MISSION_KEY = 0L
NON_ASSIGNABLE_MISSIONKEY = 4294967295L

def MissionKeyBase(key):
    return (key & -256) >> 8
    return


def MissionKeyPart(key):
    return key & 255
    return


def ValidMissionKeyMatch(m1, m2):
    if not m1:
        return True
    if not m2:
        return False
    if MissionKeyPart(m1) != 0:
        return m1 == m2
    return MissionKeyBase(m1) == MissionKeyBase(m2)
    return


def ValidMissionKeyBaseMatch(m1, m2):
    if not m1:
        return True
    if not m2:
        return False
    return MissionKeyBase(m1) == MissionKeyBase(m2)
    return


def IsMissionTeamMember(missionKey1, missionKey2):
    if not missionKey1 or not missionKey2:
        return False
    if missionKey1 == missionKey2:
        return True
    return False
    return


def IsPvPOpponent(missionKey1, missionKey2):
    if not missionKey1 or not missionKey2:
        return False
    if MissionKeyBase(missionKey1) == MissionKeyBase(missionKey2) and MissionKeyPart(missionKey1) != MissionKeyPart(missionKey2):
        return True
    return False
    return


def IsNonReservedMatchingKey(missionKey1, missionKey2):
    if not missionKey1:
        return False
    if not missionKey2:
        return False
    if not ValidMissionKeyBaseMatch(missionKey1, missionKey2):
        return False
    if MissionKeyPart(missionKey1) == 255:
        return False
    if MissionKeyPart(missionKey2) == 255:
        return False
    return True
    return


return

# okay decompiling work/monolith_pyc\missionvalidate.pyc
