# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\crafting_server.py
# Compiled at: 2006-10-03 01:17:47
import random, traceback, ability.utility, obj, stringtable_client as ST
from RewardSelection import RewardSelection
import ltfxmap as FX
COMPILE_DISCOUNT = 0.12
CODER_DURATION = 60.0

def craftingFeedback(subject, str):
    if consolevar.CraftingDebugPrint > 0:
        subject.AbilityInv.sendChat('', str)
    return


def isPill(category):
    cat = toFourCC(category)
    if cat[0] == 'I' and cat[1] == 'C':
        return 1
    return 0
    return


def gD100():
    return int(discovery.getGaussianRandomClamped(50, gD100.stdDev, 0, 100))
    return


gD100.stdDev = 16

def toFourCC(val):
    t = [
     0, 0, 0, 0]
    t[0] = chr(val & 255)
    t[1] = chr((val & 65280) >> 8)
    t[2] = chr((val & 16711680) >> 16)
    t[3] = chr((val & long(-16777216)) >> 24)
    return t
    return


def DecompileInnerStrengthCost(object):
    return 0
    return


def DecompStabPurContrib(object):
    stability = object.stability
    purity = object.purity
    pv = [
     -10, 0, 10, 20]
    pv = pv[purity]
    if pv < 0:
        stability = 7.0 - stability
    contrib = pv + stability / 7.0 * pv
    if isPill(object.GOCategoryID):
        contrib = 0
    return contrib
    return


def DecompileTest(subject, object):
    _str = 'Decompile '
    roll = gD100()
    abil_level = subject.abilities[DecompileSkillAbility]
    complexity = 0
    signalboost = subject.AbilityInv.getSignalBoostBonus(abil_level * 100.0)
    _str = _str + discovery.getGameObjectName(object.type)
    if object.Complexity is not None:
        complexity = object.Complexity
    else:
        _str = _str + 'object has no complexity'
    if complexity == 0:
        craftingFeedback(subject, _str)
        return 0
    contrib = DecompStabPurContrib(object)
    contrib = abil_level * contrib / 100.0
    abil_level = abil_level + signalboost[0]
    _str = _str + 'res : a%d + c%d + r%d > %d' % (4 * abil_level, 4 * contrib, roll, 4 * complexity + 50)
    craftingFeedback(subject, _str)
    if abil_level > 0:
        if 4 * (abil_level + contrib) + roll - (4 * complexity + 50) > 0:
            return 1
    return 0
    return


def DecompileFragmentRecoveryTest(subject, fragment, stability):
    _str = 'Frag Recovery : '
    roll = gD100()
    abil_level = subject.abilities[DecompileSkillAbility]
    complexity = 0
    signalboost = subject.AbilityInv.getSignalBoostBonus(float(abil_level * 100))
    abil_level = abil_level + signalboost[0]
    _str = _str + 'Decomp Recovery : %s C(%d) ' % (discovery.getGameObjectName(fragment.type), complexity)
    if fragment.Complexity is not None:
        complexity = fragment.Complexity
    else:
        _str = _str + 'Object has no Complexity'
        craftingFeedback(subject, _str)
        return 0
    _str = _str + ' abil %d + %d > %d' % (4 * abil_level, roll, 4 * complexity + 50)
    craftingFeedback(subject, _str)
    if abil_level > 0 and complexity > 0:
        if 4 * abil_level + roll - (4 * complexity + 50) > 0:
            return 1
    return 0
    return


def CompileInfoCost(object):
    if object.VendorPrice is not None:
        return object.VendorPrice * COMPILE_DISCOUNT
    return 1000 * COMPILE_DISCOUNT
    return


def GetAbilityBonusByCategory(subject, GOCategoryID):
    abil_level = 0
    if GOCategoryID:
        cat = toFourCC(GOCategoryID)
        if cat[0] == 'I' and cat[1] == 'W':
            abil_level = subject.abilities[WeaponCraftingAbility]
        if cat[0] == 'A':
            abil_level = subject.abilities[AbilityCraftingAbility]
        if cat[0] == 'I' and cat[1] == 'A':
            abil_level = subject.abilities[ApparelCraftingAbility]
        if cat[0] == 'I' and cat[1] == 'T':
            abil_level = subject.abilities[ToolCraftingAbility]
        if cat[0] == 'I' and cat[1] == 'C':
            abil_level = subject.abilities[UpgradeCraftingAbility]
    return abil_level
    return


def CalcItemInfoCost(subject, object):
    signalboost = subject.AbilityInv.getSignalBoostBonus(5000.0)
    info_cost = 0
    discount = 0.0
    if object.VendorPrice is not None:
        info_cost = object.VendorPrice * constants.Crafting.CompileCompileCostMultiplier
    else:
        info_cost = 1000 * constants.Crafting.CompileCompileCostMultiplier
    if subject.hasAbility[CraftingCostDiscountAbility]:
        discount = subject.abilities[CraftingCostDiscountAbility] / 100.0
    discount += signalboost[1]
    if discount > 1.0:
        discount = 1.0
    else:
        discount = 1.0 - discount
    info_cost = int(info_cost * discount)
    return info_cost
    return


