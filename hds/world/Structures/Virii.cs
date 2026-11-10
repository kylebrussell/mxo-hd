using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// The hacker virus rules of the client's scripts: research/client-scripts/ability/hacker/virii_defines.py (times,
    /// buff and debuff sizes, ranges, the hit roll, vaccination and reflection) and virii.py (the per-virus times,
    /// Deflect Bullets / Deflect Virus, Corruption). Line numbers are the decompiled files'.
    /// <para>The per-ability numbers (how much a virus does) aren't here: they are the ability data's and come from the
    /// retail captures (the timed-effects PR's TempMods).</para>
    /// </summary>
    public static class Virii
    {
        // virii_defines.py 9-21
        public const float Level1Duration = 10.0f;
        public const float Level2Duration = 15.0f;
        public const float Level3Duration = 25.0f;
        public const float MultiDuration = Level3Duration;
        public const int Level1Debuff = -10, Level2Debuff = -20, Level3Debuff = -30;
        public const int Level1Buff = 10, Level2Buff = 20, Level3Buff = 30;
        /// <summary>Virus range in world units (centimetres): 50 m for all three levels.</summary>
        public const float Level1Range = 5000.0f, Level2Range = 5000.0f, Level3Range = 5000.0f;

        // virii.py 10-21
        public const float AffectVision1Time = Level1Duration, AffectVision2Time = Level2Duration, AffectVision3Time = Level3Duration;
        public const float MovementDisrupt1Time = Level1Duration, MovementDisrupt2Time = Level2Duration;
        /// <summary>virii.py 15: Movement Disrupt 3 uses level 2's time, not level 3's (retail's script as written).</summary>
        public const float MovementDisrupt3Time = Level2Duration;
        public const float DisableAbility1Time = 500;
        /// <summary>Movement Disrupt's slow, percent (virii.py 19-21).</summary>
        public const int MovementDisrupt1Effect = 25, MovementDisrupt2Effect = 50, MovementDisrupt3Effect = 75;

        /// <summary>A virus's time by level (1-3), and MultiDuration for the multi-target ones.</summary>
        public static float DurationForLevel(int level)
        {
            switch (level)
            {
                case 1: return Level1Duration;
                case 2: return Level2Duration;
                default: return Level3Duration;
            }
        }

        public static int DebuffForLevel(int level)
        {
            switch (level)
            {
                case 1: return Level1Debuff;
                case 2: return Level2Debuff;
                default: return Level3Debuff;
            }
        }

        public static int BuffForLevel(int level)
        {
            switch (level)
            {
                case 1: return Level1Buff;
                case 2: return Level2Buff;
                default: return Level3Buff;
            }
        }

        // String ids SendVirusResultToAll and the checks use (stringtable_client)
        public const UInt32 HitLine = 0x240000f1;            // ID_CLIENT_ABILITY_VIRUS_HIT "%2s hit %3s with a %1z attack."
        public const UInt32 DeflectedLine = 0x240000f2;      // ID_CLIENT_ABILITY_VIRUS_DEFLECTED "%3s deflected %2s's %1z attack."
        public const UInt32 ReflectedLine = 0x5e00004e;      // ID_CLIENT_ABILITY_VIRUS_REFLECTED
        public const UInt32 VaccinatedLine = 0x240000f0;     // ID_CLIENT_ABILITY_VIRUS_VACCINATED "%4z successfully vaccinated ..."
        public const UInt32 NoLauncherLine = 0x240000ed;     // ID_CLIENT_ABILITY_PROGRAM_LAUNCHER_NOT_EQUIPPED

        /// <summary>The abilities the roll reads (data/abilityIDs.csv).</summary>
        public const UInt16 ExecuteProgramAbility = 45, HackerAbility = 52, ResistVirusesAbility = 76, TransmitVirusAbility = 95;

        public enum Result
        {
            Success,
            Deflected,
            Reflected,
            Failure,
        }

        /// <summary>
        /// CalcDifficultyValue (virii_defines.py 70): 50 + 4 x (the target's Awakened level + Resist Viruses). The lines
        /// before the last assignment (level or Resist Viruses, + 50) are overwritten and do nothing.
        /// </summary>
        public static int Difficulty(int awakenedLevel, int resistViruses)
        {
            return 50 + 4 * (awakenedLevel + resistViruses);
        }

        /// <summary>
        /// CalcSuccessRating (virii_defines.py 79): 4 x (Transmit Virus + Execute Program, the latter only when the caster
        /// has it). The script's first line, 4 x (TransmitVirus + ExecuteProgram) on the ability ids, is overwritten.
        /// </summary>
        public static int SuccessRating(int transmitVirus, int executeProgram)
        {
            int chance = transmitVirus;
            if (executeProgram != 0)
            {
                chance += executeProgram;
            }
            return chance * 4;
        }

        /// <summary>SingleSuccessRating (virii_defines.py 89): the rating plus random.randrange(100) (0-99).</summary>
        public static int SingleSuccess(int transmitVirus, int executeProgram, Random random)
        {
            return SuccessRating(transmitVirus, executeProgram) + random.Next(100);
        }

        /// <summary>DifficultyCheck (virii_defines.py 100): hits when the success beats the difficulty, else deflected.</summary>
        public static Result Check(int success, int difficulty, out int margin)
        {
            margin = success - difficulty;
            return margin > 0 ? Result.Success : Result.Deflected;
        }

        /// <summary>
        /// VirusSuccessCheck (virii_defines.py 32): no Program Launcher fails (NPCs always have one,
        /// ability/utility.IsProgramLauncherEquipped); a vaccinated target deflects; then the roll.
        /// </summary>
        public static Result SuccessCheck(bool hasLauncher, bool vaccinated, int transmitVirus, int executeProgram,
            int targetLevel, int targetResistViruses, Random random, out int margin)
        {
            margin = 0;
            if (!hasLauncher)
            {
                return Result.Failure;
            }
            if (vaccinated)
            {
                return Result.Deflected;
            }
            return Check(SingleSuccess(transmitVirus, executeProgram, random), Difficulty(targetLevel, targetResistViruses), out margin);
        }

        /// <summary>
        /// ReflectionCheck (virii_defines.py 58): with the reflection ability, a Hacker who rolls randrange(100) at or
        /// under their Hacker level reflects the virus.
        /// </summary>
        public static Result Reflection(bool hasReflection, int hackerLevel, int roll)
        {
            return hasReflection && hackerLevel > 0 && hackerLevel >= roll ? Result.Reflected : Result.Success;
        }

        /// <summary>
        /// Deflect Bullets 1 / Deflect Virus 1 on their target (virii.py 23-34): a 0.2 s tempmod that plays the effect.
        /// Retail's Deflect Virus 1 record: <c>80 bc 09 00 d8 00 00 7a 01 00 00 7a 01 20 08 00 28 00 cd cc 4c 3e</c>
        /// (crashover2k proxy_024 13034: ability 378 on 378, the effect id as the value, 0.2 s).
        /// </summary>
        public const float DeflectEffectSeconds = 0.2f;
        public const UInt16 DeflectBullets1Ability = 380, DeflectVirus1Ability = 378;
        public const UInt32 DeflectBullets1Fx = 0x28000824;  // FX_VIRUSCAST_SPLIT_DEFLECT_BULLETS_DEFLECTBULLETS1
        public const UInt32 DeflectVirus1Fx = 0x28000820;    // FX_VIRUSCAST_SPLIT_DEFLECT_VIRUS_DEFLECTVIRUS1

        /// <summary>Equipment slots Corruption can destroy: the weapon and the hacker tool (virii.py 46-63).</summary>
        public const int WeaponSlot = 109, HackerToolSlot = 110;

        /// <summary>
        /// GenericCorruption's roll (virii.py 68-71): random.randrange(1, 100) (1-99); above the chance it fails
        /// (deflected), otherwise the item is destroyed (<see cref="AbilityEffects.DestroyItem"/>).
        /// </summary>
        public static bool CorruptionLands(int roll, int chanceValue)
        {
            return roll <= chanceValue;
        }

        /// <summary>
        /// GenericCorruption's choice of item (virii.py 44-66): an NPC's weapon, or for a player one of their equipped
        /// weapon and hacker tool at random. Returns the slot, or null when there is nothing to destroy.
        /// </summary>
        public static int? CorruptionSlot(bool targetIsNpc, bool hasWeapon, bool hasHackerTool, Random random)
        {
            if (targetIsNpc)
            {
                return hasWeapon ? WeaponSlot : (int?)null;
            }
            List<int> slots = new List<int>();
            if (hasWeapon)
            {
                slots.Add(WeaponSlot);
            }
            if (hasHackerTool)
            {
                slots.Add(HackerToolSlot);
            }
            if (slots.Count == 0)
            {
                return null;
            }
            return slots[random.Next(slots.Count)];
        }
    }
}
