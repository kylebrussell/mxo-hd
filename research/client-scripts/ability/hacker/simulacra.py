# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\hacker\simulacra.py
# Compiled at: 2006-11-13 20:16:48
import random, traceback, obj, missionvalidate as mv, combatvalidate as cv, stringtable_client as StringTable, ability.utility as Utility
from ability.defines import *
import ltfxmap as FX
from time import ctime
print 'simulacra reloaded ', ctime()
ACOUSTICDECOYS1_DURATION = 60
ACOUSTICDECOYS2_DURATION = 120
ACOUSTICDECOYS3_DURATION = 60
ACOUSTICDECOYS1_RADIUS = 1000
ACOUSTICDECOYS2_RADIUS = 1000
ACOUSTICDECOYS3_RADIUS = 1000

def AcousticDecoy1_GetRange(level, selectionRangeDebuff):
    r = 5000.0 * selectionRangeDebuff
    return (r, r)
    return


def VecToStr(vec):
    val = '%f %f %f' % (vec.x, vec.y, vec.z)
    return val
    return


def AcousticDecoy2_Test(sentence):
    Utility.outputAbilityDebug('AcousticDecoy2_Test')
    if sentence.directObject.OwnerCharacterID is None or sentence.subject.CharacterID != sentence.directObject.OwnerCharacterID:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


AcousticDecoy2_Test.depAttr = '\ndirectObject.OwnerCharacterID\n'

def Decoy1_Test(sentence):
    Utility.outputAbilityDebug('Decoy1_Test')
    if sentence.directObject.OwnerCharacterID is None or sentence.subject.CharacterID != sentence.directObject.OwnerCharacterID:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


Decoy1_Test.depAttr = '\ndirectObject.OwnerCharacterID\n'

def StopProxy1_Test(sentence):
    Utility.outputAbilityDebug('StopProxy1_Test')
    if sentence.directObject.OwnerCharacterID is None or sentence.subject.CharacterID != sentence.directObject.OwnerCharacterID:
        sentence.result = FAILURE
    else:
        sentence.result = SUCCESS
    return


StopProxy1_Test.depAttr = '\ndirectObject.OwnerCharacterID\n'

def sim_clamp(min, max, lvl):
    if lvl < min:
        return min
    elif lvl > max:
        return max
    return lvl
    return


def LogicDaemon1_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 4
    maxLevel = 7
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    subject.AbilityInv.createSimulacrum(('Simulacra.Nuker', lvl, gender), 'SimulacraInit()', LogicDaemon1Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, LogicDaemon1Ability, msg)
    return


def LogicDaemon1_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def LogicDaemon2_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 8
    maxLevel = 17
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    subject.AbilityInv.createSimulacrum(('Simulacra.Nuker', lvl, gender), 'SimulacraInit()', LogicDaemon2Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, LogicDaemon2Ability, msg)
    return


def LogicDaemon2_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def LogicDaemon3_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 18
    maxLevel = 34
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    subject.AbilityInv.createSimulacrum(('Simulacra.Nuker', lvl, gender), 'SimulacraInit()', LogicDaemon3Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, LogicDaemon3Ability, msg)
    return


def LogicDaemon3_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def LogicDaemon4_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 35
    maxLevel = 50
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    subject.AbilityInv.createSimulacrum(('Simulacra.Nuker', lvl, gender), 'SimulacraInit()', LogicDaemon4Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, LogicDaemon4Ability, msg)
    return


def LogicDaemon4_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def PatchDaemon1_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 12
    maxLevel = 22
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    subject.AbilityInv.createSimulacrum(('Simulacra.Healer', lvl, gender), 'SimulacraInit()', PatchDaemon1Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, PatchDaemon1Ability, msg)
    return


def PatchDaemon1_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def PatchDaemon2_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 23
    maxLevel = 43
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    subject.AbilityInv.createSimulacrum(('Simulacra.Healer', lvl, gender), 'SimulacraInit()', PatchDaemon2Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, PatchDaemon2Ability, msg)
    return


def PatchDaemon2_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def PatchDaemon3_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 44
    maxLevel = 50
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    subject.AbilityInv.createSimulacrum(('Simulacra.Healer', lvl, gender), 'SimulacraInit()', PatchDaemon3Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, PatchDaemon3Ability, msg)
    return


def PatchDaemon3_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


simulacra_choices = (
 'Simulacra.Aikido', 'Simulacra.Karate', 'Simulacra.KungFu', 'Simulacra.Pistol', 'Simulacra.Rifle', 'Simulacra.SMG')

def RemoteProxy1_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 1
    maxLevel = 4
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    if len(consolevar.RemoteProxySheet) > 2:
        subject.AbilityInv.createSimulacrum((consolevar.RemoteProxySheet, lvl, gender), 'SimulacraInit()', RemoteProxy1Ability)
    else:
        subject.AbilityInv.createSimulacrum((random.choice(simulacra_choices), lvl, gender), 'SimulacraInit()', RemoteProxy1Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, RemoteProxy1Ability, msg)
    return


def RemoteProxy1_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def RemoteProxy2_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 4
    maxLevel = 7
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    if len(consolevar.RemoteProxySheet) > 2:
        subject.AbilityInv.createSimulacrum((consolevar.RemoteProxySheet, lvl, gender), 'SimulacraInit()', RemoteProxy2Ability)
    else:
        subject.AbilityInv.createSimulacrum((random.choice(simulacra_choices), lvl, gender), 'SimulacraInit()', RemoteProxy2Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, RemoteProxy2Ability, msg)
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def RemoteProxy2_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def RemoteProxy3_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 8
    maxLevel = 17
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    if len(consolevar.RemoteProxySheet) > 2:
        subject.AbilityInv.createSimulacrum((consolevar.RemoteProxySheet, lvl, gender), 'SimulacraInit()', RemoteProxy3Ability)
    else:
        subject.AbilityInv.createSimulacrum((random.choice(simulacra_choices), lvl, gender), 'SimulacraInit()', RemoteProxy3Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, RemoteProxy3Ability, msg)
    return


