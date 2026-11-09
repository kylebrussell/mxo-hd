using System;
using hds.shared;

namespace hds{

	public class RpcManager{

        public RpcManager (){

		}
		
        /** 
         * This handles all RPC requests for the client
         */

		public void HandleRpc(int header,ref byte[] rpcData){
            ServerPackets pak = new ServerPackets();
            string handleNote = "Handle RPC Client Request Header " + StringUtils.bytesToString_NS(NumericalUtils.int32ToByteArray(header, 0));
            if (Output.ChatDebugEnabled)
            {
                pak.sendSystemChatMessage(Store.currentClient, handleNote, "BROADCAST");
            }
            else
            {
                Output.WriteDebugLog(handleNote);
            }
		    switch (header)
		    {

		        case (int) RPCRequestHeader.CLIENT_SPAWN_READY:
		            new PlayerHandler().processSpawn();
		            new PlayerHandler().processAttributes();
		            break;
			        
		        case (int) RPCRequestHeader.CLIENT_CLOSE_COMBAT:
					#if DEBUG
		            Output.WriteRpcLog("CLOSE COMBAT REQUEST");
					#endif
		            CombatManager.GetHandler(Store.currentClient).ProcessRequestCloseCombat(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_LEAVE_COMBAT:
			        CombatManager.GetHandler(Store.currentClient).ProcessLeaveCloseCombat(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_COMBAT_STANCE:
		            CombatManager.GetHandler(Store.currentClient).ProcessCombatStance(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_FREE_FIRE_CHOICE:
		            CombatManager.GetHandler(Store.currentClient).ProcessFreeFireChoice(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_RANGE_COMBAT:
			        #if DEBUG
		            Output.WriteRpcLog("RANGE COMBAT REQUEST");
					#endif
		            CombatManager.GetHandler(Store.currentClient).ProcessRangeCombatRequest(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_CHAT:
		            new ChatHandler().processChat(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_WHISPER:
			        new ChatHandler().ProcessWhisperPlayer(ref rpcData);
			        break;
		        case (int) RPCRequestHeader.CLIENT_OBJECTINTERACTION_DYNAMIC:
		            new ObjectInteractionHandler().processObjectDynamic(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_OBJECTINTERACTION_STATIC:
		            new ObjectInteractionHandler().processObjectStatic(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_JUMP_START:
		            //ToDo: Split Jump and Hyperjump
			        new AbilityHandler().ProcessJump(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_JUMP_CANCEL:
					// ToDo: 
					new AbilityHandler().processHyperJumpCancel(ref rpcData);
			        break;
		        case (int) RPCRequestHeader.CLIENT_TARGET:
		            new PlayerHandler().ProcessTargetChange(ref rpcData, Store.currentClient);
		            break;
			    case (int) RPCRequestHeader.CLIENT_MISSION_INVITE_PLAYER:
				    new SocialHandler().ProcessMissionTeamInvite(ref rpcData);
				    break;
		        case (int) RPCRequestHeader.CLIENT_MISSION_REQUEST:
		            new MissionHandler().processMissionList(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_MISSION_INFO:
		            new MissionHandler().processLoadMissionInfo(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_MISSION_ACCEPT:
		            new MissionHandler().ProcessMissionaccept(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_VIEW_TOGGLED:
		            TutorialMission.Of(Store.currentClient)?.OnViewToggled((UInt16)(rpcData[0] | (rpcData[1] << 8)));
		            break;
		        case (int) RPCRequestHeader.CLIENT_SHORTCUT_ADDED:
		            TutorialMission.Of(Store.currentClient)?.OnShortcutAdded(BitConverter.ToUInt32(rpcData, 0), BitConverter.ToUInt32(rpcData, 8));
		            break;
		        case (int) RPCRequestHeader.CLIENT_MODAL_ANSWER:
		            TutorialMission.Of(Store.currentClient)?.OnModalAnswer(BitConverter.ToUInt32(rpcData, 0), rpcData[4] != 0);
		            break;
		        case (int) RPCRequestHeader.CLIENT_MISSION_EVENT:
		            TutorialMission.Of(Store.currentClient)?.OnClientEvent(BitConverter.ToUInt32(rpcData, 0));
		            break;
		        case (int) RPCRequestHeader.CLIENT_MISSION_ABORT:
		            new MissionHandler().processAbortMission(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_LEAVE_GROUP:
			        new SocialHandler().ProcessLeaveGroup(ref rpcData);
		            break;

		        // Team
		        case (int) RPCRequestHeader.CLIENT_HANDLE_MISSION_INVITE:
		            new SocialHandler().ProcessInviteAnswer(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_TEAM_LOOT_MODE:
			        new SocialHandler().ProcessLootMode(ref rpcData);
			        break;

		        // Faction and Crews
		        case (int) RPCRequestHeader.CLIENT_FACTION_CREW_INVITE_PLAYER:
		            new SocialHandler().ProcessGroupInvite(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_FACTION_INVITE:
			        new FCHandler().ProcessInvitePlayerToNewFaction(ref rpcData);
			        break;
		        case (int) RPCRequestHeader.CLIENT_FACTION_INFO:
		            new FCHandler().ProcessLoadFactionName(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_DEPOSIT_MONEY_FACTION_CREW:
			        new FCHandler().ProcessDepositMoney(ref rpcData);
			        break;
		        case (int) RPCRequestHeader.CLIENT_FACTION_DISBAND_FACTION:
					new SocialHandler().ProcessDisbandGroup(ref rpcData);
					break;
		        // Abilitys
		        case (int) RPCRequestHeader.CLIENT_UPGRADE_ABILITY_LEVEL:
			        new AbilityHandler().ProcessUpgradeAbility(ref rpcData);
			        TutorialMission.Of(Store.currentClient)?.OnAbilityUpgraded();
		            break;
		        case (int) RPCRequestHeader.CLIENT_UPGRADE_ATTRIBUTE:
			        new PlayerHandler().ProcessRaiseAttributes(ref rpcData);
			        TutorialMission.Of(Store.currentClient)?.OnAttributeRaised();
		            break;
		        case (int) RPCRequestHeader.CLIENT_ABILITY_HANDLER:
		            new AbilityHandler().ProcessAbility(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_CHANGE_CT:
			        // ToDo: Implement Change of CT
			        CombatManager.GetHandler(Store.currentClient).ProcessChangeTactic(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_ABILITY_LOADER:
		            new PlayerHandler().ProcessLoadAbility(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_HARDLINE_EXIT_LA_CONFIRM:
		            new TeleportHandler().processHardlineExitConfirm(ref rpcData);
		            break;
			    case (int)RPCRequestHeader.CLIENT_EXIT_GAME_FINISH:
				    new TeleportHandler().processGameFinish(ref rpcData);
				    break;

		        case (int) RPCRequestHeader.CLIENT_RESPAWN_REQUEST:
		            new TeleportHandler().ProcessRespawnRequest();
		            break;

		        case (int) RPCRequestHeader.CLIENT_LA_SELECT_HARDLINE:
		            new TeleportHandler().ProcessLoadingAreaHardline(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_READY_WORLDCHANGE:
		            new TeleportHandler().processTeleportReset(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_TELEPORT_HL:
		            new TeleportHandler().processHardlineTeleport(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_HARDLINE_STATUS_REQUEST:
		            new TeleportHandler().processHardlineStatusRequest(ref rpcData);
		            break;


		        // Inventory
		        case (int) RPCRequestHeader.CLIENT_ITEM_MOUNT_RSI:
		            new InventoryHandler().processMountItem(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_ITEM_UNMOUNT_RSI:
		            new InventoryHandler().processUnmountItem(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_ITEM_MOVE_SLOT:
		            new InventoryHandler().processItemMove(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_ITEM_RECYCLE:
		            new InventoryHandler().processItemDelete(ref rpcData);
		            break;

		        // Vendor
		        case (int) RPCRequestHeader.CLIENT_VENDOR_BUY:
		            new VendorHandler().processBuyItem(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_VENDOR_SELL:
		            new VendorHandler().processSellItem(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_TRADE_REQUEST:
		            new TradeHandler().ProcessRequest(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_TRADE_ACTION:
		            new TradeHandler().ProcessAction(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_ADD_FRIEND:
			        new BuddylistHandler().ProcessAddFriend(ref rpcData);
			        break;
		        case (int) RPCRequestHeader.CLIENT_REMOVE_FRIEND:
			        new BuddylistHandler().ProcessRemoveFriend(ref rpcData);
			        break;
		        // MarketPlace
		        case (int) RPCRequestHeader.CLIENT_MP_OPEN:
		            new MarketPlaceHandler().ProcessOpen(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_MP_OPEN_WINDOW:
		            new MarketPlaceHandler().ProcessOpen(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_MP_BUY:
		            new MarketPlaceHandler().ProcessBuyItem(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_MP_PLACE_ITEM:
		            new MarketPlaceHandler().ProcessPlaceItem(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_MP_LIST_ITEMS:
		            new MarketPlaceHandler().processMarketplaceList(ref rpcData);
		            break;

		        // Player
				case (int) RPCRequestHeader.CLIENT_PLAYER_GET_DETAILS:
					new PlayerHandler().ProcessPlayerGetDetails(ref rpcData);
					break;
		        case (int) RPCRequestHeader.CLIENT_PLAYER_GET_BACKGROUND:
		            new PlayerHandler().processGetBackgroundRequest(ref rpcData);
		            break;
		        case (int) RPCRequestHeader.CLIENT_PLAYER_SET_BACKGROUND:
		            new PlayerHandler().ProcessSetBackgroundRequest(ref rpcData);
		            break;
		        // Command Helper
		        case (int) RPCRequestHeader.CLIENT_CMD_WHEREAMI:
		            new CommandHandler().processWhereamiCommand(ref rpcData);
		            break;
                case (int)RPCRequestHeader.CLIENT_CMD_WHO:
                    new CommandHandler().processWhoCommand(ref rpcData);
                    break;

                // Emote and Mood Helpers
                case (int) RPCRequestHeader.CLIENT_CHANGE_MOOD:
		            new PlayerHandler().processMood(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_EMOTE:
		            new PlayerHandler().processEmote(ref rpcData);
		            break;

		        case (int) RPCRequestHeader.CLIENT_REGION_LOADED:
		            new RegionHandler().ProcessRegionLoaded(ref rpcData);
		            //new PlayerInitHelper().processRegionSettings();
		            break;

		         case (int)RPCRequestHeader.CLIENT_LOOT_ACCEPT:
		             new PlayerHandler().ProcessLootAccepted();
		             break;


		        default:
		            //PASS :D
		            byte[] headers = NumericalUtils.int32ToByteArray(header, 1);

			        #if DEBUG
		            string message = "RPCMAIN : Unknown Header " + 
		                             StringUtils.bytesToString_NS(new byte[] {headers[0], headers[1]}) + " \n Content:\n " +
		                             StringUtils.bytesToString_NS(rpcData);
		            Output.WriteRpcLog(message);
					#endif

		            break;

		    }
		}
	}
}

