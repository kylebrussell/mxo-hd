using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using hds.shared;

namespace hds
{
    public partial class ServerPackets 
    {
        public void sendWorldCMD(WorldClient client, uint districtId, string enviromentOptions)
        {
            // Prepare the dict
            Dictionary<uint, string> locs = new Dictionary<uint, string>();
            locs.Add((uint)MxOLocations.TUTORIAL, "resource/worlds/final_world/tutorial_v2/tutorial_v2.metr");
            locs.Add((uint)MxOLocations.SLUMS, "resource/worlds/final_world/slums_barrens_full.metr");
            locs.Add((uint)MxOLocations.DOWNTOWN, "resource/worlds/final_world/downtown/dt_world.metr");
            locs.Add((uint)MxOLocations.INTERNATIONAL, "resource/worlds/final_world/international/it.metr");
            locs.Add((uint)MxOLocations.ARCHIVE01, "resource/worlds/final_world/constructs/archive/archive01/archive01.metr");
            locs.Add((uint)MxOLocations.ARCHIVE02, "resource/worlds/final_world/constructs/archive/archive02/archive02.metr");
            locs.Add((uint)MxOLocations.ASHENCOURT, "resource/worlds/final_world/constructs/archive/archive_ashencourte/archive_ashencourte.metr");
            locs.Add((uint)MxOLocations.DATAMINE, "resource/worlds/final_world/constructs/archive/archive_datamine/datamine.metr");
            locs.Add((uint)MxOLocations.SAKURA, "resource/worlds/final_world/constructs/archive/archive_sakura/archive_sakura.metr");
            locs.Add((uint)MxOLocations.SATI, "resource/worlds/final_world/constructs/archive/archive_sati/sati.metr");
            locs.Add((uint)MxOLocations.WIDOWSMOOR, "resource/worlds/final_world/constructs/archive/archive_widowsmoor/archive_widowsmoor.metr");
            locs.Add((uint)MxOLocations.YUKI, "resource/worlds/final_world/constructs/archive/archive_yuki/archive_yuki.metr");
            locs.Add((uint)MxOLocations.LARGE01, "resource/worlds/final_world/constructs/large/large01/large01.metr");
            locs.Add((uint)MxOLocations.LARGE02, "resource/worlds/final_world/constructs/large/large02/large02.metr");
            locs.Add((uint)MxOLocations.MEDIUM01, "resource/worlds/final_world/constructs/medium/medium01/medium01.metr");
            locs.Add((uint)MxOLocations.MEDIUM02, "resource/worlds/final_world/constructs/medium/medium02/medium02.metr");
            locs.Add((uint)MxOLocations.MEDIUM03, "resource/worlds/final_world/constructs/medium/medium03/medium03.metr");
            locs.Add((uint)MxOLocations.SMALL03, "resource/worlds/final_world/constructs/small/small03/small03.metr");
            locs.Add((uint)MxOLocations.CAVES, "resource/worlds/final_world/zion_caves.metr");
            locs.Add((uint)MxOLocations.LA, "resource/worlds/loading_area/la.metr");

            string path = locs[districtId];
            UInt16 offsetWeatherEvent = (UInt16)(path.Length + 17);

            PacketContent pak = new PacketContent();
            pak.AddUShort((ushort)RPCResponseHeaders.SERVER_LOAD_WORLD_CMD);
            pak.AddUShort(0x0e); // dunno if header or not - just part of this
            pak.AddUShort(0);
            uint instance = districtId == 0 && client.playerData.tutorialConstructId != 0
                ? client.playerData.tutorialConstructId : PvpArchives.ConstructId(districtId);
            instance = HardlineRegistration.WorldConstructId(districtId, instance);
            pak.AddUint32(instance, 1); // native context: registered testworld ordinal, or the existing private/city fallback
            pak.AddByteArray(TimeUtils.getCurrentSimTime());
            pak.AddByte(0x01); // SimeTime + 01 
            pak.AddUint16(offsetWeatherEvent, 1);
            pak.AddSizedTerminatedString(path);
            pak.AddSizedTerminatedString(enviromentOptions);

            if (client.playerData.DistrictIsLA())
            {
                PacketContent laPak = new PacketContent();
                laPak.AddUint16(0x8109, 0);
                client.messageQueue.addRpcMessage(laPak.ReturnFinalPacket());
            }
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());

        }

