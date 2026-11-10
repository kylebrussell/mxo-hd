# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\interlock\combat_resolution.py
# Compiled at: 2007-12-11 19:34:53
import random, traceback, obj, combat_defines as CD, math, combat_utility as CU, combat_modifiers as Mods, combat_calculations as CC

def generateVulnerabilities(player):
    if consolevar.CombatTacticVulner == 0:
        return
    vulnerabilityPercentModifier = -float(player.vulnerabilityGenerationBonus) / 100.0
    vulnerabilityBase = 0.2
    vulnerabilityTactic = 0.0
    if player.tacticalSetting == constants.combat.POWER:
        vulnerabilityTactic = float(player.vuln_speed_chance) / 100.0
    elif player.tacticalSetting == constants.combat.DEFENSE:
        vulnerabilityTactic = float(player.vuln_power_chance) / 100.0
    elif player.tacticalSetting == constants.combat.SPEED:
        vulnerabilityTactic = float(player.vuln_defense_chance) / 100.0
    vulnerability = vulnerabilityBase + vulnerabilityPercentModifier + vulnerabilityTactic
    CU.outputCombatDebugMessageOnMatch('vulnerability percent: %gBase + %gBonus +%gTacticChange = %g' % (vulnerabilityBase, vulnerabilityPercentModifier, vulnerabilityTactic, vulnerability), 11)
    if player.isEnergizedTactic:
        vulnerability *= 1.5
        CU.outputCombatDebugMessageOnMatch('vulnerability doubled from energized tactic to %g' % vulnerability, 11)
    roll = random.randrange(1, 100)
    CU.outputCombatDebugMessageOnMatch('vulnerability check: %d %f' % (roll, vulnerability), 11)
    if roll > vulnerability * 100:
        if player.tacticalSetting == constants.combat.POWER:
            player.vuln_speed_chance = player.vuln_speed_chance + 5
        elif player.tacticalSetting == constants.combat.DEFENSE:
            player.vuln_power_chance = player.vuln_power_chance + 5
        elif player.tacticalSetting == constants.combat.SPEED:
            player.vuln_defense_chance = player.vuln_defense_chance + 5
        return
    if player.tacticalSetting == constants.combat.POWER:
        player.vuln_created_speed = 1
        player.vuln_speed_chance = 0
    elif player.tacticalSetting == constants.combat.DEFENSE:
        player.vuln_created_power = 1
        player.vuln_power_chance = 0
    elif player.tacticalSetting == constants.combat.SPEED:
        player.vuln_created_defense = 1
        player.vuln_defense_chance = 0
    roll = random.randrange(1, 100)
    if roll < 20:
        player.vuln_created_duration = 3
    elif roll < 50:
        player.vuln_created_duration = 4
    elif roll < 80:
        player.vuln_created_duration = 5
    else:
        player.vuln_created_duration = 6
    return


def generateExploits(player1, player2):
    if not player1.canExploitVulnerabilities:
        return
    CU.outputCombatDebugMessageOnMatch('exploit check', 3)
    if player1.tacticalSetting == constants.combat.SPEED and player2.speedVulnerabilityCount > 0:
        player2.vuln_exploited_speed = 1
    elif player1.tacticalSetting == constants.combat.POWER and player2.powerVulnerabilityCount > 0:
        player2.vuln_exploited_power = 1
    elif player1.tacticalSetting == constants.combat.DEFENSE and player2.defenseVulnerabilityCount > 0:
        player2.vuln_exploited_defense = 1
    return


def generateExploitsForPlayers(player1, player2):
    if consolevar.CombatTacticVulner == 0:
        return
    generateExploits(player1, player2)
    generateExploits(player2, player1)
    return


def isAbilityMartialArt(ability):
    if ability == AikidoAbility:
        return True
    elif ability == KarateAbility:
        return True
    elif ability == KungFuAbility:
        return True
    else:
        return False
    return


