using System;
using System.Collections.Generic;
using System.Globalization;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Dev-only presentation probe for the instant sneak openers' named moves (ILDB 19590–19595). No retail capture shows
    /// how the stock client was told to play them, so this sends candidate presentations to the stock client without
    /// changing damage, state, cost or timers:
    /// <list type="bullet">
    /// <item>a: one extra round in the caller's live Close Combat view naming the opener's landed move, then a disengage;</item>
    /// <item>b: an at-rest targeted animation block naming the move as a 25 u16 named action (PlayThrow's record shape);</item>
    /// <item>c: control, the same round as a with the ILDB "Failed Sneak v &lt;style&gt;" move.</item>
    /// </list>
    /// </summary>
    public static class OpenerMoveProbe
    {
        public const string Command = "?openermove", Mode = "MXO_OPENER_MOVE_PROBE";
        internal static readonly ushort[] Openers = { 286, 288, 289, 385, 387, 388 };

        internal static bool Allowed(string mode, string dev, string database, uint character, bool live, bool dead) =>
            mode == "1" && character == 110 && QaLoadout.Allowed(dev, database, character, live, dead);

        /// <summary>The move each variant names: a/b the landed special, c the failed-sneak control.</summary>
        internal static CombatAnimations.Move Move(ushort opener, char variant) =>
            CombatAnimations.Special(opener, variant != 'c', CombatTactic.Speed);

        /// <summary>
        /// The targeted animation block with a named action: <c>[02 00] 01 20 &lt;counter&gt; 01 &lt;stance&gt; 25 &lt;move u16&gt;
        /// &lt;ability u16&gt; &lt;target u32&gt;</c>, i.e. <see cref="StealthShieldAbilities.BuildThrow"/> with the built-in action
        /// byte replaced by the 25-prefixed u16 used by named emotes.
        /// </summary>
        public static byte[] BuildNamedMove(bool own, byte counter, ushort move, ushort ability, uint target)
        {
            var bytes = new List<byte>();
            if (own) bytes.AddRange(new byte[] { 2, 0 });
            bytes.AddRange(new byte[] { 1, 0x20, counter, 1, StealthShieldAbilities.Aggro, 0x25, (byte)move, (byte)(move >> 8) });
            bytes.AddRange(BitConverter.GetBytes(ability));
            bytes.AddRange(BitConverter.GetBytes(target));
            return bytes.ToArray();
        }

        internal static string Dev(WorldClient caller, string[] args)
        {
            if (!Allowed(Environment.GetEnvironmentVariable(Mode), Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                    Store.config?.dbParams?.DatabaseName, caller.playerData.getCharID(), caller.playerData.getOnWorld(), caller.playerData.isDead))
                return "Opener move probe requires the disposable QA fixture.";
            if (args.Length != 2 || !ushort.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out ushort opener) ||
                Array.IndexOf(Openers, opener) < 0 || args[1].Length != 1 || "abc".IndexOf(args[1][0]) < 0)
                return "Use ?openermove <286|288|289|385|387|388> <a|b|c>.";
            char variant = args[1][0];
            var move = Move(opener, variant);
            if (move == null) return $"No ILDB move for {opener} variant {variant}.";
            string log = $"[OPENER-PROBE] {opener} variant {variant} move {move.Id} ({move.Name})";

            if (variant == 'b')
            {
                ushort view = caller.playerData.currentSelectedTargetViewId;
                var target = view > 2 ? caller.viewMan.getViewById(view) : null;
                if (target?.viewCreated != true) return "Select the opener's target first.";
                byte counter = ++caller.playerData.emoteCounter;
                caller.messageQueue.addObjectMessage(BuildNamedMove(true, counter, (ushort)move.Id, opener, DuelPackets.Handle(target.ViewID, target.spawnId)), false);
                caller.FlushQueue();
                foreach (var observer in SocialHandler.OnlineWhere(c => c != caller && c.playerData.getDistrictId() == caller.playerData.getDistrictId()))
                {
                    var actor = observer.viewMan.FindViewForEntity(caller.playerData.getEntityId());
                    var seen = observer.viewMan.FindViewForEntity(target.entityId);
                    if (actor?.viewCreated != true || seen?.viewCreated != true) continue;
                    observer.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(actor.ViewID,
                        BuildNamedMove(false, counter, (ushort)move.Id, opener, DuelPackets.Handle(seen.ViewID, seen.spawnId))), false);
                    observer.FlushQueue();
                }
                Output.WriteDebugLog(log + " at rest");
                return $"Probe b sent: named action {move.Id} ({move.Name}).";
            }

            var handler = CombatManager.GetHandler(caller);
            bool sent = handler.pvpFight?.ProbeRound(caller, opener, variant == 'a') ?? false;
            if (!sent && handler.NpcPresentation is CombatPresentation npc)
            {
                npc.Round(true, CombatRules.SpecialMove, CombatTactic.Speed, CombatTactic.Speed, 0, 0, false, opener, variant == 'a');
                sent = true;
            }
            if (!sent) return "Open Close Combat with the opener's target first.";
            DelayedActions.AfterFor(caller, move.DurationMs / 1000.0 + 0.5, () => CombatManager.GetHandler(caller).LeaveOtherFights());
            Output.WriteDebugLog(log + " as one round");
            return $"Probe {variant} sent: round move {move.Id} ({move.Name}); disengaging after {move.DurationMs} ms.";
        }
    }
}
