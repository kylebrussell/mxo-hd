using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Drawing.Printing;
using System.Text;

using hds.shared;
using hds.world.Structures;

namespace hds
{
    class PlayerHandler
    {

        // Important: We musts load this after the setup state is set to 0x0f (so before that it wouldnt work properly)
        public void processAttributes()
        {
            ServerPackets server = new ServerPackets();
            server.sendPlayerAttributes(Store.currentClient);
            server.SendPlayerFriendList(Store.currentClient);
            IgnoreListHandler.SendList(Store.currentClient);
            //Store.currentClient.playerData.setPss(0x7f);
        }

        public void processMood(ref byte[] packet)
        {
            byte moodByte = packet[0];
            ServerPackets server = new ServerPackets();
            //ToDo: Announce to other Players (and find packet for it) and save this in playerObject for new players
            server.sendMoodChange(Store.currentClient, moodByte);
        }

        public void processEmote(ref byte[] packet)
        {
            byte[] emoteBytes = new byte[4];
            emoteBytes[0] = packet[0];
            emoteBytes[1] = packet[1];
            emoteBytes[2] = packet[2];
            emoteBytes[3] = packet[3];

            byte emoteByte = packet[0];
            UInt32 emoteKey = NumericalUtils.ByteArrayToUint32(emoteBytes, 0);
            
            ServerPackets server = new ServerPackets();
            server.sendEmotePerform(Store.currentClient, emoteKey);
        }

        public void processSpawn()
        {
            if (Store.currentClient.playerData.worldChangePending) return;

            // Always spawn. District 0 is the Tutorial (MxOLocations.TUTORIAL), a real world the client has
            // already loaded via sendWorldCMD; the old "districtId > 0" check skipped the self-spawn there and
            // left tutorial characters at "Negotiating with Servers..." for good.
            uint districtId = Store.currentClient.playerData.getDistrictId();

            // In the Loading Area the client sends the same 05 but retail never spawned a character there: after
            // 81 09 + 06 la.metr it only got settings and the hardline status (6d), and showed the hardline
            // picker (crashover2k exit_to_loadingarea, died_in_pvp). A self-spawn puts the client in world mode,
            // standing on the LA rooftop with no picker.
            if (Store.currentClient.playerData.DistrictIsLA())
            {
                if (TutorialMission.JackIntoRichland(Store.currentClient))
                {
                    return;
                }

                Output.WriteDebugLog("[LA] Spawn ready in the Loading Area: no self-spawn, waiting for a hardline pick");
                return;
            }

            // The weapon in the weapon slot is the one in hand (EquippedItemID); nothing set it before, so a
            // player who logged out holding a weapon came back empty-handed.
            UInt32 weapon = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(InventoryHandler.WeaponSlot);
            Store.currentClient.playerInstance.EquippedItemID.setValue(weapon);

            PvpFlag.OnEnterWorld(Store.currentClient);
            Store.currentClient.playerData.deathPenaltyRecord = 0; // the new world discarded the old modifier records
            DeathPenalty.OnEnter(Store.currentClient);
            AbilityPassives.Load(Store.currentClient, newWorld: true);

            // REFACTOR!!!!
            byte[] rsiObject = new BootingHelperRsi().generateSelfSpawnPacket(Store.currentClient);
            //Store.world.sendViewPacketToAllPlayers(new BootingHelperRsi().generatePlayerSpawnPacket(Store.currentClient), Store.currentClient.playerData.getCharID(), NumericalUtils.ByteArrayToUint16(Store.currentClient.playerInstance.getGoid(), 1), Store.currentClient.playerData.getEntityId());

            Store.currentClient.messageQueue.addObjectMessage(rsiObject, false);
            Store.currentClient.playerData.setOnWorld(true);
            if (DeathPresentation.IsCorpse(Store.currentClient)) DeathPresentation.Corpse(Store.currentClient);
            AbilityPassives.Refresh(Store.currentClient);
            if (DeathPenalty.Current(Store.currentClient) < 1) DeathPenalty.Send(Store.currentClient);
            if (Store.currentClient.playerData.pvpFlag)
            {
                // Retail's PvP server sent the flag on the own view right after the spawn (some_pvp capture)
                Store.currentClient.messageQueue.addObjectMessage(PvpFlag.BuildSelfFlags(PvpFlag.FlagsOf(Store.currentClient)), false);
            }

            // Where the player stands on the client's nav mesh, as retail's record names it: without it the client has no
            // nav location until the first teleport (it lets the player through walls, and elevator panels close at once)
            double sx = 0, sy = 0, sz = 0;
            NumericalUtils.LtVector3dToDoubles(Store.currentClient.playerInstance.Position.getValue(), ref sx, ref sy, ref sz);
            if (PacketsUtils.PlaceNear((UInt16)districtId, sx, sy, sz, out NavLocation place))
            {
                Store.currentClient.messageQueue.addObjectMessage(
                    PacketsUtils.createTeleportPacket(Store.currentClient, (float)sx, (float)sy, (float)sz), false);
                Output.WriteDebugLog("[SPAWN] On the nav mesh: sector " + place.sector + ", region " + place.region + ", polygon " + place.polygon);
            }
            if (districtId == (uint)MxOLocations.TUTORIAL)
            {
                TutorialMission.Begin(Store.currentClient);
            }

            Output.WriteLine("[SPAWN] Queued self-spawn for charId " + Store.currentClient.playerData.getCharID() +
                             " (" + rsiObject.Length + " bytes, districtId " + districtId + ")");


        }

