# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\combat_calculations.py
# Compiled at: 2006-05-17 12:54:51
import random, traceback, obj, combat_defines as CD, math, interlock.combat_utility as CU

def calcDamageWithGun(itemBaseDamage, itemDeltaDamage, tactical_setting):
    CU.outputCombatDebugMessage('range calc: %d %d' % (0, itemDeltaDamage), 2)
    calcDeltaDamage = 0
    if itemDeltaDamage > 0:
        calcDeltaDamage = random.randrange(0, itemDeltaDamage * 2) - itemDeltaDamage
    tacticalBonus = 0
    return itemBaseDamage + calcDeltaDamage + tacticalBonus
    return


def GetTacticCombatTacticsMod(tacticalSetting):
    tacticBonus = 1.0
    if tacticalSetting == constants.combat.BLOCK:
        tacticBonus = 0.0
    elif tacticalSetting == constants.combat.SPEED:
        tacticBonus = 1.15
    elif tacticalSetting == constants.combat.POWER:
        tacticBonus = 0.9
    elif tacticalSetting == constants.combat.DEFENSE:
        tacticBonus = 1.05
    return tacticBonus
    return


def GetTacticDefenseTacticsMod(tacticalSetting):
    tacticBonus = 1.0
    if tacticalSetting == constants.combat.BLOCK:
        tacticBonus = 1.5
    elif tacticalSetting == constants.combat.SPEED:
        tacticBonus = 0.9
    elif tacticalSetting == constants.combat.POWER:
        tacticBonus = 1.0
    elif tacticalSetting == constants.combat.DEFENSE:
        tacticBonus = 1.3
    return tacticBonus
    return


def GetTacticDamageMod(tacticalSetting):
    dmgBonus = 1.0
    if tacticalSetting == constants.combat.BLOCK:
        dmgBonus = 0
    elif tacticalSetting == constants.combat.SPEED:
        dmgBonus = 1.0
    elif tacticalSetting == constants.combat.POWER:
        dmgBonus = 1.2
    elif tacticalSetting == constants.combat.DEFENSE:
        dmgBonus = 0.85
    return dmgBonus
    return


def GetTacticWithdrawTacticsMod(tacticalSetting):
    dmgBonus = 1.0
    if tacticalSetting == constants.combat.BLOCK:
        dmgBonus = 1.0
    elif tacticalSetting == constants.combat.SPEED:
        dmgBonus = 1.0
    elif tacticalSetting == constants.combat.POWER:
        dmgBonus = 1.2
    elif tacticalSetting == constants.combat.DEFENSE:
        dmgBonus = 0.9
    return dmgBonus
    return


def GetFreeAttackDamage(iBonuses, damageTypeInfluence, iWeaponBaseDamage, iWeaponDamageRange, fWeaponSpeed, isEnergizedTactic, isPreciseBlows, eTacticType, bAddRandGaussian):
    baseDamage = 0
    baseDamage = calcDamageWithGun(iWeaponBaseDamage, iWeaponDamageRange, eTacticType)
    CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamage: damage with gun: %d' % baseDamage, 10)
    if iWeaponBaseDamage != 0:
        baseDamage += int(float(baseDamage) * damageTypeInfluence)
        CU.outputCombatDebugMessageOnMatch('Damage with weapon bonus (weapDmg(%d)*DamageInflu(%g)) = %d' % (iWeaponBaseDamage, damageTypeInfluence, baseDamage), 10)
    if bAddRandGaussian:
        baseDamage = discovery.getGaussianRandomClamped(baseDamage, baseDamage / 10, baseDamage / 3, 4 * baseDamage / 3)
        CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamage: new damage with rand: %d' % baseDamage, 10)
    damage_total = iBonuses + baseDamage
    CU.outputCombatDebugMessage('GetFreeAttackDamage: attacker damage: (%dDmg + %dDmgBonus) = %dTotalDmg' % (baseDamage, iBonuses, damage_total), 10)
    damage_total *= consolevar.FreeFireScalar
    CU.outputCombatDebugMessage('GetFreeAttackDamage: attacker damage after scalar: %dTotalDmg' % damage_total, 10)
    tacticDamagePercentMod = GetTacticDamageMod(eTacticType)
    damage_total = int(damage_total * tacticDamagePercentMod)
    CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamage: damage (after %g tacticMod): %d' % (tacticDamagePercentMod, damage_total), 10)
    if isEnergizedTactic:
        damage_total = int(damage_total * 1.15)
        CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamage: Engerzied boost: %d' % damage_total, 10)
    if isPreciseBlows:
        damage_total = int(damage_total * 0.85)
        CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamage: Percise blows deboost: %d' % damage_total, 10)
    damage_total = int(float(damage_total) * fWeaponSpeed)
    CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamage: damage (after %g weapon speed): %d' % (fWeaponSpeed, damage_total), 10)
    return int(damage_total)
    return


