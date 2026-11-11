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
                else if (ChatRoomHandler.TrySendRoomLine(Store.currentClient, packetData, text))
                {
                    // /room <room> <text>
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
            // 29 07 00 2e 00 00 <counter> 25 00 "SOE+MXO+Vector-Hostile+AfterWhoruNeo" 23 00 <text>
            // (mxopackets1_entilsar_enti_lastday2 21:45:22; the counter went 00, 01, 02 ... per tell).
            string receiverHandleServerString;
            string receiverMessage;
            if (rpcData.Length < 6 ||
                !SocialRequests.TryReadSized(rpcData, (rpcData[0] | (rpcData[1] << 8)) - 1, out receiverHandleServerString) ||
                !SocialRequests.TryReadSized(rpcData, (rpcData[2] | (rpcData[3] << 8)) - 1, out receiverMessage))
            {
                return;
            }

            byte chatCounter = rpcData[5];
            // The handle carries the "SOE+MXO+<server>+" prefix; whatever the server name, the handle follows the last '+'.
            string cleanHandle = receiverHandleServerString.Substring(receiverHandleServerString.LastIndexOf('+') + 1);
            WorldClient receiver = SocialHandler.FindOnlineByHandle(cleanHandle);
            if (receiver != null)
            {
                string sender = ChatAddress.Of(SocialHandler.HandleOf(Store.currentClient));
                SocialHandler.Send(receiver, SocialPackets.ChatMessage(SocialPackets.ChatTell, 0, sender, receiverMessage));
            }

            // Retail answered every tell with 2c: delivered, or 41 when nobody had that handle.
            SocialHandler.Send(Store.currentClient, SocialPackets.TellResult(chatCounter, receiver != null));
        }
    }
}