def RemoteProxy3_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def RemoteProxy4_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 18
    maxLevel = 34
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    if len(consolevar.RemoteProxySheet) > 2:
        subject.AbilityInv.createSimulacrum((consolevar.RemoteProxySheet, lvl, gender), 'SimulacraInit()', RemoteProxy4Ability)
    else:
        subject.AbilityInv.createSimulacrum((random.choice(simulacra_choices), lvl, gender), 'SimulacraInit()', RemoteProxy4Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, RemoteProxy4Ability, msg)
    return


def RemoteProxy4_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def RemoteProxy5_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    minLevel = 35
    maxLevel = 50
    lvl = sim_clamp(minLevel, maxLevel, subject.Level)
    if len(consolevar.RemoteProxySheet) > 2:
        subject.AbilityInv.createSimulacrum((consolevar.RemoteProxySheet, lvl, gender), 'SimulacraInit()', RemoteProxy5Ability)
    else:
        subject.AbilityInv.createSimulacrum((random.choice(simulacra_choices), lvl, gender), 'SimulacraInit()', RemoteProxy5Ability)
    Utility.SendAbilityOutputToCasterMsg(StringTable.ID_CLIENT_ABILITY_CREATE_SIMULACRA_CASTER, RemoteProxy5Ability, msg)
    return


def RemoteProxy5_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def KungFuSimulacra1_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.KungFu', Level_1_Sim, gender), 'SimulacraInit()', KungFuSimulacra1Ability)
    return


def KungFuSimulacra1_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def KungFuSimulacra2_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.KungFu', Level_2_Sim, gender), 'SimulacraInit()', KungFuSimulacra2Ability)
    return


def KungFuSimulacra2_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def KungFuSimulacra3_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.KungFu', Level_3_Sim, gender), 'SimulacraInit()', KungFuSimulacra3Ability)
    return


def KungFuSimulacra3_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def PistolSimulacra1_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.Pistol', Level_1_Sim, gender), 'SimulacraInit()', PistolSimulacra1Ability)
    return


def PistolSimulacra1_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def PistolSimulacra2_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.Pistol', Level_2_Sim, gender), 'SimulacraInit()', PistolSimulacra2Ability)
    return


def PistolSimulacra2_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def PistolSimulacra3_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.Pistol', Level_3_Sim, gender), 'SimulacraInit()', PistolSimulacra3Ability)
    return


def PistolSimulacra3_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def RifleSimulacra1_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.Rifle', Level_1_Sim, gender), 'SimulacraInit()', RifleSimulacra1Ability)
    return


def RifleSimulacra1_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def RifleSimulacra2_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.Rifle', Level_2_Sim, gender), 'SimulacraInit()', RifleSimulacra2Ability)
    return


def RifleSimulacra2_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def RifleSimulacra3_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.Rifle', Level_3_Sim, gender), 'SimulacraInit()', RifleSimulacra3Ability)
    return


def RifleSimulacra3_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def SMGSimulacra1_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.SMG', Level_1_Sim, gender), 'SimulacraInit()', SMGSimulacra1Ability)
    return


def SMGSimulacra1_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def SMGSimulacra2_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.SMG', Level_2_Sim, gender), 'SimulacraInit()', SMGSimulacra2Ability)
    return


def SMGSimulacra2_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def SMGSimulacra3_Subject(subject, msg):
    gender = random.choice(('f', 'm'))
    subject.AbilityInv.createSimulacrum(('Simulacra.SMG', Level_3_Sim, gender), 'SimulacraInit()', SMGSimulacra3Ability)
    return


def SMGSimulacra3_Deactivate(subject):
    subject.AbilityInv.destroyCurrentSimulacrum()
    return


def MessageCloud_GetRange(level, selectionRangeDebuff):
    return (
     3000.0 * selectionRangeDebuff, 3000.0 * selectionRangeDebuff)
    return


def MessageCloud_DirectObject(subject, msg):
    Utility.outputAbilityDebug('MSG CLOUD DIRECT OBJECT ')
    percentage_decrease_in_range = 80
    duration = 4.0
    mem_cost = 0
    mc_procs = ('MessageCloudInit', 'MessageCloudTerm', '', '', '')
    subject.AbilityInv.addTempModProcs(mc_procs, MessageCloudAbility, SelectionRangeAbility, duration, mem_cost, 0, percentage_decrease_in_range)
    return


def MessageCloud_Subject(subject, msg):
    Utility.outputAbilityDebug('MSG CLOUD')
    objects = discovery.getAllObjects()
    msgcldid = objects['MessageCloud']
    print 'msg cld id : ', msgcldid
    duration = 20.0
    msgcld = subject.AbilityInv.createStaticObject(msgcldid, {'Position': (msg.location), 'Orientation': (subject.CharMvt.Orientation), 'Duration': duration})
    procs = (
     '', '', 'MessageCloudPulse', '', '')
    msgcld.Abilities.addTempModProcs(procs, duration, 0, 0)
    procs = ('MessageCloudInit', 'MessageCloudTerm', '', '', '')
    msgcld.Abilities.addTempModProcs(procs, duration, 0, 0)
    return


return

# okay decompiling work/monolith_pyc\ability\hacker\simulacra.pyc
