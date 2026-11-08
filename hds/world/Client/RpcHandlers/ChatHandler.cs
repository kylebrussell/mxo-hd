using System;
using System.Collections.Generic;
using System.Text;
using hds.shared;

namespace hds
{
    public class ChatHandler
    {

        private ChatCommandsHelper chatCommands;


        public void processChat(ref byte[] packetData)
        {

            //10 00 08 00 00 00 00 06 00 3f 53 61 76 65 00
            // The text length is a u16 (it used to be read as one byte, which broke lines over 255 characters).
            SocialRequests.ChatLine line;
            if (!SocialRequests.TryParseChat(packetData, out line) || string.IsNullOrEmpty(line.Text))
            {
                return;
            }

            UInt16 typeId = line.Channel;
            chatCommands = new ChatCommandsHelper();
            {
                string text = line.Text;

                if (text[0] == '?')
                {
                    //Maybe a parameter			
                    chatCommands.parseCommand(text); // Parse for commands
                }
                else if (SocialHandler.TrySendGroupChat(Store.currentClient, line.Channel, text))
                {
                    // Crew, faction or team channel.
                }
                else
                {
                    // Not a Param - lets distribute the Message throw our Area
                    ServerPackets pak = new ServerPackets();
                    pak.SendChatMessage(Store.currentClient, text, Store.currentClient.playerData.getCharID(), StringUtils.charBytesToString_NZ(Store.currentClient.playerInstance.CharacterName.getValue()), typeId);
                    // ToDo: Send the ChatMessage to the Scope of Players

                    
                }
            }

        }

        public void ProcessWhisperPlayer(ref byte[] rpcData)
        {
            PacketReader reader = new PacketReader(rpcData);
            // We read the offset but we dont need it really
            UInt16 offsetName = reader.ReadUInt16(1);
            UInt16 offsetMessage = reader.ReadUInt16(1);
            uint unknownZeroShort = reader.ReadUint8();
            uint chatCounter = reader.ReadUint8(); // ChatCounter (increment by 1 each message)
            string receiverHandleServerString = reader.ReadSizedZeroTerminatedString();
            string receiverMessage = reader.ReadSizedZeroTerminatedString();

            string cleanHandle = receiverHandleServerString.Replace("SOE+MXO+" + Store.worldConfig.serverName +"+", "");
            
            ServerPackets packets = new ServerPackets();
            packets.SendWhisperMesage(Store.currentClient, cleanHandle, receiverMessage);
        }
    }
}
