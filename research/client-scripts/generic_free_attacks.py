# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\generic_free_attacks.py
# Compiled at: 2007-11-13 02:02:54
import random, traceback, interlock.combat_utility as CU, ability.hacker.virii_defines as VIRII, ability.utility as Utility, stringtable_client as StringTable, ltfxmap as FX, combat_calculations as CC

def GenericFreeAttackTest(attackerBonus, atrTotal, defenderBonus, defTotal):
    CU.outputCombatDebugMessage('GenericFreeAttackTest: %d(%d) > %d(%d)?' % (atrTotal, attackerBonus, defTotal, defenderBonus), 2)
    if atrTotal > defTotal:
        return 1
    return 0
    return


def ComputeAttackerTotal(attackerBonus, consistancy):
    if not consolevar.OldTacticRolls:
        attackerTotal = combatlib.getTacticRoll(attackerBonus, consistancy, 100)
    else:
        atrRoll = random.randrange(1, 100)
        attackerTotal = atrRoll + attackerBonus
    return attackerTotal
    return


def ComputeDefenderTotal(defenderBonus, consistancy):
    if not consolevar.OldTacticRolls:
        defenderTotal = combatlib.getTacticRoll(defenderBonus, consistancy, 69)
    else:
        defRoll = random.randrange(1, 100)
        defenderTotal = defRoll + defenderBonus
    return defenderTotal
    return


