using System;
using System.Collections;
using System.Data.Common;
using hds.shared;

namespace hds
{
    public class BuddylistHandler
    {
        private static string Prefix => ChatAddress.Prefix;

        /// <summary>Client 80 d6 04 00 &lt;sized "SOE+MXO+&lt;server&gt;+handle"&gt;.</summary>
        public void ProcessAddFriend(ref byte[] packet)
        {
            EditFriend(Store.currentClient, packet, remove: false);
        }

        public void ProcessRemoveFriend(ref byte[] packet)
        {
            EditFriend(Store.currentClient, packet, remove: true);
        }

        private static void EditFriend(WorldClient client, byte[] packet, bool remove)
        {
            if (!ReadHandle(packet, out string address)) return;

            var db = Store.dbManager.WorldDbHandler;
            uint owner = client.playerData.getCharID();
            uint friendId;
            try
            {
                string handle = address.Replace(Prefix, "");
                friendId = db.GetCharIdByHandle(handle);
                if (friendId != 0)
                {
                    if (remove) db.RemoveFriend(owner, friendId);
                    else db.AddFriend(owner, friendId);
                }
            }
            catch (DbException error)
            {
                Output.WriteDebugLog($"[BUDDY] Friend list edit could not be saved char={owner} remove={remove}: {error.Message}");
                SocialHandler.Send(client, SocialPackets.FriendListResult(remove, address, SocialPackets.FriendSaveFailed));
                return;
            }

            // An already absent entry can be removed again to clear a stale client row.
            uint status = friendId != 0 || remove
                ? SocialPackets.FriendSuccess
                : SocialPackets.FriendMissing;
            SocialHandler.Send(client, SocialPackets.FriendListResult(remove, address, status));
            if (!remove && friendId != 0 && SocialHandler.FindOnlineByCharId(friendId) != null)
                SocialHandler.Send(client, SocialPackets.FriendOnline(address));
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
