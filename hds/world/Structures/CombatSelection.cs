using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Combat selection (3e), distinct from using an ability (80 b9) or attacking (40/41).
    /// client.dll 6219e500/6219fd10; 625dd5f0 stores either a base ability or 80000000|bag slot.
    /// 1000 and slot 6b mean "no change", including the common world-entry request.</summary>
    public static class CombatSelection
    {
        public const uint NoAbility = 0x1000;
        public const byte NoSlot = 0x6b;
        public readonly struct Request
        {
            public readonly byte Slot;
            public readonly uint Ability;
            public readonly bool IsSlot, IgnoreStyleUpdate;
            public Request(byte slot, uint ability, bool isSlot, bool ignoreStyleUpdate)
            { Slot = slot; Ability = ability; IsSlot = isSlot; IgnoreStyleUpdate = ignoreStyleUpdate; }
        }

        public static Request? Parse(byte[] body) => body?.Length == 7 && body[5] <= 1 && body[6] <= 1
            ? new Request(body[0], BitConverter.ToUInt32(body, 1), body[5] != 0, body[6] != 0) : (Request?)null;

        public static uint? Value(Request request)
        {
            // The retail producer leaves IsSlot zero even for slots. Follow the actual selection setter.
            if (request.Ability != NoAbility)
                return request.Ability <= NoAbility ? request.Ability : (uint?)null;
            return request.Slot == NoSlot ? (uint?)null : 0x80000000u | request.Slot;
        }

        public static bool IsStyle(ushort ability) => AbilityCatalog.Find(ability)?.Number("Ability/IsCombatExclusive") == 1;

        public static bool IsLoaded(ushort ability, IEnumerable<MarginAbilityItem> loadout)
        {
            // Awakened grants Self Defense even when hidden; hidden grants use their parent's loaded state.
            return loadout.Any(row => (row.getLoaded() || AbilityCatalog.IdOf(unchecked((uint)row.getAbilityID())) == 2) &&
                (AbilityCatalog.IdOf(unchecked((uint)row.getAbilityID())) == ability ||
                 AbilityProgression.GrantsOf(AbilityCatalog.IdOf(unchecked((uint)row.getAbilityID()))).Contains(ability)));
        }

        public static void Process(WorldClient client, byte[] body)
        {
            Request? parsed = Parse(body);
            if (!parsed.HasValue || client.playerData.isDead || client.playerData.duelDeath) return;
            Request request = parsed.Value;
            uint? value = Value(request);
            if (!value.HasValue) return;
            if ((value.Value & 0x80000000) != 0)
            {
                if (!Store.dbManager.WorldDbHandler.GetInventoryItems(client.playerData.getCharID())
                    .Any(i => i.Slot == request.Slot)) return;
            }
            else if (value.Value != 0 && !IsLoaded((ushort)value.Value,
                Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID()))) return;

            client.playerData.combatSelection = value.Value;
            // The selection is the client's own style toolbar: a style becomes the active one (stats, special-move set and
            // interlock exchanges, CombatStyles). Retail's producers set IgnoreStyleUpdate when only a bag slot moved.
            if (!request.IgnoreStyleUpdate && IsStyle((ushort)value.Value))
                CombatStyles.Select(client, (ushort)value.Value);
            client.playerInstance.CurCombatExclusiveAbility.setValue(value.Value);
            client.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((39, PlayerViewAttributes.U32(value.Value))), false);
            client.FlushQueue();
        }

        /// <summary>43's u16 MartialArt. The 7.6005 client never sends it: its only producer (client.dll 6219f0a0, which
        /// also stores the value at this+0x88) has no call or data reference, and its constructor 6219e890 none either.
        /// The matching server message 81 6e (Flags, ForcedTacticalSetting, ForcedMartialArt, ForcedTacticalValue; handler
        /// 6219e240) stores ForcedMartialArt at +0xb6 and nothing reads it. Styles are chosen with 3e instead. The value is
        /// retained for logging only; it neither changes the style nor gets an answer.</summary>
        public static void ProcessMartialArt(WorldClient client, byte[] body)
        {
            if (body?.Length == 2 && !client.playerData.isDead && !client.playerData.duelDeath)
                client.playerData.martialArt = BitConverter.ToUInt16(body, 0);
        }
    }
}
