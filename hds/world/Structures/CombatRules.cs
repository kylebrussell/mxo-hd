using System;

namespace hds
{
    /// <summary>
    /// How a close-combat round turns out, by retail's own scripts (research/client-scripts, from the client's
    /// Monolith.ltp): interlock/combat_resolution.determineAttacker and determineResultForStandardCombat, with
    /// combat_utility.isPreferredTactical and finishingMoveAllowed. Checked against the 762 plain rounds (no ability,
    /// opening or withdrawal) among 1,121 retail RPC 45s in research/packet-dumps: the rule gives retail's "won" byte in
    /// 664 of the 665 it decides. The other 97 had the same tactic on both sides and both or neither hitting; retail
    /// settled those by ability level or a coin, and they went about half each way.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>
        /// RPC 45's outcome flags (constants.combat.*). The values were matched to the scripts' cases in the captures:
        /// 0x801 and 0x820 only on opening rounds, 0x41 and 0x48 only on kills, 0x200 with a withdrawal.
        /// </summary>
        public const UInt32 AttackerHits = 0x01, NoHits = 0x04, BothHit = 0x08, Draw = 0x20, FinishingMove = 0x40,
            SpecialMove = 0x100, Withdraw = 0x200, OpportunityAttack = 0x800;

        /// <summary>
        /// combat_utility.isPreferredTactical: Power beats Speed, Grab beats Power, Speed beats Grab, and anything
        /// beats Block. The preferred side attacks first.
        /// </summary>
        public static bool PreferredTactic(CombatTactic tactic, CombatTactic against)
        {
            switch (against)
            {
                case CombatTactic.Speed: return tactic == CombatTactic.Power;
                case CombatTactic.Power: return tactic == CombatTactic.Grab;
                case CombatTactic.Grab: return tactic == CombatTactic.Speed;
                case CombatTactic.Block: return tactic != CombatTactic.Block;
                default: return false;
            }
        }

        /// <summary>One side of a round, as the round's rolls and damage left it.</summary>
        public class Fighter
        {
            public CombatTactic Tactic;
            /// <summary>Its attack roll beat the other's defense roll (combat_resolution.getCTandDTotals).</summary>
            public bool Hits;
            /// <summary>Its attack roll (the move's TacticalValue): the higher one wins when both sides use special moves.</summary>
            public int AttackRoll;
            /// <summary>It used a special move (an ability as its move) this round.</summary>
            public bool SpecialMove;
            /// <summary>The damage it deals if it lands, and the absorbed part (combat_resolution.calcDamageForLoser).</summary>
            public int Damage;
            public int Absorbed;
            /// <summary>Health before the round: a hit that takes all of it is a finishing move.</summary>
            public int Health;
        }

        public class Outcome
        {
            /// <summary>RPC 45's "won": the player holds retail's winnerSlot.</summary>
            public bool PlayerWins;
            public UInt32 Flags;
            /// <summary>The player's special move landed: no damage either way this round.</summary>
            public bool SpecialMoveLands;
            /// <summary>
            /// Damage each side takes, as RPC 45 carries it. After a finishing move these can be odd: retail swapped
            /// the killer's damage and absorbed fields (see <see cref="Resolve"/>), so 0 with 1 absorbed, or -1.
            /// </summary>
            public int DamageToPlayer, PlayerAbsorbed, DamageToNpc, NpcAbsorbed;
        }

        /// <summary>
        /// combat_resolution.determineAttacker for a fight between one player and one NPC: on the opening round, whoever
        /// started the fight (opportunityAttack); otherwise the preferred tactic; with the same tactic retail compared
        /// ability levels and then tossed a coin. This server doesn't model ability levels, so it's the coin.
        /// </summary>
        public static bool PlayerAttacksFirst(CombatTactic player, CombatTactic npc, bool opening, bool playerOpened, Func<bool> coin)
        {
            if (opening)
            {
                return playerOpened;
            }
            if (player != npc)
            {
                return PreferredTactic(player, npc);
            }
            return coin();
        }

