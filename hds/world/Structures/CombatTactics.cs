using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// The four tactic buttons. The value is what the client sends in RPC <c>42 &lt;tactic&gt; 00 00 00 00 00</c> and what
    /// retail stored in the CurrentTacticAbility stat (1012).
    /// </summary>
    public enum CombatTactic : byte
    {
        Speed = 0,
        Power = 1,
        Grab = 2,
        Block = 3
    }

    /// <summary>
    /// RPC 42's TacticType. client.dll's button events: ITxx normal, IExx energized (1), IPxx precise (2); e.g. IEPo is
    /// Power with type 1. combat_calculations: Energized +15% damage and -15% attack roll, Precise the reverse.
    /// </summary>
    public enum CombatTacticType : byte
    {
        Normal = 0,
        Energized = 1,
        Precise = 2
    }

    /// <summary>
    /// Combat tactics as retail ran them. Changing tactic (in or out of a fight) was answered with <c>80 bc</c> modifier
    /// records: the old tactic's six records with the off byte, then the new tactic's six with new record ids (tutorial
    /// capture, crashover2k ..._uriah.log.txt 13:29:10). The values were the same in every capture (303 changes).
    /// No record is sent when the button pressed is already the current tactic.
    /// </summary>
    public static class CombatTactics
    {
        public const UInt16 StatEvadeInterlock = 520, StatForceCombat = 519, StatDamagePercent = 1104,
            StatCurrentTactic = 1012, StatDefensePercent = 1105, StatTacticPercent = 1106;

        public class Tactic
        {
            /// <summary>SpeedTacticUsed 1015, PowerTacticUsed 1017, GrabTacticUsed 1016, BlockTacticUsed 1013.</summary>
            public UInt16 Ability;
            /// <summary>ForceCombatAbility: a Grab forces a fight on someone evading it; Block doesn't.</summary>
            public Int32 ForceCombat;
            /// <summary>EvadeInterlockAbility: how much harder this fighter is to hit.</summary>
            public Int32 EvadeInterlock;
            /// <summary>DamagePercentAbility: damage dealt. Block deals none.</summary>
            public Int32 DamagePercent;
            /// <summary>DefensePercentAbility: damage taken is reduced by this much.</summary>
            public Int32 DefensePercent;
            /// <summary>CombatTacticPercentAbility: the attack roll.</summary>
            public Int32 TacticPercent;
        }

        /// <summary>Indexed by <see cref="CombatTactic"/>.</summary>
        private static readonly Tactic[] Retail =
        {
            new Tactic { Ability = 1015, ForceCombat = -20, EvadeInterlock = 0, DamagePercent = 0, DefensePercent = -10, TacticPercent = 15 },
            new Tactic { Ability = 1017, ForceCombat = 0, EvadeInterlock = -20, DamagePercent = 20, DefensePercent = 0, TacticPercent = -10 },
            new Tactic { Ability = 1016, ForceCombat = 100, EvadeInterlock = -65, DamagePercent = -15, DefensePercent = 30, TacticPercent = 5 },
            new Tactic { Ability = 1013, ForceCombat = -400, EvadeInterlock = 60, DamagePercent = -100, DefensePercent = 50, TacticPercent = -100 },
        };

        public static Tactic Get(CombatTactic tactic)
        {
            return Retail[(int)tactic];
        }

        /// <summary>The tactic an RPC 42 asks for, or null for a value the buttons don't send.</summary>
        public static CombatTactic? FromRpc(byte value)
        {
            return value <= (byte)CombatTactic.Block ? (CombatTactic)value : (CombatTactic?)null;
        }

        /// <summary>The type an RPC 42 asks for; Normal for a value the buttons don't send.</summary>
        public static CombatTacticType TypeFromRpc(int value)
        {
            return value == 1 ? CombatTacticType.Energized : value == 2 ? CombatTacticType.Precise : CombatTacticType.Normal;
        }

        /// <summary>The script toggles set tactical mode; keep it effective across subsequent tactic-button RPCs.</summary>
        public static CombatTacticType Type(WorldClient client) => AbilityBuffs.IsActive(client, 9) ? CombatTacticType.Energized :
            AbilityBuffs.IsActive(client, 15) ? CombatTacticType.Precise : client.playerData.combatTacticType;

        public static double TypeDamageMultiplier(CombatTacticType type)
        {
            return type == CombatTacticType.Energized ? 1.15 : type == CombatTacticType.Precise ? 0.85 : 1.0;
        }

        public static double TypeAttackRollMultiplier(CombatTacticType type)
        {
            return type == CombatTacticType.Energized ? 0.85 : type == CombatTacticType.Precise ? 1.15 : 1.0;
        }

        public const int RecordsPerTactic = 6;

        /// <summary>The tactic's records in retail's order: EvadeInterlock, ForceCombat, Damage%, CurrentTactic, Defense%, Tactic%.</summary>
        public static List<byte[]> BuildRecords(CombatTactic tactic, UInt16[] recordIds, bool off)
        {
            Tactic t = Get(tactic);
            return new List<byte[]>
            {
                AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, recordIds[0], t.Ability, StatEvadeInterlock, t.EvadeInterlock, off, 0f),
                AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, recordIds[1], t.Ability, StatForceCombat, t.ForceCombat, off, 0f),
                AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, recordIds[2], t.Ability, StatDamagePercent, t.DamagePercent, off, 0f),
                AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, recordIds[3], t.Ability, StatCurrentTactic, (int)tactic, off, 0f),
                AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, recordIds[4], t.Ability, StatDefensePercent, t.DefensePercent, off, 0f),
                AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, recordIds[5], t.Ability, StatTacticPercent, t.TacticPercent, off, 0f),
            };
        }

        private static UInt16[] NewRecordIds(ClientData data)
        {
            UInt16[] ids = new UInt16[RecordsPerTactic];
            lock (data.activeBuffs)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = data.nextBuffRecordId++;
                    if (data.nextBuffRecordId == 0)
                    {
                        data.nextBuffRecordId = AbilityBuffs.FirstRecordId;
                    }
                }
            }
            return ids;
        }

        /// <summary>Queues the current tactic's records at world entry (retail's login carried them too). The caller flushes.</summary>
        public static void SendCurrent(WorldClient client)
        {
            ClientData data = client.playerData;
            data.tacticRecordIds = NewRecordIds(data);
            foreach (byte[] record in BuildRecords(data.combatTactic, data.tacticRecordIds, false))
            {
                client.messageQueue.addRpcMessage(record);
            }
        }

        /// <summary>Makes the tactic current and queues the old tactic's records off and the new one's on. The caller flushes.</summary>
        public static void Select(WorldClient client, CombatTactic tactic)
        {
            ClientData data = client.playerData;
            if (data.tacticRecordIds != null)
            {
                foreach (byte[] record in BuildRecords(data.combatTactic, data.tacticRecordIds, true))
                {
                    client.messageQueue.addRpcMessage(record);
                }
            }
            data.combatTactic = tactic;
            data.tacticRecordIds = NewRecordIds(data);
            foreach (byte[] record in BuildRecords(tactic, data.tacticRecordIds, false))
            {
                client.messageQueue.addRpcMessage(record);
            }
        }

        /// <summary>
        /// What a change costs from this much Inner Strength: the new value, or null when it can't be paid. The cost is
        /// this server's rule (<see cref="CombatCalculator.TACTIC_CHANGE_IS_COST"/>); see docs/COMBAT-SYSTEM-ROADMAP.md.
        /// </summary>
        public static UInt16? PayForChange(UInt16 innerStrength, int cost)
        {
            return innerStrength < cost ? (UInt16?)null : (UInt16)(innerStrength - cost);
        }
    }
}