def GetFreeAttackDamageMelee(iBonuses, fWeaponSpeed, isEnergizedTactic, isPreciseBlows, eTacticType, bAddRandGaussian):
    baseDamage = iBonuses
    if bAddRandGaussian:
        baseDamage = discovery.getGaussianRandomClamped(baseDamage, baseDamage / 10, baseDamage / 3, 4 * baseDamage / 3)
        CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamageMelee: new damage with rand: %d' % baseDamage, 10)
    damage_total = baseDamage
    CU.outputCombatDebugMessage('GetFreeAttackDamageMelee: attacker damage: (%dDmgBonus) = %dTotalDmg' % (iBonuses, damage_total), 10)
    damage_total *= consolevar.MeleeFreeFireScalar
    CU.outputCombatDebugMessage('GetFreeAttackDamageMelee: attacker damage after scalar: %dTotalDmg' % damage_total, 10)
    tacticDamagePercentMod = GetTacticDamageMod(eTacticType)
    damage_total = int(damage_total * tacticDamagePercentMod)
    CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamageMelee: damage (after %g tacticMod): %d' % (tacticDamagePercentMod, damage_total), 10)
    if isEnergizedTactic:
        damage_total = int(damage_total * 1.15)
        CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamageMelee: Engerzied boost = %d' % damage_total, 10)
    if isPreciseBlows:
        damage_total = int(damage_total * 0.85)
        CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamageMelee: Percise blows deboost = %d' % damage_total, 10)
    damage_total = int(float(damage_total) * fWeaponSpeed)
    CU.outputCombatDebugMessageOnMatch('GetFreeAttackDamageMelee: damage (after %g weapon speed): %d' % (fWeaponSpeed, damage_total), 10)
    return int(damage_total)
    return


def GetCloseCombatDamage(iBonuses, damageTypeInfluence, iWeaponType, iWeaponBaseDamage, iWeaponDamageRange, eTacticType, bAddRandGaussian, defaultMultiplier, isUsingPreciseBlows, isUsingEnergizedTactic, winnerOpponentCount=1):
    damage_amount = 0
    CU.outputCombatDebugMessage('---- begin damage calc -----', 10)
    CU.outputCombatDebugMessageOnMatch('base damage:%d' % damage_amount, 10)
    if CU.isUsingWeapon(iWeaponType):
        damage_amount = calcDamageWithGun(iWeaponBaseDamage, iWeaponDamageRange, eTacticType)
        CU.outputCombatDebugMessage('weapon damage weaponBaseDamage: %d weaponDelta: %d, weaponDamageRoll: %d' % (iWeaponBaseDamage, iWeaponDamageRange, damage_amount), 10)
        damage_amount += int(float(damage_amount) * damageTypeInfluence)
        CU.outputCombatDebugMessageOnMatch('Damage with weapon bonus (weapDmg(%d)*DamageInflu(%g)) = %d' % (iWeaponBaseDamage, damageTypeInfluence, damage_amount), 10)
    CU.outputCombatDebugMessageOnMatch('damage with gun/martialArt: %d' % damage_amount, 10)
    damage_multiplier = defaultMultiplier
    damage_adder = iBonuses
    damage_base = 0
    if isUsingEnergizedTactic:
        CU.outputCombatDebugMessageOnMatch('giving energized damage bonus', 10)
        damage_multiplier += 0.15
    if isUsingPreciseBlows:
        CU.outputCombatDebugMessageOnMatch('giving precise blows damage penalty', 10)
        damage_multiplier -= 0.15
    damage_base = damage_amount
    damage_amount += damage_adder
    damage_amount = int(float(damage_amount) * damage_multiplier)
    CU.outputCombatDebugMessageOnMatch('damage: (bd:%d + da:%d) * dm:%g = dt%d' % (damage_base, damage_adder, damage_multiplier, damage_amount), 10)
    if bAddRandGaussian:
        stddev = damage_amount / 10
        absoluteMin = damage_amount / 3
        absoluteMax = 4 * damage_amount / 3
        avg = damage_amount
        damage_amount = discovery.getGaussianRandomClamped(avg, stddev, absoluteMin, absoluteMax)
        CU.outputCombatDebugMessageOnMatch('damage with rand: %d' % damage_amount, 10)
    tacticDamagePercentMod = GetTacticDamageMod(eTacticType)
    damage_amount = int(damage_amount * tacticDamagePercentMod)
    CU.outputCombatDebugMessageOnMatch('damage (after %g tacticMod): %d' % (tacticDamagePercentMod, damage_amount), 10)
    damage_amount = int(float(damage_amount) * 4.0)
    CU.outputCombatDebugMessageOnMatch('damage (after %g weapon speed): %d' % (4.0, damage_amount), 10)
    return damage_amount
    return