def calcDamageForLoser(winner, loser):
    damage_multiplier = 1.0
    if loser.vuln_exploited_count > 0:
        CU.outputCombatDebugMessageOnMatch('calcDamageForLoser: giving exploited damage bonus', 10)
        damage_multiplier += 0.15
        damage_multiplier += float(winner.damageAgainstVulnerability) / 100.0
    damage_amount = CC.GetCloseCombatDamage(winner.damageBonus, winner.damageInfluence, winner.equippedItemType, winner.equippedItemBaseDamage, winner.equippedItemDeltaDamage, winner.tacticalSetting, 1, damage_multiplier, winner.isPreciseBlow, winner.isEnergizedTactic, winner.opponentCount)
    CU.outputCombatDebugMessage('calcDamageForLoser: Damage returned: %d.' % damage_amount, 10)
    damage_resist = loser.toughness
    bUsing_weapon = CU.isUsingWeapon(winner.equippedItemType)
    if bUsing_weapon == True:
        damage_resist_boost = loser.rangedToughness
    else:
        damage_resist_boost = loser.meleeToughness
    damage_resist_total = CC.GetDamageReductionValue(winner.playerLevel, loser.playerLevel, damage_resist + damage_resist_boost, damage_amount)
    CU.outputCombatDebugMessage('calcDamageForLoser: resistance = %g' % damage_resist_total, 10)
    damage_resisted = int(float(damage_amount) * damage_resist_total)
    CU.outputCombatDebugMessage('calcDamageForLoser: damage resisted = %d' % damage_resisted, 10)
    damage_amount -= damage_resisted
    if damage_amount < 1:
        damage_amount = 1
    CU.outputCombatDebugMessage('calcDamageForLoser: final Damage: %d' % damage_amount, 10)
    return (
     int(damage_amount), int(damage_resisted))
    return


def calculateDamage(winner, loser, result):
    if CU.isSpecialMove(winner.requestedSpecialMove):
        return (
         0, 0)
    (damage_taken, damage_resisted) = calcDamageForLoser(winner, loser)
    damage_taken = int(float(damage_taken) * consolevar.CombatScaleDamage)
    CU.outputCombatDebugMessage('Damage done: %d, Damage resisted: %d' % (damage_taken, damage_resisted), 2)
    return (
     damage_taken, damage_resisted)
    return


def determineAttacker(player1, player2, combat_range):
    if CU.isPlayerWithdrawing(player1):
        CU.outputCombatDebugMessage('determineAttacker: Player 1 is withdrawing... player 2 is attacker', 13)
        return player2
    elif CU.isPlayerWithdrawing(player2):
        CU.outputCombatDebugMessage('determineAttacker: Player 2 is withdrawing... player 1 is attacker', 13)
        return player1
    if CU.isPlayerCombatProne(player1):
        return player2
    elif CU.isPlayerCombatProne(player2):
        return player1
    if player1.opportunityAttack:
        return player1
    elif player2.opportunityAttack:
        return player2
    if player1.tacticalSetting != player2.tacticalSetting:
        if CU.isPreferredTactical(player1.tacticalSetting, player2.tacticalSetting):
            attacker = player1
        else:
            attacker = player2
    elif CU.isPlayerBeingGanged(player1) and CU.isPlayerBeingGanged(player2):
        if player1.targetSlot == player2.slot:
            attacker = player1
        if player2.targetSlot == player1.slot:
            attacker = player2
        else:
            attacker = player1
    elif CU.isPlayerBeingGanged(player1):
        CU.outputCombatDebugMessage('determineAttacker() player %d being ganged' % player1.slot, 3)
        attacker = player2
    elif CU.isPlayerBeingGanged(player2):
        CU.outputCombatDebugMessage('determineAttacker() player %d being ganged' % player2.slot, 3)
        attacker = player1
    elif player1.abilityLevel > player2.abilityLevel:
        attacker = player1
    elif player2.abilityLevel > player1.abilityLevel:
        attacker = player2
    elif random.randint(0, 1) > 0:
        attacker = player1
    else:
        attacker = player2
    return attacker
    return


def handleSpecialMove(attacker, result):
    CU.outputCombatDebugMessage('Short circuiting for special move, true_attacker is winner', 3)
    result.winnerSlot = attacker.slot
    result.outcomeFlag = constants.combat.SPECIAL_MOVE
    result.winnerDamage = 0
    result.loserDamage = 0
    return


