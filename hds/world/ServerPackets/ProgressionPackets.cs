using System;
using hds.shared;

namespace hds
{
    public partial class ServerPackets
    {
        /// <summary>
        /// LTRS_ABILITYLEVELUPINPROGRESS, 0x98000000 | 0x0c in the client's status table. Guess: the client has no closer
        /// status for a refused upgrade and normally blocks these itself, so no capture shows one.
        /// </summary>
        public const UInt32 StatusAbilityUpgradeRefused = 0x9800000c;

        /// <summary>Reason on the 80 e7 / 80 e4 cash messages of an ability upgrade.</summary>
        public const UInt32 InfoReasonAbilityUpgrade = 0x11;

        /// <summary>
        /// 80 e5: the experience total, with the gain's type. Retail right after 80 e6:
        /// <c>80 e6 87 01 00 00 01 00 00 00</c>, <c>80 e5 7c 03 00 00 01 00 00 00</c>.
        /// </summary>
        public static byte[] BuildEXPTotal(UInt32 total, byte gainType)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_PLAYER_EXP, 0);
            pak.AddUint32(total, 1);
            pak.AddUint32(gainType, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Own view at a level-up. Retail (uriah tutorial, without its MissionKey):
        /// <c>02 00 02 80 80 83 c8 00 02 d4 67 00 67 00 c8 00 30 b9 05 00 28 01</c> =
        /// MaxHealth, Level, InnerStrengthMax, InnerStrengthAvailable, Health, EffectID, EffectCounter.
        /// </summary>
        public static byte[] BuildLevelUpUpdate(byte level, UInt16 maxHealth, UInt16 maxInnerStrength,
            UInt16 innerStrength, UInt16 health, byte effectCounter)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByteArray(new byte[] { 0x02, 0x80, 0x80, 0x83 });
            pak.AddUint16(maxHealth, 1);
            pak.AddByte(level);
            pak.AddByte(0xd4);
            pak.AddUint16(maxInnerStrength, 1);
            pak.AddUint16(innerStrength, 1);
            pak.AddUint16(health, 1);
            pak.AddByte(0x30);
            pak.AddUint32(PlayerProgression.LevelUpEffect, 1);
            pak.AddByte(effectCounter);
            return pak.ReturnFinalPacket();
        }

        /// <summary>The system line retail sent with every level-up (ID_LEVEL_UP_FEEDBACK), byte for byte.</summary>
        public static byte[] BuildLevelUpMessage()
        {
            return StringUtils.hexStringToBytes("2e1700000000000000000000008900006000000000000000000000000000000000000000");
        }

        /// <summary>A bought ability in the bag: <c>5e 58 00 2e 00 00 00 01 08 00 80 01</c> (retail, Awakened in slot 0x58).</summary>
        public static byte[] BuildAbilityCodeAdd(UInt16 slot, UInt32 abilityGoid)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5e);
            pak.AddUint16(slot, 1);
            pak.AddUint32(AbilityProgression.AbilityCodeItem, 1);
            pak.AddUint32(AbilityProgression.AbilityCodeValue(abilityGoid), 1);
            pak.AddByte(0x01);
            return pak.ReturnFinalPacket();
        }

        /// <summary>The 81 0f answer to buying an ability: <c>81 0f 2e 00 00 00 01 08 00 80 64 00 00 00 00</c> (retail, 100 info).</summary>
        public static byte[] BuildAbilityCodeBuyAck(UInt32 abilityGoid, UInt32 price)
        {
            return BuildVendorBuyAck(AbilityProgression.AbilityCodeItem,
                NumericalUtils.uint32ToByteArray(AbilityProgression.AbilityCodeValue(abilityGoid), 1), price);
        }

        /// <summary>
        /// A new entry in the known-abilities list, which also holds code storage: <c>5e slot u16, 00 00 00 00, value u32, 00</c>.
        /// No capture uploads an ability, so this follows the code storage records of the same list.
        /// </summary>
        public static byte[] BuildKnownAbilityAdd(UInt16 slot, UInt32 value)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5e);
            pak.AddUint16(slot, 1);
            pak.AddUint32(0, 1);
            pak.AddUint32(value, 1);
            pak.AddByte(0x00);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 b2: ability u16, level u16, slot u16. Retail: <c>80 b2 74 02 32 00 89 01</c>.</summary>
        public static byte[] BuildAbilityLoad(UInt16 ability, UInt16 level, UInt16 slot)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_ABILITY_LOAD, 0);
            pak.AddUint16(ability, 1);
            pak.AddUint16(level, 1);
            pak.AddUint16(slot, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 b3: ability u16. Retail: <c>80 b3 74 02</c>.</summary>
        public static byte[] BuildAbilityUnload(UInt16 ability)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_ABILITY_UNLOAD, 0);
            pak.AddUint16(ability, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>80 b8: ability u16, new level u16, four zeros. Retail: <c>80 b8 02 00 02 00 00 00 00 00</c>.</summary>
        public static byte[] BuildAbilityUpgraded(UInt16 ability, UInt16 level, UInt32 status = 0)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_PLAYER_UPGRADE_ABILITY, 0);
            pak.AddUint16(ability, 1);
            pak.AddUint16(level, 1);
            pak.AddUint32(status, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 80 b6 answers an attribute raise with the new value: 08 00, four zeros, count u16, then 00, attribute u32,
        /// value u8. Retail: <c>80 b6 08 00 00 00 00 00 01 00 00 51 00 00 00 09</c>.
        /// </summary>
        public static byte[] BuildAttributeRaised(byte attribute, byte value)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_PLAYER_UPGRADE_ATTRIBUTE, 0);
            pak.AddUint16(8, 1);
            pak.AddUint32(0, 1);
            pak.AddUint16(1, 1);
            pak.AddByte(0);
            pak.AddUint32(attribute, 1);
            pak.AddByte(value);
            return pak.ReturnFinalPacket();
        }
    }
}
