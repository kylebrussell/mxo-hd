using System;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Presentation of an instant sneak opener landed at rest (CastWhenUsed 1, Spy Style, Stealthed, outside combat):
    /// one Close Combat exchange naming the opener's ILDB "Sneak Attacks" move, shown to the caster, a player target and
    /// observers through the ordinary combat view object, then removed after the move. The stock client plays the named
    /// full-body move from this round record (PR1191 probe variant a: 286 Twist Throw, 288 Topsy Turvy) and not from an
    /// at-rest animation block (variant b). Damage, state, cost and timers are unchanged; nothing enters combat.
    /// </summary>
    public static class SneakOpenerRound
    {
        public const string FailMode = "MXO_SNEAK_OPENER_FAIL_QA";
        public static readonly ushort[] Openers = { 152, 153, 286, 287, 288, 289, 385, 386, 387, 388 };

        private sealed class Until { internal double Seconds; }
        private static readonly ConditionalWeakTable<Mob, Until> holds = new ConditionalWeakTable<Mob, Until>();

        /// <summary>Defers this NPC's retaliation until its opener exchange has finished (the HideFighters delay).</summary>
        internal static void Hold(Mob mob, double seconds, double now)
        {
            if (mob == null || seconds <= 0) return;
            var until = holds.GetValue(mob, _ => new Until());
            lock (until) until.Seconds = Math.Max(until.Seconds, now + seconds);
        }

        /// <summary>Seconds left before the NPC may retaliate after an at-rest opener exchange, or 0.</summary>
        internal static double RetaliationHold(Mob mob, double now)
        {
            if (mob == null || !holds.TryGetValue(mob, out var until)) return 0;
            lock (until) return Math.Max(0, until.Seconds - now);
        }

        public static bool IsOpener(ushort ability) => Array.IndexOf(Openers, AbilityCatalog.Canonical(ability)) >= 0;

        /// <summary>Only an at-rest landing: a queued opener inside an interlock already presents as that fight's round.</summary>
        internal static bool Applies(WorldClient caster, ushort ability)
        {
            var handler = CombatManager.GetHandler(caster);
            return IsOpener(ability) && !handler.isCombatRunning && !handler.HasActiveInterlock && handler.pvpFight == null &&
                CombatAnimations.Special(ability, true, CombatTactic.Speed) != null;
        }

        public const string Command = "?openerfail";
        private static volatile bool qaFail;

        private static bool QaAllowed(WorldClient caster) =>
            Environment.GetEnvironmentVariable(FailMode) == "1" && caster.playerData.getCharID() == 110 &&
            QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                caster.playerData.getCharID(), caster.playerData.getOnWorld(), caster.playerData.isDead);

        /// <summary>QA only: present the ILDB "Failed Sneak v &lt;style&gt;" exchange instead (presentation, not outcome).</summary>
        internal static bool ForcedFail(WorldClient caster) => qaFail && QaAllowed(caster);

        internal static string Dev(WorldClient caller, string[] args)
        {
            if (!QaAllowed(caller)) return "Opener fail presentation requires the disposable QA fixture.";
            if (args.Length != 1 || (args[0] != "on" && args[0] != "off")) return "Use ?openerfail on|off.";
            qaFail = args[0] == "on";
            return $"Opener fail presentation {(qaFail ? "on" : "off")}.";
        }

        internal static void Present(WorldClient caster, WorldClient target, Mob mob, ushort ability)
        {
            ability = AbilityCatalog.Canonical(ability);
            if ((target == null) == (mob == null) || !Applies(caster, ability)) return;
            bool landed = !ForcedFail(caster);
            var move = CombatAnimations.Special(ability, landed, CombatTactic.Speed, target == null ? 'S' : CombatStyles.Letter(target));
            if (move == null) return;

            var handler = new AttributeClass3664("ILCombatHandler", 55); // one object, not all ~40k definitions
            handler.DisableAllAttributes();
            handler.StartTime.enable();
            handler.Position.enable();
            handler.StartTime.setValue(CombatHandler.CombatStartTime());
            handler.Position.setValue(caster.playerInstance.Position.getValue());
            ulong entity;
            lock (WorldServer.gameServerEntities.SyncRoot)
            {
                entity = WorldServer.entityIdCounter++;
                WorldServer.gameServerEntities.Add(handler);
            }
            var presentation = new CombatPresentation(caster, target, mob, handler, entity);
            presentation.ShowFighters();
            presentation.Round(true, CombatRules.SpecialMove, CombatTactic.Speed, CombatTactic.Speed, 0, 0, false, ability, landed);
            Output.WriteDebugLog($"[SNEAK-OPENER-ROUND] {ability} {(landed ? "landed" : "failed (QA)")} move {move.Id} ({move.Name}) " +
                $"on {(target != null ? TimedEffects.PlayerName(target) : mob.getName())}");
            double presentSeconds = move.DurationMs / 1000.0 + 0.7;
            Hold(mob, presentSeconds, TimeUtils.GetSimTimeSeconds());
            DelayedActions.After(presentSeconds, () =>
            {
                presentation.HideFighters();
                lock (WorldServer.gameServerEntities.SyncRoot) WorldServer.gameServerEntities.Remove(handler);
                // The exchange turned the fighters toward each other on every screen. Re-send each one's real pose: the
                // stock client otherwise keeps the target facing the caster, and a later rear opener is refused locally.
                RestorePose(caster);
                if (target != null) RestorePose(target);
                if (mob != null && !mob.getIsDead()) mob.updateAnimation((byte)Mob.Animations.STAND);
            });
        }

        /// <summary>Other views: the moving-player state part with the server's own yaw and position (PvpFight.BuildFacing).</summary>
        internal static byte[] FacingRecord(WorldClient player)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(player.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return PvpFight.BuildFacing(player.playerInstance.YawInterval.getValue()[0], (float)x, (float)y, (float)z);
        }

        private static void RestorePose(WorldClient player)
        {
            if (!player.Alive || !player.playerData.getOnWorld() || player.playerData.isDead) return;
            PvpFlag.ToWatchers(player, FacingRecord(player));
            // Own view: the stock-verified stance reset at the current position (StealthShieldAbilities.ResetMotion).
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(player.playerInstance.Position.getValue(), ref x, ref y, ref z);
            byte counter = ++player.playerData.emoteCounter;
            player.messageQueue.addObjectMessage(DeathPresentation.Build(true, counter, player.playerData.stance, 0, DeathPresentation.NoAbility,
                (float)x, (float)y, (float)z, CombatHandler.CombatStartTime()), false);
            player.FlushQueue();
        }
    }
}