def GetCombatTacticRoll(iConsistancyValue, iCombatTacticBonuses, iWeaponType, eTacticType, bAddRandGaussian, isUsingPreciseBlows, isUsingEnergizedTactic):
    tactic_Multiplier = GetTacticCombatTacticsMod(eTacticType)
    totalTacticBase = int(iCombatTacticBonuses * tactic_Multiplier)
    CU.outputCombatDebugMessageOnMatch('combat tactics %d (after %g tacticMod): %d' % (iCombatTacticBonuses, tactic_Multiplier, totalTacticBase), 12)
    if not bAddRandGaussian:
        iConsistancyValue = 100
    damage_multiplier = 1.0
    if isUsingEnergizedTactic:
        CU.outputCombatDebugMessageOnMatch('giving energized tactic penalty', 12)
        damage_multiplier -= 0.15
    if isUsingPreciseBlows:
        CU.outputCombatDebugMessageOnMatch('giving precise blows tactics bonus', 12)
        damage_multiplier += 0.15
    totalTacticBase = int(float(totalTacticBase) * damage_multiplier)
    finalTactics = combatlib.getTacticRoll(totalTacticBase, iConsistancyValue, 100)
    CU.outputCombatDebugMessageOnMatch('combat tactics after gaussian(consistancy %d): %d' % (iConsistancyValue, finalTactics), 12)
    return finalTactics
    return


def GetDefenseTacticRoll(iConsistancyValue, iDefenseTacticsBonus, iWeaponType, eTacticType, bAddRandGaussian, isUsingPowerShot, isUsingEnergizedTactic):
    tactic_Multiplier = GetTacticDefenseTacticsMod(eTacticType)
    totalTacticBase = int(iDefenseTacticsBonus * tactic_Multiplier)
    CU.outputCombatDebugMessageOnMatch('defense tactics %d (after %g tacticMod): %d' % (iDefenseTacticsBonus, tactic_Multiplier, totalTacticBase), 12)
    if not bAddRandGaussian:
        iConsistancyValue = 100
    finalTactics = combatlib.getTacticRoll(totalTacticBase, iConsistancyValue, 69)
    CU.outputCombatDebugMessageOnMatch('defense tactics after gaussian(consistancy %d): %d' % (iConsistancyValue, finalTactics), 12)
    return finalTactics
    return


