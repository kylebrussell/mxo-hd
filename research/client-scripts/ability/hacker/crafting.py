# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\hacker\crafting.py
# Compiled at: 2007-08-21 02:24:03
import ability.utility as Utility

def WriteCodeActivate_Subject(subject, msg):
    Utility.outputAbilityDebug(' Write Code subject ')
    subject.CharMvt.playScript(Stance_StandType, Action_Idle, 1)
    return


def WriteCodeActivate_Deactivate(subject):
    Utility.outputAbilityDebug(' Write Code deactivate %d %d' % (subject.Stance, Stance_StandType))
    if subject.Stance == Stance_StandType:
        subject.CharMvt.playScript(Stance_Stand, Action_Idle)
    return


def CompileActivate_Subject(subject, msg):
    Utility.outputAbilityDebug(' Compile subject ')
    subject.CharMvt.playScript(Stance_StandType, Action_Idle, 1)
    return


def CompileActivate_Deactivate(subject):
    Utility.outputAbilityDebug(' Compile deactivate ')
    if subject.Stance == Stance_StandType:
        subject.CharMvt.playScript(Stance_Stand, Action_Idle)
    return


return

# okay decompiling work/monolith_pyc\ability\hacker\crafting.pyc
