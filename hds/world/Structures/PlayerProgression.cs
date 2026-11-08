using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Character progression rules, taken from client.dll and the client's rule scripts (resource/Python/Monolith.ltp,
    /// rshooks.GoUpALevel) and checked against retail captures (luizoe Recursion and Unknown, crashover2k uriah
    /// create-and-tutorial, the Vector ability-upgrade logs).
    /// </summary>
    public static class PlayerProgression
    {
        public const int MaxLevel = 50;

        /// <summary>
        /// The total experience of a level, as the client computes it to show the level at login (client.dll 0x625d8590,
        /// 0x625d8610). The base adds 100 x k^2 per level up to 10 and 1000 x k after that; the result is
        /// base x ((XPConstantMultiplier x (level - 1) - 1) x XPKnob + 1), with the client's defaults 5 and 1.
        /// Retail agrees: Recursion reached level 3 between 4,318 and 9,818 (5,000), level 4 between 11,665 and 22,665
        /// (21,000) and level 5 between 30,274 and 63,274 (60,000). rshooks.level_exp_reqs (4,500, 16,000, 39,000...) is
        /// not this table: the client still showed level 4 at 40,000.
        /// </summary>
        public static long ExperienceForLevel(int level)
        {
            long total = 0;
            for (int k = 1; k < level; k++)
            {
                total += k < 10 ? 100L * k * k : 1000L * k;
            }
            return total * 5 * Math.Max(0, level - 1);
        }

        /// <summary>FX_CHARACTER_LEVEL_UP, the EffectID retail set on the own view at a level-up.</summary>
        public const UInt32 LevelUpEffect = 0x280005b9;

        /// <summary>Experience never takes a level away, so a character whose level is already higher keeps it.</summary>
        public static int LevelForExperience(int currentLevel, long experience)
        {
            int level = Math.Max(1, currentLevel);
            while (level < MaxLevel && experience >= ExperienceForLevel(level + 1))
            {
                level++;
            }
            return level;
        }

        /// <summary>Retail own-view MaxHealth: 150 at level 1 (tutorial), then 200, 250, 300, 350 at levels 2-5.</summary>
        public static UInt16 MaxHealthAtLevel(int level)
        {
            return (UInt16)(100 + 50 * level);
        }

        /// <summary>Retail own-view InnerStrengthMax: 100 at level 1 (tutorial), then 103, 106, 109, 112 at levels 2-5.</summary>
        public static UInt16 MaxInnerStrengthAtLevel(int level)
        {
            return (UInt16)(97 + 3 * level);
        }

        /// <summary>
        /// Raises the level if the experience total calls for it: own-view stats and effect, the feedback line, the
        /// update other players see, and the database. As in GoUpALevel, Health and InnerStrengthAvailable grow by what
        /// their maximum grew by. Maxima grow by the retail step rather than being set outright, so characters saved with
        /// other maxima (the old fixed 500/200 rows) keep their margin instead of dropping.
        /// </summary>
        public static void ApplyLevelUps(WorldClient client)
        {
            int oldLevel = client.playerInstance.Level.getValue()[0];
            int newLevel = LevelForExperience(oldLevel, client.playerData.getExperience());
            if (newLevel <= oldLevel)
            {
                return;
            }

            UInt16 healthGain = (UInt16)(MaxHealthAtLevel(newLevel) - MaxHealthAtLevel(oldLevel));
            UInt16 innerStrengthGain = (UInt16)(MaxInnerStrengthAtLevel(newLevel) - MaxInnerStrengthAtLevel(oldLevel));

            UInt16 maxHealth = (UInt16)(NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1) + healthGain);
            UInt16 health = (UInt16)(NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1) + healthGain);
            UInt16 maxInnerStrength = (UInt16)(NumericalUtils.ByteArrayToUint16(client.playerInstance.InnerStrengthMax.getValue(), 1) + innerStrengthGain);
            UInt16 innerStrength = (UInt16)(NumericalUtils.ByteArrayToUint16(client.playerInstance.InnerStrengthAvailable.getValue(), 1) + innerStrengthGain);
            byte effectCounter = (byte)(client.playerInstance.EffectCounter.getValue()[0] + 1);

            client.playerInstance.Level.setValue((byte)newLevel);
            client.playerInstance.MaxHealth.setValue(maxHealth);
            client.playerInstance.Health.setValue(health);
            client.playerInstance.InnerStrengthMax.setValue(maxInnerStrength);
            client.playerInstance.InnerStrengthAvailable.setValue(innerStrength);
            client.playerInstance.EffectID.setValue(LevelUpEffect);
            client.playerInstance.EffectCounter.setValue(effectCounter);

            Store.dbManager.WorldDbHandler.SaveLevel(client.playerData.getCharID(), newLevel, health, maxHealth,
                innerStrength, maxInnerStrength);

            client.messageQueue.addObjectMessage(ServerPackets.BuildLevelUpUpdate((byte)newLevel, maxHealth,
                maxInnerStrength, innerStrength, health, effectCounter), false);
            client.messageQueue.addRpcMessage(ServerPackets.BuildLevelUpMessage());
            client.FlushQueue();

            AnnounceToOthers(client, new List<Attribute>
            {
                client.playerInstance.MaxHealth, client.playerInstance.Level, client.playerInstance.Health,
                client.playerInstance.EffectID, client.playerInstance.EffectCounter
            });

            Output.WriteDebugLog("[LEVEL] Character " + client.playerData.getCharID() + " is now level " + newLevel +
                                 " (" + client.playerData.getExperience() + " experience)");
        }

        /// <summary>Sends changed attributes to the players who can see this player, in the other-view attribute order.</summary>
        public static void AnnounceToOthers(WorldClient client, List<Attribute> changed)
        {
            byte[] update = SocialPackets.OtherViewUpdate(client.playerInstance.GetUpdateAttributes(changed));
            Store.world.SendViewPacketToAllPlayers(update, client.playerData.getCharID(),
                NumericalUtils.ByteArrayToUint16(client.playerInstance.GetGoid(), 1), client.playerData.getEntityId());
        }
    }

    /// <summary>
    /// Ability upgrades and loadouts. Abilities are upgraded with Info, 200 x (level squared) for each new level, which
    /// matches every upgrade in the captures: 800 for level 2, 5,000 for level 5, 245,000 for level 35, 8,584,800 for
    /// levels 2 to 50 in one go. There is no ability-point currency in retail 7.6005.
    /// </summary>
    public static class AbilityProgression
    {
        public const UInt16 AwakenedAbility = 2;
        public const UInt16 SelfDefenseAbility = 17;

        /// <summary>WriteCode and HackerCombatStyle cost half: retail charged 2,900 instead of 5,800 for levels 2 to 4.</summary>
        private static readonly HashSet<UInt16> HalfPrice = new HashSet<UInt16> { 37, 1006 };

        /// <summary>
        /// Abilities that come with a hidden second one (80 b2 ... 08 02 next to the load, 80 b3 next to the unload).
        /// Retail: WriteCode -> Write Code (970), Compile -> Compile Activate (969), Awakened -> Self Defense (17),
        /// which also carries Awakened's level. Only abilities that showed this when loaded on their own are listed.
        /// </summary>
        private static readonly Dictionary<UInt16, UInt16> Grants = new Dictionary<UInt16, UInt16>
        {
            { AwakenedAbility, SelfDefenseAbility },
            { 37, 970 },
            { 277, 969 }
        };

        /// <summary>The slot retail gave hidden abilities and attributes in 80 b2 (bytes 08 02).</summary>
        public const UInt16 HiddenSlot = 0x0208;

        /// <summary>
        /// The bag item of a bought ability. Retail, buying Awakened (81 0e 00 08 00 80 00): the bag got item 46 with the
        /// ability's value, 0x80000801, as its descriptor: <c>5e 58 00 2e 00 00 00 01 08 00 80 01</c>.
        /// </summary>
        public const UInt32 AbilityCodeItem = 46;

        /// <summary>Ability goids have the top bit set and sell on vendors like items (vendor_items.csv).</summary>
        public static bool IsAbilityCode(UInt32 goid)
        {
            return (goid & 0x80000000) != 0;
        }

        /// <summary>The value at level 1, in the known-abilities packing (0x80000000 | id &lt;&lt; 10 | level).</summary>
        public static UInt32 AbilityCodeValue(UInt32 goid)
        {
            return goid | 1;
        }

        public static UInt32 LevelPrice(int level)
        {
            return (UInt32)(200 * level * level);
        }

        public static UInt32 UpgradeCost(UInt16 ability, int fromLevel, int levels)
        {
            UInt32 total = 0;
            for (int level = fromLevel + 1; level <= fromLevel + levels; level++)
            {
                total += LevelPrice(level);
            }
            return HalfPrice.Contains(ability) ? total / 2 : total;
        }

        public static UInt16? GrantOf(UInt16 ability)
        {
            return Grants.TryGetValue(ability, out UInt16 grant) ? grant : (UInt16?)null;
        }

        /// <summary>Only Awakened's hidden ability follows its level; the others stayed at 0 in retail.</summary>
        public static UInt16 GrantLevel(UInt16 ability, UInt16 abilityLevel)
        {
            return ability == AwakenedAbility ? abilityLevel : (UInt16)0;
        }

        public struct UpgradeRequest
        {
            public UInt16 Ability;
            public UInt16 Slot;
            public byte Levels;
        }

        /// <summary>Client 80 b7: ability u16, slot u16, levels to add u8 (retail <c>80 b7 dd 01 37 01 01</c>).</summary>
        public static UpgradeRequest? ParseUpgrade(byte[] rpcData)
        {
            if (rpcData == null || rpcData.Length < 5)
            {
                return null;
            }

            return new UpgradeRequest
            {
                Ability = (UInt16)(rpcData[0] | rpcData[1] << 8),
                Slot = (UInt16)(rpcData[2] | rpcData[3] << 8),
                Levels = rpcData[4]
            };
        }

        public struct LoadoutEntry
        {
            public UInt16 Slot;
            public UInt16 Ability;
            public UInt16 Level;
        }

        /// <summary>
        /// Client 80 ae: object u32, unload flag u16 (0a 00 to unload), load flag u16 (0a 00 to load), count u16, then per
        /// ability 00, slot u16, ability u32, level u16. Retail unload of Force Enhancer's neighbour:
        /// <c>80 ae 00000000 0a00 0000 0100 00 8901 74020000 3200</c>.
        /// </summary>
        public static bool TryParseLoadout(byte[] rpcData, out bool unload, out List<LoadoutEntry> entries)
        {
            unload = false;
            entries = new List<LoadoutEntry>();
            if (rpcData == null || rpcData.Length < 10)
            {
                return false;
            }

            unload = (rpcData[4] | rpcData[5] << 8) != 0;
            int count = rpcData[8] | rpcData[9] << 8;
            const int entrySize = 9;
            for (int i = 0; i < count; i++)
            {
                int p = 10 + i * entrySize;
                if (p + entrySize > rpcData.Length)
                {
                    return false;
                }

                entries.Add(new LoadoutEntry
                {
                    Slot = (UInt16)(rpcData[p + 1] | rpcData[p + 2] << 8),
                    Ability = (UInt16)(rpcData[p + 3] | rpcData[p + 4] << 8),
                    Level = (UInt16)(rpcData[p + 7] | rpcData[p + 8] << 8)
                });
            }
            return true;
        }
    }

    /// <summary>
    /// The five attributes of the attribute level-up panel. In retail they are hidden abilities in 80 b2 (slot 08 02).
    /// They are kept as char_abilities rows in slots from <see cref="FirstSlot"/> up, which the margin does not list.
    /// </summary>
    public static class CharacterAttributes
    {
        public const byte Focus = 0x4e;
        public const byte Perception = 0x4f;
        public const byte Reason = 0x51;
        public const byte Belief = 0x52;
        public const byte Vitality = 0x54;

        public static readonly byte[] All = { Focus, Perception, Reason, Belief, Vitality };

        /// <summary>First char_abilities slot that is not an ability. Retail ability slots stay well below it.</summary>
        public const int FirstSlot = 0x0208;

        /// <summary>Ability/StartingValue of all five attributes in gameobjects.pkb.</summary>
        public const UInt16 StartingValue = 8;

        /// <summary>Ability/AbilityLevelCap of all five attributes in gameobjects.pkb.</summary>
        public const UInt16 Cap = 30;

        /// <summary>
        /// A new character's shown value: the starting value with the archetype's shift. Retail shifted one attribute up 3
        /// and one down 3. These are the shifts of the character block the margin sends everyone (Trinity's): the real
        /// client offers level - 1 points with exactly these values, and fewer with 8 in all five. Which field of the block
        /// carries the archetype is not known, so all characters share this one.
        /// </summary>
        public static UInt16 BaseValue(byte attribute)
        {
            switch (attribute)
            {
                case Perception: return 11;
                case Belief: return 5;
                default: return StartingValue;
            }
        }

        public static UInt16 SpentPoints(byte attribute, UInt16 value)
        {
            UInt16 baseValue = BaseValue(attribute);
            return value > baseValue ? (UInt16)(value - baseValue) : (UInt16)0;
        }

        /// <summary>
        /// The NewLevel of 80 b6 is the starting value plus points spent, without the shift: retail answered 9 to the first
        /// point in an attribute shown as 5 or 11, and the client adds the shift back (it showed 3 when told 6 for Belief).
        /// </summary>
        public static byte NewLevelOf(byte attribute, UInt16 value)
        {
            return (byte)(StartingValue + SpentPoints(attribute, value));
        }

        /// <summary>The attribute's shown value, from its saved row or the old fixed value.</summary>
        public static UInt16 ValueOf(Dictionary<byte, UInt16> stored, byte attribute)
        {
            return stored.TryGetValue(attribute, out UInt16 value) ? value : LegacyValue(attribute);
        }

        /// <summary>
        /// Whether a raise fits: the client offers level - 1 points in total (client.dll 0x625c5320) and an attribute stops
        /// at its cap. The client checks this too; this keeps a forged request from buying attributes.
        /// </summary>
        public static bool CanRaise(Dictionary<byte, UInt16> stored, byte attribute, int points, int characterLevel)
        {
            int spent = 0;
            foreach (byte each in All)
            {
                spent += SpentPoints(each, ValueOf(stored, each));
            }
            return points > 0 && spent + points <= characterLevel - 1 && ValueOf(stored, attribute) + points <= Cap;
        }

        public static UInt16 SlotOf(byte attribute)
        {
            return (UInt16)(FirstSlot + attribute);
        }

        public static Int32 RowValueOf(byte attribute)
        {
            return unchecked((Int32)(0x80000000u | (UInt32)attribute << 10));
        }

        /// <summary>Rows that are not abilities: the margin must leave them out of the known-abilities list.</summary>
        public static bool IsHiddenSlot(int slot)
        {
            return slot >= FirstSlot;
        }

        /// <summary>The value characters without attribute rows (from before these were saved) always got at login.</summary>
        public static UInt16 LegacyValue(byte attribute)
        {
            switch (attribute)
            {
                case Focus: return 24;
                case Perception: return 27;
                case Reason: return 28;
                case Belief: return 25;
                default: return 26;
            }
        }

        /// <summary>The fixed list of spent points the margin sent for every character before attributes were saved.</summary>
        private static readonly byte[] LegacySpentPoints = StringUtils.hexStringToBytes("03510000000400520000000b00540000000100");

        /// <summary>
        /// The end of the margin's character block: count u8, then attribute u32 and points spent u16 for each raised
        /// attribute. The client takes the points it offers from this. Retail, level 17 Trinity with base 8/5/8:
        /// <c>03 51000000 0400 52000000 0b00 54000000 0100</c> (Reason 12, Belief 16, Vitality 9).
        /// </summary>
        public static byte[] BuildSpentPoints(Dictionary<byte, UInt16> values)
        {
            if (values.Count == 0)
            {
                return LegacySpentPoints;
            }

            List<byte> list = new List<byte> { 0 };
            foreach (byte attribute in All)
            {
                if (values.TryGetValue(attribute, out UInt16 value) && SpentPoints(attribute, value) > 0)
                {
                    list[0]++;
                    list.AddRange(new byte[] { attribute, 0, 0, 0 });
                    UInt16 spent = SpentPoints(attribute, value);
                    list.Add((byte)spent);
                    list.Add((byte)(spent >> 8));
                }
            }
            return list.ToArray();
        }

        public struct Raise
        {
            public byte Attribute;
            public byte Points;
        }

        /// <summary>
        /// Client 80 b5: 04 00, count u16, then per attribute 00, attribute u32, points u8
        /// (retail <c>80 b5 04 00 01 00 00 51 00 00 00 01</c>).
        /// </summary>
        public static List<Raise> ParseRaises(byte[] rpcData)
        {
            List<Raise> raises = new List<Raise>();
            if (rpcData == null || rpcData.Length < 4)
            {
                return raises;
            }

            int count = rpcData[2] | rpcData[3] << 8;
            const int entrySize = 6;
            for (int i = 0; i < count && 4 + (i + 1) * entrySize <= rpcData.Length; i++)
            {
                int p = 4 + i * entrySize;
                raises.Add(new Raise { Attribute = rpcData[p + 1], Points = rpcData[p + 5] });
            }
            return raises;
        }
    }
}
