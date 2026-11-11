using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// The PvP flag and the rest of a player's ConditionStateFlags, as the player and everyone who sees them get it.
    /// The flag is bit 0x01000000: the client prints "You are now / no longer susceptible to PvP" when the bit changes
    /// on its own view (client.dll 0x625fdca0) and reads it off other players' views to decide whether they can be
    /// attacked (<see cref="PvpRules.IsOpponent"/>). Retail carried it in creation records too. See docs/PVP.md.
    /// </summary>
    public static class PvpFlag
    {
        public const UInt32 InCombatBit = 0x00100000, PvpBit = 0x01000000;

        /// <summary>ID_PVPFLAGFAILURE_INCORRECTSERVERTYPE: the client never shows it by itself, so the server sent it.</summary>
        public const UInt32 WrongServerTypeString = 0x7a000294;

        /// <summary>The value of the client's PvPServer setting on this server: hostile (6) or by flag (2).</summary>
        public static int ServerMode => Store.worldConfig != null && Store.worldConfig.IsPvpServer ? PvpRules.ModeHostile : PvpRules.ModeByFlag;

        public static int MaxSafeLevel => Store.worldConfig != null ? Store.worldConfig.PvpMaxSafeLevel : 15;

        public static UInt32 FlagsOf(WorldClient client)
        {
            return client.playerData.conditionStateFlags | (client.playerData.pvpFlag ? PvpBit : 0u);
        }

        public static UInt32 FlagsOf(ClientData data)
        {
            return data.conditionStateFlags | (data.pvpFlag ? PvpBit : 0u);
        }

        /// <summary>Own view: <c>02 00 02 80 80 80 80 80 80 01 &lt;flags u32&gt;</c> (attribute 42).</summary>
        public static byte[] BuildSelfFlags(UInt32 flags)
        {
            return PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfConditionStateFlags, PlayerViewAttributes.U32(flags)));
        }

        /// <summary>
        /// Another player's view of them, without the view id: <c>02 80 80 80 80 04 &lt;flags u32&gt;</c> (attribute 30;
        /// retail e.g. <c>03 00 02 80 80 80 81 00 0c 80 00 10 01 22</c> with more in it).
        /// </summary>
        public static byte[] BuildOtherFlags(UInt32 flags)
        {
            return PlayerViewAttributes.Build((PlayerViewAttributes.OtherConditionStateFlags, PlayerViewAttributes.U32(flags)));
        }

        /// <summary>Sends the player's flags to their own view and to everyone who has a view of them.</summary>
        public static void SendFlags(WorldClient client)
        {
            UInt32 flags = FlagsOf(client);
            client.playerInstance.ConditionStateFlags.setValue(flags);
            client.messageQueue.addObjectMessage(BuildSelfFlags(flags), false);
            client.FlushQueue();
            ToWatchers(client, BuildOtherFlags(flags));
        }

        /// <summary>An update of the player's view, to everyone who has one.</summary>
        public static void ToWatchers(WorldClient client, byte[] otherViewUpdate)
        {
            Store.world?.SendViewPacketToAllPlayers(otherViewUpdate, client.playerData.getCharID(),
                NumericalUtils.ByteArrayToUint16(client.playerInstance.GetGoid(), 1), client.playerData.getEntityId());
        }

        /// <summary>Turns the flag on or off, tells the clients and saves it. Returns false when it was already so.</summary>
        public static bool Set(WorldClient client, bool on)
        {
            if (client.playerData.pvpFlag == on)
            {
                return false;
            }
            client.playerData.pvpFlag = on;
            Output.WriteDebugLog("[PVP] " + Name(client) + " PvP flag " + (on ? "on" : "off"));
            SendFlags(client);
            if (!client.playerData.isBot)
            {
                try
                {
                    Store.dbManager?.WorldDbHandler?.SetPvpFlag(client.playerData.getCharID(), on);
                }
                catch (Exception ex)
                {
                    Output.WriteDebugLog("[PVP] Flag not saved: " + ex.Message);
                }
            }
            return true;
        }

        /// <summary>The respawn heal sets ConditionStateFlags to 0: the flag goes with it, without a record of its own.</summary>
        public static void ClearForRespawn(WorldClient client)
        {
            bool was = client.playerData.pvpFlag;
            client.playerData.pvpFlag = false;
            client.playerData.conditionStateFlags = 0;
            client.playerInstance.ConditionStateFlags.setValue((UInt32)0);
            if (was && !client.playerData.isBot)
            {
                try
                {
                    Store.dbManager?.WorldDbHandler?.SetPvpFlag(client.playerData.getCharID(), false);
                }
                catch (Exception ex)
                {
                    Output.WriteDebugLog("[PVP] Flag not saved: " + ex.Message);
                }
            }
        }

        internal static string Name(WorldClient client)
        {
            return StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
        }

        internal static int LevelOf(WorldClient client)
        {
            byte[] level = client.playerInstance.Level.getValue();
            return level != null && level.Length > 0 ? level[0] : 1;
        }

        /// <summary>
        /// World entry. On a PvP server everyone above the safe level is flagged (retail's some_pvp capture: the flag
        /// arrives on the own view right after the spawn) and a lower character keeps the flag it chose. Elsewhere the
        /// flag only comes from the zone abilities and doesn't outlast the session (ours: nothing there could clear it).
        /// </summary>
        public static void OnEnterWorld(WorldClient client)
        {
            if (ServerMode == PvpRules.ModeHostile)
            {
                client.playerData.pvpFlag |= PvpRules.HostileServerFlags(LevelOf(client), MaxSafeLevel);
            }
            else
            {
                client.playerData.pvpFlag = false;
            }
            client.playerData.conditionStateFlags = 0;
            client.playerData.duelId = 0;
            client.playerData.duelDeath = false;
            client.playerInstance.DuelID.setValue((UInt32)0);
            client.playerInstance.ConditionStateFlags.setValue(FlagsOf(client));
        }

        /// <summary>
        /// A fight starts. Retail's PvP server put the flag back on then (after a respawn had cleared it: some_pvp
        /// capture, at the start of an NPC fight). A protected low-level character who attacks another player there is
        /// flagged too (ours: the client only reads the target's flag, so without this the target couldn't hit back).
        /// </summary>
        public static void OnFightStarts(WorldClient client, bool againstPlayer)
        {
            if (ServerMode != PvpRules.ModeHostile || client.playerData.duelId != 0)
            {
                return;
            }
            if (againstPlayer || PvpRules.HostileServerFlags(LevelOf(client), MaxSafeLevel))
            {
                Set(client, true);
            }
        }

        /// <summary>
        /// RPC 81 9e, the client's /pvp: no fields, and no reply but the flag itself (client.dll 0x621d5280). Off a PvP
        /// server retail refused with ID_PVPFLAGFAILURE_INCORRECTSERVERTYPE, "Cannot change flag. Not a PvP server."
        /// (the client only shows that string when sent). On one, the flag is the choice of characters at or below the
        /// safe level; above it they are always flagged. Not during a fight. The last two rules are ours.
        /// </summary>
        public static void ProcessToggle(WorldClient client)
        {
            if (ServerMode != PvpRules.ModeHostile)
            {
                client.messageQueue.addRpcMessage(DuelPackets.BuildSystemString(WrongServerTypeString));
                client.FlushQueue();
                return;
            }
            if (PvpRules.HostileServerFlags(LevelOf(client), MaxSafeLevel))
            {
                new ServerPackets().sendSystemChatMessage(client, "Above level " + MaxSafeLevel + " you are always open to PvP here.", "SYSTEM");
                Set(client, true);
                return;
            }
            if (client.playerData.isDead || CombatManager.GetHandler(client).isCombatRunning)
            {
                new ServerPackets().sendSystemChatMessage(client, "You can't change your PvP flag during a fight.", "SYSTEM");
                return;
            }
            Set(client, !client.playerData.pvpFlag);
        }

        public enum AbilityAction { None, Flip, On, OnWithMessage, OffWithMessage }

        /// <summary>
        /// The abilities whose tempmod flips the flag. The tempmods are retail's (tempmods.py: PVPFlagFlipperInit flips,
        /// PVPFlagOnInit sets, PVPFlagMsgOnInit sets and says ID_ABILITY_PVPPULSE, PVPFlagMsgOffInit clears and says
        /// ID_ABILITY_PACZONE). Which ability carries which tempmod is in the ability data, not in the scripts; this
        /// table goes by the abilities' names (abilityIDs.csv), which is an inference. None is a player's ability:
        /// they belong to NPCs and to the PvP-pulse and pacification-zone objects of live events.
        /// </summary>
        public static readonly Dictionary<UInt16, AbilityAction> Abilities = new Dictionary<UInt16, AbilityAction>
        {
            { 721, AbilityAction.Flip },            // PVPFlipperAbility "NPC PVP Flipper (single target)"
            { 735, AbilityAction.Flip },            // PVPFlipper2Ability (friendly PBAE)
            { 736, AbilityAction.Flip },            // PVPFlipper3Ability (enemy PBAE)
            { 1210, AbilityAction.OnWithMessage },  // PVPPulseFlipper20Ability "PVP Zone Effect"
            { 1212, AbilityAction.OnWithMessage },  // PVPPulseFlipper10Ability
            { 1214, AbilityAction.OnWithMessage },  // PVPPulseFlipper40Ability
            { 1232, AbilityAction.OffWithMessage }, // PacZoneFlipper10Ability "Pacification Zone Effect"
            { 1233, AbilityAction.OffWithMessage }, // PacZoneFlipper20Ability
            { 1234, AbilityAction.OffWithMessage }, // PacZoneFlipper40Ability
        };

        /// <summary>ID_ABILITY_PVPPULSE and ID_ABILITY_PACZONE (stringtable_client), sent as system messages by the tempmods.</summary>
        public const UInt32 PvpPulseString = 0x08000247, PacZoneString = 0x04000029;

        /// <summary>What the tempmod does to a flag, by tempmods.py. Returns the new flag and whether it says something.</summary>
        public static bool AfterAbility(AbilityAction action, bool flag, out bool message)
        {
            message = false;
            switch (action)
            {
                case AbilityAction.Flip: return !flag;
                case AbilityAction.On: return true;
                case AbilityAction.OnWithMessage:
                    message = !flag;
                    return true;
                case AbilityAction.OffWithMessage:
                    message = flag;
                    return false;
                default: return flag;
            }
        }

        /// <summary>A flag ability lands on a player. Returns false when the ability isn't one.</summary>
        public static bool ApplyAbility(WorldClient subject, UInt16 abilityId)
        {
            if (!Abilities.TryGetValue(abilityId, out AbilityAction action))
            {
                return false;
            }
            bool flag = AfterAbility(action, subject.playerData.pvpFlag, out bool message);
            if (message)
            {
                subject.messageQueue.addRpcMessage(DuelPackets.BuildSystemString(
                    action == AbilityAction.OnWithMessage ? PvpPulseString : PacZoneString));
            }
            Set(subject, flag);
            subject.FlushQueue();
            return true;
        }
    }
}
