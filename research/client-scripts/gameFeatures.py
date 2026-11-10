# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\gameFeatures.py
# Compiled at: 2008-11-22 02:03:12
import traceback, obj, math
RECAPTURE2_FEATURE = 203
RECAPTURE1_FEATURE = 202
HYPER_JUMP_BETA_FEATURE = 201
MOBIUS_CODE_CONVERSION_FEATURE = 200
FREE_TRAIL_ACCOUNT_BIT = 1 << 1

def IsFreeTrail(subscriptionData):
    if subscriptionData & FREE_TRAIL_ACCOUNT_BIT:
        return 1
    return 0
    return


def EmailAbility(character, abilityID):
    character.Inventory.addAbilityCode(abilityID)
    return


def EmailItem(character, itemID):
    character.Inventory.addItem(itemID, 0)
    return


def HandleAction(character, gameFeature):
    if gameFeature.featureID == HYPER_JUMP_BETA_FEATURE:
        return EmailAbility(character, PreOrderHyperLeapAbility)
    elif gameFeature.featureID == MOBIUS_CODE_CONVERSION_FEATURE:
        return EmailAbility(character, MobiusCodeAbility)
    elif gameFeature.featureID == RECAPTURE1_FEATURE:
        if character.Inventory.isMale():
            return EmailItem(character, 48122)
        else:
            return EmailItem(character, 48120)
    elif gameFeature.featureID == RECAPTURE2_FEATURE:
        if character.Inventory.isMale():
            return EmailItem(character, 48123)
        else:
            return EmailItem(character, 48121)
    return


def HandleConditional(character, gameFeature, subscriptionData):
    isFreeTrailPlayer = IsFreeTrail(subscriptionData)
    if gameFeature.featureID == HYPER_JUMP_BETA_FEATURE:
        if gameFeature.isActive and gameFeature.consumeCount > 0:
            return True
    elif gameFeature.featureID == MOBIUS_CODE_CONVERSION_FEATURE:
        if not isFreeTrailPlayer and gameFeature.isActive and gameFeature.consumeCount > 0:
            return True
    elif gameFeature.featureID == RECAPTURE1_FEATURE:
        if not isFreeTrailPlayer and gameFeature.isActive and gameFeature.consumeCount > 0:
            return True
    elif gameFeature.featureID == RECAPTURE2_FEATURE:
        if not isFreeTrailPlayer and gameFeature.isActive and gameFeature.consumeCount > 0:
            return True
    return False
    return


def HandleOnGameFeatures(character, gameFeatureArray, subscriptionData):
    usedGameFeatures_list = []
    isFreeTrailPlayer = IsFreeTrail(subscriptionData)
    for curGameFeature in gameFeatureArray:
        bCanBeUsed = HandleConditional(character, curGameFeature, subscriptionData)
        if bCanBeUsed == True:
            HandleAction(character, curGameFeature)
            usedGameFeatures_list.append(curGameFeature)

    return tuple(usedGameFeatures_list)
    return


return

# okay decompiling work/monolith_pyc\gameFeatures.pyc