def handleOppertunityAttack(attacker, defender, result, true_attacker):
    CU.outputCombatDebugMessage('handleOppertunityAttack: Handling attacker opportunity attack', 3)
    result.winnerSlot = attacker.slot
    if true_attacker == attacker:
        CU.outputCombatDebugMessage('handleOppertunityAttack: Attacker wins', 3)
        result.outcomeFlag = constants.combat.AHITS_DMISSES | constants.combat.OPPORTUNITY_ATTACK
        (result.loserDamage, result.loserDamageAbsorbed) = calculateDamage(attacker, defender, result)
    else:
        CU.outputCombatDebugMessage('handleOppertunityAttack: Defender wins', 3)
        result.outcomeFlag = constants.combat.DRAW | constants.combat.OPPORTUNITY_ATTACK
        (result.winnerDamage, result.winnerDamageAbsorbed) = calculateDamage(defender, attacker, result)
    return


def handleStandardExchange(true_attacker, true_defender, result, attackerHits, defenderHits):
    CU.outputCombatDebugMessage('true_attacker is winner', 3)
    result.winnerSlot = true_attacker.slot
    flipAtkDef = False
    if attackerHits:
        (result.loserDamage, result.loserDamageAbsorbed) = calculateDamage(true_attacker, true_defender, result)
        CU.outputCombatDebugMessage('Attacker deals %d. Defender resisted %d' % (result.loserDamage, result.loserDamageAbsorbed), 10)
    if defenderHits:
        (result.winnerDamage, result.winnerDamageAbsorbed) = calculateDamage(true_defender, true_attacker, result)
        CU.outputCombatDebugMessage('Defender deals %d. Attacker resisted %d' % (result.winnerDamage, result.winnerDamageAbsorbed), 10)
    if attackerHits and defenderHits:
        CU.outputCombatDebugMessage('Result: HH', 3)
        result.outcomeFlag = constants.combat.AHITS_DHITS
    elif attackerHits and not defenderHits:
        CU.outputCombatDebugMessage('Result: HM', 3)
        result.outcomeFlag = constants.combat.AHITS_DMISSES
    elif not attackerHits and defenderHits:
        CU.outputCombatDebugMessage('Result: MH', 3)
        result.outcomeFlag = constants.combat.AMISSES_DHITS
    elif not attackerHits and not defenderHits:
        CU.outputCombatDebugMessage('Result: MM', 3)
        result.outcomeFlag = constants.combat.AMISSES_DMISSES
    atkIsFM = CU.finishingMoveAllowed(true_attacker, true_defender, result, False)
    defIsFM = CU.finishingMoveAllowed(true_attacker, true_defender, result, True)
    if not atkIsFM and not defIsFM and CU.killingMoveAllowed(true_attacker, true_defender, result):
        result.outcomeFlag = result.outcomeFlag | constants.combat.KILLING_MOVE
        CU.outputCombatDebugMessage('Result: KILLING_MOVE', 3)
    elif atkIsFM or defIsFM:
        if result.outcomeFlag == constants.combat.AHITS_DHITS:
            if atkIsFM and not defIsFM:
                result.outcomeFlag = result.outcomeFlag | constants.combat.FINISHING_MOVE
                CU.outputCombatDebugMessage('Result: HFM vs H', 3)
                result.winnerSlot = true_defender.slot
                tempLoserDmg = result.loserDamage
                tempLoserDmgAb = result.loserDamageAbsorbed
                tempWinnerDmgAb = result.winnerDamage
                tempWinnerDmg = result.winnerDamageAbsorbed
                result.loserDamage = tempWinnerDmg
                result.loserDamageAbsorbed = tempWinnerDmgAb
                result.winnerDamage = tempLoserDmg
                result.winnerDamageAbsorbed = tempLoserDmgAb
            elif not atkIsFM and defIsFM:
                result.outcomeFlag = result.outcomeFlag | constants.combat.FINISHING_MOVE
                CU.outputCombatDebugMessage('Result: H vs HFM', 3)
            elif atkIsFM and defIsFM:
                result.outcomeFlag = result.outcomeFlag | constants.combat.FINISHING_MOVE_BOTH
                CU.outputCombatDebugMessage('Result: FMH vs HFM', 3)
        else:
            result.outcomeFlag = result.outcomeFlag | constants.combat.FINISHING_MOVE
            CU.outputCombatDebugMessage('Result: FMH vs M', 3)
    return flipAtkDef
    return


