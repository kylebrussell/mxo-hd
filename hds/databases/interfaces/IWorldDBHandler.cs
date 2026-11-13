using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using hds.world.Structures;

namespace hds.databases.interfaces{
    
    public interface IWorldDBHandler
    {
        PrisonDevices.Owned ReadPrisonDevice(uint character, ushort slot);
        bool MovePrisonDevice(uint character, PrisonDevices.Owned expected, ushort destination);
        List<LocalSupport.Ticket> SupportTickets(UInt32 character);
        UInt32 SaveSupportTicket(UInt32 character, string handle, UInt32 ticket, string text, bool cancel);
        UInt32 FindSupportGroup(string name, bool faction);
        ClientActionRequests.Materialized MaterializeStoredCode(UInt32 character, UInt16 sourceSlot, UInt32 goid, long cost);
        UInt32 GetCharIdByHandle(string handle);
        Hashtable GetCharInfo (UInt32 charId);

        void SetReputation(UInt32 charId, string reputationColumn, int value);
        void SetOrgId(UInt32 charId, int orgId);
        void SetPvpFlag(UInt32 charId, bool flagged);
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
        List<UInt16> GetUploadedHardlines(UInt32 charId, UInt16 district);
        bool AddUploadedHardline(UInt32 charId, UInt16 district, UInt16 hardline);
        void updateSourceHlForObjectTracking(UInt16 sourceDistrict, UInt16 sourceHl, UInt32 lastObjectId);
        void SetPlayerValues();
        void SetRsiValues();
        void SetOnlineStatus(UInt32 charId, ushort isOnline);
        void ResetOnlineStatus();
        void SavePlayer(WorldClient client);
        void SaveExperience(WorldClient client, long exp);
        void SaveInfo(WorldClient client, long exp);
        InventoryPayout ExchangeInventoryForInfo(UInt32 character, UInt16 slot, UInt32 expectedGoid, int expectedCount, UInt32 quote, bool storage = false);

        AbilityMemory.Result CommitAbilityLoadout(uint charId, IReadOnlyList<AbilityProgression.LoadoutEntry> entries, bool unload, int characterLevel);
        void UpdateAbilityLoadOut(List<UInt16> abilitySlots, uint loaded);

        UInt16 GetCurrentAbilityLevel(Int32 abilityGoId, UInt32 charId);
        /// <summary>The level of that ability in that slot, or null if the character has no such ability there.</summary>
        int? GetAbilityLevelAtSlot(UInt32 charId, UInt16 slot, Int32 abilityGoId);
        void SetAbilityLevel(UInt32 charId, UInt16 slot, int level);
        AbilityTraining.Result TrainOwnedAbility(uint character, AbilityProgression.UpgradeRequest request);
        bool HasAbility(UInt32 charId, Int32 abilityGoId);
        void AddAbility(UInt32 charId, UInt16 slot, Int32 abilityGoId, int level);
        void SaveLevel(UInt32 charId, int level, UInt16 health, UInt16 maxHealth, UInt16 innerStrength, UInt16 maxInnerStrength);
        Dictionary<byte, UInt16> GetCharacterAttributes(UInt32 charId);
        void SetCharacterAttribute(UInt32 charId, byte attribute, UInt16 value);
        //NEW
        void UpdateInventorySlot(UInt16 sourceSlot, UInt16 destSlot, UInt32 charId);
        UInt16 GetFirstNewSlot();
        System.Collections.Generic.HashSet<int> GetCompletedMissions(UInt32 charId);
        void SaveMissionCompleted(UInt32 charId, int missionId);
        void EnsureMissionPersistence();
        string LoadActiveMission(UInt32 charId);
        MissionCompletion CompleteActiveMission(UInt32 charId, string expected, UInt32 experience, UInt32 info);
        MissionAccountUpdate CommitActiveMission(UInt32 charId, string expected, string next, UInt16 fromSlot = 0, UInt16 toSlot = 0);
        void AddItemToInventory(UInt16 slotId, UInt32 itemGoID);
        void RemoveItemAtInventorySlot(UInt16 slotId, UInt32 charId);
        void setBackground(string backgroundText);
        UInt32 GetItemGOIDAtInventorySlot(UInt16 slotId);
        List<Crafting.InventoryItem> GetInventoryItems(UInt32 charId);
        void SetInventoryCount(UInt16 slotId, UInt32 charId, int count);
        void EnsureTransactionalInventory();
        CollectorExchange ExchangeCollectorItems(UInt32 charId, UInt32 requiredItem, int quantity, UInt32 rewardItem, bool uniqueReward = false);
        void AddItemToInventory(UInt16 slotId, UInt32 itemGoID, int count);
        List<CodeStorage.Entry> GetCodeStorage(UInt32 charId);
        UInt16 GetNextAbilitySlot(UInt32 charId);
        void AddCodeStorage(UInt32 charId, CodeStorage.Entry entry);
        void SetCodeStorageCount(UInt32 charId, UInt16 slot, int count);
        HashSet<UInt32> GetLearnedRecipes(UInt32 charId);
        void LearnRecipe(UInt32 charId, UInt32 goid);
        (float Factor, double At) GetDeathPenalty(UInt32 charId);
        void SaveDeathPenalty(UInt32 charId, float factor, double at);
        void RemoveCodeStorage(UInt32 charId, UInt16 slot);
        /// <summary>Known-abilities slots (below 0x208) an ability or stored code holds: central storage's occupied slots.</summary>
        HashSet<int> GetAbilityInventorySlots(UInt32 charId);
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
        void SetCrewFirstMate(UInt32 crewId, UInt32 charId);
        void SetCrewFactionRank(UInt32 crewId, ushort rank);
        void SetFactionMaster(UInt32 factionId, string handle);
        bool IsOnFriendList(UInt32 charId, UInt32 friendId);
    }
}