        public void SendServerSettingUInt(WorldClient client, string key, UInt32 value)
        {
            client.messageQueue.addRpcMessage(BuildServerSettingUInt(key, value));
        }

        /// <summary>
        /// A numeric client setting: <c>3a 05 00 &lt;offset of the value u16&gt; &lt;key: length with the 0 u16, text, 0&gt;
        /// &lt;value: 04 00, u32&gt;</c>. Retail, at every world entry on its PvP server:
        /// <c>3a 05 00 11 00 0a 00 "PvPServer" 00 04 00 06 00 00 00</c> and
        /// <c>3a 05 00 17 00 10 00 "PvPMaxSafeLevel" 00 04 00 0f 00 00 00</c>.
        /// This used to write one byte of the header (05), a length in place of the offset and the key without its 0;
        /// nothing sent it with the default configuration.
        /// </summary>
        public static byte[] BuildServerSettingUInt(string key, UInt32 value)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("3a0500");
            pak.AddUint16((UInt16)(7 + key.Length + 1), 1);
            pak.AddSizedTerminatedString(key);
            pak.AddUint16(4, 1);
            pak.AddUint32(value, 1);
            return pak.ReturnFinalPacket();
        }
        
        public void SendServerSettingCheckMotdMessage(WorldClient client, string key)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((ushort)RPCResponseHeaders.SERVER_FEATURE_EVENT,0);
            int fullLen = key.Length + 2 + 4;
            pak.AddInt16((short)fullLen,1);
            pak.AddSizedString(key);
            pak.AddUint16(1,0);
            pak.AddUint16(1,0);

            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }
        
        public void SendServerSettingString(WorldClient client, string key, string value)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((ushort)RPCResponseHeaders.SERVER_FEATURE_EVENT,0);
            int fullLen = key.Length + value.Length;
            pak.AddInt16((short)fullLen,1);
            pak.AddSizedString(key);
            pak.AddSizedString(value);
            
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());
        }


        public void SendWorldSetup(WorldClient client)
        {
            // The Packet with PVP Flag etc. 
            // ToDo: load world setting and define it
            // The client's own PvP rule reads these two (client.dll 0x625ed830; docs/PVP.md). Retail sent PvPServer 6
            // on its PvP server and 2 elsewhere: with 0, the client's default, it ignores every player's PvP flag.
            // The safe level used to be sent as 16 whatever the configuration said.
            SendServerSettingUInt(client, "PvPServer", (UInt32)PvpFlag.ModeFor(client));
            SendServerSettingUInt(client, "PvPMaxSafeLevel", (UInt32)PvpFlag.MaxSafeLevel);

            if (Store.worldConfig.FixedBinkIDOverride > 0)
            {
                SendServerSettingUInt(client, "FixedBinkIDOverride", Store.worldConfig.FixedBinkIDOverride );
            }
            
            // Load Events
            if (Store.worldConfig.events.Count > 0)
            {
                foreach (DictionaryEntry eventItem in Store.worldConfig.events)
                {
                    SendServerSettingString(client, eventItem.Key.ToString(), eventItem.Value.ToString());
                }
            }

            SendServerSettingCheckMotdMessage(client, "CheckMotdTimestamp");

        }

        public void createFlashTraffic(WorldClient client, string url)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_FLASH_TRAFFIC,0);
            pak.AddHexBytes("0000070005");
            pak.AddSizedString(url);
            pak.AddByte(0);
            client.messageQueue.addRpcMessage(pak.ReturnFinalPacket());

        }
    }
}