def handleBlock(true_attacker, true_defender, result):
    CU.outputCombatDebugMessage('Handling a block!', 3)
    if true_defender.tacticalSetting == constants.combat.BLOCK:
        if CU.isPlayerBeingGanged(true_defender):
            CU.outputCombatDebugMessage('defender is being ganged, true_attacker is winner', 3)
            result.winnerSlot = true_attacker.slot
        elif CU.isPlayerBeingGanged(true_attacker):
            CU.outputCombatDebugMessage('attacker is being ganged, true_defender is winner', 3)
            result.winnerSlot = true_defender.slot
        else:
            CU.outputCombatDebugMessage('no one is being ganged, true_attacker is winner', 3)
            result.winnerSlot = true_attacker.slot
    else:
        CU.outputCombatDebugMessage('true_defender is winner', 3)
        result.winnerSlot = true_defender.slot
    result.outcomeFlag = constants.combat.AMISSES_DMISSES
    result.loserDamage = 0
    if true_defender.tacticalSetting != constants.combat.BLOCK:
        CU.outputCombatDebugMessage('Checking for hyper dodge', 3)
        if true_attacker.tacticalValue - true_defender.meleeDefenseTactics > 50:
            CU.outputCombatDebugMessage('using hyper dodge', 3)
            result.hyper_dodge = 1
    return


def GetCorrectDefenseBonus(character, abilityType):
    if abilityType == None:
        return 0
    elif abilityType == RangedDefenseTacticsAbility:
        return character.rangeDefenseTactics
    elif abilityType == ViralDeflectionAbility:
        return character.viralDefenseTactics
    elif abilityType == MeleeDefenseTacticsAbility:
        return character.meleeDefenseTactics
    elif abilityType == ThrowDefenseTacticsAbility:
        return character.thrownDefenseTactics
    else:
        return 0
    return


def getCorrectDTTotals(attacker, defender):
    defenderD_total = 0
    attackerD_total = 0
    attackerAssigned = 0
    defenderAssigned = 0
    if CU.isSpecialMove(attacker.requestedSpecialMove):
        abilityDefenseType = abilitylib.getDefenseTacticBonusType(attacker.requestedSpecialMove)
        defenderD_total = GetCorrectDefenseBonus(defender, abilityDefenseType)
        CU.outputCombatDebugMessage('(ASM) defender DT: %d ' % defenderD_total, 1)
        defenderAssigned = 1
    if CU.isSpecialMove(defender.requestedSpecialMove):
        abilityDefenseType = abilitylib.getDefenseTacticBonusType(defender.requestedSpecialMove)
        attackerD_total = GetCorrectDefenseBonus(attacker, abilityDefenseType)
        CU.outputCombatDebugMessage('(DSM) attacker DT: %d ' % attackerD_total, 1)
        attackerAssigned = 1
    if attackerAssigned == 0:
        if defender.equippedItemType:
            attackerD_total = attacker.rangeDefenseTactics
            CU.outputCombatDebugMessage('(Defender Gun) attacker DT: %d ' % attackerD_total, 1)
        else:
            attackerD_total = attacker.meleeDefenseTactics
            CU.outputCombatDebugMessage('(Defender Fists) attacker DT: %d ' % attackerD_total, 1)
    if defenderAssigned == 0:
        if attacker.equippedItemType:
            defenderD_total = defender.rangeDefenseTactics
            CU.outputCombatDebugMessage('(Attacker Gun) defender DT: %d ' % defenderD_total, 1)
        else:
            defenderD_total = defender.meleeDefenseTactics
            CU.outputCombatDebugMessage('(Attacker Fists) defender DT: %d ' % defenderD_total, 1)
    return (
     attackerD_total, defenderD_total)
    return


