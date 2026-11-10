# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\soldier\misc.py
# Compiled at: 2006-03-30 16:29:38
import random, traceback, obj, missionvalidate as mv, combatvalidate as cv, stringtable_client as StringTable, ability.utility as Utility, ltfxmap as FX
from ability.defines import *
SYNAPTIC_SWEEP_KICK_DAMAGE_MIN = 12
SYNAPTIC_SWEEP_KICK_DAMAGE_MAX = 48
SYNAPTIC_SWEEP_KICK_FX = FX.FX_CHARACTER_SYNAPTIC_SWEEP_KICK

def SynapticSweepKick_DirectObject(subject, msg):
    Utility.outputAbilityDebug('SynapticSweepKick_DirectObject:')
    if not subject.CharMvt.isEnemy(msg.subjectLocator):
        return
    damage = random.randrange(SYNAPTIC_SWEEP_KICK_DAMAGE_MIN, SYNAPTIC_SWEEP_KICK_DAMAGE_MAX)
    Utility.outputAbilityDebug('SynapticSweepKick_DirectObject: %d damage dealt' % damage)
    damage = subject.Interlock.physicalDamageFromAbility(msg.subjectLocator, damage)
    Utility.SendTakeDamageResultToAll(SUCCESS, SynapticSweepKickAbility, msg.subjectLocator, msg.directObjectLocator, damage)
    discovery.playEffect(subject.locator, subject.locator, SYNAPTIC_SWEEP_KICK_FX, 0)
    return


def SynapticSweepKick_Subject(subject, msg):
    Utility.outputAbilityDebug('SynapticSweepKick_Subject  : ')
    loc = subject.CharMvt.Position
    subject.AbilityInv.applyAbilityToArea(SynapticSweepKickAbility, SUCCESS, loc.x, loc.y, loc.z, 400)
    if not subject.isInCombat:
        subject.CharMvt.playScript(Stance_Stand, Action_SweepKick, SynapticSweepKickAbility)
    return


return

# okay decompiling work/monolith_pyc\ability\soldier\misc.pyc
