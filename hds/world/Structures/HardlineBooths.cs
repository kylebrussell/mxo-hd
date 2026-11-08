using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Hardline phone booths as retail served them. Retail spawned every booth near the player as a view of class
    /// 592 (Hardline_PhoneBooth) tied to its static object, with only a Position, 15 units below the static
    /// object (e.g. 08 50 02 ea 01 30 39 da cd ab 01 01 &lt;17000, 530, 2200&gt; for static 0x393001ea at y 545,
    /// RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline packet 52; the same -15 for 25 booths).
    /// The client then used the booth through 80 c7 &lt;view&gt; &lt;spawn&gt; &lt;action&gt; 00:
    ///   03 jack out here     → 80 fb &lt;hardline u32&gt; 00, client 80 fc 00, server 80 fd 00 00 00 00 00 00 at once
    ///   05 exit to the LA    → 80 fb &lt;hardline u32&gt; 01, client 80 fc 01, server 80 fd 00 00 00 00 01 00
    ///   04 upload            → 81 18 00 00 0d 00 00 98 (also seen as static 80 c8 &lt;booth&gt; 04 00)
    /// (18 hardline prompts across the captures; e.g. crashover2k proxy_second_pc_001_exit_to_loadingarea,
    /// crashover2k_008_some_pvp_and_teleports for the upload.)
    /// </summary>
    public static class HardlineBooths
    {
        public const UInt16 PhoneBoothType = 592;
        public const double ViewYOffset = -15.0;

        public const byte ActionJackOut = 0x03;
        public const byte ActionUpload = 0x04;
        public const byte ActionExitToLoadingArea = 0x05;

        public static bool IsBooth(UInt32 typeId)
        {
            return typeId == PhoneBoothType;
        }

        /// <summary>The booth as a spawnable object: Position only, related to its static object.</summary>
        public static GameObject BuildBoothObject(StaticWorldObject booth)
        {
            AttributeClass3632 obj = new AttributeClass3632("Hardline_PhoneBooth", PhoneBoothType);
            obj.SetRelatedStaticObjId(booth.mxoStaticId);
            obj.DisableAllAttributes();
            obj.Position.enable();
            obj.Position.setValue(NumericalUtils.doublesToLtVector3d(booth.pos_x, booth.pos_y + ViewYOffset, booth.pos_z));
            return obj;
        }

        /// <summary>80 fb &lt;hardline u32&gt; &lt;00 jack out | 01 exit to the Loading Area&gt;: the confirmation prompt.</summary>
        public static byte[] BuildExitPrompt(UInt16 hardline, bool toLoadingArea)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_EXIT_HL, 0);
            pak.AddUint32(hardline, 1);
            pak.AddByte(toLoadingArea ? (byte)0x01 : (byte)0x00);
            return pak.ReturnFinalPacket();
        }

        /// <summary>81 18 00 00 0d 00 00 98: the answer to uploading a hardline, byte-identical in all 23 captures.</summary>
        public static byte[] BuildUploadDone()
        {
            return new byte[] { 0x81, 0x18, 0x00, 0x00, 0x0d, 0x00, 0x00, 0x98 };
        }

        /// <summary>
        /// 81 18 after an upload that moved items: SingleItemUploaded u8, AbilityCodeUploaded u8, Status u32 (field names
        /// from the client's RPC printer). Retail sent 01 for one item and 00 for several. The ability flag is a guess
        /// from its name: no capture uploads an ability.
        /// </summary>
        public static byte[] BuildItemsUploaded(int items, bool abilityCode)
        {
            return new byte[] { 0x81, 0x18, (byte)(items == 1 ? 1 : 0), (byte)(abilityCode ? 1 : 0), 0, 0, 0, 0 };
        }

        /// <summary>Entity id of a booth view, the same metrId + mxoStaticId scheme doors and subways use.</summary>
        public static UInt64 EntityId(StaticWorldObject booth)
        {
            return UInt64.Parse("" + booth.metrId + booth.mxoStaticId);
        }
    }
}
