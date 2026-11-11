using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Duels: the challenge (RPC 50), the answer (52), the duel itself and its end. The records are client.dll's
    /// (<see cref="DuelPackets"/>); the order and who gets what are retail's where the three duel captures show it,
    /// and ours elsewhere. docs/PVP.md separates the two.
    /// <para>A duel is two players with paired DuelIDs (the challenger's even, the other's odd): that pair is what makes
    /// each client treat the other as an opponent, and lets it select nobody else. The fight is an ordinary one. It is
    /// "to the death": the loser lies down with IsDuelDeath set, and stands up where they fell with 1 health when their
    /// client asks to (81 4c, the client's /reconstruct).</para>
    /// </summary>
    public static class DuelHandler
    {
        /// <summary>How long the target has to answer: the client's ChallengeMaxConsiderTime (15 s, then it declines
        /// by itself), with a few seconds for the answer to arrive.</summary>
        public const double ChallengeSeconds = 20;

        /// <summary>
        /// The wait before challenging the same player again after they declined. Retail: a decline at 03:24:42, a new
        /// challenge at 03:24:57 answered with reason 12 and 45.0 s to wait (niobe3dualduality capture).
        /// </summary>
        public const double RechallengeSeconds = 60;

        /// <summary>How far apart a challenge can be made, and how far apart duelists can drift (ours; the client has the
        /// messages, the limits were the server's).</summary>
        public const float ChallengeMetres = 30f, SeparationMetres = 60f;

        private sealed class Challenge
        {
            public WorldClient Challenger, Target;
            public double At;
        }

        public sealed class Duel
        {
            public WorldClient Challenger, Accepter;
            public UInt32 ChallengerId, AccepterId;

            public WorldClient Other(WorldClient one) => one == Challenger ? Accepter : Challenger;
        }

        private static readonly object Sync = new object();
        private static readonly List<Challenge> Challenges = new List<Challenge>();
        private static readonly Dictionary<WorldClient, Duel> Duels = new Dictionary<WorldClient, Duel>();
        private static readonly Dictionary<(UInt32, UInt32), double> DeclinedAt = new Dictionary<(UInt32, UInt32), double>();
        private static UInt32 nextDuelId = 0x1000;
        private static System.Threading.Timer watchdog;

        public static Duel DuelOf(WorldClient client)
        {
            lock (Sync)
            {
                return Duels.TryGetValue(client, out Duel duel) ? duel : null;
            }
        }

        public static bool AreDuelling(WorldClient a, WorldClient b)
        {
            return PvpRules.DuelOpponents(a.playerData.duelId, b.playerData.duelId);
        }

        private static double Now => TimeUtils.GetSimTimeSeconds();

        private static void Send(WorldClient client, byte[] rpc)
        {
            client.messageQueue.addRpcMessage(rpc);
            client.FlushQueue();
        }

        private static bool OnTeam(WorldClient client)
        {
            byte[] team = client.playerInstance.MissionTeamID.getValue();
            return team != null && team.Length >= 4 && NumericalUtils.ByteArrayToUint32(team, 1) != 0;
        }

        private static bool InCombat(WorldClient client) => CombatManager.GetHandler(client).isCombatRunning;

        /// <summary>What stands in the way of a challenge, as a 53 reason and whose condition it is; null when nothing does.</summary>
        public static (byte Reason, bool FromTarget)? Obstacle(bool selfDueling, bool selfDuelingTarget, bool selfChallengingTarget,
            bool selfChallengingOther, bool selfConsidering, bool selfInCombat, bool selfOnTeam, bool targetAvailable, bool targetOnTeam,
            bool targetInCombat, bool targetDueling, bool targetChallenging, bool targetConsidering, bool tooFar, double waitSeconds)
        {
            if (selfDuelingTarget) return (DuelPackets.FailAlreadyDuelingSame, false);
            if (selfDueling) return (DuelPackets.FailAlreadyDueling, false);
            if (selfChallengingTarget) return (DuelPackets.FailChallengingSame, false);
            if (selfChallengingOther) return (DuelPackets.FailChallengingOther, false);
            if (selfConsidering) return (DuelPackets.FailConsideringOther, false);
            if (selfInCombat) return (DuelPackets.FailInCombat, false);
            if (selfOnTeam) return (DuelPackets.FailOnTeam, false);
            if (!targetAvailable) return (DuelPackets.FailUnavailable, false);
            if (targetOnTeam) return (DuelPackets.FailOnTeam, true);
            if (targetInCombat) return (DuelPackets.FailInCombat, true);
            if (targetDueling) return (DuelPackets.FailAlreadyDueling, true);
            if (targetChallenging) return (DuelPackets.FailChallengingOther, true);
            if (targetConsidering) return (DuelPackets.FailConsideringOther, true);
            if (tooFar) return (DuelPackets.FailTooFar, false);
            if (waitSeconds > 0) return (DuelPackets.FailMustWait, false);
            return null;
        }

        /// <summary>
        /// RPC 50 &lt;target&gt;: the client's /duel on its target; target 0 is /duel stop. Retail answered a good challenge
        /// with 54 to the challenger and 51 to the target, and a bad one with 53.
        /// </summary>
        public static void ProcessChallenge(WorldClient client, byte[] data)
        {
            UInt32? handle = DuelPackets.ParseChallenge(data);
            if (handle == null)
            {
                return;
            }
            if (handle.Value == 0)
            {
                Stop(client);
                return;
            }

            WorldClient target = PvpCombat.PlayerInView(client, (UInt16)handle.Value);
            if (target == null)
            {
                Send(client, DuelPackets.BuildRequestFailed(DuelPackets.FailNotPlayer, false, handle.Value));
                return;
            }

            (byte Reason, bool FromTarget)? obstacle;
            double wait;
            lock (Sync)
            {
                ExpireChallenges();
                Duels.TryGetValue(client, out Duel mine);
                Challenge outgoing = Challenges.FirstOrDefault(c => c.Challenger == client);
                DeclinedAt.TryGetValue((client.playerData.getCharID(), target.playerData.getCharID()), out double declined);
                wait = declined > 0 ? Math.Max(0, RechallengeSeconds - (Now - declined)) : 0;
                obstacle = Obstacle(
                    mine != null, mine != null && mine.Other(client) == target,
                    outgoing != null && outgoing.Target == target, outgoing != null && outgoing.Target != target,
                    Challenges.Any(c => c.Target == client), InCombat(client), OnTeam(client),
                    target.Alive && target.playerData.getOnWorld() && !target.playerData.isDead && !target.playerData.duelDeath &&
                    !client.playerData.isDead && !client.playerData.duelDeath,
                    OnTeam(target), InCombat(target), Duels.ContainsKey(target),
                    Challenges.Any(c => c.Challenger == target), Challenges.Any(c => c.Target == target),
                    PvpCombat.MetresBetween(client, target) > ChallengeMetres, wait);
                if (obstacle == null)
                {
                    Challenges.Add(new Challenge { Challenger = client, Target = target, At = Now });
                    EnsureWatchdog();
                }
            }

            if (obstacle != null)
            {
                Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(client) + " can't challenge " + PvpFlag.Name(target) + ": reason " +
                                     obstacle.Value.Reason + (obstacle.Value.FromTarget ? " (target)" : ""));
                if (obstacle.Value.Reason == DuelPackets.FailOnTeam && obstacle.Value.FromTarget)
                {
                    // Retail said "challenged" first and then refused: 54 <target> 00 00, 53 04 01 <target> ...
                    // The target hears of it too (ID_DUEL_FAILED_ONTEAM_AUTOREJECT); that the server sent that one is inferred
                    Send(client, DuelPackets.BuildChallengeSent(handle.Value));
                    Send(target, DuelPackets.BuildRequestFailed(DuelPackets.FailOnTeamAutoReject, false,
                        PvpCombat.HandleOf(target, client), PvpFlag.Name(client)));
                }
                Send(client, DuelPackets.BuildRequestFailed(obstacle.Value.Reason, obstacle.Value.FromTarget, handle.Value, null,
                    obstacle.Value.Reason == DuelPackets.FailMustWait ? (float)wait : 0f));
                return;
            }

            Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(client) + " challenges " + PvpFlag.Name(target));
            Send(client, DuelPackets.BuildChallengeSent(handle.Value));
            Send(target, DuelPackets.BuildChallenged(PvpCombat.HandleOf(target, client)));
            DuelBot.OnChallenged(target, client);
        }

        /// <summary>RPC 52 &lt;challenger&gt; &lt;accepts&gt; &lt;reason&gt;: the target's Yes or No (No also when its dialog timed out).</summary>
        public static void ProcessResponse(WorldClient client, byte[] data)
        {
            if (!DuelPackets.TryParseResponse(data, out UInt32 challengerHandle, out bool accepts, out byte reason))
            {
                return;
            }
            Challenge challenge;
            lock (Sync)
            {
                challenge = Challenges.FirstOrDefault(c => c.Target == client);
                if (challenge != null)
                {
                    Challenges.Remove(challenge);
                }
            }
            if (challenge == null)
            {
                Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(client) + " answered a challenge that is no longer open");
                if (accepts)
                {
                    Send(client, DuelPackets.BuildRequestFailed(DuelPackets.FailUnavailable, false, challengerHandle));
                }
                return;
            }
            Answer(challenge.Challenger, client, accepts);
        }

        private static void Answer(WorldClient challenger, WorldClient target, bool accepts)
        {
            UInt32 targetForChallenger = PvpCombat.HandleOf(challenger, target);
            if (!accepts)
            {
                Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(target) + " declines " + PvpFlag.Name(challenger));
                lock (Sync)
                {
                    DeclinedAt[(challenger.playerData.getCharID(), target.playerData.getCharID())] = Now;
                }
                // Retail: 53 00 01 <target> ... ("%s declined your challenge.")
                Send(challenger, DuelPackets.BuildRequestFailed(DuelPackets.FailUnknownOrDeclined, true, targetForChallenger, PvpFlag.Name(target)));
                return;
            }

            bool possible = challenger.Alive && challenger.playerData.getOnWorld() && !challenger.playerData.isDead &&
                            !target.playerData.isDead && !InCombat(challenger) && !InCombat(target) && targetForChallenger != 0 &&
                            PvpCombat.HandleOf(target, challenger) != 0;
            if (!possible)
            {
                Send(challenger, DuelPackets.BuildRequestFailed(DuelPackets.FailUnavailable, false, targetForChallenger, PvpFlag.Name(target)));
                Send(target, DuelPackets.BuildRequestFailed(DuelPackets.FailUnavailable, false, PvpCombat.HandleOf(target, challenger), PvpFlag.Name(challenger)));
                return;
            }
            Begin(challenger, target);
        }

        /// <summary>
        /// The duel begins. Retail, in one packet to each (niobe3dualduality 03:03:20): the own view's DuelID
        /// (<c>02 00 02 80 80 80 80 80 01 a3 cc 00 00</c>), the opponent's on their view (<c>2d 00 02 80 80 80 20 a2 cc 00 00</c>)
        /// and <c>55 &lt;opponent&gt;</c>. In all three captures the challenger's id is the even one.
        /// </summary>
        private static void Begin(WorldClient challenger, WorldClient accepter)
        {
            Duel duel;
            lock (Sync)
            {
                nextDuelId += 2;
                duel = new Duel { Challenger = challenger, Accepter = accepter, ChallengerId = nextDuelId, AccepterId = nextDuelId + 1 };
                Duels[challenger] = duel;
                Duels[accepter] = duel;
                EnsureWatchdog();
            }
            Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(challenger) + " and " + PvpFlag.Name(accepter) + " duel (ids " +
                                 duel.ChallengerId.ToString("x") + "/" + duel.AccepterId.ToString("x") + ")");
            SetDuelId(challenger, duel.ChallengerId);
            SetDuelId(accepter, duel.AccepterId);
            Send(challenger, DuelPackets.BuildBegin(PvpCombat.HandleOf(challenger, accepter)));
            Send(accepter, DuelPackets.BuildBegin(PvpCombat.HandleOf(accepter, challenger)));
            DuelBot.OnDuelBegan(challenger, accepter);
            DuelBot.OnDuelBegan(accepter, challenger);
        }

        /// <summary>Sets a player's DuelID on their own view (attribute 35) and on everyone's view of them (26).</summary>
        private static void SetDuelId(WorldClient client, UInt32 id)
        {
            client.playerData.duelId = id;
            client.playerInstance.DuelID.setValue(id);
            client.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfDuelId, PlayerViewAttributes.U32(id))), false);
            client.FlushQueue();
            PvpFlag.ToWatchers(client, PlayerViewAttributes.Build((PlayerViewAttributes.OtherDuelId, PlayerViewAttributes.U32(id))));
        }

        /// <summary>Takes the duel off the books and clears both DuelIDs. Returns null when the player isn't in one.</summary>
        private static Duel Close(WorldClient one, bool clearIds = true)
        {
            Duel duel;
            lock (Sync)
            {
                if (!Duels.TryGetValue(one, out duel))
                {
                    return null;
                }
                Duels.Remove(duel.Challenger);
                Duels.Remove(duel.Accepter);
            }
            double now = Now;
            duel.Challenger.playerData.lastDuelEndedAt = now;
            duel.Accepter.playerData.lastDuelEndedAt = now;
            if (clearIds)
            {
                SetDuelId(duel.Challenger, 0);
                SetDuelId(duel.Accepter, 0);
            }
            return duel;
        }

        /// <summary>The duel result for everyone nearby: 57 inside 81 9d, the loser first (retail, niobe3dualduality).</summary>
        private static void Broadcast(byte reason, WorldClient loser, WorldClient winner)
        {
            byte[] record = DuelPackets.BuildBroadcast(reason, PvpFlag.Name(loser), PvpFlag.Name(winner));
            foreach (WorldClient receiver in PvpCombat.Onlookers(loser, winner))
            {
                Send(receiver, DuelPackets.BuildAboutTwo(PvpCombat.HandleOf(receiver, loser), PvpCombat.HandleOf(receiver, winner), record));
            }
        }

        /// <summary>
        /// A duelist is out of health at the other's hand: the duel is over and the loser lies down, not dead. Retail,
        /// the loser's side (niobe3dualduality 03:03:51): own view <c>02 00 02 80 80 c1 &lt;MaxHealth&gt; 01 c1 01 00 00 80 89
        /// 00 00 00 00 &lt;DeathPenalty&gt; 01 &lt;flags&gt;</c> (IsDuelDeath 1, IsDead 1, Health 0, DuelID 0), the winner's DuelID 0
        /// on their view, the kill notice, the buffs' off-records, <c>2e 67</c> with ID_DUEL_DEATH_MESSAGE, the broadcast
        /// and <c>56 01 00 &lt;winner&gt;</c>. The winner (lastday2): own DuelID 0, the loser's view IsDead 1, Health 0,
        /// DuelID 0, and <c>56 01 01 &lt;loser&gt;</c>. DeathPenalty isn't sent: what it does is unknown.
        /// </summary>
        public static void Defeat(WorldClient loser, WorldClient winner)
        {
            Duel duel = Close(loser, clearIds: false);
            if (duel == null)
            {
                return;
            }
            Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(winner) + " defeats " + PvpFlag.Name(loser));
            PvpCombat.EndFightsOf(loser, PvpFight.EndReason.DuelOver);
            PvpCombat.EndFightsOf(winner, PvpFight.EndReason.DuelOver);
            TradeHandler.Abort(loser);

            ClientData data = loser.playerData;
            data.duelDeath = true;
            data.duelId = 0;
            winner.playerData.duelId = 0;
            loser.playerInstance.DuelID.setValue((UInt32)0);
            winner.playerInstance.DuelID.setValue((UInt32)0);
            loser.playerInstance.Health.setValue((UInt16)0);
            loser.playerInstance.IsDuelDeath.setValue(true);
            UInt16 maxHealth = NumericalUtils.ByteArrayToUint16(loser.playerInstance.MaxHealth.getValue(), 1);

            loser.messageQueue.addObjectMessage(BuildSelfDuelDeath(maxHealth, PvpFlag.FlagsOf(loser)), false);
            AbilityBuffs.RemoveAll(loser);
            PvpFlag.ToWatchers(loser, BuildOtherDuelDeath());
            winner.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfDuelId, PlayerViewAttributes.U32(0))), false);
            PvpFlag.ToWatchers(winner, PlayerViewAttributes.Build((PlayerViewAttributes.OtherDuelId, PlayerViewAttributes.U32(0))));

            byte[] killLine = DuelPackets.BuildKillLine(PvpFlag.Name(winner), PvpFlag.Name(loser));
            foreach (WorldClient receiver in PvpCombat.Onlookers(loser, winner))
            {
                Send(receiver, DuelPackets.BuildAboutTwo(PvpCombat.HandleOf(receiver, winner), PvpCombat.HandleOf(receiver, loser), killLine));
            }
            Send(loser, DuelPackets.BuildSystemString(DuelPackets.DuelDeathMessageString));
            Broadcast(DuelPackets.StopDefeated, loser, winner);
            Send(loser, DuelPackets.BuildStop(DuelPackets.StopDefeated, false, PvpCombat.HandleOf(loser, winner)));
            Send(winner, DuelPackets.BuildStop(DuelPackets.StopDefeated, true, PvpCombat.HandleOf(winner, loser)));
            DuelBot.OnDuelOver(loser);
            DuelBot.OnDuelOver(winner);
        }

        public static byte[] BuildSelfDuelDeath(UInt16 maxHealth, UInt32 flags)
        {
            return PlayerViewAttributes.BuildSelf(
                (PlayerViewAttributes.SelfMaxHealth, PlayerViewAttributes.U16(maxHealth)),
                (PlayerViewAttributes.SelfIsDuelDeath, PlayerViewAttributes.U8(1)),
                (PlayerViewAttributes.SelfIsDead, PlayerViewAttributes.U8(1)),
                (PlayerViewAttributes.SelfHealth, PlayerViewAttributes.U16(0)),
                (PlayerViewAttributes.SelfDuelId, PlayerViewAttributes.U32(0)),
                (PlayerViewAttributes.SelfConditionStateFlags, PlayerViewAttributes.U32(flags)));
        }

        /// <summary>The loser on other players' views: IsDead 1, Health 0, DuelID 0 (retail: <c>0d 00 02 80 82 &lt;MaxHealth&gt; 92 01 00 00 e0 00 00 00 00 ...</c>).</summary>
        public static byte[] BuildOtherDuelDeath()
        {
            return PlayerViewAttributes.Build(
                (PlayerViewAttributes.OtherIsDead, PlayerViewAttributes.U8(1)),
                (PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(0)),
                (PlayerViewAttributes.OtherDuelId, PlayerViewAttributes.U32(0)));
        }

        /// <summary>
        /// 81 4c from a duel's loser: they stand up where they are with 1 health. Retail (niobe3dualduality 03:04:08):
        /// <c>02 00 03 28 0c 40 00 00 00 10 &lt;x y z f32&gt; &lt;sim ms&gt; 80 80 80 41 00 01 00</c>, an action with the position
        /// and time, then IsDead 0 and Health 1. No Loading Area. IsDuelDeath stays set, as in retail.
        /// </summary>
        public static bool StandUp(WorldClient client)
        {
            if (!client.playerData.duelDeath)
            {
                return false;
            }
            client.playerData.duelDeath = false;
            client.playerInstance.Health.setValue((UInt16)1);
            client.playerInstance.IsDead.setValue(false);
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            client.playerData.emoteCounter++;
            client.messageQueue.addObjectMessage(BuildStandUp(client.playerData.emoteCounter, (float)x, (float)y, (float)z,
                CombatHandler.CombatStartTime()), false);
            client.FlushQueue();
            PvpFlag.ToWatchers(client, PlayerViewAttributes.Build(
                (PlayerViewAttributes.OtherIsDead, PlayerViewAttributes.U8(0)),
                (PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(1))));
            Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(client) + " stands up");
            return true;
        }

        public static byte[] BuildStandUp(byte actionCounter, float x, float y, float z, UInt32 simMs)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("020003" + "28");
            pak.AddByte(actionCounter);
            pak.AddHexBytes("4000000010");
            pak.AddFloatLtVector3f(x, y, z);
            pak.AddUint32(simMs, 1);
            pak.AddHexBytes("80808041" + "00" + "0100");
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// /duel stop (50 with target 0): in a duel it is a surrender, 56 reason 2 to both and the broadcast (the client
        /// has "You have surrendered to %s" and "%1s has surrendered to %2s"; no capture shows one). Nobody falls. A
        /// challenge still unanswered is withdrawn.
        /// </summary>
        private static void Stop(WorldClient client)
        {
            Duel duel = Close(client);
            if (duel == null)
            {
                lock (Sync)
                {
                    Challenges.RemoveAll(c => c.Challenger == client);
                }
                return;
            }
            WorldClient other = duel.Other(client);
            Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(client) + " surrenders to " + PvpFlag.Name(other));
            PvpCombat.EndFightsOf(client, PvpFight.EndReason.DuelOver);
            PvpCombat.EndFightsOf(other, PvpFight.EndReason.DuelOver);
            Broadcast(DuelPackets.StopSurrendered, client, other);
            Send(client, DuelPackets.BuildStop(DuelPackets.StopSurrendered, false, PvpCombat.HandleOf(client, other)));
            Send(other, DuelPackets.BuildStop(DuelPackets.StopSurrendered, true, PvpCombat.HandleOf(other, client)));
            DuelBot.OnDuelOver(client);
            DuelBot.OnDuelOver(other);
        }

        /// <summary>Someone outside the duel killed a duelist: 56 reason 3, "killed by an external entity".</summary>
        public static void OnKilledByOutsider(WorldClient victim)
        {
            Duel duel = Close(victim);
            if (duel == null)
            {
                return;
            }
            WorldClient other = duel.Other(victim);
            PvpCombat.EndFightsOf(other, PvpFight.EndReason.DuelOver);
            Send(victim, DuelPackets.BuildStop(DuelPackets.StopDeath, false, PvpCombat.HandleOf(victim, other)));
            Send(other, DuelPackets.BuildStop(DuelPackets.StopDeath, true, PvpCombat.HandleOf(other, victim)));
            DuelBot.OnDuelOver(other);
        }

        /// <summary>
        /// A player left (disconnected, or their world reloaded): their duel stops with reason 4 for the one who stays,
        /// and challenges they were part of are dropped.
        /// </summary>
        public static void OnUnavailable(WorldClient gone)
        {
            List<Challenge> dropped;
            lock (Sync)
            {
                dropped = Challenges.Where(c => c.Challenger == gone || c.Target == gone).ToList();
                Challenges.RemoveAll(c => c.Challenger == gone || c.Target == gone);
            }
            foreach (Challenge c in dropped.Where(c => c.Target == gone))
            {
                Send(c.Challenger, DuelPackets.BuildRequestFailed(DuelPackets.FailUnavailable, false, 0, PvpFlag.Name(gone)));
            }

            Duel duel;
            lock (Sync)
            {
                if (!Duels.TryGetValue(gone, out duel))
                {
                    return;
                }
                Duels.Remove(duel.Challenger);
                Duels.Remove(duel.Accepter);
            }
            WorldClient other = duel.Other(gone);
            Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(gone) + " left the duel with " + PvpFlag.Name(other));
            gone.playerData.duelId = 0;
            gone.playerInstance.DuelID.setValue((UInt32)0);
            SetDuelId(other, 0);
            PvpCombat.EndFightsOf(other, PvpFight.EndReason.DuelOver);
            Send(other, DuelPackets.BuildStop(DuelPackets.StopUnavailable, true, PvpCombat.HandleOf(other, gone), PvpFlag.Name(gone)));
            DuelBot.OnDuelOver(other);
        }

        private static void ExpireChallenges()
        {
            double now = Now;
            foreach (Challenge c in Challenges.Where(c => now - c.At > ChallengeSeconds).ToList())
            {
                Challenges.Remove(c);
                // Unanswered counts as declined, as the client's own timeout does
                DeclinedAt[(c.Challenger.playerData.getCharID(), c.Target.playerData.getCharID())] = now;
                Send(c.Challenger, DuelPackets.BuildRequestFailed(DuelPackets.FailUnknownOrDeclined, true,
                    PvpCombat.HandleOf(c.Challenger, c.Target), PvpFlag.Name(c.Target)));
            }
        }

        private static void EnsureWatchdog()
        {
            if (watchdog == null)
            {
                watchdog = new System.Threading.Timer(_ => Watch(), null, 2000, 2000);
            }
        }

        /// <summary>Every two seconds: unanswered challenges run out, and duelists who drifted apart or lost each other stop.</summary>
        private static void Watch()
        {
            try
            {
                List<Duel> open;
                lock (Sync)
                {
                    ExpireChallenges();
                    open = Duels.Values.Distinct().ToList();
                }
                foreach (Duel duel in open)
                {
                    WorldClient a = duel.Challenger, b = duel.Accepter;
                    if (!a.Alive || !a.playerData.getOnWorld())
                    {
                        OnUnavailable(a);
                    }
                    else if (!b.Alive || !b.playerData.getOnWorld())
                    {
                        OnUnavailable(b);
                    }
                    else if (PvpCombat.HandleOf(a, b) == 0 || PvpCombat.HandleOf(b, a) == 0 || PvpCombat.MetresBetween(a, b) > SeparationMetres)
                    {
                        if (Close(a) != null)
                        {
                            Output.WriteDebugLog("[DUEL] " + PvpFlag.Name(a) + " and " + PvpFlag.Name(b) + " are too far apart");
                            PvpCombat.EndFightsOf(a, PvpFight.EndReason.DuelOver);
                            PvpCombat.EndFightsOf(b, PvpFight.EndReason.DuelOver);
                            Send(a, DuelPackets.BuildStop(DuelPackets.StopTooFar, false, PvpCombat.HandleOf(a, b), PvpFlag.Name(b)));
                            Send(b, DuelPackets.BuildStop(DuelPackets.StopTooFar, false, PvpCombat.HandleOf(b, a), PvpFlag.Name(a)));
                            DuelBot.OnDuelOver(a);
                            DuelBot.OnDuelOver(b);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine("[DUEL] Watch failed: " + ex);
            }
        }

        /// <summary>Lets a server-played player answer a challenge (the dev bots).</summary>
        internal static void AnswerFor(WorldClient target, bool accepts)
        {
            Challenge challenge;
            lock (Sync)
            {
                challenge = Challenges.FirstOrDefault(c => c.Target == target);
                if (challenge != null)
                {
                    Challenges.Remove(challenge);
                }
            }
            if (challenge != null)
            {
                Answer(challenge.Challenger, target, accepts);
            }
        }
    }
}