        /// <summary>
        /// determineResultForStandardCombat with no withdrawal, no prone states and nobody else in the fight:
        /// <list type="bullet">
        /// <item>When the defender hits and the attacker doesn't, they swap: the "true attacker" is whoever landed.</item>
        /// <item>A special move that hits wins the round outright (handleSpecialMove, 0x100).</item>
        /// <item>The opening round is the starter's opportunity attack (handleOppertunityAttack): the starter deals its
        /// damage unless the other side hit and it didn't, in which case only the other side does (0x20, a draw). The
        /// starter is the winner either way.</item>
        /// <item>A true attacker on Block (handleBlock): nobody is hurt, and the side not on Block wins.</item>
        /// <item>Otherwise a plain exchange (handleStandardExchange): each side that hit deals its damage, the true
        /// attacker wins. A hit that takes all of the other's health is a finishing move (0x40). When both hit and only
        /// the true attacker's hit kills, the win goes to the defender and the damage fields swap sides, with retail's
        /// bug: the killer's own damage and absorbed amounts come out swapped (retail: 0 damage, 1 absorbed).</item>
        /// </list>
        /// </summary>
        public static Outcome Resolve(Fighter player, Fighter npc, bool playerAttacksFirst, bool opening)
        {
            Fighter attacker = playerAttacksFirst ? player : npc;
            Fighter defender = playerAttacksFirst ? npc : player;
            bool attackerHits = attacker.Hits, defenderHits = defender.Hits;

            bool swap = defenderHits && !attackerHits;
            if (attackerHits && defenderHits && attacker.SpecialMove && defender.SpecialMove)
            {
                swap = defender.AttackRoll > attacker.AttackRoll;
            }
            else if (defender.SpecialMove && defenderHits)
            {
                swap = true;
            }
            Fighter trueAttacker = swap ? defender : attacker;
            Fighter trueDefender = swap ? attacker : defender;
            bool trueAttackerHits = swap ? defenderHits : attackerHits;
            bool trueDefenderHits = swap ? attackerHits : defenderHits;

            Fighter winner;
            UInt32 flags;
            // Damage each takes: [0] = winner, [1] = loser (retail's winnerDamage and loserDamage)
            int winnerDamage = 0, winnerAbsorbed = 0, loserDamage = 0, loserAbsorbed = 0;
            bool specialMoveLands = false;

            if (trueAttacker.SpecialMove && trueAttackerHits)
            {
                winner = trueAttacker;
                flags = SpecialMove;
                specialMoveLands = true;
            }
            else if (opening)
            {
                winner = attacker;
                if (trueAttacker == attacker)
                {
                    flags = AttackerHits | OpportunityAttack;
                    loserDamage = DamageOf(attacker);
                    loserAbsorbed = AbsorbedOf(attacker);
                }
                else
                {
                    flags = Draw | OpportunityAttack;
                    winnerDamage = DamageOf(defender);
                    winnerAbsorbed = AbsorbedOf(defender);
                }
            }
            else if (trueAttacker.Tactic == CombatTactic.Block)
            {
                winner = trueDefender.Tactic == CombatTactic.Block ? trueAttacker : trueDefender;
                flags = NoHits;
            }
            else
            {
                winner = trueAttacker;
                if (trueAttackerHits)
                {
                    loserDamage = DamageOf(trueAttacker);
                    loserAbsorbed = AbsorbedOf(trueAttacker);
                }
                if (trueDefenderHits)
                {
                    winnerDamage = DamageOf(trueDefender);
                    winnerAbsorbed = AbsorbedOf(trueDefender);
                }
                flags = trueAttackerHits && trueDefenderHits ? BothHit : trueAttackerHits || trueDefenderHits ? AttackerHits : NoHits;

                bool attackerFinishes = loserDamage > 0 && loserDamage >= trueDefender.Health;
                bool defenderFinishes = winnerDamage > 0 && winnerDamage >= trueAttacker.Health;
                if (attackerFinishes || defenderFinishes)
                {
                    if (flags == BothHit && attackerFinishes && !defenderFinishes)
                    {
                        winner = trueDefender;
                        int killerTakes = winnerDamage, killerAbsorbs = winnerAbsorbed;
                        winnerDamage = loserDamage;
                        winnerAbsorbed = loserAbsorbed;
                        loserDamage = killerAbsorbs; // retail's swap of the two fields
                        loserAbsorbed = killerTakes;
                    }
                    // Both finishing (FINISHING_MOVE_BOTH) has a flag value no capture shows; this uses the plain one
                    flags |= FinishingMove;
                }
            }

            bool playerWins = winner == player;
            return new Outcome
            {
                PlayerWins = playerWins,
                Flags = flags,
                SpecialMoveLands = specialMoveLands && playerWins,
                DamageToPlayer = playerWins ? winnerDamage : loserDamage,
                PlayerAbsorbed = playerWins ? winnerAbsorbed : loserAbsorbed,
                DamageToNpc = playerWins ? loserDamage : winnerDamage,
                NpcAbsorbed = playerWins ? loserAbsorbed : winnerAbsorbed,
            };
        }