def getCTandDTotals(attacker, defender):
    attackerCT_total = attacker.tacticalValue
    defenderCT_total = defender.tacticalValue
    (attackerD_total, defenderD_total) = getCorrectDTTotals(attacker, defender)
    attackersAdvantage = attackerCT_total - defenderD_total
    defendersAdvantage = defenderCT_total - attackerD_total
    attackerHits = attackersAdvantage > 0
    defenderHits = defendersAdvantage > 0
    attackersSM = 0
    if CU.isSpecialMove(attacker.requestedSpecialMove):
        attackersSM = attacker.requestedSpecialMove
    defendersSM = 0
    if CU.isSpecialMove(defender.requestedSpecialMove):
        defendersSM = defender.requestedSpecialMove
    CU.outputCombatDebugMessageAll('attacker CT: %d  ' % attacker.tacticalValue, 1, attacker, defender)
    CU.outputCombatDebugMessageAll('attacker DT: %d ' % attackerD_total, 1, attacker, defender)
    CU.outputCombatDebugMessageAll('defender CT: %d' % defender.tacticalValue, 1, attacker, defender)
    CU.outputCombatDebugMessageAll('defender DT: %d ' % defenderD_total, 1, attacker, defender)
    CU.outputCombatDebugMessageAll('result: AHits(%d) & DHits(%d)' % (attackerHits, defenderHits), 1, attacker, defender)
    CU.outputCombatDebugMessageAll('result: ASM(%d) & DSM(%d)' % (attackersSM, defendersSM), 1, attacker, defender)
    return (
     attackerCT_total, attackerD_total, attackersAdvantage, attackerHits, defenderCT_total, defenderD_total, defendersAdvantage, defenderHits)
    return


def FlipAtkDef(result, winner, loser):
    result.winnerSlot = loser.slot
    return


def determineResultForStandardCombat(attacker, defender, failed_to_withdraw, result):
    result.attacker_level = attacker.tacticalValue
    result.defender_level = defender.tacticalValue
    (attackerCT_total, attackerD_total, attackersAdvantage, attackerHits, defenderCT_total, defenderD_total, defendersAdvantage, defenderHits) = getCTandDTotals(attacker, defender)
    if consolevar.SendClientCombatData >= 1:
        discovery.clientSystemMessage2(attacker.locator, 'You are attacker.', constants.Chat.CT_SYS_DEBUG)
        discovery.clientSystemMessage2(defender.locator, 'You are defender.', constants.Chat.CT_SYS_DEBUG)
    if defender.isAttemptingWithdraw or CU.isEscapeAbility(defender.requestedSpecialMove):
        CU.outputCombatDebugMessageAll('defender withdrawing: CT -> 0. No hit', 1, attacker, defender)
        defenderCT_total = 0
        defenderHits = False
    if attacker.isAttemptingWithdraw or CU.isEscapeAbility(attacker.requestedSpecialMove):
        CU.outputCombatDebugMessageAll('attacker withdrawing: CT -> 0. No hit', 1, attacker, defender)
        attackerCT_total = 0
        attackerHits = False
    switchAttackerDefender = 0
    true_attacker = attacker
    true_defender = defender
    if defenderHits and not attackerHits:
        switchAttackerDefender = 1
    bothHit = defenderHits and attackerHits
    aUsesSM = CU.isSpecialMove(true_attacker.requestedSpecialMove)
    dUsesSM = CU.isSpecialMove(true_defender.requestedSpecialMove)
    bothUseSM = aUsesSM and dUsesSM
    if aUsesSM:
        CU.outputCombatDebugMessageAll('Attacker requesting special move', 1, attacker, defender)
    if dUsesSM:
        CU.outputCombatDebugMessageAll('Defender requesting special move', 1, attacker, defender)
    if bothHit and bothUseSM:
        CU.outputCombatDebugMessageAll('SM BOTH: !!!', 1, attacker, defender)
        if defenderCT_total > attackerCT_total:
            CU.outputCombatDebugMessageAll('SM BOTH: Defender wins!', 1, attacker, defender)
            switchAttackerDefender = 1
    elif dUsesSM and defenderHits:
        switchAttackerDefender = 1
    if switchAttackerDefender == 1:
        CU.outputCombatDebugMessageAll('switching Attacker/Defender', 1, attacker, defender)
        true_attacker = defender
        true_defender = attacker
        tempAHit = attackerHits
        tempAAdvan = attackersAdvantage
        tempACT = attackerCT_total
        tempAD = attackerD_total
        attackerHits = defenderHits
        attackersAdvantage = defendersAdvantage
        attackerCT_total = defenderCT_total
        attackerD_total = defenderD_total
        defenderHits = tempAHit
        defendersAdvantage = tempAAdvan
        defenderCT_total = tempACT
        defenderD_total = tempAD
    flipAtkDef = False
    if CU.isSpecialMove(true_attacker.requestedSpecialMove) and attackerHits:
        CU.outputCombatDebugMessageAll('Special Move handled!', 1, attacker, defender)
        handleSpecialMove(true_attacker, result)
        defenderHits = False
    elif attacker.opportunityAttack:
        CU.outputCombatDebugMessageAll('Opportunity Attack Handled!', 1, attacker, defender)
        handleOppertunityAttack(attacker, defender, result, true_attacker)
    elif true_attacker.tacticalSetting == constants.combat.BLOCK:
        CU.outputCombatDebugMessageAll('Block Handled!', 1)
        handleBlock(true_attacker, true_defender, result)
    else:
        CU.outputCombatDebugMessageAll('Standard Exchange Handled!', 1, attacker, defender)
        flipAtkDef = handleStandardExchange(true_attacker, true_defender, result, attackerHits, defenderHits)
    winner = None
    loser = None
    if result.winnerSlot == attacker.slot:
        winner = attacker
        loser = defender
        result.attackerValue = attackerCT_total
        result.defenderValue = defenderCT_total
    else:
        winner = defender
        loser = attacker
        result.attackerValue = defenderCT_total
        result.defenderValue = attackerCT_total
    CU.outputCombatDebugMessageAll('winner: %d(%d) loser %d' % (winner.slot, result.winnerSlot, loser.slot), 1, attacker, defender)
    if result.outcomeFlag == constants.combat.SPECIAL_MOVE:
        if CU.isSpecialMove(winner.requestedSpecialMove):
            result.winnerSpecialMoveUsed = 1
        elif CU.isSpecialMove(loser.requestedSpecialMove):
            result.loserSpecialMoveUsed = 1
    return