        public void processPlayerSetup()
        {
            // REFACTOR: Move to PlayerHandler and PlayerPackets (and location header to Server Packets)	

            ServerPackets packets = new ServerPackets();

            packets.sendWorldCMD(Store.currentClient, Store.currentClient.playerData.getDistrictId(), Store.worldConfig.weather); // Genereal Summer Sky
            //packets.sendWorldCMD(Store.currentClient, Store.currentClient.playerData.getDistrictId(),"Massive");
            //packets.sendWorldCMD(Store.currentClient, Store.currentClient.playerData.getDistrictId(),"WinterSky3"); // Winter is coming to MxO
            // packets.SendServerSettingString(Store.currentClient,"WR_RezEvents","bluesky2");

            // This is more a test
            packets.SendWorldSetup(Store.currentClient);

            packets.sendEXPCurrent(Store.currentClient, (UInt32)Store.currentClient.playerData.getExperience());
            packets.SendInfoCurrent(Store.currentClient, (UInt32)Store.currentClient.playerData.getInfo());

            /*
            long exp = Store.currentClient.playerData.getExperience();
            long cash = Store.currentClient.playerData.getInfo();
            string expStr = "80e1" + StringUtils.bytesToString_NS(NumericalUtils.uint32ToByteArray((UInt32)exp, 1)) + "00000000";
            string cashStr = "80df" + StringUtils.bytesToString_NS(NumericalUtils.uint32ToByteArray((UInt32)cash, 1)) + "00000000";
            */

            UInt32 charId = Store.currentClient.playerData.getCharID();
            Dictionary<byte, UInt16> attributes = Store.dbManager.WorldDbHandler.GetCharacterAttributes(charId);
            foreach (byte attribute in CharacterAttributes.All)
            {
                UInt16 value = attributes.TryGetValue(attribute, out UInt16 stored)
                    ? stored
                    : CharacterAttributes.LegacyValue(attribute);
                packets.sendAttribute(Store.currentClient, value, attribute);
            }

            // Retail loaded Awakened's hidden Self Defense at its level with the attributes (80 b2 11 00 05 00 08 02).
            UInt16 awakened = Store.dbManager.WorldDbHandler.GetCurrentAbilityLevel(
                DataLoader.getInstance().getAbilityByID(AbilityProgression.AwakenedAbility).getGOID(), charId);
            Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildAbilityLoad(
                AbilityProgression.SelfDefenseAbility, awakened, AbilityProgression.HiddenSlot));
            foreach (var loaded in Store.dbManager.MarginDbHandler.LoadAbilities((int)charId))
                if (loaded.getLoaded())
                {
                    UInt16 id = AbilityCatalog.IdOf(unchecked((UInt32)loaded.getAbilityID()));
                    if (id == AbilityProgression.AwakenedAbility) continue;
                    foreach (UInt16 grant in AbilityProgression.GrantsOf(id))
                        Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildAbilityLoad(grant,
                            AbilityProgression.GrantLevel(id, loaded.getLevel()), AbilityProgression.HiddenSlot));
                }