def AbilityThrowFreeAttack(sentence):
    if sentence.subject.Interlock.isInCombat and sentence.subject.Interlock.isCombatTarget(sentence.directObject.locator) and abilitylib.createsAnExchange(sentence.verbID):
        sentence.result = SUCCESS
        return
    defBonusTotal = 0
    atrBonusTotal = 0
    atrHasThrowBonus = sentence.subject.hasAbility[ThrowAccuracyAbility]
    defHasThrowDefenseTacticsBonus = sentence.directObject.hasAbility[ThrowDefenseTacticsAbility]
    atrConsistancy = sentence.subject.abilities[ConsistencyAbility]
    defConsistancy = sentence.directObject.abilities[ConsistencyAbility]
    if atrHasThrowBonus:
        atrBonusTotal += sentence.subject.abilities[ThrowAccuracyAbility]
    if defHasThrowDefenseTacticsBonus:
        defBonusTotal += sentence.directObject.abilities[ThrowDefenseTacticsAbility]
    atrTactic = sentence.subject.abilities[CurrentTacticAbility]
    defTactic = sentence.directObject.abilities[CurrentTacticAbility]
    atrBonusTotal = int(float(atrBonusTotal) * CC.GetTacticCombatTacticsMod(atrTactic))
    defBonusTotal = int(float(defBonusTotal) * CC.GetTacticDefenseTacticsMod(defTactic))
    attackerTotal = ComputeAttackerTotal(atrBonusTotal, atrConsistancy)
    defenderTotal = ComputeDefenderTotal(defBonusTotal, defConsistancy)
    if not consolevar.OldTacticRolls:
        defBonusTotal = 0
        atrBonusTotal = 0
    atrWon = GenericFreeAttackTest(atrBonusTotal, attackerTotal, defBonusTotal, defenderTotal)
    sentence.result = SUCCESS
    if atrWon == 1:
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_SUCCESS_CASTER, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
        Utility.SendAbilityOutputToTarget(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_SUCCESS_TARGET, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
        return
    Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_FAILURE_CASTER, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
    Utility.SendAbilityOutputToTarget(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_FAILURE_TARGET, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
    sentence.result = DEFLECTED
    return


AbilityThrowFreeAttack.depAttr = '\ndirectObject.hasAbility[CurrentTacticAbility]\ndirectObject.hasAbility[ThrowDefenseTacticsAbility]\ndirectObject.abilities[ThrowDefenseTacticsAbility]\ndirectObject.abilities[ConsistencyAbility]\ndirectObject.abilities[CurrentTacticAbility]\ndirectObject.locator\n'

def AbilityMeleeFreeAttack(sentence):
    if sentence.subject.Interlock.isInCombat and sentence.subject.Interlock.isCombatTarget(sentence.directObject.locator) and abilitylib.createsAnExchange(sentence.verbID):
        sentence.result = SUCCESS
        return
    defBonusTotal = 0
    atrBonusTotal = 0
    atrHasMeleeBonus = sentence.subject.hasAbility[MeleeCombatTacticsAbility]
    defHasMeleeDefBonus = sentence.directObject.hasAbility[MeleeDefenseTacticsAbility]
    atrConsistancy = sentence.subject.abilities[ConsistencyAbility]
    defConsistancy = sentence.directObject.abilities[ConsistencyAbility]
    if atrHasMeleeBonus:
        atrBonusTotal += sentence.subject.abilities[MeleeCombatTacticsAbility]
    if defHasMeleeDefBonus:
        defBonusTotal += sentence.directObject.abilities[MeleeDefenseTacticsAbility]
    atrTactic = sentence.subject.abilities[CurrentTacticAbility]
    defTactic = sentence.directObject.abilities[CurrentTacticAbility]
    atrBonusTotal = int(float(atrBonusTotal) * CC.GetTacticCombatTacticsMod(atrTactic))
    defBonusTotal = int(float(defBonusTotal) * CC.GetTacticDefenseTacticsMod(defTactic))
    attackerTotal = ComputeAttackerTotal(atrBonusTotal, atrConsistancy)
    defenderTotal = ComputeDefenderTotal(defBonusTotal, defConsistancy)
    if not consolevar.OldTacticRolls:
        defBonusTotal = 0
        atrBonusTotal = 0
    atrWon = GenericFreeAttackTest(atrBonusTotal, attackerTotal, defBonusTotal, defenderTotal)
    sentence.result = SUCCESS
    if atrWon == 1:
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_SUCCESS_CASTER, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
        Utility.SendAbilityOutputToTarget(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_SUCCESS_TARGET, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
        return
    Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_FAILURE_CASTER, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
    Utility.SendAbilityOutputToTarget(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_FAILURE_TARGET, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
    sentence.result = DEFLECTED
    return


AbilityMeleeFreeAttack.depAttr = '\ndirectObject.hasAbility[CurrentTacticAbility]\ndirectObject.hasAbility[MeleeDefenseTacticsAbility]\ndirectObject.abilities[MeleeDefenseTacticsAbility]\ndirectObject.abilities[ConsistencyAbility]\ndirectObject.abilities[CurrentTacticAbility]\ndirectObject.locator\n'

def AbilityRangeFreeAttack(sentence):
    if sentence.subject.Interlock.isInCombat and sentence.subject.Interlock.isCombatTarget(sentence.directObject.locator) and abilitylib.createsAnExchange(sentence.verbID):
        sentence.result = SUCCESS
        return
    defBonusTotal = 0
    atrBonusTotal = 0
    atrHasRangedBonus = sentence.subject.hasAbility[RangeCombatTacticsAbility]
    defHasRangedDefBonus = sentence.directObject.hasAbility[RangedDefenseTacticsAbility]
    atrConsistancy = sentence.subject.abilities[ConsistencyAbility]
    defConsistancy = sentence.directObject.abilities[ConsistencyAbility]
    if atrHasRangedBonus:
        atrBonusTotal += sentence.subject.abilities[RangeCombatTacticsAbility]
    if defHasRangedDefBonus:
        defBonusTotal += sentence.directObject.abilities[RangedDefenseTacticsAbility]
    atrTactic = sentence.subject.abilities[CurrentTacticAbility]
    defTactic = sentence.directObject.abilities[CurrentTacticAbility]
    CU.outputCombatDebugMessage('RangedAB Tactic A(%d) D(%d)' % (atrTactic, defTactic), 2)
    atrBonusTotal = int(float(atrBonusTotal) * CC.GetTacticCombatTacticsMod(atrTactic))
    defBonusTotal = int(float(defBonusTotal) * CC.GetTacticDefenseTacticsMod(defTactic))
    attackerTotal = ComputeAttackerTotal(atrBonusTotal, atrConsistancy)
    defenderTotal = ComputeDefenderTotal(defBonusTotal, defConsistancy)
    if not consolevar.OldTacticRolls:
        defBonusTotal = 0
        atrBonusTotal = 0
    atrWon = GenericFreeAttackTest(atrBonusTotal, attackerTotal, defBonusTotal, defenderTotal)
    sentence.result = SUCCESS
    if atrWon == 1:
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_SUCCESS_CASTER, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
        Utility.SendAbilityOutputToTarget(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_SUCCESS_TARGET, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
        return
    Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_FAILURE_CASTER, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
    Utility.SendAbilityOutputToTarget(StringTable.ID_ABILITY_RANGED_FREE_ATTACK_FAILURE_TARGET, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
    sentence.result = DEFLECTED
    return


AbilityRangeFreeAttack.depAttr = '\ndirectObject.hasAbility[CurrentTacticAbility]\ndirectObject.hasAbility[RangedDefenseTacticsAbility]\ndirectObject.abilities[RangedDefenseTacticsAbility]\ndirectObject.abilities[ConsistencyAbility]\ndirectObject.abilities[CurrentTacticAbility]\ndirectObject.locator\n'

def AbilityViralFreeAttack(sentence):
    if sentence.subject.Interlock.isInCombat and sentence.subject.Interlock.isCombatTarget(sentence.directObject.locator) and abilitylib.createsAnExchange(sentence.verbID):
        sentence.result = SUCCESS
        VIRII.SendVirusResultToAll(sentence.result, sentence.verbID, sentence.subject, sentence.directObject)
        return
    defBonusTotal = 0
    atrBonusTotal = 0
    atrHasTransmitBonus = sentence.subject.hasAbility[ViralTransmissionAbility]
    defHasViralDeflectionBonus = sentence.directObject.hasAbility[ViralDeflectionAbility]
    atrConsistancy = sentence.subject.abilities[ConsistencyAbility]
    defConsistancy = sentence.directObject.abilities[ConsistencyAbility]
    if atrHasTransmitBonus:
        atrBonusTotal += sentence.subject.abilities[ViralTransmissionAbility]
    if defHasViralDeflectionBonus:
        defBonusTotal += sentence.directObject.abilities[ViralDeflectionAbility]
    atrTactic = sentence.subject.abilities[CurrentTacticAbility]
    defTactic = sentence.directObject.abilities[CurrentTacticAbility]
    CU.outputCombatDebugMessage('Viral Tactic A(%d) D(%d)' % (atrTactic, defTactic), 2)
    CU.outputCombatDebugMessage('Viral Tactic before: %dvs%d' % (atrBonusTotal, defBonusTotal), 2)
    atrTacticBonus = CC.GetTacticCombatTacticsMod(atrTactic)
    defTacticBonus = CC.GetTacticDefenseTacticsMod(defTactic)
    atrBonusTotal = int(float(atrBonusTotal) * atrTacticBonus)
    defBonusTotal = int(float(defBonusTotal) * defTacticBonus)
    CU.outputCombatDebugMessage('Viral Tactic after: %d(%gmod)vs%d(w%gmod)' % (atrBonusTotal, atrTacticBonus, defBonusTotal, defTacticBonus), 2)
    attackerTotal = ComputeAttackerTotal(atrBonusTotal, atrConsistancy)
    defenderTotal = ComputeDefenderTotal(defBonusTotal, defConsistancy)
    if not consolevar.OldTacticRolls:
        defBonusTotal = 0
        atrBonusTotal = 0
    atrWon = GenericFreeAttackTest(atrBonusTotal, attackerTotal, defBonusTotal, defenderTotal)
    if atrWon == 1:
        sentence.result = SUCCESS
    else:
        CU.outputCombatDebugMessage('AbilityViralFreeAttack: FAILED', 2)
        sentence.result = DEFLECTED
        discovery.playEffect(sentence.directObject.locator, sentence.directObject.locator, FX.FX_CHARACTER_DEFLECTION, 0)
    VIRII.SendVirusResultToAll(sentence.result, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, True, (attackerTotal, atrBonusTotal, defenderTotal, defBonusTotal))
    return


AbilityViralFreeAttack.depAttr = '\ndirectObject.hasAbility[CurrentTacticAbility]\ndirectObject.hasAbility[ViralDeflectionAbility]\ndirectObject.abilities[ViralDeflectionAbility]\ndirectObject.abilities[ConsistencyAbility]\ndirectObject.abilities[CurrentTacticAbility]\ndirectObject.locator\n'

def AbilityOutOfCombat(sentence):
    if sentence.subject.AbilityInv.ConditionStateFlags & constants.combat.ST_COMBAT_EFFECT:
        sentence.result = DEFLECTED
        Utility.SendAbilityOutputToCaster(StringTable.ID_ABILITY_NONCOMBAT_ABILITY, sentence.verbID, sentence.subject.locator, sentence.directObject.locator, (0, 0, 0, 0))
    else:
        sentence.result = SUCCESS
    return


AbilityOutOfCombat.depAttr = '\ndirectObject.locator\n'
return

# okay decompiling work/monolith_pyc\generic_free_attacks.pyc
