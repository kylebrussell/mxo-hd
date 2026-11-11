using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Attribute updates of a PlayerCharacter view (update part 02): group mask bytes of seven attributes each, 0x80 on
    /// every mask byte but the last, each followed by its group's values in index order. A player's own view (view 2) and
    /// other players' views of them number the attributes differently (Object12: attributesSelfView, and the second
    /// AddAttribute index). Retail examples, both from the PvP captures (see docs/PVP.md):
    /// own death <c>02 80 80 80 c1 01 00 00 80 80 01 00 00 00 01</c> (IsDead 21, Health 27, ConditionStateFlags 42);
    /// another player's health <c>02 80 80 10 0e 06</c> (Health 18).
    /// </summary>
    public static class PlayerViewAttributes
    {
        // Own view (Object12.setSelfViewAttributes)
        public const int SelfOrganizationId = 8, SelfMaxHealth = 14, SelfIsDuelDeath = 20, SelfIsDead = 21, SelfInnerStrength = 25, SelfHealth = 27,
            SelfDuelId = 35, SelfDeathPenalty = 38, SelfConditionStateFlags = 42, SelfCombatantMode = 43;

        // Another player's view (Object12 AddAttribute's second index)
        public const int OtherOrganizationId = 4, OtherMaxHealth = 8, OtherIsDuelDeath = 14, OtherIsDead = 15, OtherHealth = 18, OtherEffectId = 23,
            OtherEffectCounter = 24, OtherDuelId = 26, OtherConditionStateFlags = 30, OtherCombatantMode = 31;

        public static byte[] U8(byte value) => new[] { value };
        public static byte[] U16(UInt16 value) => new[] { (byte)value, (byte)(value >> 8) };
        public static byte[] U32(UInt32 value) => new[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };
        public static byte[] F32(float value) => BitConverter.GetBytes(value);

        /// <summary>Update part 02 with the given attributes (index, value bytes), in any order.</summary>
        public static byte[] Build(params (int Index, byte[] Value)[] attributes)
        {
            if (attributes.Length == 0)
            {
                throw new ArgumentException("An attribute update needs an attribute");
            }
            List<(int Index, byte[] Value)> sorted = attributes.OrderBy(a => a.Index).ToList();
            int lastGroup = sorted[sorted.Count - 1].Index / 7;
            List<byte> result = new List<byte> { 0x02 };
            for (int group = 0; group <= lastGroup; group++)
            {
                byte mask = group < lastGroup ? (byte)0x80 : (byte)0x00;
                List<(int Index, byte[] Value)> inGroup = sorted.Where(a => a.Index / 7 == group).ToList();
                foreach ((int index, _) in inGroup)
                {
                    mask |= (byte)(1 << (index % 7));
                }
                result.Add(mask);
                foreach ((_, byte[] value) in inGroup)
                {
                    result.AddRange(value);
                }
            }
            return result.ToArray();
        }

        /// <summary>The same for the player's own view: <c>02 00</c> (view 2) in front.</summary>
        public static byte[] BuildSelf(params (int Index, byte[] Value)[] attributes)
        {
            byte[] update = Build(attributes);
            byte[] result = new byte[update.Length + 2];
            result[0] = 0x02;
            Array.Copy(update, 0, result, 2, update.Length);
            return result;
        }
    }
}
