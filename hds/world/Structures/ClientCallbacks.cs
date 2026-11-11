using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Callbacks for the two things the client hands back to the server: a two-button prompt's answer (server 81 8f,
    /// client 81 90 &lt;Message&gt; &lt;LeftChosen&gt;) and an FX token (server 81 42 &lt;fx&gt; &lt;token&gt;, client
    /// 81 91 &lt;token&gt; when the FX ends). The client only echoes the Message and token it was given, so any feature can
    /// open a prompt or play an FX and be called back. Ids come from ranges the tutorial doesn't use (its prompts and
    /// tokens are small numbers from its script); anything not registered here goes on to the tutorial, as before.
    /// </summary>
    public static class ClientCallbacks
    {
        public const UInt32 FirstModalMessage = 0x00010000;
        public const UInt32 FirstFxToken = 0x00010000;

        /// <summary>Oldest callbacks are dropped beyond this many per player: a prompt can be closed without an answer.</summary>
        public const int MaxPending = 32;

        public sealed class State
        {
            internal readonly object Sync = new object();
            internal UInt32 NextMessage = FirstModalMessage;
            internal UInt32 NextToken = FirstFxToken;
            internal readonly List<KeyValuePair<UInt32, Action<bool>>> Modals = new List<KeyValuePair<UInt32, Action<bool>>>();
            internal readonly List<KeyValuePair<UInt32, Action>> Fx = new List<KeyValuePair<UInt32, Action>>();
        }

        private static State Of(WorldClient client)
        {
            lock (client.playerData)
            {
                return client.playerData.callbacks ?? (client.playerData.callbacks = new State());
            }
        }

        /// <summary>Registers an answer callback and returns the Message to put in the 81 8f.</summary>
        public static UInt32 RegisterModal(WorldClient client, Action<bool> onAnswer) => RegisterModal(Of(client), onAnswer);

        internal static UInt32 RegisterModal(State state, Action<bool> onAnswer)
        {
            lock (state.Sync)
            {
                UInt32 message = state.NextMessage++;
                state.Modals.Add(new KeyValuePair<UInt32, Action<bool>>(message, onAnswer));
                if (state.Modals.Count > MaxPending)
                {
                    state.Modals.RemoveAt(0);
                }

                return message;
            }
        }

        /// <summary>Registers an FX-end callback and returns the token to put in the 81 42.</summary>
        public static UInt32 RegisterFx(WorldClient client, Action onEnd) => RegisterFx(Of(client), onEnd);

        internal static UInt32 RegisterFx(State state, Action onEnd)
        {
            lock (state.Sync)
            {
                UInt32 token = state.NextToken++;
                state.Fx.Add(new KeyValuePair<UInt32, Action>(token, onEnd));
                if (state.Fx.Count > MaxPending)
                {
                    state.Fx.RemoveAt(0);
                }

                return token;
            }
        }

        /// <summary>Shows a two-button prompt (81 8f) and calls <paramref name="onAnswer"/> with true for the left button.</summary>
        public static void ShowModal(WorldClient client, UInt32 image, UInt32 stringId, UInt32 leftButton, UInt32 rightButton,
            Action<bool> onAnswer)
        {
            UInt32 message = RegisterModal(client, onAnswer);
            client.messageQueue.addRpcMessage(ServerPackets.BuildModalMessage(image, stringId, message, leftButton, rightButton));
            client.FlushQueue();
        }

        /// <summary>Plays an FX on the player's client (81 42) and calls <paramref name="onEnd"/> when it says the FX ended.</summary>
        public static void PlayFx(WorldClient client, UInt32 fx, Action onEnd)
        {
            UInt32 token = RegisterFx(client, onEnd);
            client.messageQueue.addRpcMessage(ServerPackets.BuildLocalFx(fx, token));
            client.FlushQueue();
        }

        /// <summary>Client 81 90. Runs and forgets the callback for that Message; false when none is registered.</summary>
        public static bool OnModalAnswer(WorldClient client, UInt32 message, bool left) => OnModalAnswer(Of(client), message, left);

        internal static bool OnModalAnswer(State state, UInt32 message, bool left)
        {
            Action<bool> callback = Take(state, s => s.Modals, message);
            if (callback == null)
            {
                return false;
            }

            Run(() => callback(left));
            return true;
        }

        /// <summary>Client 81 91. Runs and forgets the callback for that token; false when none is registered.</summary>
        public static bool OnFxEnded(WorldClient client, UInt32 token) => OnFxEnded(Of(client), token);

        internal static bool OnFxEnded(State state, UInt32 token)
        {
            Action callback = Take(state, s => s.Fx, token);
            if (callback == null)
            {
                return false;
            }

            Run(callback);
            return true;
        }

        private static T Take<T>(State state, Func<State, List<KeyValuePair<UInt32, T>>> list, UInt32 id) where T : class
        {
            lock (state.Sync)
            {
                List<KeyValuePair<UInt32, T>> entries = list(state);
                int index = entries.FindIndex(e => e.Key == id);
                if (index < 0)
                {
                    return null;
                }

                T callback = entries[index].Value;
                entries.RemoveAt(index);
                return callback;
            }
        }

        private static void Run(Action callback)
        {
            try
            {
                callback();
            }
            catch (Exception e)
            {
                Output.WriteDebugLog("[CALLBACK] A prompt or FX callback failed: " + e);
            }
        }

        /// <summary>RpcManager's 81 90: a registered callback, else the tutorial (its own prompts).</summary>
        public static void DispatchModalAnswer(WorldClient client, byte[] rpcData)
        {
            if (rpcData.Length < 5)
            {
                return;
            }

            UInt32 message = BitConverter.ToUInt32(rpcData, 0);
            bool left = rpcData[4] != 0;
            if (!OnModalAnswer(client, message, left))
            {
                TutorialMission.Of(client)?.OnModalAnswer(message, left);
            }
        }

        /// <summary>RpcManager's 81 91: a registered callback, else the tutorial (its FX tokens).</summary>
        public static void DispatchFxEnded(WorldClient client, byte[] rpcData)
        {
            if (rpcData.Length < 4)
            {
                return;
            }

            UInt32 token = BitConverter.ToUInt32(rpcData, 0);
            if (!OnFxEnded(client, token))
            {
                TutorialMission.Of(client)?.OnClientEvent(token);
            }
        }
    }
}