            /*
            string focusRPC = "80ad4e" + StringUtils.bytesToString_NS(NumericalUtils.uint16ToByteArray(focus, 0)) + "000802";
            string beliefRPC = "80ad52" + StringUtils.bytesToString_NS(NumericalUtils.uint16ToByteArray(belief, 0)) + "000802";
            string vitalityRPC = "80ad54" + StringUtils.bytesToString_NS(NumericalUtils.uint16ToByteArray(vitality, 0)) + "000802";
            string perceptionRPC = "80ad4f" + StringUtils.bytesToString_NS(NumericalUtils.uint16ToByteArray(perception, 0)) + "000802";
            string reasonRPC = "80ad51" + StringUtils.bytesToString_NS(NumericalUtils.uint16ToByteArray(reason, 0)) + "000802";

            
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes(focusRPC)); // Focus 
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes(beliefRPC)); // Belief
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes(vitalityRPC)); // Vitality
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes(perceptionRPC)); // Perception
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes(reasonRPC)); // Reason
            */
            //Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("808615A0070000000000000000000000000000000000000000210000000000230000000000")); // Cew and Faction Window ena
            // Disable later

            //8167170020001C2200C60111000000000000002900000807006D786F656D750007006D786F656D750002000200000000000000
            //Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80b2110011000802")); // What is this ? Check it later
            /*
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc4503110000020000001100110000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc450002000002000000cc00040000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000300000b0000003702330000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000400002d0000002304030000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000500002d0000002004030000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000600002d0000001904010000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000700002d0000004204010000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc4500080000340000008302440000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000900004c0000001504040000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000a00004c0000000604040000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000b00004c0000001904020000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000c0000530000001504010000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000d0000530000001904010000000000000000"));
            */
            //Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80b23a0400000802"));
            /*
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45033a0400530000003a04000000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000e0000530000000604010000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45000f00005f0000007c04050000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc45001000005f0000000704050000000000000000"));
            Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80bc4500110000620000000604010000000000000000"));
            */
            //Store.currentClient.messageQueue.addRpcMessage(StringUtils.hexStringToBytes("80b2350400000802"));

            // Buddylist
            BuddylistHandler buddylistHandler = new BuddylistHandler();
            buddylistHandler.ProcessAnnounceFriendOnline(Store.currentClient.playerData.getCharID(), StringUtils.charBytesToString_NZ(Store.currentClient.playerInstance.CharacterName.getValue()));
            ;

            ServerPackets pak = new ServerPackets();

            UInt32 factionId =
                NumericalUtils.ByteArrayToUint32(Store.currentClient.playerInstance.FactionID.getValue(), 1);
            if (factionId > 0)
            {
                Faction faction = Store.dbManager.WorldDbHandler.FetchFaction(factionId);
                faction.masterPlayerCharId =
                    Store.dbManager.WorldDbHandler.GetCharIdByHandle(faction.masterPlayerHandle);
                pak.SendFactionInfo(Store.currentClient, faction, false);
                pak.SendFactionCrews(Store.currentClient, faction, false);

            }
            
            // Retail's login carried the Speed tactic's records (this used to send only one of them, as a test)
            CombatTactics.SendCurrent(Store.currentClient);
            
