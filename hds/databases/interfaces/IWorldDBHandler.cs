using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using hds.world.Structures;

namespace hds.databases.interfaces{
    
    public interface IWorldDBHandler
    {
        UInt32 GetCharIdByHandle(string handle);
        Hashtable GetCharInfo (UInt32 charId);

        void SetReputation(UInt32 charId, string reputationColumn, int value);
        void SetOrgId(UInt32 charId, int orgId);
        Hashtable GetCharInfoByHandle(string handle);
        UInt32 GetUserIdForCharId(byte[] charIdHex);
        string GetPathForDistrictKey(string key);
        void AddHandleToFriendList(string handleToAdd, UInt32 charId);
        void RemoveHandleFromFriendList(string handleToRemove, UInt32 charId);
        ArrayList FetchPlayersWhoAddedMeToBuddylist(UInt32 charId);
        ArrayList FetchFriendList(UInt32 charId);
        Faction FetchFaction(UInt32 factionId);
        bool IsFactionnameAvailable(string factionname);
        bool IsHandleCaptainOfACrew(string handle);
        UInt32 createFaction(string factionname, Crew masterCrew, Crew secondCrew);
        void IncreaseCrewMoney(UInt32 crewId, UInt32 amount);
        void DecreaseCrewMoney(UInt32 crewId, UInt32 amount);
        void IncreaseFactionMoney(UInt32 crewId, UInt32 amount);
        void DecreaseFactionMoney(UInt32 crewId, UInt32 amount);
        Crew GetCrewData(UInt32 crewId);
        List<CrewMember> GetCrewMembersForCrewId(UInt32 crewId);
        void AddMemberToCrew(UInt32 charId, UInt32 crewId, UInt32 factionId, ushort isCaptain, ushort isFirstMate);
        void RemoveMemberFromCrew(UInt32 charId, UInt32 crewId);
        void AddCrewToFaction(UInt32 factionId, UInt32 crewId);
        void RemoveCrewFromFaction(UInt32 factionId, UInt32 crewId);
        void DeleteCrew(UInt32 crewId);

        void updateLocationByHL(UInt16 district, UInt16 hardline);
        List<UInt16> GetHardlineIdsForDistrict(UInt16 district);
        UInt16 GetNearestHardline(UInt16 district, double x, double z);
        void updateSourceHlForObjectTracking(UInt16 sourceDistrict, UInt16 sourceHl, UInt32 lastObjectId);
        void SetPlayerValues();
        void SetRsiValues();
        void SetOnlineStatus(UInt32 charId, ushort isOnline);
        void ResetOnlineStatus();
        void SavePlayer(WorldClient client);
        void SaveExperience(WorldClient client, long exp);
        void SaveInfo(WorldClient client, long exp);

        void UpdateAbilityLoadOut(List<UInt16> abilitySlots, uint loaded);

        ushort UpgradeAbilityLevel(Int32 abilityId, uint level);
        //NEW
        void UpdateInventorySlot(UInt16 sourceSlot, UInt16 destSlot, UInt32 charId);
        UInt16 GetFirstNewSlot();
        void AddItemToInventory(UInt16 slotId, UInt32 itemGoID);
        void RemoveItemAtInventorySlot(UInt16 slotId, UInt32 charId);
        void setBackground(string backgroundText);
        UInt32 GetItemGOIDAtInventorySlot(UInt16 slotId);
        List<Crafting.InventoryItem> GetInventoryItems(UInt32 charId);
        void SetInventoryCount(UInt16 slotId, UInt32 charId, int count);
        void AddItemToInventory(UInt16 slotId, UInt32 itemGoID, int count);
        void UpdateRsiPartValue(string part, uint value, UInt32 charId);
        bool IsSlotinUseByItem(UInt16 slotId);

        bool IsCrewNameAvailable(string crewName);
        void AddCrew(string crewName, string masterHandle);
        UInt16 GetCrewMemberCountByCrewName(string crewName);
        UInt32 GetCrewIdByCrewMasterHandle(string playerHandle);
        UInt32 GetCrewIdByInviterHandle(string playerHandle);

        string GetFactionNameById(UInt32 factionId);

        // Social groups (parameterized; names may contain quotes)
        bool CrewNameExists(string crewName);
        UInt32 CreateCrew(string crewName, string captainHandle, ushort org);
        void SetCrewCaptain(UInt32 crewId, UInt32 charId, string handle);
        void DisbandCrew(UInt32 crewId);
        void SetFactionOfCrewMembers(UInt32 crewId, UInt32 factionId);
        void DisbandFaction(UInt32 factionId);
        UInt32 CreateFaction(string factionName, string masterHandle);
        bool IsOnFriendList(UInt32 charId, UInt32 friendId);
    }
}