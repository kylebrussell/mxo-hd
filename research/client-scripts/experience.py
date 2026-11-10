# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\experience.py
# Compiled at: 2006-03-30 16:29:38
experienceBaseMap = {}
groupXPMap = {}

def Init():
    experienceBaseMap[constants.AI.WEAKLING] = consolevar.WeaklingKillMultiplier
    experienceBaseMap[constants.AI.TOUGH] = consolevar.ToughKillMultiplier
    experienceBaseMap[constants.AI.BOSS] = consolevar.BossKillMultiplier
    groupXPMap[1] = 1.0
    groupXPMap[2] = 1.2
    groupXPMap[3] = 1.5
    groupXPMap[4] = 1.6
    groupXPMap[5] = 1.75
    groupXPMap[6] = 2.1
    print 'Experience system Inited'
    return


def ComputeExperience(pParent, playerLevel, playerDamage, totalDamage):
    if pParent.AI is None:
        return None
    if pParent.OwnerCharacterID:
        return float(0)
    NPCRank = constants.AI.WEAKLING
    if pParent.AI.NPCRank is not None and experienceBaseMap.has_key(pParent.AI.NPCRank):
        NPCRank = pParent.AI.NPCRank
    percent = playerDamage / totalDamage
    numerator = 1 + (pParent.CharacterBase.Level - playerLevel)
    exponent = numerator / 7
    rankConstant = experienceBaseMap[NPCRank]
    experience = rankConstant * pow(3, exponent) * percent
    if playerLevel != 1:
        playerLevel = playerLevel - 1
    experience = experience + consolevar.XPKnob * (playerLevel * experience * consolevar.XPConstantMultiplier - experience)
    return float(experience)
    return


def ComputeGroupBonus(currentExperience, numberPlayers):
    experience = currentExperience
    if groupXPMap.has_key(numberPlayers):
        experience = currentExperience * groupXPMap[numberPlayers]
    return float(experience)
    return


def ComputeExperienceDecompile(playerLevel, itemComplexity):
    numerator = 1 + (itemComplexity - playerLevel)
    exponent = numerator / 10
    constant = experienceBaseMap[constants.AI.WEAKLING]
    experience = constant * pow(3, exponent)
    if playerLevel != 1:
        playerLevel = playerLevel - 1
    experience = experience + consolevar.XPKnob * (playerLevel * experience * consolevar.XPConstantMultiplier - experience)
    return float(experience)
    return


def ComputeExperienceWriteCode(playerLevel, itemComplexity):
    numerator = 1 + (itemComplexity - playerLevel)
    exponent = numerator / 10
    constant = experienceBaseMap[constants.AI.WEAKLING]
    experience = constant * pow(3, exponent)
    if playerLevel != 1:
        playerLevel = playerLevel - 1
    experience = experience + consolevar.XPKnob * (playerLevel * experience * consolevar.XPConstantMultiplier - experience)
    return float(experience)
    return


def ComputeGenericExperience(playerLevel, taskLevel, rank=constants.AI.WEAKLING):
    numerator = 1 + (taskLevel - playerLevel)
    exponent = numerator / 10
    constant = experienceBaseMap[rank]
    experience = constant * pow(3, exponent)
    if playerLevel != 1:
        playerLevel = playerLevel - 1
    experience = experience + consolevar.XPKnob * (playerLevel * experience * consolevar.XPConstantMultiplier - experience)
    return float(experience)
    return


return

# okay decompiling work/monolith_pyc\experience.pyc
