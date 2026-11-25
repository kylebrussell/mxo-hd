using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Implements the player-target portions of <c>HolyWaterAttack_DirectObject</c> (547, general.py:99-137) and
    /// <c>HolyWaterPlusAttack_DirectObject</c> (1023, general.py:140-154):
    /// <list type="bullet">
    /// <item>Multiplies damage against a player holding <c>NPCKilledOnlyByHolyWaterAbility</c> (432): 100 for 547, 750 for 1023.</item>
    /// <item>When a player holds <c>ZombieVirus2BaseAbility</c> (1235) or <c>ZombieVirus3BaseAbility</c> (1238), removes the five
    /// Zombie Virus ability grants (1235, 1238, 1236, 1237, 432), deactivates the 24 <c>RSICap*</c> abilities for 547 only,
    /// and reverts the target to their normal RSI (<c>revertToRSI()</c>).</item>
    /// </list>
    /// </summary>
    public static class HolyWaterCure
    {
        public const string QaMode = "MXO_HOLY_WATER_QA";
        public const ushort ZombieVirus2Base = 1235;
        public const ushort ZombieVirus2Bite = 1236;
        public const ushort ZombieVirus3Bite = 1237;
        public const ushort ZombieVirus3Base = 1238;
        public const ushort KilledOnlyByHolyWater = CombatSpecials.KilledOnlyByHolyWater; // 432
        public const ushort RsiCapture = 1388;
        public const uint ZombieModelDescription = 0x3b000001u;

        public static readonly ushort[] ZombieVirusGrants =
        {
            ZombieVirus2Base,
            ZombieVirus3Base,
            ZombieVirus2Bite,
            ZombieVirus3Bite,
            KilledOnlyByHolyWater
        };

        // Exact 24 RSI capture abilities deactivated by HolyWaterAttack_DirectObject (general.py:112-135).
        public static readonly ushort[] RsiCaptureAbilities =
        {
            1388, // RSICaptureAbility
            1465, // RSICapEliteAbility
            1467, // RSICapExecAbility
            1445, // RSICapKuno83Ability
            1447, // RSICapKuno84Ability
            1446, // RSICapKuno85Ability
            1448, // RSICapKuno86Ability
            1449, // RSICapKuno87Ability
            1450, // RSICapKuno88Ability
            1451, // RSICapKuno89Ability
            1452, // RSICapKuno90Ability
            1453, // RSICapKuno91Ability
            1454, // RSICapKuno92Ability
            1455, // RSICapKuno93Ability
            1456, // RSICapKuno94Ability
            1457, // RSICapKuno95Ability
            1458, // RSICapKuno96Ability
            1459, // RSICapKuno97Ability
            1460, // RSICapKuno98Ability
            1461, // RSICapKuno99Ability
            1462, // RSICapKuno100Ability
            1463, // RSICapSSRAbility
            1464, // RSICapValfAbility
            1466  // RSICapWrightAbility
        };

        private sealed class State
        {
            public readonly HashSet<ushort> Grants = new HashSet<ushort>();
            public readonly HashSet<ushort> RsiCaps = new HashSet<ushort>();
            public bool ModelOverridden;
        }

        private static readonly ConditionalWeakTable<ClientData, State> states = new ConditionalWeakTable<ClientData, State>();
        private static State Of(WorldClient client) => states.GetOrCreateValue(client.playerData);

        public static bool HasGrant(WorldClient client, ushort ability)
        {
            if (client?.playerData == null) return false;
            ushort id = AbilityCatalog.Canonical(ability);
            var state = Of(client);
            lock (state)
                if (state.Grants.Contains(id)) return true;
            return AbilityPassives.IsLoaded(client, id) || AbilityBuffs.IsActive(client, id);
        }

        public static bool HasActiveRsiCapture(WorldClient client, ushort ability)
        {
            if (client?.playerData == null) return false;
            ushort id = AbilityCatalog.Canonical(ability);
            var state = Of(client);
            lock (state)
                if (state.RsiCaps.Contains(id)) return true;
            return AbilityBuffs.IsActive(client, id);
        }

        public static ushort Weakness(WorldClient target) =>
            HasGrant(target, KilledOnlyByHolyWater) ? KilledOnlyByHolyWater : (ushort)0;

        public static bool IsInfected(WorldClient target) =>
            HasGrant(target, ZombieVirus2Base) || HasGrant(target, ZombieVirus3Base);

        public static void Grant(WorldClient target, ushort ability, ushort level = 0)
        {
            if (target?.playerData == null) return;
            ushort id = AbilityCatalog.Canonical(ability);
            var state = Of(target);
            bool added;
            lock (state) added = state.Grants.Add(id);
            if (added && target.messageQueue != null)
            {
                target.messageQueue.addRpcMessage(ServerPackets.BuildAbilityLoad(id, level, AbilityProgression.HiddenSlot));
                target.FlushQueue();
            }
        }

        public static void ActivateRsiCapture(WorldClient target, ushort ability, uint modelDescription = ZombieModelDescription)
        {
            if (target?.playerData == null) return;
            ushort id = AbilityCatalog.Canonical(ability);
            var state = Of(target);
            lock (state)
            {
                state.RsiCaps.Add(id);
                state.ModelOverridden = true;
            }
            ApplyModelOverride(target, modelDescription);
        }

        public static void ApplyModelOverride(WorldClient target, uint modelDescription = ZombieModelDescription)
        {
            if (target?.playerInstance == null) return;
            var state = Of(target);
            lock (state) state.ModelOverridden = true;
            byte[] useRsi = new byte[] { 0 };
            byte[] desc = PlayerViewAttributes.U32(modelDescription);
            target.playerInstance.UseRSIDescription.setValue(useRsi);
            target.playerInstance.Description.setValue(desc);
            target.messageQueue?.addObjectMessage(PlayerViewAttributes.BuildSelf((0, useRsi), (37, desc)), false);
            PvpFlag.ToWatchers(target, PlayerViewAttributes.Build((0, useRsi), (28, desc)));
            target.FlushQueue();
        }

        public static bool LandedOnPlayer(WorldClient caster, WorldClient target, ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            if (id != CombatSpecials.HolyWater && id != CombatSpecials.HolyWaterPlus) return false;
            if (target?.playerData == null || !IsInfected(target)) return false;

            var state = Of(target);
            var removedGrants = new List<ushort>();
            foreach (ushort grant in ZombieVirusGrants)
            {
                bool had;
                lock (state) had = state.Grants.Remove(grant);
                if (AbilityBuffs.Remove(target, grant)) had = true;
                if (had)
                {
                    removedGrants.Add(grant);
                    target.messageQueue?.addRpcMessage(ServerPackets.BuildAbilityUnload(grant));
                }
            }

            var removedCaps = new List<ushort>();
            if (id == CombatSpecials.HolyWater)
            {
                foreach (ushort cap in RsiCaptureAbilities)
                {
                    bool had;
                    lock (state) had = state.RsiCaps.Remove(cap);
                    if (AbilityBuffs.Remove(target, cap)) had = true;
                    if (TimedEffects.RemoveParent(TimedEffects.ForPlayer(target), cap)) had = true;
                    if (had) removedCaps.Add(cap);
                }
            }

            RevertToRsi(target);

            string casterName = caster != null ? PvpFlag.Name(caster) : "?";
            string targetName = PvpFlag.Name(target);
            string grantList = removedGrants.Count > 0 ? string.Join(",", removedGrants) : "none";
            string capList = removedCaps.Count > 0 ? string.Join(",", removedCaps) : "none";
            Output.WriteDebugLog($"[HOLY-WATER-CURE] caster={casterName} target={targetName} ability={id} removed={grantList} rsiCaps={capList} revertedRsi=true");
            return true;
        }

        public static void RevertToRsi(WorldClient target)
        {
            if (target?.playerInstance == null) return;
            var state = Of(target);
            lock (state) state.ModelOverridden = false;

            byte[] useRsi = new byte[] { 1 };
            byte[] zeroDesc = PlayerViewAttributes.U32(0);
            target.playerInstance.UseRSIDescription.setValue(useRsi);
            target.playerInstance.Description.setValue(zeroDesc);
            target.messageQueue?.addObjectMessage(PlayerViewAttributes.BuildSelf((0, useRsi), (37, zeroDesc)), false);
            PvpFlag.ToWatchers(target, PlayerViewAttributes.Build((0, useRsi), (28, zeroDesc)));

            int[] rsiValues = target.playerData?.getRsiValues();
            if (rsiValues != null)
            {
                byte[] rsi = PacketsUtils.getRSIBytes(rsiValues);
                target.playerInstance.RSIDescription.setValue(rsi);
                target.messageQueue?.addObjectMessage(ServerPackets.BuildAppearanceUpdate(rsi), false);
                if (Store.world != null)
                    PlayerProgression.AnnounceToOthers(target, new List<Attribute> { target.playerInstance.RSIDescription });
            }
            target.FlushQueue();
        }

        private static bool QaEnabled(WorldClient caster) =>
            caster?.playerData != null && caster.playerData.getCharID() == 110 &&
            AbilitySeverArteryStates.QaAllowed(
                Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                Store.config?.dbParams?.DatabaseName,
                Environment.GetEnvironmentVariable(QaMode));

        internal static bool AllowsQaWin(WorldClient caster, WorldClient opponent, ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return (id == CombatSpecials.HolyWater || id == CombatSpecials.HolyWaterPlus) &&
                QaEnabled(caster) && opponent != null &&
                string.Equals(PvpFlag.Name(opponent), "QAEnemy", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool AllowsNpcQaWin(WorldClient caster, ushort abilityId)
        {
            ushort id = AbilityCatalog.Canonical(abilityId);
            return (id == CombatSpecials.HolyWater || id == CombatSpecials.HolyWaterPlus) && QaEnabled(caster);
        }

        public static string Dev(WorldClient client, string[] args)
        {
            if (!QaEnabled(client))
                return "Holy Water QA command requires MXO_DEV_COMMANDS=1, MXO_HOLY_WATER_QA=1, and a disposable test database.";
            if (args == null || args.Length < 2 || !args[0].Equals("infect", StringComparison.OrdinalIgnoreCase))
                return "Usage: ?holywater infect <2|3> [rsicap]";

            ushort baseAbility, biteAbility;
            if (args[1] == "2") { baseAbility = ZombieVirus2Base; biteAbility = ZombieVirus2Bite; }
            else if (args[1] == "3") { baseAbility = ZombieVirus3Base; biteAbility = ZombieVirus3Bite; }
            else return "Usage: ?holywater infect <2|3> [rsicap]";

            bool withRsiCap = args.Length >= 3 && args[2].Equals("rsicap", StringComparison.OrdinalIgnoreCase);
            WorldClient target = FindQaTarget();
            if (target == null) return "Holy Water QA target QAEnemy is not online.";

            // Restore target HP to max so repeated infect/cure rounds stay alive.
            ushort maxHp = NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(), 1);
            if (maxHp > 0) AbilityHandler.HealSelf(target, maxHp);

            Grant(target, baseAbility, 0);
            Grant(target, biteAbility, 0);
            Grant(target, KilledOnlyByHolyWater, 1);
            if (withRsiCap) ActivateRsiCapture(target, RsiCapture);
            else ApplyModelOverride(target);

            Output.WriteDebugLog($"[HOLY-WATER-QA] Infected {PvpFlag.Name(target)} with variant={args[1]} grants={baseAbility},{biteAbility},{KilledOnlyByHolyWater} rsicap={(withRsiCap ? RsiCapture.ToString() : "none")}");
            return $"Infected {PvpFlag.Name(target)} with ZombieVirus{args[1]} ({baseAbility},{biteAbility},{KilledOnlyByHolyWater})" +
                (withRsiCap ? $" + RSICapture({RsiCapture})." : ".");
        }

        private static WorldClient FindQaTarget()
        {
            var clients = WorldServer.Clients;
            if (clients == null) return null;
            lock (clients)
                return clients.Values.FirstOrDefault(c => c?.playerData != null &&
                    string.Equals(PvpFlag.Name(c), "QAEnemy", StringComparison.OrdinalIgnoreCase));
        }
    }
}