            // Retail sent one 80 86 at login: the crew for a crew member (AfterWhoruNeo) or the empty form for
            // a player without one (mxoemu3). This used to send the crew and then the empty form, with a fixed
            // char id, which emptied the crew window again.
            UInt32 crewId = NumericalUtils.ByteArrayToUint32(Store.currentClient.playerInstance.CrewID.getValue(), 1);
            SocialHandler.SendCrewInfo(Store.currentClient, crewId);
        }

        public void processInitUDPSession(ref byte[] packetData)
        {
            PacketReader reader = new PacketReader(packetData);
            reader.SetOffsetOverrideValue(11);

            Store.currentClient.playerData.setCharID(reader.ReadUInt32(1));

            Store.dbManager.WorldDbHandler.SetPlayerValues();
            Store.dbManager.WorldDbHandler.SetRsiValues();
            Store.dbManager.WorldDbHandler.SetOnlineStatus(Store.currentClient.playerData.getCharID(), 1);

            // send the init UDP packet * 5
            byte[] response = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05 };
            for (int i = 0; i < 5; i++)
            {
                Store.currentClient.messageQueue.addRawMessage(response);    
            }
            
            Store.currentClient.FlushQueue();
            Store.margin.SendUDPSessionReply(Store.currentClient.playerData.getCharID());
        }

        public void processGetBackgroundRequest(ref byte[] packetData)
        {
            ServerPackets packet = new ServerPackets();
            packet.sendGetBackgroundMessage(Store.currentClient);
        }

        public void IncrementPlayerExp(UInt32 expToIncrement)
        {
            PlayerRewards.GrantExperience(Store.currentClient, expToIncrement);
        }

        /// <summary>
        /// Client 80 b5 from the attribute level-up panel: the points to add per attribute; 80 b6 answers with the new
        /// value, or with LTRS_INADEQUATEATTRIBUTEPOINTS when the raise does not fit.
        /// </summary>
        public void ProcessRaiseAttributes(ref byte[] rpcData)
        {
            WorldClient client = Store.currentClient;
            UInt32 charId = client.playerData.getCharID();

            foreach (CharacterAttributes.Raise raise in CharacterAttributes.ParseRaises(rpcData))
            {
                if (Array.IndexOf(CharacterAttributes.All, raise.Attribute) < 0)
                {
                    Output.WriteDebugLog("[ATTRIBUTE] Ignored unknown attribute " + raise.Attribute);
                    continue;
                }

                Dictionary<byte, UInt16> stored = Store.dbManager.WorldDbHandler.GetCharacterAttributes(charId);
                int level = client.playerInstance.Level.getValue()[0];
                if (!CharacterAttributes.CanRaise(stored, raise.Attribute, raise.Points, level))
                {
                    Output.WriteDebugLog("[ATTRIBUTE] Raise of " + raise.Attribute + " by " + raise.Points + " refused at level " + level);
                    client.messageQueue.addRpcMessage(ServerPackets.BuildAttributeRaiseRefused());
                    continue;
                }

                UInt16 raised = (UInt16)(CharacterAttributes.ValueOf(stored, raise.Attribute) + raise.Points);
                Store.dbManager.WorldDbHandler.SetCharacterAttribute(charId, raise.Attribute, raised);
                client.messageQueue.addRpcMessage(ServerPackets.BuildAttributeRaised(raise.Attribute,
                    CharacterAttributes.NewLevelOf(raise.Attribute, raised)));
                Output.WriteDebugLog("[ATTRIBUTE] " + raise.Attribute + " raised to " + raised);
            }
            client.FlushQueue();
        }

        public void ProcessSetBackgroundRequest(ref byte[] packetData)
        {
            UInt16 backgroundSize = NumericalUtils.ByteArrayToUint16(new byte[] {packetData[3], packetData[4]},1);

            byte[] backgroundBytes = new byte[backgroundSize-1];
            ArrayUtils.copy(packetData,5,backgroundBytes,0,backgroundSize-1);

            string backgroundText = StringUtils.charBytesToString(backgroundBytes);
            Store.dbManager.WorldDbHandler.setBackground(backgroundText);

        }

        public void ProcessLootAccepted()
        {
            if (NpcLoot.TryTakeAll(Store.currentClient))
            {
                return;
            }
            if (TutorialMission.Of(Store.currentClient)?.OnTakeAll() == true || MissionRuntime.OnTakeAll(Store.currentClient))
            {
                return;
            }

            new ServerPackets().SendLootAccepted(Store.currentClient);
        }

        public void ProcessPlayerGetDetails(ref byte[] rpcData)
        {
            PacketReader reader = new PacketReader(rpcData);
            UInt32 unknownAlwaysZeroUint32 = reader.ReadUInt32(1);
            UInt16 alwaysEightUint16 = reader.ReadUInt16(1);
            string handle = reader.ReadSizedString();
            
            // Load Details from Database about the Handle
            Hashtable charInfo = Store.dbManager.WorldDbHandler.GetCharInfoByHandle(handle);
            if (charInfo.Count > 0)
            {
                ServerPackets packets = new ServerPackets();
                packets.SendPlayerGetDetails(Store.currentClient, charInfo);
                packets.SendPlayerBackground(Store.currentClient, charInfo);
            }
            
        }
        
        public void ProcessTargetChange(ref byte[] rpcData, WorldClient currentClient)
        {
            UInt16 viewId = NumericalUtils.ByteArrayToUint16(new byte[] {rpcData[0], rpcData[1]}, 1);
            ushort spawnId = rpcData[2];
            // ToDo: add this to the ClientData

            if (viewId == 0)
            {
                viewId = 2;
            }
            currentClient.playerData.currentSelectedTargetViewId = viewId;
            currentClient.playerData.currentSelectedTargetSpawnId = spawnId;
            ServerPackets pak = new ServerPackets();
            if (DebugChat.Enabled)
            pak.sendSystemChatMessage(Store.currentClient,
                "TARGET CHANGE For ViewID " + viewId.ToString() + " AND SPAWN ID : " + spawnId.ToString(), "MODAL");
        }
        
        public void ProcessLoadAbility(ref byte[] packet)
        {
            if (!AbilityProgression.TryParseLoadout(packet, out bool unload, out List<AbilityProgression.LoadoutEntry> entries))
            {
                Output.WriteDebugLog("[ABILITY] Malformed loadout request ignored");
                return;
            }

            List<UInt16> abilitySlots = new List<UInt16>();
            var ownedAbilities = Store.dbManager.MarginDbHandler.LoadAbilities((int)Store.currentClient.playerData.getCharID());
            foreach (AbilityProgression.LoadoutEntry entry in entries)
            {
                var owned = ownedAbilities.FirstOrDefault(row => row.getSlot() == entry.Slot &&
                    AbilityCatalog.IdOf(unchecked((uint)row.getAbilityID())) == AbilityCatalog.Canonical(entry.Ability));
                if (owned == null) continue;
                List<UInt16> grants = AbilityProgression.GrantsOf(entry.Ability);
                if (unload)
                {
                    Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUnload(entry.Ability));
                    foreach (UInt16 grant in grants) Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUnload(grant));
                }
                else
                {
                    Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildAbilityLoad(entry.Ability, owned.getLevel(), entry.Slot));
                    foreach (UInt16 grant in grants)
                    {
                        Store.currentClient.messageQueue.addRpcMessage(ServerPackets.BuildAbilityLoad(grant,
                            AbilityProgression.GrantLevel(entry.Ability, owned.getLevel()), AbilityProgression.HiddenSlot));
                    }
                }
                abilitySlots.Add(entry.Slot);
            }

            if (abilitySlots.Count == 0) return;
            Store.dbManager.WorldDbHandler.UpdateAbilityLoadOut(abilitySlots, unload ? 0u : 1u);
            AbilityPassives.Load(Store.currentClient);
            Store.currentClient.FlushQueue();
            Output.WriteDebugLog("[ABILITY] " + (unload ? "Unloaded " : "Loaded ") + entries.Count + " abilities");
        }
    }
}
