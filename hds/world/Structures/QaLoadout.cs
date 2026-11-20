using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Disposable native-QA bridge to the ordinary 80 ae handler; never grants an ability.</summary>
    public static class QaLoadout
    {
        private static bool DisposableDatabase(string database) =>
            database != null && database.StartsWith("mxotest_", StringComparison.Ordinal) &&
            database.Length > 8 && database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

        public static bool Allowed(string dev, string database, uint character, bool inWorld, bool dead) =>
            dev == "1" && DisposableDatabase(database) && character != 0 && character != 2 && inWorld && !dead;

        public const string ZenTechniqueMode = "MXO_ZEN_TECHNIQUE_QA_LOAD";

        internal static int? TechniqueQualificationLevel(string enabled, string dev, string database, uint character,
            int storedLevel, IReadOnlyList<AbilityProgression.LoadoutEntry> entries) =>
            enabled == "1" && dev == "1" && DisposableDatabase(database) && character == 110 && storedLevel == 99 &&
            entries.Count == 1 && entries[0].Ability == 860 ? storedLevel : (int?)null;

        public static bool Parse(string[] words, out bool unload, out ushort ability)
        {
            unload = words != null && words.Length > 0 && words[0].Equals("?qaunload", StringComparison.OrdinalIgnoreCase);
            ability = 0;
            return words != null && words.Length == 2 &&
                (unload || words[0].Equals("?qaload", StringComparison.OrdinalIgnoreCase)) &&
                ushort.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out ability) && ability > 0 && ability < 4096;
        }

        public static byte[] Request(IEnumerable<MarginAbilityItem> owned, ushort ability, bool unload)
        {
            // The caller supplies only a canonical id. Resolve the authenticated character's actual archive slot and level.
            var rows = owned.Where(r => r.getSlot() < CharacterAttributes.FirstSlot &&
                AbilityProgression.IsAbilityCode(unchecked((uint)r.getAbilityID())) &&
                AbilityCatalog.IdOf(unchecked((uint)r.getAbilityID())) == ability).Take(2).ToArray();
            if (rows.Length != 1) return null; // refuse ambiguous archive rows rather than guessing a slot
            return Request(rows, unload);
        }

        internal static byte[] Request(IReadOnlyList<MarginAbilityItem> rows, bool unload)
        {
            byte[] body = new byte[10 + 9 * rows.Count];
            body[unload ? 4 : 6] = 10;
            body[8] = (byte)rows.Count; body[9] = (byte)(rows.Count >> 8);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int offset = 10 + 9 * i;
                ushort id = AbilityCatalog.IdOf(unchecked((uint)row.getAbilityID()));
                body[offset + 1] = (byte)row.getSlot(); body[offset + 2] = (byte)(row.getSlot() >> 8);
                body[offset + 3] = (byte)id; body[offset + 4] = (byte)(id >> 8);
                body[offset + 7] = (byte)row.getLevel(); body[offset + 8] = (byte)(row.getLevel() >> 8);
            }
            return body;
        }

        public static bool Dev(WorldClient client, string[] words)
        {
            if (words.Length == 0 || (!words[0].Equals("?qaload", StringComparison.OrdinalIgnoreCase) &&
                !words[0].Equals("?qaunload", StringComparison.OrdinalIgnoreCase))) return false;
            void Notice(string text) => new ServerPackets().sendSystemChatMessage(client, text, "MODAL");
            if (!ReferenceEquals(client, Store.currentClient) || !Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                client.playerData.getCharID(), client.playerData.getOnWorld(), client.playerData.isDead))
            { Notice("QA loadout requires MXO_DEV_COMMANDS=1, an mxotest_ database and a live in-world character other than 2."); return true; }
            if (!Parse(words, out bool unload, out ushort ability))
            { Notice("Usage: ?qaload <canonical ability id> or ?qaunload <canonical ability id>."); return true; }
            var owned = Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID());
            byte[] request = Request(owned, ability, unload);
            if (request == null) { Notice("QA loadout requires exactly one owned archive row for that ability; nothing granted."); return true; }
            // Synchronous chat dispatch already binds Store.currentClient. Do not mutate SQL, loaded sets or passive state here.
            new PlayerHandler().ProcessLoadAbility(ref request);
            Notice("QA loadout submitted through normal 80ae: " + (unload ? "unload " : "load ") + ability + ".");
            return true;
        }
    }
}