def GetDamageReductionValue(attackerLvl, defenderLvl, curDamageReduction, damageAmount):
    CU.outputCombatDebugMessageOnMatch('GetDamageReductionValue Input: atkLvl(%d) defLvl(%d) defDmgReduc(%d), dmgAmnt(%d)' % (attackerLvl, defenderLvl, curDamageReduction, damageAmount), 21)
    fOverallDamageReduction = 0.0
    fLevelDifDamageReduction = 0.0
    fArmorDamageReduction = 0.0
    fLevelScale = 4.0
    fScalePerLevelDef = 0.2
    fMinArmorReduction = -0.33
    fMaxArmorReduction = 0.33
    fArmorReductionRange = fMaxArmorReduction - fMinArmorReduction
    fMinReduction = -0.9
    fMaxReduction = 0.9
    fArmorStartPos = -20.0
    levelDiff = float(defenderLvl) - float(attackerLvl)
    fLevelDifDamageReduction = levelDiff * fScalePerLevelDef
    CU.outputCombatDebugMessageOnMatch('GetDamageReductionValue: LevelDif(%f), LevelReduc(%f)' % (levelDiff, fLevelDifDamageReduction), 21)
    fTotalRange = float(defenderLvl) * fLevelScale - fArmorStartPos
    fTickSize = fArmorReductionRange / fTotalRange
    fArmorDamageReduction = fTickSize * (float(curDamageReduction) - fArmorStartPos) + fMinArmorReduction
    CU.outputCombatDebugMessageOnMatch('GetDamageReductionValue: defDmgReduc(%d), fArmorDamageReduction(%f)' % (curDamageReduction, fArmorDamageReduction), 21)
    if fArmorDamageReduction > fMaxArmorReduction:
        fArmorDamageReduction = fMaxArmorReduction
        CU.outputCombatDebugMessageOnMatch('GetDamageReductionValue: Armor capped at %f' % fMaxArmorReduction, 21)
    fOverallDamageReduction = fLevelDifDamageReduction + fArmorDamageReduction
    CU.outputCombatDebugMessageOnMatch('GetDamageReductionValue: levelDmgReduc(%f) + armorDmgReduc(%f) = totalDmgReduc(%f)' % (fLevelDifDamageReduction, fArmorDamageReduction, fOverallDamageReduction), 21)
    if fOverallDamageReduction > fMaxReduction:
        fOverallDamageReduction = fMaxReduction
        CU.outputCombatDebugMessageOnMatch('GetDamageReductionValue: Capped at %f' % fMaxReduction, 21)
    elif fOverallDamageReduction < fMinReduction:
        fOverallDamageReduction = fMinReduction
        CU.outputCombatDebugMessageOnMatch('GetDamageReductionValue: Capped at %f' % fMinReduction, 21)
    return fOverallDamageReduction
    return


def GetShieldDamageValue(attackerLvl, defenderLvl, forceCombatBonus):
    CU.outputCombatDebugMessageOnMatch('GetShieldDamageValue Input: atkLvl(%d) defLvl(%d) forceCombatBonus(%d)' % (attackerLvl, defenderLvl, forceCombatBonus), 21)
    if attackerLvl >= defenderLvl:
        baseDamage = (4 - (defenderLvl - attackerLvl)) * 7.3 + 6
    else:
        baseDamage = (defenderLvl - attackerLvl - 9) * -2 + 22
    bonusPercent = float(forceCombatBonus) / 100.0
    bonusDamage = int(baseDamage * bonusPercent)
    totalDamage = baseDamage + bonusDamage
    CU.outputCombatDebugMessageOnMatch('GetShieldDamageValue Total: base(%d) bonus(%d) total(%d)' % (baseDamage, bonusDamage, totalDamage), 21)
    if totalDamage > 500:
        totalDamage = 500
        CU.outputCombatDebugMessageOnMatch('GetShieldDamageValue: Capped at 500', 21)
    elif totalDamage < 10:
        totalDamage = 10
        CU.outputCombatDebugMessageOnMatch('GetShieldDamageValue: Capped at 10', 21)
    return int(totalDamage)
    return


def GetShieldResistanceISDrainValue(attackerLvl, defenderLvl):
    CU.outputCombatDebugMessageOnMatch('GetShieldResistanceISDrainValue Input: atkLvl(%d) defLvl(%d)' % (attackerLvl, defenderLvl), 21)
    ISCost = 10
    levelDiff = defenderLvl - attackerLvl
    if levelDiff <= 0:
        CU.outputCombatDebugMessageOnMatch('GetShieldResistanceISDrainValue Total: lvldiff(%d) ISCost(%d)' % (levelDiff, ISCost), 21)
        return ISCost
    LevelDiffAfterScale = max(levelDiff - 5, 0)
    ISPerLevelCost = 1
    ISCost -= LevelDiffAfterScale * ISPerLevelCost
    CU.outputCombatDebugMessageOnMatch('GetShieldResistanceISDrainValue Total: lvldiff(%d) ISCost(%d)' % (levelDiff, ISCost), 21)
    if ISCost < 2:
        ISCost = 2
        CU.outputCombatDebugMessageOnMatch('GetShieldResistanceISDrainValue: Capped at -2', 21)
    elif ISCost > 10:
        ISCost = 10
        CU.outputCombatDebugMessageOnMatch('GetShieldResistanceISDrainValue: Capped at -10', 21)
    return ISCost
    return


