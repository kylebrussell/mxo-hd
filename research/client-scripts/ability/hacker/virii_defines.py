# uncompyle6 version 3.9.3
# Python bytecode version base 2.3 (62011)
# Decompiled from: Python 3.12.10 (tags/v3.12.10:0cc8128, Apr  8 2025, 12:21:36) [MSC v.1943 64 bit (AMD64)]
# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ability\hacker\virii_defines.py
# Compiled at: 2006-03-30 16:29:38
import ability.utility as Utility, random, traceback
from ability.defines import *
import stringtable_client as StringTable
VIRII_LEVEL_1_DURATION = 10.0
VIRII_LEVEL_2_DURATION = 15.0
VIRII_LEVEL_3_DURATION = 25.0
VIRII_MULTI_DURATION = VIRII_LEVEL_3_DURATION
VIRII_LEVEL_1_DEBUFF = -10
VIRII_LEVEL_2_DEBUFF = -20
VIRII_LEVEL_3_DEBUFF = -30
VIRII_LEVEL_1_BUFF = 10
VIRII_LEVEL_2_BUFF = 20
VIRII_LEVEL_3_BUFF = 30
VIRII_LEVEL_1_RANGE = 5000.0
VIRII_LEVEL_2_RANGE = 5000.0
VIRII_LEVEL_3_RANGE = 5000.0

def SendVirusResultToAll(result, virus, subject, directobject, send_message=True, exValues=()):
    if send_message == False:
        return
    if result == SUCCESS:
        Utility.SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_VIRUS_HIT, virus, subject, directobject, exValues)
    elif result == DEFLECTED:
        Utility.SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_VIRUS_DEFLECTED, virus, subject, directobject, exValues)
    elif result == REFLECTED:
        Utility.SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_VIRUS_REFLECTED, virus, subject, directobject, exValues)
    return


def VirusSuccessCheck(sentence, virus, vaccination, allow_success_msg=False):
    if ProgramLauncherCheck(sentence.subject) == FAILURE:
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_PROGRAM_LAUNCHER_NOT_EQUIPPED, virus, sentence)
        return (FAILURE, 0)
    if VaccinationCheck(sentence.directObject, vaccination) == DEFLECTED:
        vaccination_name_id = Utility.GetAbilityInfoID(vaccination)
        Utility.SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_VIRUS_VACCINATED, virus, sentence.subject.locator, sentence.directObject.locator, vaccination_name_id)
        return (DEFLECTED, 0)
    difficultyRating = CalcDifficultyValue(sentence.directObject)
    successChance = SingleSuccessRating(sentence.subject)
    return DifficultyCheck(virus, sentence.subject.locator, sentence.directObject.locator, successChance, difficultyRating, allow_success_msg)
    return


def ProgramLauncherCheck(player):
    if Utility.IsProgramLauncherEquipped(player) == FAILURE:
        return FAILURE
    return SUCCESS
    return


def VaccinationCheck(player, vaccination):
    if vaccination:
        if player.hasAbility[vaccination]:
            Utility.outputAbilityDebug('VaccinationCheck: Player deflected due to vaccinate!')
            return DEFLECTED
    return SUCCESS
    return


def ReflectionCheck(subject, reflection):
    if subject.hasAbility[reflection]:
        randomRoll = random.randrange(100)
        if subject.hasAbility[HackerAbility]:
            if subject.abilities[HackerAbility] >= randomRoll:
                Utility.outputAbilityDebug('Relection Check : virus reflected! ')
                return REFLECTED
    return SUCCESS
    return


def CalcDifficultyValue(player):
    difficulty = player.Level
    if player.abilities[ResistVirusesAbility] > difficulty:
        difficulty = player.abilities[ResistVirusesAbility]
    difficulty += 50
    difficulty = 50 + 4 * (Utility.Get_HPAL_Level(player) + player.abilities[ResistVirusesAbility])
    return difficulty
    return


def CalcSuccessRating(player):
    success_chance = 4 * (TransmitVirus + ExecuteProgram)
    success_chance = player.abilities[TransmitVirusAbility]
    if player.abilities[ExecuteProgramAbility]:
        success_chance += player.abilities[ExecuteProgramAbility]
    success_chance *= 4
    return success_chance
    return


def SingleSuccessRating(player):
    success_chance = CalcSuccessRating(player)
    success_chance += random.randrange(100)
    return success_chance
    return


def MultiSuccessRating(player):
    return CalcSuccessRating(player)
    return


def DifficultyCheck(virus, subjectLocator, directobjectLocator, success_chance, difficulty_rating, allow_success_msg):
    diff = success_chance - difficulty_rating
    if diff > 0:
        SendVirusResultToAll(SUCCESS, virus, subjectLocator, directobjectLocator, allow_success_msg, diff)
        Utility.outputAbilityDebug('DifficultyCheck: SUCCEEDED: %d > %d' % (success_chance, difficulty_rating))
        return (SUCCESS, diff)
    else:
        SendVirusResultToAll(FAILURE, virus, subjectLocator, directobjectLocator, allow_success_msg, diff)
        Utility.outputAbilityDebug('DifficultyCheck: DEFLECTED: %d > %d' % (success_chance, difficulty_rating))
        return (DEFLECTED, diff)
    return


def MultiDifficultyCheck(virus, vaccination, subjectLocator, directobject, success_chance, difficulty_rating, allow_success_msg=True):
    if VaccinationCheck(directobject, vaccination) == DEFLECTED:
        vaccination_name_id = Utility.GetAbilityInfoID(vaccination)
        if allow_success_msg:
            Utility.SendAbilityOutputToAll(StringTable.ID_CLIENT_ABILITY_VIRUS_VACCINATED, virus, subjectLocator, directobject.locator, vaccination_name_id)
        return (
         DEFLECTED, 0)
    success_chance += random.randrange(100)
    return DifficultyCheck(virus, subjectLocator, directobject.locator, success_chance, difficulty_rating, allow_success_msg)
    return


def MultVirusSuccessCheck(sentence, virus, vaccination):
    if ProgramLauncherCheck(sentence.subject) == FAILURE:
        Utility.SendAbilityOutputToCasterSentence(StringTable.ID_CLIENT_ABILITY_PROGRAM_LAUNCHER_NOT_EQUIPPED, virus, sentence)
        sentence.result = FAILURE
        return (FAILURE, 0)
    sentence.result = MultiSuccessRating(sentence.subject)
    return (SUCCESS, 0)
    return


return

# okay decompiling work/monolith_pyc\ability\hacker\virii_defines.pyc
