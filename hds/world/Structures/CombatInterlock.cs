using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// The close-combat round records, decoded from retail fights (1346 RPC 45s and 2548 RPC 47s; the examples are from
    /// the tutorial fight, crashover2k ..._uriah.log.txt 13:28:32-13:29:26). Retail resolved a round every ~4.5 s
    /// (median of the intervals between 45s) and sent:
    /// <list type="bullet">
    /// <item>RPC 47: the fighter's move just decided and the next one. Kind 3 at each new round, kind 6 when the next
    /// move changed in between (an ability used, RPC 80 b9, or a new tactic, RPC 42), kind 4 once at the start.</item>
    /// <item>RPC 45: the round's outcome: both fighters' moves and the damage.</item>
    /// </list>
    /// The rules are retail's own: the client's resource/Python/Monolith.ltp carries the server's combat scripts
    /// (combat_calculations, interlock/combat_resolution). What's a guess is marked; mainly the fighters' stats, which
    /// this server doesn't model, are taken from their level.
    /// </summary>
    public static class CombatInterlock
    {
        /// <summary>Median time between retail's RPC 45s.</summary>
        public const int RoundMs = 4500;

        public const byte KindNewRound = 3, KindStart = 4, KindNextMoveChanged = 6;

        /// <summary>Result field of RPC 45: one side hit, neither, both, a round spent on an ability; retail ORed 0x40 on a kill.</summary>
        public const UInt32 ResultOneHit = 0x01, ResultNoHit = 0x04, ResultBothHit = 0x08, ResultKill = 0x40, ResultAbility = 0x100;

        /// <summary>
        /// One fighter's move: <c>01 &lt;seq u8&gt; &lt;value u16&gt; &lt;ability u32&gt; &lt;four u16&gt;</c>. The ability field is
        /// 0x80000000 | ability id (e.g. <c>01 0b 1e 00 c6 00 00 80 ...</c> = Cheap Shot); only the first move of a
        /// fight has it 0. Retail's value and the four numbers look like rolls; their meaning is unknown.
        /// </summary>
        public class Move
        {
            public byte Seq;
            public UInt16 Value;
            public bool First;
            public UInt16 Ability;
            public CombatTactic Tactic;
            public UInt16 R1, R2, R3, R4;

            public bool HasAbility => Ability != 0;
        }

        private static void AddMove(PacketContent pak, Move move)
        {
            pak.AddByte(0x01);
            pak.AddByte(move.Seq);
            pak.AddUint16(move.Value, 1);
            pak.AddUint32(move.First ? 0u : 0x80000000u | move.Ability, 1);
            pak.AddUint16(move.R1, 1);
            pak.AddUint16(move.R2, 1);
            pak.AddUint16(move.R3, 1);
            pak.AddUint16(move.R4, 1);
        }

        /// <summary>
        /// RPC 47: <c>47 &lt;first&gt; &lt;has ability&gt; &lt;tactic&gt; &lt;move&gt; 1a 00 &lt;kind u32&gt;</c>, then
        /// <c>01 00 00 &lt;has ability&gt; &lt;tactic&gt; &lt;next move&gt;</c> or <c>00 00</c> when there is no next move.
        /// <c>first</c> is 3 on the opening move in retail's has-ability slot.
        /// </summary>
        public static byte[] BuildRoundState(byte kind, Move move, Move next)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x47);
            pak.AddByte(0x00);
            pak.AddByte(move.First ? (byte)0x03 : move.HasAbility ? (byte)0x01 : (byte)0x00);
            pak.AddByte((byte)move.Tactic);
            AddMove(pak, move);
            pak.AddHexBytes("1a00");
            pak.AddUint32(kind, 1);
            if (next == null)
            {
                pak.AddHexBytes("0000");
                return pak.ReturnFinalPacket();
            }
            pak.AddHexBytes("010000");
            pak.AddByte(next.HasAbility ? (byte)0x01 : (byte)0x00);
            pak.AddByte((byte)next.Tactic);
            AddMove(pak, next);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// RPC 45 (89 bytes): <c>45 &lt;round u16&gt; &lt;round+1 u16&gt; &lt;won u8&gt; 00 &lt;has ability&gt; &lt;tactic&gt; &lt;move&gt;
        /// 00 00 &lt;NPC tactic&gt; &lt;NPC move&gt; 00000000 01 &lt;target u32&gt; &lt;target u32&gt; 00000000 &lt;result u32&gt; 00000000
        /// &lt;damage to the player u16&gt; &lt;modifier i16&gt; &lt;damage to the target u16&gt; &lt;modifier i16&gt; &lt;sim ms u32&gt; 8 x 00</c>.
        /// The damage slots follow the health updates: the tutorial's <c>04 00 00 00 15 00 fb ff</c> took 4 off the player
        /// and 21 off the NPC. <c>won</c> is retail's winnerSlot: 1 when the player wins the round by <see cref="CombatRules.Resolve"/>.
        /// </summary>
        public static byte[] BuildRoundResult(UInt16 round, bool won, Move player, Move npc, UInt32 target, UInt32 result,
            UInt16 damageToPlayer, Int16 playerModifier, UInt16 damageToTarget, Int16 targetModifier, UInt32 simMs)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x45);
            pak.AddUint16(round, 1);
            pak.AddUint16((UInt16)(round + 1), 1);
            pak.AddByte(won ? (byte)0x01 : (byte)0x00);
            pak.AddByte(0x00);
            pak.AddByte(player.First ? (byte)0x03 : player.HasAbility ? (byte)0x01 : (byte)0x00);
            pak.AddByte((byte)player.Tactic);
            AddMove(pak, player);
            pak.AddHexBytes("0000");
            pak.AddByte((byte)npc.Tactic);
            AddMove(pak, npc);
            pak.AddHexBytes("0000000001");
            pak.AddUint32(target, 1);
            pak.AddUint32(target, 1);
            pak.AddUint32(0, 1);
            pak.AddUint32(result, 1);
            pak.AddUint32(0, 1);
            pak.AddUint16(damageToPlayer, 1);
            pak.AddInt16(playerModifier, 1);
            pak.AddUint16(damageToTarget, 1);
            pak.AddInt16(targetModifier, 1);
            pak.AddUint32(simMs, 1);
            pak.AddUint32(0, 1);
            pak.AddUint32(0, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>The result field for a round: <paramref name="ability"/> means the player's special move landed.</summary>
        public static UInt32 ResultFor(bool playerHit, bool npcHit, bool ability, bool targetDied)
        {
            UInt32 result = ability ? ResultAbility
                : playerHit && npcHit ? ResultBothHit
                : playerHit || npcHit ? ResultOneHit
                : ResultNoHit;
            return targetDied ? result | ResultKill : result;
        }

        /// <summary>The range constants of retail's rolls: combatlib.getTacticRoll(base, consistency, 100) and (..., 69).</summary>
        public const int AttackRollRange = 100, DefenseRollRange = 69;

        /// <summary>
        /// combatlib.getTacticRoll in client.dll (0x6258da00, Gaussian mode): normal around (base + range) / 2 with a deviation
        /// of (base + range) / 6, between 1 and base + range. Consistency narrows the deviation; this server has no such
        /// stat, so it's left out.
        /// </summary>
        public static UInt16 TacticRoll(Random rng, int baseValue, int range)
        {
            double total = baseValue + range;
            return (UInt16)CombatCalculator.GaussianClamped(rng, total / 2.0, total / 6.0, 1, total);
        }

        /// <summary>
        /// Does the attacker land a hit? combat_resolution.getCTandDTotals: attack roll &gt; defense roll, the rolls based on the
        /// fighter's Combat Tactics and Defense Tactics stats times the tactic's GetTacticCombatTacticsMod (Tactic%) and
        /// GetTacticDefenseTacticsMod (Defense%). The stats aren't modelled here: their base is the level (a guess; level 1
        /// fits the rolls of retail's tutorial fight). A fighter on Block doesn't hit (retail: handleBlock).
        /// </summary>
        public static bool RollHit(Random rng, CombatTactic attacker, int attackerLevel, CombatTactic defender, int defenderLevel,
            out UInt16 attackRoll, out UInt16 defenseRoll, CombatTacticType attackerType = CombatTacticType.Normal)
        {
            CombatTactics.Tactic a = CombatTactics.Get(attacker);
            CombatTactics.Tactic d = CombatTactics.Get(defender);
            int attackBase = (int)(attackerLevel * (100 + a.TacticPercent) / 100 * CombatTactics.TypeAttackRollMultiplier(attackerType));
            attackRoll = TacticRoll(rng, attackBase, AttackRollRange);
            defenseRoll = TacticRoll(rng, defenderLevel * (100 + d.DefensePercent) / 100, DefenseRollRange);
            return attacker != CombatTactic.Block && attackRoll > defenseRoll;
        }

        /// <summary>NPCs change tactic round by round, as often as retail's RPC 45s show: 488 Speed, 393 Power, 287 Grab, 178 Block.</summary>
        public static CombatTactic NpcTactic(Random rng)
        {
            int roll = rng.Next(0, 488 + 393 + 287 + 178);
            return roll < 488 ? CombatTactic.Speed : roll < 488 + 393 ? CombatTactic.Power : roll < 488 + 393 + 287 ? CombatTactic.Grab : CombatTactic.Block;
        }

        /// <summary>
        /// A tactic's special effect on whoever it hits, for 10 s: SpeedTactic -&gt; Daze, GrabTactic -&gt; Off Balance,
        /// PowerTactic -&gt; Staggered, shown with these ConditionStateFlags bits (own view: e.g. 80 bc 206 201 on for 10 s with
        /// flags 0x00100008 on, tutorial 13:29:05, off 13:29:15). NPC views carried the same bits. Block has none.
        /// </summary>
        public static UInt32 ConditionBit(CombatTactic tactic)
        {
            switch (tactic)
            {
                case CombatTactic.Speed: return 0x02;
                case CombatTactic.Grab: return 0x04;
                case CombatTactic.Power: return 0x08;
                default: return 0;
            }
        }

        public const double ConditionSeconds = 10;

        /// <summary>How often a landed hit also causes the condition: a guess; captures show the conditions but not their odds.</summary>
        public const int ConditionChancePercent = 25;

        /// <summary>ConditionStateFlags of anyone in a fight (retail: 0x00100000).</summary>
        public const UInt32 InCombatFlag = 0x00100000;

        /// <summary>
        /// CombatEffectAbility (670): retail put these on at the start of every fight and took them off at the end, -80% to
        /// Inner Strength regen (stat 114, kind 0x1d) and health regen (stat 112, kind 0x15).
        /// </summary>
        public static List<byte[]> BuildCombatEffectRecords(UInt16 firstRecordId, bool off)
        {
            return new List<byte[]>
            {
                AbilityBuffs.BuildModifier(AbilityBuffs.KindLinked, firstRecordId, 670, 114, -80, off, 0f),
                AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, (UInt16)(firstRecordId + 1), 670, 112, -80, off, 0f),
            };
        }
    }
}
