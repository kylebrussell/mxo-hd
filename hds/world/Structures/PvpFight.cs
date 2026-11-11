using System;
using System.Timers;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Close combat (the interlock) between two players. The rules are the ones NPC fights use
    /// (<see cref="CombatRules"/>, <see cref="CombatInterlock"/>: retail's own scripts), applied to two players' tactics,
    /// levels and special moves; each side gets the round records from its own point of view.
    /// <para>What each side is sent, from the retail captures (docs/PVP.md): the attacker gets what a player who
    /// attacks an NPC gets. The attacked player gets, unasked, the combat view, their own first move in an RPC 47 kind 4
    /// with the has-ability byte 0, the CombatEffect records, <c>40 &lt;attacker&gt; 00 6b</c> and <c>46 &lt;attacker&gt; 01</c>,
    /// then the combat init with the attacker first. In RPC 45 the first move, the "won" byte and the first damage
    /// field are always the receiver's own.</para>
    /// </summary>
    public sealed class PvpFight
    {
        public enum EndReason
        {
            /// <summary>The named player died (or lost the duel).</summary>
            Death,
            /// <summary>The named player withdrew (RPC 45 result 0x200).</summary>
            Withdrew,
            /// <summary>The named player's client is gone or reloading its world: it gets no teardown.</summary>
            Left,
            /// <summary>The duel the fight belonged to is over.</summary>
            DuelOver,
        }

        public sealed class Side
        {
            public WorldClient Client;
            public Side Other;
            /// <summary>The opponent as this side's client knows them.</summary>
            public UInt32 OpponentHandle;
            public UInt16 HandlerViewId;
            /// <summary>The move the next round decides.</summary>
            public CombatInterlock.Move Move;
            public AbilityItem QueuedAbility;
            public bool Withdrawing;
            public UInt16 RoundRecord;
            public UInt16 CombatEffectRecordId;
        }

        /// <summary>The player who asked for the fight.</summary>
        public Side Attacker { get; }
        public Side Defender { get; }
        public bool IsActive { get; private set; }
        public UInt32 Rounds { get; private set; }

        private readonly object sync = new object();
        private readonly Random rng = new Random();
        private Timer timer;
        private GameObject handlerObject;
        private UInt64 handlerEntity;
        private byte[] centre;

        private PvpFight(WorldClient attacker, WorldClient defender)
        {
            Attacker = new Side { Client = attacker };
            Defender = new Side { Client = defender };
            Attacker.Other = Defender;
            Defender.Other = Attacker;
        }

        public bool Involves(WorldClient client) => Attacker.Client == client || Defender.Client == client;

        public Side SideOf(WorldClient client) => Attacker.Client == client ? Attacker : Defender.Client == client ? Defender : null;

        private CombatInterlock.Move NewMove(byte seq, CombatTactic tactic)
        {
            lock (rng)
            {
                return new CombatInterlock.Move
                {
                    Seq = seq,
                    Tactic = tactic,
                    Value = (UInt16)rng.Next(1, 100),
                    R1 = (UInt16)rng.Next(5, 100),
                    R2 = (UInt16)rng.Next(5, 100),
                    R3 = (UInt16)rng.Next(5, 100),
                    R4 = (UInt16)rng.Next(5, 100),
                };
            }
        }

        /// <summary>
        /// Starts the interlock: <paramref name="request"/> is the attacker's RPC 40 (target u32, ForcedInterlock,
        /// WeaponToUseSlot). The opening round is resolved at once, as retail did.
        /// </summary>
        public static PvpFight Start(WorldClient attacker, WorldClient defender, byte[] request)
        {
            PvpFight fight = new PvpFight(attacker, defender);
            lock (fight.sync)
            {
                fight.Begin(request);
            }
            fight.ResolveRound(true);
            return fight;
        }

        private void Begin(byte[] request)
        {
            WorldClient attacker = Attacker.Client, defender = Defender.Client;
            // Whatever either was doing ends here (ours: retail's interlocks could hold more fighters)
            CombatManager.GetHandler(attacker).LeaveOtherFights();
            CombatManager.GetHandler(defender).LeaveOtherFights();
            TradeHandler.Abort(attacker);
            TradeHandler.Abort(defender);

            Attacker.OpponentHandle = PvpCombat.HandleOf(attacker, defender);
            Defender.OpponentHandle = PvpCombat.HandleOf(defender, attacker);
            PvpFlag.OnFightStarts(attacker, true);
            PvpFlag.OnFightStarts(defender, false);
            Output.WriteDebugLog("[PVP] " + PvpFlag.Name(attacker) + " pulls " + PvpFlag.Name(defender) + " into close combat");

            // One combat view object, seen by both
            var handler = new GameObjectDefinitions().Object55;
            handler.DisableAllAttributes();
            handler.StartTime.enable();
            handler.Position.enable();
            handler.StartTime.setValue(CombatHandler.CombatStartTime());
            centre = attacker.playerInstance.Position.getValue();
            handler.Position.setValue(centre);
            handlerObject = handler;
            lock (WorldServer.gameServerEntities)
            {
                handlerEntity = WorldServer.entityIdCounter++;
                WorldServer.gameServerEntities.Add(handler);
            }

            ServerPackets packets = new ServerPackets();
            UInt32 attackerSelf = PvpCombat.HandleOf(attacker, attacker), defenderSelf = PvpCombat.HandleOf(defender, defender);

            // The attacker: the answers a close-combat request at an NPC gets (CombatHandler.ProcessRequestCloseCombat)
            if (request.Length > 4 && request[4] != 0)
            {
                attacker.messageQueue.addRpcMessage(new byte[] { 0x4e, 0x01 });
            }
            attacker.messageQueue.addRpcMessage(CombatHandler.BuildCloseCombatEcho(request));
            packets.SendEnterCombatMode(attacker);
            Attacker.HandlerViewId = SpawnHandlerFor(attacker, packets);
            attacker.messageQueue.addObjectMessage(CombatInit(Attacker.HandlerViewId, attackerSelf, Attacker.OpponentHandle), false);
            attacker.playerData.combatGeneration++;
            attacker.messageQueue.addRpcMessage(ServerPackets.BuildCombatTargetFlag(Attacker.OpponentHandle, 0x01));
            Attacker.Move = NewMove(1, attacker.playerData.combatTactic);
            Attacker.Move.First = true;
            attacker.messageQueue.addRpcMessage(CombatInterlock.BuildRoundState(CombatInterlock.KindStart, Attacker.Move, null));
            SendCombatEffect(Attacker, false);
            attacker.FlushQueue();

            // The attacked player (retail, died_in_pvp 19:41:11): own flags, the combat view, 47 kind 4 with their own
            // move and the has-ability byte 0, the CombatEffect records, 40 <attacker> 00 6b, 46 <attacker> 01, then the
            // combat init with the attacker first
            packets.SendEnterCombatMode(defender);
            Defender.HandlerViewId = SpawnHandlerFor(defender, packets);
            defender.playerData.combatGeneration++;
            Defender.Move = NewMove(1, defender.playerData.combatTactic);
            Defender.Move.NoAbilityField = true;
            defender.messageQueue.addRpcMessage(CombatInterlock.BuildRoundState(CombatInterlock.KindStart, Defender.Move, null));
            SendCombatEffect(Defender, false);
            defender.messageQueue.addRpcMessage(BuildAttackedBy(Defender.OpponentHandle));
            defender.messageQueue.addRpcMessage(ServerPackets.BuildCombatTargetFlag(Defender.OpponentHandle, 0x01));
            defender.messageQueue.addObjectMessage(CombatInit(Defender.HandlerViewId, Defender.OpponentHandle, defenderSelf), false);
            defender.FlushQueue();

            foreach (Side side in new[] { Attacker, Defender })
            {
                CombatHandler h = CombatManager.GetHandler(side.Client);
                h.pvpFight = this;
                h.isCombatRunning = true;
            }
            IsActive = true;
            timer = new Timer(CombatInterlock.RoundMs) { AutoReset = true };
            timer.Elapsed += (s, e) => OnRoundTimer();
            timer.Start();
        }

        /// <summary>
        /// The combat init with the rest of its record (the fixed block NPC fights send after it), as one message: the
        /// two halves are one record and nothing may come between them.
        /// </summary>
        private byte[] CombatInit(UInt16 handlerViewId, UInt32 first, UInt32 second)
        {
            byte[] init = ServerPackets.BuildCombatInitialize(handlerViewId, centre, first, second);
            byte[] rest = CombatHandler.CombatStateBlob();
            byte[] record = new byte[init.Length + rest.Length];
            Array.Copy(init, record, init.Length);
            Array.Copy(rest, 0, record, init.Length, rest.Length);
            return record;
        }

        /// <summary>
        /// <c>40 &lt;attacker&gt; 00 6b</c>: what the attacked player got in place of the echo of a request of their own
        /// (retail: <c>40 05 00 f8 00 00 6b</c>, with no client 40 before it).
        /// </summary>
        public static byte[] BuildAttackedBy(UInt32 attacker)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x40);
            pak.AddUint32(attacker, 1);
            pak.AddByte(0x00);
            pak.AddByte(0x6b);
            return pak.ReturnFinalPacket();
        }

        private UInt16 SpawnHandlerFor(WorldClient client, ServerPackets packets)
        {
            packets.SendSpawnGameObject(client, handlerObject, handlerEntity);
            return client.viewMan.GetViewForEntityAndGo(handlerEntity,
                NumericalUtils.ByteArrayToUint16(handlerObject.GetGoid(), 1)).ViewID;
        }

        private static void SendCombatEffect(Side side, bool off)
        {
            ClientData data = side.Client.playerData;
            if (!off)
            {
                lock (data.activeBuffs)
                {
                    if (data.nextBuffRecordId > UInt16.MaxValue - 2)
                    {
                        data.nextBuffRecordId = AbilityBuffs.FirstRecordId;
                    }
                    side.CombatEffectRecordId = data.nextBuffRecordId;
                    data.nextBuffRecordId += 2;
                }
            }
            else if (side.CombatEffectRecordId == 0)
            {
                return;
            }
            foreach (byte[] record in CombatInterlock.BuildCombatEffectRecords(side.CombatEffectRecordId, off))
            {
                side.Client.messageQueue.addRpcMessage(record);
            }
        }

        private void OnRoundTimer()
        {
            try
            {
                if (!IsActive)
                {
                    return;
                }
                // A player whose connection died is cleaned up by the visibility thread; one who is dead or gone for
                // another reason ends the fight here
                foreach (Side side in new[] { Attacker, Defender })
                {
                    WorldClient c = side.Client;
                    if (!c.Alive || !c.playerData.getOnWorld() || c.playerData.isDead || c.playerData.duelDeath)
                    {
                        End(c.playerData.isDead || c.playerData.duelDeath ? EndReason.Death : EndReason.Left, c);
                        return;
                    }
                }
                Rounds++;
                CombatHandler.ApplyRoundInnerStrength(Attacker.Client);
                CombatHandler.ApplyRoundInnerStrength(Defender.Client);
                ResolveRound(false);
            }
            catch (Exception ex)
            {
                Output.WriteLine("[PVP] Round failed: " + ex);
            }
        }

        /// <summary>The player's next move changed (a new tactic, an ability, a withdrawal): RPC 47 kind 6.</summary>
        private void SendMoveChanged(Side side)
        {
            CombatInterlock.Move decided = side.Move;
            CombatInterlock.Move next = NewMove((byte)(decided.Seq + 1), side.Client.playerData.combatTactic);
            if (side.QueuedAbility != null)
            {
                next.Ability = side.QueuedAbility.getAbilityID();
            }
            next.Withdraw = side.Withdrawing;
            side.Client.messageQueue.addRpcMessage(CombatInterlock.BuildRoundState(CombatInterlock.KindNextMoveChanged, decided, next));
            side.Move = next;
            side.Client.FlushQueue();
        }

        public void OnTacticChanged(WorldClient client)
        {
            lock (sync)
            {
                Side side = SideOf(client);
                if (IsActive && side?.Move != null)
                {
                    SendMoveChanged(side);
                }
            }
        }

        /// <summary>An instant ability on the opponent becomes the player's next move. False when the target isn't the opponent.</summary>
        public bool QueueAbility(WorldClient client, AbilityItem ability, UInt16 targetViewId)
        {
            lock (sync)
            {
                Side side = SideOf(client);
                if (!IsActive || side?.Move == null || (UInt16)side.OpponentHandle != targetViewId)
                {
                    return false;
                }
                side.QueuedAbility = ability;
                SendMoveChanged(side);
                Output.WriteDebugLog("[PVP] " + ability.getAbilityName() + " is " + PvpFlag.Name(client) + "'s next move");
                return true;
            }
        }

        /// <summary>RPC 44: the player wants out. It is tried at each round until their roll beats the opponent's.</summary>
        public void RequestWithdraw(WorldClient client)
        {
            lock (sync)
            {
                Side side = SideOf(client);
                if (!IsActive || side?.Move == null || side.Withdrawing)
                {
                    return;
                }
                side.Withdrawing = true;
                SendMoveChanged(side);
                Output.WriteDebugLog("[PVP] " + PvpFlag.Name(client) + " is withdrawing");
            }
        }

        private void ResolveRound(bool opening)
        {
            WorldClient dead = null, killer = null;
            AbilityItem landed = null;
            Side landedBy = null;
            Side withdrew = null;
            lock (sync)
            {
                if (!IsActive)
                {
                    return;
                }
                // A server-played fighter picks a tactic each round, like an NPC
                foreach (Side side in new[] { Attacker, Defender })
                {
                    if (side.Client.playerData.isBot)
                    {
                        lock (rng)
                        {
                            side.Client.playerData.combatTactic = CombatInterlock.NpcTactic(rng);
                        }
                        side.Move.Tactic = side.Client.playerData.combatTactic;
                    }
                }

                // The roles of CombatRules.Resolve: its "player" may withdraw, so a withdrawing defender takes that role
                bool bothWithdraw = Attacker.Withdrawing && Defender.Withdrawing && !opening;
                Side p = Defender.Withdrawing && !Attacker.Withdrawing && !opening ? Defender : Attacker;
                Side n = p.Other;

                int pLevel = PvpFlag.LevelOf(p.Client), nLevel = PvpFlag.LevelOf(n.Client);
                CombatTactic pTactic = p.Client.playerData.combatTactic, nTactic = n.Client.playerData.combatTactic;
                CombatTacticType pType = p.Client.playerData.combatTacticType, nType = n.Client.playerData.combatTacticType;
                AbilityItem pAbility = opening ? null : p.QueuedAbility, nAbility = opening ? null : n.QueuedAbility;
                if (!opening)
                {
                    p.QueuedAbility = null;
                    n.QueuedAbility = null;
                }

                bool pHit, nHit, pCondition, nCondition, pFirst;
                UInt16 pAttack, pDefense, nAttack, nDefense, pDamage, nDamage;
                Int16 absorbedByN, absorbedByP;
                lock (rng)
                {
                    pHit = CombatInterlock.RollHit(rng, pTactic, pLevel, nTactic, nLevel, out pAttack, out nDefense, pType);
                    nHit = CombatInterlock.RollHit(rng, nTactic, nLevel, pTactic, pLevel, out nAttack, out pDefense, nType);
                    pCondition = rng.Next(100) < CombatInterlock.ConditionChancePercent;
                    nCondition = rng.Next(100) < CombatInterlock.ConditionChancePercent;
                    pDamage = CombatCalculator.CloseCombatDamage(rng, pLevel, nLevel, pTactic, out absorbedByN, pType);
                    pDamage = (UInt16)Math.Round(pDamage * PvpCombat.IncomingDamageMultiplier(n.Client));
                    nDamage = CombatCalculator.CloseCombatDamage(rng, nLevel, pLevel, nTactic, out absorbedByP, nType);
                    nDamage = (UInt16)Math.Round(nDamage * PvpCombat.IncomingDamageMultiplier(p.Client));
                    pFirst = CombatRules.PlayerAttacksFirst(pTactic, nTactic, opening, p == Attacker, () => rng.Next(2) == 0);
                }

                CombatRules.Outcome outcome = bothWithdraw
                    ? new CombatRules.Outcome { PlayerWins = true, Flags = CombatRules.Withdraw, Withdrew = true }
                    : CombatRules.Resolve(
                        new CombatRules.Fighter
                        {
                            Tactic = pTactic, Hits = pHit, AttackRoll = pAttack, SpecialMove = pAbility != null,
                            Damage = pDamage, Absorbed = absorbedByN, Health = PvpCombat.HealthOf(p.Client)
                        },
                        new CombatRules.Fighter
                        {
                            Tactic = nTactic, Hits = nHit, AttackRoll = nAttack, SpecialMove = nAbility != null,
                            Damage = nDamage, Absorbed = absorbedByP, Health = PvpCombat.HealthOf(n.Client)
                        },
                        pFirst, opening, p.Withdrawing);

                // The moves this round decided carry the rolls (attack roll as the value, defense roll first)
                CombatInterlock.Move pDecided = p.Move, nDecided = n.Move;
                pDecided.Value = pAttack;
                pDecided.R1 = pDefense;
                nDecided.Value = nAttack;
                nDecided.R1 = nDefense;

                SendRound(p, pDecided, nDecided, outcome.PlayerWins, outcome.Flags, outcome.DamageToPlayer, outcome.PlayerAbsorbed,
                    outcome.DamageToNpc, outcome.NpcAbsorbed, opening, p.Withdrawing && !opening ? pAttack - nAttack : 0);
                SendRound(n, nDecided, pDecided, !outcome.PlayerWins, outcome.Flags, outcome.DamageToNpc, outcome.NpcAbsorbed,
                    outcome.DamageToPlayer, outcome.PlayerAbsorbed, opening, 0);

                bool special = (outcome.Flags & CombatRules.SpecialMove) != 0;
                Output.WriteDebugLog($"[PVP] Round {Rounds}{(opening ? " (opening)" : "")}: {PvpFlag.Name(p.Client)} {pTactic} ({pAttack}/{pDefense}) vs " +
                                     $"{PvpFlag.Name(n.Client)} {nTactic} ({nAttack}/{nDefense}), result 0x{outcome.Flags:x}, " +
                                     $"{outcome.DamageToNpc} to {PvpFlag.Name(n.Client)}, {outcome.DamageToPlayer} to {PvpFlag.Name(p.Client)}");

                if (outcome.Withdrew)
                {
                    withdrew = p;
                }
                else if (special)
                {
                    landedBy = outcome.PlayerWins ? p : n;
                    landed = outcome.PlayerWins ? pAbility : nAbility;
                }
                else
                {
                    // A finishing move can leave a damage field at -1 (retail's swap); nobody is healed by it
                    int toN = Math.Max(0, outcome.DamageToNpc), toP = Math.Max(0, outcome.DamageToPlayer);
                    bool nDied = toN > 0 && PvpCombat.Damage(n.Client, toN);
                    bool pDied = toP > 0 && PvpCombat.Damage(p.Client, toP);
                    if (toN > 0 && !nDied && pCondition)
                    {
                        PvpCombat.ApplyCondition(n.Client, CombatInterlock.ConditionBit(pTactic));
                    }
                    if (toP > 0 && !pDied && nCondition)
                    {
                        PvpCombat.ApplyCondition(p.Client, CombatInterlock.ConditionBit(nTactic));
                    }
                    if (nDied)
                    {
                        dead = n.Client;
                        killer = p.Client;
                    }
                    else if (pDied)
                    {
                        dead = p.Client;
                        killer = n.Client;
                    }
                }
                p.Client.FlushQueue();
                n.Client.FlushQueue();
            }

            // Outside the fight's lock: these end the fight, and may end a duel
            if (withdrew != null)
            {
                Output.WriteDebugLog("[PVP] " + PvpFlag.Name(withdrew.Client) + " withdraws");
                End(EndReason.Withdrew, withdrew.Client);
            }
            else if (landed != null)
            {
                PvpCombat.LandAbilityOn(landedBy.Client, landed, landedBy.Other.Client);
            }
            else if (dead != null)
            {
                PvpCombat.Defeated(dead, killer);
            }
        }

        /// <summary>One side's records of a round: RPC 47 kind 3 with its next move (after the 45 on the opening round) and RPC 45.</summary>
        private void SendRound(Side side, CombatInterlock.Move own, CombatInterlock.Move theirs, bool won, UInt32 flags,
            int damageToSelf, int selfAbsorbed, int damageToOpponent, int opponentAbsorbed, bool opening, int combatResult)
        {
            WorldClient client = side.Client;
            CombatInterlock.Move next = NewMove((byte)(own.Seq + 1), client.playerData.combatTactic);
            next.Withdraw = side.Withdrawing;
            if (!opening)
            {
                client.messageQueue.addRpcMessage(CombatInterlock.BuildRoundState(CombatInterlock.KindNewRound, own, next));
            }
            client.messageQueue.addRpcMessage(CombatInterlock.BuildRoundResult(side.RoundRecord, won, own, theirs, side.OpponentHandle, flags,
                (UInt16)damageToSelf, (Int16)selfAbsorbed, (UInt16)damageToOpponent, (Int16)opponentAbsorbed, CombatHandler.CombatStartTime(),
                combatResult, theirs.Flag));
            side.RoundRecord++;
            if (opening)
            {
                // As after an NPC fight's opening round: the next move against a blank decided one
                client.messageQueue.addRpcMessage(CombatInterlock.BuildRoundState(CombatInterlock.KindNewRound,
                    new CombatInterlock.Move { Seq = 0, Tactic = client.playerData.combatTactic }, next));
            }
            side.Move = next;
        }

        /// <summary>
        /// Ends the fight. Each side still there gets the teardown an NPC fight ends with (the combat view's final
        /// record, <c>46 &lt;opponent&gt; 00</c>, the delete of the combat view, CombatEffect off, and the combat stance and
        /// flags reset ten seconds later). <paramref name="who"/> is the player the reason is about.
        /// </summary>
        public void End(EndReason reason, WorldClient who)
        {
            lock (sync)
            {
                if (!IsActive)
                {
                    return;
                }
                IsActive = false;
                timer?.Stop();
                timer?.Dispose();
                timer = null;
                Output.WriteDebugLog("[PVP] Close combat between " + PvpFlag.Name(Attacker.Client) + " and " + PvpFlag.Name(Defender.Client) +
                                     " ends: " + reason + " (" + (who != null ? PvpFlag.Name(who) : "-") + ")");

                ServerPackets packets = new ServerPackets();
                foreach (Side side in new[] { Attacker, Defender })
                {
                    WorldClient client = side.Client;
                    CombatHandler handler = CombatManager.GetHandler(client);
                    handler.pvpFight = null;
                    handler.isCombatRunning = false;
                    if (reason == EndReason.Left && client == who)
                    {
                        // Its views are gone with its world; a pending leave-combat reset belongs to the old one
                        client.playerData.combatGeneration++;
                        continue;
                    }
                    SendCombatEffect(side, true);
                    packets.SendCombatEnd(client, side.HandlerViewId, side.OpponentHandle, centre, Rounds);
                    client.viewMan.removeViewByViewId(side.HandlerViewId);
                    client.FlushQueue();
                }
                lock (WorldServer.gameServerEntities)
                {
                    WorldServer.gameServerEntities.Remove(handlerObject);
                }
            }
            if (reason == EndReason.Left && who != null)
            {
                DuelHandler.OnUnavailable(who);
            }
        }
    }
}
