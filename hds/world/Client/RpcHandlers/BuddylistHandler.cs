using System;
using System.Collections;
using hds.shared;

namespace hds
{
    public class BuddylistHandler
    {
        private static string Prefix => "SOE+MXO+" + Store.worldConfig.serverName + "+";

        /// <summary>Client 80 d6 04 00 &lt;sized "SOE+MXO+&lt;server&gt;+handle"&gt;.</summary>
        public void ProcessAddFriend(ref byte[] packet)
        {
            string playerHandleWithPrefix;
            if (!ReadHandle(packet, out playerHandleWithPrefix))
            {
                return;
            }

            string playerHandleClean = playerHandleWithPrefix.Replace(Prefix, "");
            UInt32 myCharId = Store.currentClient.playerData.getCharID();
            UInt32 friendId = Store.dbManager.WorldDbHandler.GetCharIdByHandle(playerHandleClean);
            if (friendId == 0)
            {
                // mxopackets1_crashover2k_009_session_complete: 41 answered two of 29 adds (handles we take to be gone).
                SocialHandler.Send(Store.currentClient, SocialPackets.FriendListStatus(SocialPackets.FriendNotFound, playerHandleWithPrefix));
                return;
            }

            if (!Store.dbManager.WorldDbHandler.IsOnFriendList(myCharId, friendId))
            {
                Store.dbManager.WorldDbHandler.AddHandleToFriendList(playerHandleClean, myCharId);
            }

            // Same capture: 84 answered the other adds.
            SocialHandler.Send(Store.currentClient, SocialPackets.FriendListStatus(SocialPackets.FriendAdded, playerHandleWithPrefix));
            if (SocialHandler.FindOnlineByCharId(friendId) != null)
            {
                SocialHandler.Send(Store.currentClient, SocialPackets.FriendOnline(playerHandleWithPrefix));
            }
        }

        public void ProcessRemoveFriend(ref byte[] packet)
        {
            string playerHandleWithPrefix;
            if (!ReadHandle(packet, out playerHandleWithPrefix))
            {
                return;
            }

            string playerHandleClean = playerHandleWithPrefix.Replace(Prefix, "");
            Store.dbManager.WorldDbHandler.RemoveHandleFromFriendList(playerHandleClean,
                Store.currentClient.playerData.getCharID());
            ServerPackets packets = new ServerPackets();
            packets.SendRemoveFriend(Store.currentClient, playerHandleWithPrefix);
        }

        public void ProcessAnnounceFriendsOffline(UInt32 charId, string handle)
        {
            ArrayList friends = Store.dbManager.WorldDbHandler.FetchPlayersWhoAddedMeToBuddylist(charId);
            Store.world.SendRPCToPlayerList(friends, SocialPackets.FriendOffline(Prefix + handle));
        }

        public void ProcessAnnounceFriendOnline(uint charId, string handle)
        {
            ArrayList friends = Store.dbManager.WorldDbHandler.FetchPlayersWhoAddedMeToBuddylist(charId);
            Store.world.SendRPCToPlayerList(friends, SocialPackets.FriendOnline(Prefix + handle));
        }

        /// <summary>The handle sits at the offset in the first u16 (counted from the two-byte header).</summary>
        private static bool ReadHandle(byte[] packet, out string handle)
        {
            handle = null;
            return packet.Length >= 4 && SocialRequests.TryReadSized(packet, (packet[0] | (packet[1] << 8)) - 2, out handle);
        }
    }
}