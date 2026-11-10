# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\combatvalidate.py
# Compiled at: 2006-03-30 16:29:38
import random, traceback, obj, missionvalidate as mv

def DistanceWithin(pos1, pos2, dist):
    """ Returns true if the vectors pos1 and pos2 have a distance of less than dist. """
    x_diff = pos1.x - pos2.x
    y_diff = pos1.y - pos2.y
    z_diff = pos1.z - pos2.z
    return x_diff * x_diff + y_diff * y_diff + z_diff * z_diff < dist * dist
    return


def CombatPhysicsOkay(subject, directObject, rangeValue=700):
    pos1 = subject.Position
    pos2 = directObject.Position
    if not DistanceWithin(pos1, pos2, rangeValue):
        return False
    if not physics.clearLine(pos1, pos2):
        return False
    return True
    return


def CanFight(subject, directObject):
    """
    This rule should become quite complex as it tries to figure
        out if anyone can fight anything.
        
        Nope, all the complexity is buried in CharMvt.isEnemy.
    """
    if directObject.IsDead or subject.IsDead:
        return False
    if not subject.CharMvt.isEnemy(directObject.locator):
        return False
    if not CombatPhysicsOkay(subject, directObject):
        return False
    return True
    return


CanFight.depAttr = '\ndirectObject.IsDead\ndirectObject.Position\ndirectObject.locator\n'
return

# okay decompiling work/monolith_pyc\combatvalidate.pyc
