# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\npccommands.py
# Compiled at: 2007-11-13 02:02:54
import traceback, movement as Movement

def AttackNearestPlayer():
    nearest_player = physics.findNearestPlayer(consoletarget.Position, consoletarget.locator)
    if nearest_player:
        consoletarget.AI.attack(nearest_player)
    return


def AttackNearestAI():
    nearest_ai = physics.findNearestAI(consoletarget.Position, consoletarget.locator)
    if nearest_ai:
        consoletarget.AI.attack(nearest_ai)
    return


def AttackMe():
    consoletarget.AI.attack(consoleplayer.locator)
    return


def FollowMe():
    consoletarget.AI.followTarget(consoleplayer.locator)
    return


def SetMood(mood):
    consoletarget.CharMvt.setDemeanor(mood)
    return


def NPCType(config=None):
    consoletarget.AI.NPCType(config)
    return


def Behaviour(config=None):
    consoletarget.AI.behavior(config)
    return


def AddUnkeyedItem(item):
    consoletarget.Container.addStableItem(item)
    return


def AddRewardItem(encodedReward):
    rewardselection = RewardSelection()
    rewards = rewardselection.Decode_Reward(encodedReward)
    rewardselection.Apply_Rewards(rewards, consoletarget.Container)
    if consoletarget.MissionElement is not None:
        consoletarget.MissionElement.setMissionKey(AddKeyOffset(missionkey, 255))
    return


def AddRewardGroup(rewardtable, teamlevel=None):
    if teamlevel is not None:
        consoletarget.TeamLevel = teamlevel
    rewardselection = RewardSelection()
    rewardselection.DeterminePopulateReward('TeamLevelRewardRule', rewardtable, consoletarget.Container)
    if consoletarget.MissionElement is not None:
        consoletarget.MissionElement.setMissionKey(AddKeyOffset(missionkey, 255))
    return


def SetName(npcname):
    consoletarget.AI.NamedCharacterChat = 1
    consoletarget.CharacterBase.setName(npcname)
    return


def NoLoot():
    consoletarget.Rewards.RewardTable = None
    consoletarget.Rewards.RewardRule = None
    return


def Immortal():
    consoletarget.AI.immortal()
    return


def Healthy():
    consoletarget.AI.healthy()
    return


def Wounded():
    consoletarget.AI.wounded()
    return


def NearDead():
    consoletarget.AI.neardead()
    return


def Dead():
    consoletarget.AI.dead()
    return


def SetPermanentExclusion(playerid):
    consoletarget.Container.setPermanentExclusion(playerid)
    return


def ExecuteAbilityOnNearestAI(abilityid):
    nearest_ai = physics.findNearestAI(consoletarget.Position, consoletarget.locator)
    if nearest_ai:
        consoletarget.AI.executeAbility(abilityid, nearest_ai)
    return


def ExecuteAbilityOnSelf(abilityid):
    consoletarget.AI.executeAbility(abilityid, consoletarget.locator)
    return


def ExecuteAbilityOnMe(abilityid):
    consoletarget.AI.executeAbility(abilityid, consoleplayer.locator)
    return


return

# okay decompiling work/monolith_pyc\npccommands.pyc
