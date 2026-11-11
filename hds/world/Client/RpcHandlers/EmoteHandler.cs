using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Emotes (client 30), paired emotes (30 with a target, the server's 31 asking the target, the client's 32 answer)
    /// and stances (client 34 enters one, 33 leaves it). Everyone who sees the player gets the animation in their view of
    /// them; the player gets it in their own view. docs/CLIENT-ACTIONS.md, "Emotes and stances in detail".
    /// </summary>
    public static class EmoteHandler
    {
        /// <summary>The client's interactive emotes (research/client-scripts/emotes.py, addInteractiveEmote): the slash
        /// command's string id, and its request ('Beckon'), accepted and refused action ids, numbered in the order the
        /// client's createActionID hands them out. Retail /manhug: request 0xd5, accepted 0xd6.</summary>
        internal static readonly Dictionary<UInt32, (UInt16 Request, UInt16 Accept, UInt16 Refuse)> Paired =
            new Dictionary<UInt32, (UInt16, UInt16, UInt16)>
            {
                [0x3a00004b] = (189, 190, 191), // shakehands
                [0x580002e4] = (192, 193, 194), // slap
                [0x58000da8] = (195, 196, 197), // holdhands
                [0x58000dab] = (198, 199, 200), // dogsniff
                [0x58000dfd] = (201, 202, 203), // hug
                [0x58000dca] = (204, 205, 206), // weddingkiss
                [0x58000dff] = (207, 208, 209), // holdbothhands
                [0x58000dfb] = (210, 211, 212), // kissthering
                [0x58000df5] = (213, 214, 215), // manhug
                [0x58000e05] = (216, 217, 218), // pound
                [0x58000e03] = (219, 220, 221), // weddingring
                [0x58000e01] = (222, 223, 224), // propose
                [0x580014c5] = (225, 226, 227), // dap
                [0x58000e0b] = (228, 229, 230), // kiss
                [0x58001605] = (231, 232, 233), // givecake
                [0xe0000007] = (234, 235, 236), // bumpbutts
                [0x58001659] = (237, 238, 239), // bangbang
            };

        public const UInt32 Hug = 0x58000dfd;

        // 32 Result values (client.dll 0x6224e620, for the requester)
        public const byte ResultNoAnswer = 0;
        public const byte ResultAccepted = 1;
        public const byte ResultRefused = 2;
        public const byte ResultCannotReach = 3;
        public const byte ResultCannotPerform = 4;
        public const byte ResultNotAPlayer = 5;

        /// <summary>How long a request waits for the target (the client registers EmoteQueryDurationMS = 20000).</summary>
        public static readonly TimeSpan RequestLifetime = TimeSpan.FromSeconds(20);

        /// <summary>The client's MaxInteractiveEmoteSeperation is 1200 world units; the server allows some slack.</summary>
        public const double MaxSeparation = 1500;

        private sealed class Request
        {
            public WorldClient Requester, Target;
            public UInt32 EmoteId;
            public DateTime At;
        }

        private static readonly object Sync = new object();
        private static readonly List<Request> Requests = new List<Request>();

        // ---- sending animations --------------------------------------------------------------------------------

        private static AnimationPackets.Animation Next(WorldClient client, byte kind, byte stance, UInt16? action)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            client.playerData.emoteCounter++;
            return new AnimationPackets.Animation
            {
                Counter = client.playerData.emoteCounter, Kind = kind, Stance = stance, Action = action,
                X = (float) x, Y = (float) y, Z = (float) z
            };
        }

        /// <summary>The partner as <paramref name="viewer"/>'s client knows them: <c>&lt;view u16&gt; &lt;spawn id&gt; 00</c>.</summary>
        private static UInt32 Seen(WorldClient viewer, WorldClient partner) =>
            partner == null ? 0u : PvpCombat.HandleOf(viewer, partner);

        /// <summary>The animation to the subject's own view and to everyone with a view of them, each with the partner
        /// as they know them.</summary>
        private static void Play(WorldClient subject, AnimationPackets.Animation a, WorldClient partner, UInt32? flags = null)
        {
            if (!subject.IsBot)
            {
                subject.messageQueue.addObjectMessage(AnimationPackets.Own(a, Seen(subject, partner),
                    (UInt32) (TimeUtils.GetSimTimeSeconds() * 1000), flags), false);
                subject.FlushQueue();
            }

            List<WorldClient> others;
            lock (WorldServer.Clients)
            {
                others = WorldServer.Clients.Values.Where(c => c != null && c != subject && !c.IsBot).ToList();
            }

            foreach (WorldClient viewer in others)
            {
                ClientView view = viewer.viewMan.FindViewForEntity(subject.playerData.getEntityId());
                if (view == null || !view.viewCreated)
                {
                    continue;
                }

                viewer.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view.ViewID,
                    AnimationPackets.Other(a, Seen(viewer, partner), flags)), false);
                viewer.FlushQueue();
            }
        }

        // ---- emotes ----------------------------------------------------------------------------------------------

        /// <summary>Client 30 &lt;EmoteID u32&gt; &lt;TargetID u32&gt;: every emote. An interactive one asks the target.</summary>
        public static void ProcessEmote(WorldClient client, byte[] rpcData)
        {
            if (rpcData.Length < 4)
            {
                return;
            }

            UInt32 emoteId = BitConverter.ToUInt32(rpcData, 0);
            UInt32 targetId = rpcData.Length >= 8 ? BitConverter.ToUInt32(rpcData, 4) : 0;
            if (Paired.ContainsKey(emoteId))
            {
                RequestEmote(client, FindPlayerInView(client, targetId), emoteId);
                return;
            }

            // The server's emote table keeps the wire bytes read big-endian (/clap 0d 00 00 3a is 0x0d00003a)
            byte action = DataLoader.getInstance().findEmoteByLongId(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(emoteId));
            if (action == 0)
            {
                Output.WriteDebugLog("[EMOTE] Unknown emote 0x" + emoteId.ToString("x8"));
                return;
            }

            Play(client, Next(client, AnimationPackets.KindPlain, Stance(client), action), null);
        }

        private static WorldClient FindPlayerInView(WorldClient client, UInt32 targetId)
        {
            if (targetId == 0)
            {
                return null;
            }

            ClientView view = client.viewMan.getViewById((UInt16) targetId);
            if (view == null)
            {
                return null;
            }

            return SocialHandler.OnlineWhere(c => c != client && c.playerData.getEntityId() == view.entityId).FirstOrDefault();
        }

        private static double Distance(WorldClient a, WorldClient b)
        {
            double ax = 0, ay = 0, az = 0, bx = 0, by = 0, bz = 0;
            NumericalUtils.LtVector3dToDoubles(a.playerInstance.Position.getValue(), ref ax, ref ay, ref az);
            NumericalUtils.LtVector3dToDoubles(b.playerInstance.Position.getValue(), ref bx, ref by, ref bz);
            return Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by) + (az - bz) * (az - bz));
        }

        /// <summary>
        /// A paired emote's request: the target's client gets 31 and asks its player; the requester beckons, in their own
        /// view and everyone else's. Retail two-person capture: 30, then the own view's request animation, then (7 s
        /// later, after the target's 32) 32 with IsRequester 1 and the accepted animation.
        /// </summary>
        internal static void RequestEmote(WorldClient requester, WorldClient target, UInt32 emoteId)
        {
            byte refusal = 0xff;
            if (target == null)
            {
                refusal = ResultNotAPlayer;
            }
            else if (Distance(requester, target) > MaxSeparation)
            {
                refusal = ResultCannotReach;
            }
            else if (Stance(target) != AnimationPackets.StanceStanding)
            {
                refusal = ResultCannotPerform;
            }

            lock (Sync)
            {
                Requests.RemoveAll(r => DateTime.UtcNow - r.At > RequestLifetime);
                if (refusal == 0xff && Requests.Any(r => r.Target == target || r.Requester == requester))
                {
                    refusal = ResultCannotPerform;
                }

                if (refusal == 0xff)
                {
                    Requests.Add(new Request {Requester = requester, Target = target, EmoteId = emoteId, At = DateTime.UtcNow});
                }
            }

            if (refusal != 0xff)
            {
                SocialHandler.Send(requester, AnimationPackets.EmoteAnswer(refusal, emoteId, Seen(requester, target), 0, true));
                Output.WriteDebugLog("[EMOTE] " + SocialHandler.HandleOf(requester) + " paired emote 0x" + emoteId.ToString("x8") +
                                     " refused: " + refusal);
                return;
            }

            SocialHandler.Send(target, AnimationPackets.EmoteRequest(emoteId, Seen(target, requester)));
            Play(requester, Next(requester, AnimationPackets.KindRequest, AnimationPackets.StanceStanding, Paired[emoteId].Request), target);
            Output.WriteDebugLog("[EMOTE] " + SocialHandler.HandleOf(requester) + " asks " + SocialHandler.HandleOf(target) +
                                 " for 0x" + emoteId.ToString("x8"));
            SocialBot.OnEmoteRequested(target, emoteId);

            Request mine;
            lock (Sync)
            {
                mine = Requests.Last(r => r.Target == target);
            }

            DelayedActions.After(RequestLifetime.TotalSeconds, () => Expire(mine));
        }

        /// <summary>Nobody answered in time: the requester hears the refusal text (ours: the client never sends one).</summary>
        private static void Expire(Request request)
        {
            lock (Sync)
            {
                if (!Requests.Remove(request))
                {
                    return;
                }
            }

            SocialHandler.Send(request.Requester, AnimationPackets.EmoteAnswer(ResultNoAnswer, request.EmoteId,
                Seen(request.Requester, request.Target), 0, true));
        }

        /// <summary>Client 32 &lt;Result&gt; &lt;EmoteID&gt; &lt;Partner&gt; &lt;WaitTime&gt; &lt;IsRequester&gt;: the target's
        /// answer (1 accepts; 2 refuses; 4 and 7 come from the client itself, busy or ignoring the requester).</summary>
        public static void ProcessEmoteAnswer(WorldClient client, byte[] rpcData)
        {
            if (rpcData.Length < 5)
            {
                return;
            }

            Answer(client, rpcData[0], BitConverter.ToUInt32(rpcData, 1));
        }

        internal static void Answer(WorldClient target, byte result, UInt32 emoteId)
        {
            Request request;
            lock (Sync)
            {
                request = Requests.FirstOrDefault(r => r.Target == target && r.EmoteId == emoteId);
                if (request != null)
                {
                    Requests.Remove(request);
                }
            }

            if (request == null || !request.Requester.playerData.getOnWorld())
            {
                return;
            }

            WorldClient requester = request.Requester;
            SocialHandler.Send(requester, AnimationPackets.EmoteAnswer(result, emoteId, Seen(requester, target), 0, true));
            Output.WriteDebugLog("[EMOTE] " + SocialHandler.HandleOf(target) + " answers " + SocialHandler.HandleOf(requester) +
                                 ": " + result);
            if (result == ResultAccepted)
            {
                // Only the requester's view animates; retail sent the target nothing (the client moves the pair itself).
                Play(requester, Next(requester, AnimationPackets.KindAccepted, AnimationPackets.StanceStanding, Paired[emoteId].Accept), target);
            }
        }

        // ---- stances ---------------------------------------------------------------------------------------------

        public static byte Stance(WorldClient client) => client.playerData.stance;

        /// <summary>Client 34 &lt;stance&gt;: /sit (0a) and /dance (11..15) put the player in a stance.</summary>
        public static void ProcessStanceStart(WorldClient client, byte[] rpcData)
        {
            if (rpcData.Length >= 1)
            {
                SetStance(client, rpcData[0]);
            }
        }

        /// <summary>Client 33 &lt;current stance&gt;: /stand, /sit again, or moving out of a stance. Back to standing; the
        /// client keeps resending 33 until the stance it gets is 0 (retail ProxyM2_m2_packets6).</summary>
        public static void ProcessStanceStop(WorldClient client, byte[] rpcData)
        {
            SetStance(client, AnimationPackets.StanceStanding);
        }

        /// <summary>The stance to the player's own view and everyone else's; sitting also sets ConditionStateFlags 0x00200000.</summary>
        internal static void SetStance(WorldClient client, byte stance)
        {
            bool wasSitting = client.playerData.stance == AnimationPackets.StanceSitting;
            bool sitting = stance == AnimationPackets.StanceSitting;
            client.playerData.stance = stance;
            UInt32? flags = null;
            if (wasSitting != sitting)
            {
                if (sitting)
                {
                    client.playerData.conditionStateFlags |= AnimationPackets.SittingFlag;
                }
                else
                {
                    client.playerData.conditionStateFlags &= ~AnimationPackets.SittingFlag;
                }

                flags = PvpFlag.FlagsOf(client);
                client.playerInstance.ConditionStateFlags.setValue(flags.Value);
            }

            Play(client, Next(client, AnimationPackets.KindPlain, stance, null), null, flags);
            Output.WriteDebugLog("[EMOTE] " + SocialHandler.HandleOf(client) + " stance " + stance.ToString("x2"));
        }

        /// <summary>A player left the world: their requests go, and so do requests to them.</summary>
        public static void PlayerLeftWorld(WorldClient client)
        {
            lock (Sync)
            {
                Requests.RemoveAll(r => r.Requester == client || r.Target == client);
            }
        }
    }
}