def determineResultOfWithdrawalRequest(attacker, defender, result):
    CU.outputCombatDebugMessage('determineResultOfWithdrawalRequest: Redraw request check.', 13)
    if CU.isPlayerWithdrawing(attacker) and CU.isPlayerWithdrawing(defender) and attacker.proneState == 0 and defender.proneState == 0:
        if CU.isEscapeAbility(attacker.requestedSpecialMove) == True:
            result.outcomeFlag = constants.combat.SPECIAL_MOVE
            result.winnerSlot = attacker.slot
            result.winnerSpecialMoveUsed = 1
        else:
            result.outcomeFlag = constants.combat.WITHDRAW
            result.winnerSlot = attacker.slot
        CU.outputCombatDebugMessage('uncontested withdraw successful', 13)
        return (True, False)
    if CU.isPlayerWithdrawing(defender) != True:
        CU.outputCombatDebugMessage('determineResultOfWithdrawalRequest: No withdraw request from defender.', 13)
        return (False, False)
    if defender.proneState != 0:
        CU.outputCombatDebugMessage('player attempting to withdraw while prone', 13)
        return (False, False)
    if defender.opponentCount < 1:
        CU.outputCombatDebugMessage('determineResultOfWithdrawalRequest: Noone targeting defender.', 13)
        return (True, False)
    CU.outputCombatDebugMessage('determining withdraw result', 13)
    attacker_total = attacker.tacticalValue
    defender_total = defender.tacticalValue
    result.combatResult = defender_total - attacker_total
    if result.combatResult > 0 or consolevar.CombatFreeWithdraw > 0:
        CU.outputCombatDebugMessage('Successfuly withdrew: %d <= %d' % (attacker_total, defender_total), 13)
        result.winnerSlot = defender.slot
        result.outcomeFlag = constants.combat.WITHDRAW
        if CU.isEscapeAbility(defender.requestedSpecialMove) == True:
            CU.outputCombatDebugMessage('Escape performed', 3)
            result.outcomeFlag = constants.combat.SPECIAL_MOVE
            result.winnerSpecialMoveUsed = 1
        return (True, False)
    else:
        CU.outputCombatDebugMessage('Unsuccessfuly withdrew: %d <= %d' % (defender_total, attacker_total), 13)
        return (True, True)
    return


return

# okay decompiling work/monolith_pyc\interlock\combat_resolution.pyc