def DamageInterruptsCasting(damageAmount, maxHealth, maxHealthBuffValue, charLvl, concentrationValue):
    bResisted = False
    percentOfLeftTaken = float(damageAmount / maxHealth)
    attackerMod = 0.75
    basicChance = 0.75
    concentrationChance = float(concentrationValue) * 0.005
    lvlChance = 0
    fDefenderChance = basicChance + concentrationChance + lvlChance
    fAttackerChance = percentOfLeftTaken * attackerMod
    fFinal = fDefenderChance - fAttackerChance
    CU.outputCombatDebugMessageOnMatch('CastThrough Def(%g) - Atk(%g) = chance(%g)' % (fDefenderChance, fAttackerChance, fFinal), 21)
    min = 0.33
    max = 0.9
    if fFinal < min:
        fFinal = min
    elif fFinal > max:
        fFinal = max
    roll = random.randrange(1, 100)
    bResisted = roll <= int(fFinal * 100.0)
    if bResisted:
        CU.outputCombatDebugMessageOnMatch('Cast Through: SUCCESS', 21)
    else:
        CU.outputCombatDebugMessageOnMatch('Cast Through: FAILED', 21)
    return bResisted
    return


def GetFullAbilityDamage(curDamageValue, damageTypeInfluence, damageBonus, weaponDamage, eTacticTypeUsed):
    damageTotal = curDamageValue
    damageTypeInfluencePercent = 1.0 + damageTypeInfluence / 100.0
    damageTotal += damageBonus
    CU.outputCombatDebugMessageOnMatch('Ability Damage with %d damageBonus = %d' % (damageBonus, damageTotal), 20)
    if weaponDamage != 0:
        damageTotal += int(float(weaponDamage) * damageTypeInfluencePercent)
        CU.outputCombatDebugMessageOnMatch('Ability Damage with weapon bonus (weapDmg(%d)*DamageInflu(%f))+curDamage = %d' % (weaponDamage, damageTypeInfluencePercent, damageTotal), 20)
    tacticDamagePercentMod = GetTacticDamageMod(eTacticTypeUsed)
    damageTotal = int(float(damageTotal) * tacticDamagePercentMod)
    CU.outputCombatDebugMessageOnMatch('Ability Damage with %g tacticBonus = %d' % (tacticDamagePercentMod, damageTotal), 20)
    return int(damageTotal)
    return


def GetAbilityMitigationDuration(fCurDuration, casterLvl, determinationLvl):
    fNewDuration = 0.0
    fDerminationPerLevel = 5.0
    targetLevel = float(determinationLvl) / fDerminationPerLevel
    levelDef = targetLevel - float(casterLvl)
    Nob = 0.066
    clampLow = 0.33
    clampHigh = 1.0
    CU.outputCombatDebugMessageOnMatch('Mit. Dur. Input: duration(%g), caster(%d), target(%d -> %g), lvlDef(%g)' % (fCurDuration, casterLvl, determinationLvl, targetLevel, levelDef), 22)
    if levelDef <= 0:
        CU.outputCombatDebugMessageOnMatch('Mit. Dur. Output: duration(%g)' % fCurDuration, 22)
        return fCurDuration
    durationScale = 1.0 - levelDef * Nob
    if durationScale < 0.33:
        durationScale = 0.33
    elif durationScale > 1.0:
        durationScale = 1.0
    fNewDuration = fCurDuration * durationScale
    CU.outputCombatDebugMessageOnMatch('Mit. Dur. Output: duration(%g) mod(%g)' % (fNewDuration, durationScale), 22)
    return fNewDuration
    return


return

# okay decompiling work/monolith_pyc\combat_calculations.pyc