def CompileTest(subject, object):
    abil_level = subject.abilities[CompileSkillAbility]
    signalboost = subject.AbilityInv.getSignalBoostBonus(5000.0)
    info_cost = CalcItemInfoCost(subject, object)
    discount = 0.0
    _str = discovery.getGameObjectName(object.type)
    if info_cost > subject.PlayerCharacter.Information:
        subject.Inventory.craftUIFeedback(ST.ID_COMPILE_NOT_ENOUGH_INFO_TO_COMPILE_ITEM)
        craftingFeedback(subject, _str + ' Not enough Info to Compile .. ')
        return 0
    roll = gD100()
    complexity = 0
    if object.Complexity is not None:
        complexity = object.Complexity
    else:
        _str = _str + ' No Complexity failed to Compile'
        craftingFeedback(subject, _str)
        return 0
    specializationBonus = GetAbilityBonusByCategory(subject, object.GOCategoryID)
    if specializationBonus > 0:
        specializationBonus = int(specializationBonus * 0.1)
        craftingFeedback(subject, 'Specialization bonus : %d ' % specializationBonus)
    abil_level = abil_level + specializationBonus
    subject.PlayerCharacter.addInformation(-info_cost, constants.RST.COMPILE)
    craftingFeedback(subject, 'Compile : roll : %d+%d (%d) v.s %d+50 (%d)' % (4 * abil_level, roll, 4 * abil_level + roll, 4 * complexity, 4 * complexity + 50))
    if abil_level > 0:
        if 4 * abil_level + roll - (4 * complexity + 50) > 0:
            return 1
    subject.Inventory.craftUIFeedback(ST.ID_COMPILE_FAILED_TO_COMPILE_ITEM)
    return 0
    return


def WriteCodeTest(subject, invItem, GOCatID):
    _str = 'Write Code : '
    abil_level = subject.abilities[CodeWritingAbility]
    boost = subject.AbilityInv.getSignalBoostBonus(abil_level * 100.0)
    info_cost = 0.01 * (100 - subject.abilities[CodeWritingDiscountAbility]) * CalcItemInfoCost(subject, invItem)
    if info_cost > subject.PlayerCharacter.Information:
        subject.Inventory.craftUIFeedback(ST.ID_COMPILE_NOT_ENOUGH_INFO_TO_WRITECODE_ITEM)
        craftingFeedback(subject, _str + ' Not enough Info to WriteCode .. ')
        return 0
    roll = gD100()
    _str += ' roll : %d ' % roll
    roll = roll + boost[0]
    _str += ' boost %d ' % boost[0]
    complexity = invItem.Complexity
    _str += ' object diff %d ' % int(complexity)
    cat_boost = GetAbilityBonusByCategory(subject, GOCatID)
    _str += ' cat_boost : %d ' % cat_boost
    _str += ' abil lvl  : %d ' % abil_level
    _str += ' objcat : %s ' % toFourCC(GOCatID)
    if cat_boost > abil_level:
        abil_level = cat_boost + boost[0]
    else:
        abil_level = abil_level + boost[0]
    craftingFeedback(subject, _str)
    craftingFeedback(subject, 'WC contest: %d v.s %d' % (4 * abil_level + roll, 4 * complexity + 50))
    if abil_level > 0:
        if 4 * abil_level + roll - (4 * complexity + 50) > 0:
            subject.PlayerCharacter.addInformation(-info_cost, constants.RST.WRITECODE)
            return 1
    return 0
    return


def CoderApplication(subject, invItem, buffValue):
    if buffValue > 0:
        subject.AbilityInv.addTempMod(CoderAbility, CodeWritingAbility, buffValue, CODER_DURATION)
        subject.AbilityInv.addTempMod(CoderAbility, DecompileSkillAbility, buffValue, CODER_DURATION)
        subject.AbilityInv.addTempMod(CoderAbility, AbilityCraftingAbility, buffValue, CODER_DURATION)
        subject.AbilityInv.addTempMod(CoderAbility, ApparelCraftingAbility, buffValue, CODER_DURATION)
        subject.AbilityInv.addTempMod(CoderAbility, ToolCraftingAbility, buffValue, CODER_DURATION)
        subject.AbilityInv.addTempMod(CoderAbility, UpgradeCraftingAbility, buffValue, CODER_DURATION)
        subject.AbilityInv.addTempMod(CoderAbility, WeaponCraftingAbility, buffValue, CODER_DURATION)
        ability.utility.SendAbilityOutputToCaster(ST.ID_CLIENT_ABILITY_CODER_SUBJECT, CoderAbility, subject.locator, subject.locator, buffValue)
    return 1
    return


return

# okay decompiling work/monolith_pyc\crafting_server.pyc
