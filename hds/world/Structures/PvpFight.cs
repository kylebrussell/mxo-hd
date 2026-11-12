using System;
using System.Collections.Generic;
using System.Linq;
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
            public byte Slot;
            public bool Present = true;
            public Side Other;
            /// <summary>The opponent as this side's client knows them.</summary>
            public UInt32 OpponentHandle;
            public UInt16 HandlerViewId;
            /// <summary>The move the next round decides.</summary>
            public CombatInterlock.Move Move;
            public AbilityItem QueuedAbility;
            public bool SneakOpener;
            public bool Withdrawing;
            public UInt16 RoundRecord;
            public UInt16 CombatEffectRecordId;
            /// <summary>The fight is over but this side's client still has the combat view (see <see cref="End"/>).</summary>
            public bool TeardownPending;
            /// <summary>This side lost by death: retail sent it no <c>46 &lt;opponent&gt; 00</c>.</summary>
            public bool Fell;
        }

        /// <summary>
        /// How long after a death (a duel lost, a PvP kill) the interlock is taken down. Retail sent the death first and
        /// deleted the combat view 8 to 9 s later, ticking it meanwhile: the loser at 03:03:51 and 03:04:00
        /// (niobe3dualduality), the winner at 21:54:53 and 21:55:01 (lastday2). Taking it down first, in the same packet
        /// as the death, left the real client standing at 0 health with its DuelID still set (PR 94 QA).
        /// </summary>
        public const double DeathTeardownSeconds = 8;

        /// <summary>The same for a duel stopped otherwise (surrender, distance, a player gone): ours, just long enough for
        /// the duel's end to reach the client in a packet of its own first.</summary>
        public const double StopTeardownSeconds = 1;

        private static readonly object PendingSync = new object();
        private static readonly System.Collections.Generic.Dictionary<WorldClient, PvpFight> PendingTeardowns =
            new System.Collections.Generic.Dictionary<WorldClient, PvpFight>();

        /// <summary>The delay before the teardown for a fight that ends for this reason.</summary>
        public static double TeardownDelayFor(EndReason reason)
        {
            switch (reason)
            {
                case EndReason.Death: return DeathTeardownSeconds;
                case EndReason.DuelOver:
                case EndReason.Left: return StopTeardownSeconds;
                default: return 0;
            }
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
        private CombatPresentation presentation;
        private readonly List<Side> members = new List<Side>();
        public const int MaximumFighters = 6;
        public int FighterCount { get { lock (sync) return members.Count(s => s.Present); } }

        private PvpFight(WorldClient attacker, WorldClient defender)
        {
            Attacker = new Side { Client = attacker, Slot = 1 };
            Defender = new Side { Client = defender, Slot = 2 };
            Attacker.Other = Defender;
            Defender.Other = Attacker;
            members.Add(Attacker); members.Add(Defender);
        }

        public bool Involves(WorldClient client) { lock (sync) return members.Any(s => s.Present && s.Client == client); }

        public Side SideOf(WorldClient client) { lock (sync) return members.LastOrDefault(s => s.Client == client); }

        public bool ChangeTarget(WorldClient client, WorldClient target, byte[] request)
        {
            lock (sync)
            {
                Side side = SideOf(client), opponent = SideOf(target);
                if (!IsActive || side?.Present != true || opponent?.Present != true || side == opponent) return false;
                if (side.Other == opponent) return true;
                client.messageQueue.addRpcMessage(ServerPackets.BuildCombatTargetFlag(side.OpponentHandle, 0));
                side.Other = opponent;
                side.OpponentHandle = PvpCombat.HandleOf(client, target);
                client.messageQueue.addRpcMessage(CombatHandler.BuildCloseCombatEcho(request));
                client.messageQueue.addRpcMessage(ServerPackets.BuildCombatTargetFlag(side.OpponentHandle, 1));
                client.FlushQueue();
                return true;
            }
        }

        /// <summary>Join the target's existing six-slot interlock. Duels are still two people, by PvpRules.Check.</summary>
        public bool Join(WorldClient client, WorldClient target, byte[] request)
        {
            Side joined, opponent;
            lock (sync)
            {
                opponent = SideOf(target);
                if (!IsActive || opponent?.Present != true || FighterCount >= MaximumFighters || Involves(client)) return false;
                if (members.Where(s => s.Present).Any(s => PvpCombat.HandleOf(client, s.Client) == 0 ||
                    PvpCombat.HandleOf(s.Client, client) == 0)) return false;
                byte slot = Enumerable.Range(1, MaximumFighters).Select(i => (byte)i).First(i => !members.Any(s => s.Present && s.Slot == i));
                CombatManager.GetHandler(client).LeaveOtherFights();
                TradeHandler.Abort(client);
                joined = new Side { Client = client, Other = opponent, Slot = slot,
                    OpponentHandle = PvpCombat.HandleOf(client, target), Move = NewMove(1, client.playerData.combatTactic) };
                joined.Move.First = true;
                members.Add(joined);
                CombatHandler handler = CombatManager.GetHandler(client);
                handler.pvpFight = this; handler.isCombatRunning = true;
                client.playerData.combatGeneration++;
                PvpFlag.OnFightStarts(client, true);
                var packets = new ServerPackets();
                if (request.Length > 4 && request[4] != 0) client.messageQueue.addRpcMessage(new byte[] { 0x4e, 1 });
                client.messageQueue.addRpcMessage(CombatHandler.BuildCloseCombatEcho(request));
                packets.SendEnterCombatMode(client);
                joined.HandlerViewId = SpawnHandlerFor(client, packets);
                presentation.AddFighter(client, slot);
                client.messageQueue.addRpcMessage(ServerPackets.BuildCombatTargetFlag(joined.OpponentHandle, 1));
                client.messageQueue.addRpcMessage(CombatInterlock.BuildRoundState(CombatInterlock.KindStart, joined.Move, null));
                SendCombatEffect(joined, false);
                client.FlushQueue();
                if (client.IsBot) FaceOpponent(client, target);
            }
            ResolvePair(joined, opponent, true, false);
            return true;
        }

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
                if (side.Client.IsBot)
                {
                    FaceOpponent(side.Client, side.Other.Client);
                }
            }
            IsActive = true;
            presentation = new CombatPresentation(attacker, defender, null, handler, handlerEntity);
            timer = new Timer(CombatInterlock.RoundMs) { AutoReset = true };
            timer.Elapsed += (s, e) => OnRoundTimer();
            timer.Start();
        }

        /// <summary>
        /// A complete sized combat init, without a captured exchange from an unrelated fight.
        /// </summary>
        private byte[] CombatInit(UInt16 handlerViewId, UInt32 first, UInt32 second)
        {
            return ServerPackets.BuildCombatInitialize(handlerViewId, centre, first, second);
        }

        /// <summary>
        /// A server-played fighter turns to its opponent on everyone's screen, as an NPC does when a fight starts
        /// (CombatHandler.FaceThePlayer). A real client turns its own character. The record is the state part other
        /// players' views get for a moving player: <c>&lt;view&gt; 01 0c &lt;yaw&gt; &lt;x y z f32&gt;</c>.
        /// </summary>
        private static void FaceOpponent(WorldClient bot, WorldClient opponent)
        {
            double bx = 0, by = 0, bz = 0, ox = 0, oy = 0, oz = 0;
            NumericalUtils.LtVector3dToDoubles(bot.playerInstance.Position.getValue(), ref bx, ref by, ref bz);
            NumericalUtils.LtVector3dToDoubles(opponent.playerInstance.Position.getValue(), ref ox, ref oy, ref oz);
            byte yaw = MobAggro.YawTowards(bx, bz, ox, oz);
            bot.playerInstance.YawInterval.setValue(yaw);
            PvpFlag.ToWatchers(bot, BuildFacing(yaw, (float)bx, (float)by, (float)bz));
        }

        public static byte[] BuildFacing(byte yaw, float x, float y, float z)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("010c");
            pak.AddByte(yaw);
            pak.AddFloatLtVector3f(x, y, z);
            return pak.ReturnFinalPacket();
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
            ClientView existing = client.viewMan.FindViewForEntity(handlerEntity);
            if (existing?.viewCreated == true) return existing.ViewID;
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

        private int resolvingRound;
        private void OnRoundTimer()
        {
            if (System.Threading.Interlocked.Exchange(ref resolvingRound, 1) != 0) return;
            try
            {
                if (!IsActive)
                {
                    return;
                }
                // A player whose connection died is cleaned up by the visibility thread; one who is dead or gone for
                // another reason ends the fight here
                Side[] present;
                lock (sync) present = members.Where(s => s.Present).ToArray();
                foreach (Side side in present)
                {
                    WorldClient c = side.Client;
                    if (!c.Alive || !c.playerData.getOnWorld() || c.playerData.isDead || c.playerData.duelDeath)
                    {
                        End(c.playerData.isDead || c.playerData.duelDeath ? EndReason.Death : EndReason.Left, c);
                        if (!IsActive) return;
                    }
                }
                Rounds++;
                foreach (Side side in present.Where(s => s.Present)) CombatHandler.ApplyRoundInnerStrength(side.Client);
                ResolveRound(false);
            }
            catch (Exception ex)
            {
                Output.WriteLine("[PVP] Round failed: " + ex);
            }
            finally { System.Threading.Volatile.Write(ref resolvingRound, 0); }
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
        public bool QueueAbility(WorldClient client, AbilityItem ability, UInt16 targetViewId, bool sneakOpener = false)
        {
            if (!ControlActions.CanUseAbilities(client)) return false;
            lock (sync)
            {
                Side side = SideOf(client);
                if (!IsActive || side?.Move == null || (UInt16)side.OpponentHandle != targetViewId)
                {
                    return false;
                }
                side.QueuedAbility = ability;
                side.SneakOpener = sneakOpener;
                SendMoveChanged(side);
                Output.WriteDebugLog("[PVP] " + ability.getAbilityName() + " is " + PvpFlag.Name(client) + "'s next move");
                return true;
            }
        }

        /// <summary>RPC 44: the player wants out. It is tried at each round until their roll beats the opponent's.</summary>
        public void RequestWithdraw(WorldClient client)
        {
            if (!ControlActions.CanWithdraw(TimedEffects.ForPlayer(client).Effects)) return;
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
            Side[] present;
            lock (sync) present = members.Where(s => s.Present).ToArray();
            var acted = new HashSet<Side>();
            foreach (Side side in present)
            {
                if (!side.Present || acted.Contains(side)) continue;
                Side opponent = side.Other;
                if (opponent?.Present != true) continue;
                bool counter = !acted.Contains(opponent) && opponent.Other == side;
                acted.Add(side);
                if (counter) acted.Add(opponent);
                ResolvePair(side, opponent, opening, counter);
            }
        }

        private void ResolvePair(Side first, Side second, bool opening, bool counter)
        {
            WorldClient dead = null, killer = null;
            AbilityItem landed = null;
            Side landedBy = null;
            bool sneakOpener = false;
            Side withdrew = null;
            lock (sync)
            {
                if (!IsActive || !first.Present || !second.Present)
                {
                    return;
                }
                // A server-played fighter picks a tactic each round, like an NPC
                foreach (Side side in new[] { first, second })
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
                foreach (Side side in new[] { first, second })
                    if (!ControlActions.CanWithdraw(TimedEffects.ForPlayer(side.Client).Effects)) side.Withdrawing = false;
                bool bothWithdraw = first.Withdrawing && second.Withdrawing && counter && !opening;
                Side p = second.Withdrawing && !first.Withdrawing && counter && !opening ? second : first;
                Side n = p == first ? second : first;

                int pLevel = PvpFlag.LevelOf(p.Client), nLevel = PvpFlag.LevelOf(n.Client);
                CombatTactic pTactic = p.Client.playerData.combatTactic, nTactic = n.Client.playerData.combatTactic;
                CombatTacticType pType = CombatTactics.Type(p.Client), nType = CombatTactics.Type(n.Client);
                AbilityItem pAbility = opening ? null : p.QueuedAbility, nAbility = opening ? null : n.QueuedAbility;
                if (!counter) nAbility = null;
                if (!ControlActions.CanUseAbilities(p.Client)) pAbility = null;
                if (!ControlActions.CanUseAbilities(n.Client)) nAbility = null;
                if (!opening)
                {
                    p.QueuedAbility = null;
                    if (counter) n.QueuedAbility = null;
                }

                bool pHit, nHit, pCondition, nCondition, pFirst;
                UInt16 pAttack, pDefense, nAttack, nDefense, pDamage, nDamage;
                Int16 absorbedByN, absorbedByP;
                lock (rng)
                {
                    pHit = CombatInterlock.RollHit(rng, pTactic, pLevel, nTactic, nLevel, out pAttack, out nDefense, pType,
                        AbilityStats.Attack(p.Client, false), AbilityStats.SpecialDefense(n.Client, pAbility)) &&
                        !ControlActions.Incapacitated(TimedEffects.ForPlayer(p.Client).Effects);
                    nHit = CombatInterlock.RollHit(rng, nTactic, nLevel, pTactic, pLevel, out nAttack, out pDefense, nType,
                        AbilityStats.Attack(n.Client, false), AbilityStats.SpecialDefense(p.Client, nAbility)) &&
                        !ControlActions.Incapacitated(TimedEffects.ForPlayer(n.Client).Effects) && counter;
                    pCondition = rng.Next(100) < CombatInterlock.ConditionChancePercent;
                    nCondition = rng.Next(100) < CombatInterlock.ConditionChancePercent;
                    pDamage = CombatCalculator.CloseCombatDamage(rng, pLevel, nLevel, pTactic, out absorbedByN, pType,
                        AbilityStats.Player(p.Client, 1142), AbilityStats.Toughness(n.Client, false));
                    pDamage = (UInt16)Math.Round(pDamage * PvpCombat.IncomingDamageMultiplier(n.Client));
                    nDamage = CombatCalculator.CloseCombatDamage(rng, nLevel, pLevel, nTactic, out absorbedByP, nType,
                        AbilityStats.Player(n.Client, 1142), AbilityStats.Toughness(p.Client, false));
                    nDamage = (UInt16)Math.Round(nDamage * PvpCombat.IncomingDamageMultiplier(p.Client));
                    pFirst = !counter || CombatRules.PlayerAttacksFirst(pTactic, nTactic, opening, p == first, () => rng.Next(2) == 0);
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
                    outcome.DamageToNpc, outcome.NpcAbsorbed, opening, p.Withdrawing && !opening ? pAttack - nAttack : 0, PvpCombat.HandleOf(p.Client,n.Client));
                SendRound(n, nDecided, pDecided, !outcome.PlayerWins, outcome.Flags, outcome.DamageToNpc, outcome.NpcAbsorbed,
                    outcome.DamageToPlayer, outcome.PlayerAbsorbed, opening && counter, 0, PvpCombat.HandleOf(n.Client,p.Client));
                presentation.RoundPair(p.Slot, n.Slot, outcome.PlayerWins, outcome.Flags,
                    pTactic, nTactic, pAttack, nAttack, opening,
                    (outcome.PlayerWins ? pAbility : nAbility)?.getAbilityID() ?? 0, (outcome.Flags & CombatRules.SpecialMove) != 0);

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
                    sneakOpener = landedBy.SneakOpener;
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
                PvpCombat.LandAbilityOn(landedBy.Client, landed, (landedBy == first ? second : first).Client, sneakOpener);
            }
            else if (dead != null)
            {
                PvpCombat.Defeated(dead, killer);
            }
        }

        /// <summary>One side's records of a round: RPC 47 kind 3 with its next move (after the 45 on the opening round) and RPC 45.</summary>
        private void SendRound(Side side, CombatInterlock.Move own, CombatInterlock.Move theirs, bool won, UInt32 flags,
            int damageToSelf, int selfAbsorbed, int damageToOpponent, int opponentAbsorbed, bool opening, int combatResult, uint roundOpponent)
        {
            WorldClient client = side.Client;
            CombatInterlock.Move next = NewMove((byte)(own.Seq + 1), client.playerData.combatTactic);
            next.Withdraw = side.Withdrawing;
            if (!opening)
            {
                client.messageQueue.addRpcMessage(CombatInterlock.BuildRoundState(CombatInterlock.KindNewRound, own, next));
            }
            client.messageQueue.addRpcMessage(CombatInterlock.BuildRoundResult(side.RoundRecord, won, own, theirs, roundOpponent, flags,
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
        /// Ends the fight: no more rounds, and neither player counts as fighting. Each side still there then gets the
        /// teardown an NPC fight ends with (the combat view's final record, <c>46 &lt;opponent&gt; 00</c>, the delete of the
        /// combat view, CombatEffect off, and the combat stance and flags reset ten seconds later), after
        /// <see cref="TeardownDelayFor"/> the reason: whatever ends the fight (a death, the duel's end) goes out first,
        /// in packets of its own, as retail did. <paramref name="who"/> is the player the reason is about.
        /// </summary>
        public void End(EndReason reason, WorldClient who)
        {
            double delay = TeardownDelayFor(reason);
            lock (sync)
            {
                if (!IsActive)
                {
                    return;
                }
                if (reason != EndReason.DuelOver && FighterCount > 2 && who != null && SideOf(who)?.Present == true)
                {
                    RemoveMember(reason, SideOf(who));
                    return;
                }
                IsActive = false;
                presentation?.End(delay);
                timer?.Stop();
                timer?.Dispose();
                timer = null;
                Output.WriteDebugLog("[PVP] Close combat between " + PvpFlag.Name(Attacker.Client) + " and " + PvpFlag.Name(Defender.Client) +
                                     " ends: " + reason + " (" + (who != null ? PvpFlag.Name(who) : "-") + ")" +
                                     (delay > 0 ? ", interlock down in " + delay + " s" : ""));

                foreach (Side side in members.Where(s => s.Present))
                {
                    side.Present = false;
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
                    side.Fell = reason == EndReason.Death && client == who;
                    side.TeardownPending = true;
                    if (delay > 0)
                    {
                        lock (PendingSync)
                        {
                            PendingTeardowns[client] = this;
                        }
                    }
                }
            }
            if (delay > 0)
            {
                DelayedActions.After(delay, () =>
                {
                    foreach (Side side in members.ToArray()) Teardown(side);
                });
            }
            else
            {
                foreach (Side side in members.ToArray()) Teardown(side);
            }
            if (reason == EndReason.Left && who != null)
            {
                DuelHandler.OnUnavailable(who);
            }
        }

        private void RemoveMember(EndReason reason, Side removed)
        {
            removed.Present = false;
            var controller = CombatManager.GetHandler(removed.Client);
            controller.pvpFight = null; controller.isCombatRunning = false;
            presentation.RemoveFighter(removed.Client, removed.Slot);
            foreach (Side side in members.Where(s => s.Present && s.Other == removed).ToArray())
            {
                Side replacement = members.FirstOrDefault(s => s.Present && s != side &&
                    PvpCombat.Check(side.Client, s.Client) == PvpRules.Verdict.Allowed);
                if (replacement == null)
                {
                    // Nobody left whom this player may fight: end the remaining interlock cleanly.
                    End(EndReason.DuelOver, null);
                    break;
                }
                side.Client.messageQueue.addRpcMessage(ServerPackets.BuildCombatTargetFlag(side.OpponentHandle, 0));
                side.Other = replacement;
                side.OpponentHandle = PvpCombat.HandleOf(side.Client, replacement.Client);
                side.Client.messageQueue.addRpcMessage(BuildAttackedBy(side.OpponentHandle));
                side.Client.messageQueue.addRpcMessage(ServerPackets.BuildCombatTargetFlag(side.OpponentHandle, 1));
                side.Client.FlushQueue();
            }
            if (reason == EndReason.Left)
            {
                removed.Client.playerData.combatGeneration++;
                DuelHandler.OnUnavailable(removed.Client);
                return;
            }
            removed.Fell = reason == EndReason.Death;
            removed.TeardownPending = true;
            double delay = TeardownDelayFor(reason);
            if (delay > 0)
            {
                lock (PendingSync) PendingTeardowns[removed.Client] = this;
                DelayedActions.After(delay, () => Teardown(removed));
            }
            else Teardown(removed);
        }

        /// <summary>One side's interlock teardown, once. Nothing for a client that is gone or on its way to the Loading Area.</summary>
        private void Teardown(Side side)
        {
            WorldClient client = side.Client;
            lock (sync)
            {
                if (!side.TeardownPending)
                {
                    return;
                }
                side.TeardownPending = false;
                if (!IsActive && members.All(s => !s.TeardownPending))
                {
                    lock (WorldServer.gameServerEntities)
                    {
                        WorldServer.gameServerEntities.Remove(handlerObject);
                    }
                }
            }
            lock (PendingSync)
            {
                if (PendingTeardowns.TryGetValue(client, out PvpFight pending) && pending == this)
                {
                    PendingTeardowns.Remove(client);
                }
            }
            if (!client.Alive || !client.playerData.getOnWorld() || client.playerData.respawnPending)
            {
                return;
            }
            SendCombatEffect(side, true);
            uint mask = 0;
            lock (sync)
                foreach (Side member in members) mask |= 1u << (member.Slot - 1);
            new ServerPackets().SendCombatEnd(client, side.HandlerViewId, side.OpponentHandle, centre, Rounds, !side.Fell, mask);
            client.viewMan.removeViewByViewId(side.HandlerViewId);
            client.FlushQueue();
            DuelHandler.AfterInterlockTeardown(client);
            DeathPresentation.AfterInterlockTeardown(client);
        }

        /// <summary>
        /// The player's last fight is over but its interlock is still up (<see cref="DeathTeardownSeconds"/>): take it
        /// down now, before something new (another fight, standing up) is sent. True when there was one.
        /// </summary>
        public static bool RunPendingTeardown(WorldClient client)
        {
            PvpFight fight;
            lock (PendingSync)
            {
                if (!PendingTeardowns.TryGetValue(client, out fight))
                {
                    return false;
                }
            }
            fight.Teardown(fight.SideOf(client));
            return true;
        }

        /// <summary>
        /// A player leaves (disconnect, world reload): their own pending teardown is dropped, their views being gone.
        /// With <paramref name="othersNow"/>, anyone whose interlock was with them gets theirs now, before the player's
        /// view is deleted (a dev bot taken away); otherwise theirs runs when it is due.
        /// </summary>
        public static void OnPlayerGone(WorldClient client, bool othersNow)
        {
            System.Collections.Generic.List<PvpFight> involved;
            lock (PendingSync)
            {
                involved = PendingTeardowns.Values.Distinct().ToList();
            }
            involved = involved.Where(f => f.SideOf(client) != null).ToList();
            foreach (PvpFight f in involved)
            {
                Side mine = f.SideOf(client);
                lock (f.sync)
                {
                    if (mine.TeardownPending)
                    {
                        mine.TeardownPending = false;
                        client.playerData.combatGeneration++;
                    }
                }
                lock (PendingSync)
                {
                    if (PendingTeardowns.TryGetValue(client, out PvpFight p) && p == f)
                    {
                        PendingTeardowns.Remove(client);
                    }
                }
                if (othersNow)
                {
                    lock (f.sync)
                        foreach (Side side in f.members.Where(s => s != mine).ToArray()) f.Teardown(side);
                }
                bool complete;
                lock (f.sync) complete = !f.IsActive && f.members.All(s => !s.TeardownPending);
                if (complete)
                {
                    lock (WorldServer.gameServerEntities)
                    {
                        WorldServer.gameServerEntities.Remove(f.handlerObject);
                    }
                }
            }
        }
    }
}