        /// <summary>calculateDamage: nothing for a fighter whose move is a special move.</summary>
        private static int DamageOf(Fighter f) => f.SpecialMove ? 0 : f.Damage;
        private static int AbsorbedOf(Fighter f) => f.SpecialMove ? 0 : f.Absorbed;

        #region combat_modifiers

        // interlock/combat_modifiers: roll modifiers for fights with more than one opponent and for a gun against
        // fists inside the interlock. No script calls getAttackerMods/getDefenderMods, so the engine applied them, if
        // at all. They don't come up on this server yet: fights are one on one, and close combat is always fists.

        /// <summary>getMultiOnOnePenalty: -5 per opponent fought once more than <paramref name="multiOnOneCount"/>
        /// (consolevar.CombatMultiOnOneCount, unknown) are on you. An ability that negates it still costs -5 per
        /// opponent beyond two.</summary>
        public static int MultiOnOnePenalty(int opponentCount, int opponentsFought, bool penaltyNegated, int multiOnOneCount)
        {
            if (opponentCount <= multiOnOneCount)
            {
                return 0;
            }
            if (!penaltyNegated)
            {
                return opponentsFought * -5;
            }
            return opponentsFought < 3 ? 0 : (opponentsFought - 2) * -5;
        }

        /// <summary>getBaseRangedValue: a gun's edge over fists, by class.</summary>
        public static int GunEdge(byte attackClass)
        {
            switch (attackClass)
            {
                case RangedWeapons.ClassPistol:
                case RangedWeapons.ClassDualPistol: return 5;
                case RangedWeapons.ClassSubmachinegun: return 10; // RANGED_MACHINE_GUN; dual SMGs aren't in the script's list
                case RangedWeapons.ClassRifle: return 15;
                default: return 0;
            }
        }

        /// <summary>
        /// getRangedVsMeleeAttackModifier: a gun against fists. At range the gun gets its edge; at short range the edge
        /// becomes a penalty (unless an ability negates it); firing into a fight where the target has more than one
        /// opponent costs 15 more (unless negated).
        /// </summary>
        public static int GunAgainstFistsModifier(byte attackerGunClass, bool defenderHasGun, bool shortRange, int defenderOpponents,
            bool intoMeleePenaltyNegated = false, bool shortRangePenaltyNegated = false, bool meleeAtRangePenaltyNegated = false)
        {
            if (attackerGunClass == 0 || defenderHasGun)
            {
                return 0;
            }
            int baseModifier = defenderOpponents > 1 && !intoMeleePenaltyNegated ? -15 : 0;
            int edge = GunEdge(attackerGunClass);
            if (shortRange)
            {
                return shortRangePenaltyNegated ? baseModifier : baseModifier - edge;
            }
            return meleeAtRangePenaltyNegated ? baseModifier : baseModifier + edge;
        }

        #endregion
    }
}
