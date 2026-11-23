using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Buff abilities, as retail applied them (research/packet-dumps). When the cast ends the server sent one
    /// <c>80 bc</c> modifier record per stat the buff changes:
    /// <code>80 bc &lt;kind u16&gt; &lt;record id u16&gt; 00 &lt;ability u32&gt; &lt;stat u16&gt; &lt;value i32&gt; &lt;00 on / 01 off&gt; &lt;seconds f32&gt;</code>
    /// e.g. Overclock (abilities_useoverclocked): <c>80 bc 15 00 42 01 00 47 01 00 00 ac 00 2b 00 00 00 00 00 00 a0 41</c>
    /// (kind 0x15, record 0x142, ability 327, stat 172 +43, on, 20 s). The client shows the ability's icon in
    /// the buff bar with the countdown. When the time was up the server sent the same records with the off
    /// byte set (Consistent Technique, entilsar lastday2: on at 21:58:20, off at 21:58:41, 20 s).
    /// "Exclusive" buffs (Evade Combat, Hyper-Sprint, the Hyper- defences, the Kung Fu / class buffs) have no
    /// time; they were switched on with <c>80 bd 05 &lt;ability u16&gt; 00 00 00 00 00 01</c> and stayed on
    /// until another buff of the same family replaced them (Corpsman -&gt; Team Healer and Hyper-Deflect -&gt;
    /// Hyper-Block were turned off with <c>... 00</c> and their records sent off in the same packet).
    /// Movement buffs also set the own-view MovementScale (the extra speed: Hyper-Speed 0.45, Hyper-Sprint 0.15)
    /// and DissemblingType, cleared to 0 when they end (Hyper-Speed, entilsar lastday2, 600 s later).
    /// </summary>
    public static class AbilityBuffs
    {
        /// <summary>Record kinds seen in retail buffs.</summary>
        public const UInt16 KindStat = 0x15, KindState = 0x19, KindLinked = 0x1d;

        /// <summary>
        /// Record ids are the client's handle for a modifier. The login sequence uses 0x02-0x11 for the loaded
        /// abilities' passive modifiers, so buffs count up from here.
        /// </summary>
        public const UInt16 FirstRecordId = 0x100;

        public struct Modifier
        {
            public UInt16 Kind;
            /// <summary>The record's ability field; 0 means the buff's own ability (Evade's second record names 624).</summary>
            public UInt32 Source;
            public UInt16 Stat;
            public Int32 Value;

            public Modifier(UInt16 kind, UInt16 stat, Int32 value, UInt32 source = 0)
            {
                Kind = kind;
                Stat = stat;
                Value = value;
                Source = source;
            }
        }

        public class Buff
        {
            /// <summary>Countdown the client shows; 0 = no time limit (exclusive buffs).</summary>
            public float Seconds;
            public bool Exclusive;
            public int RefundIS, TermHeal, TermSweepChance;
            public string HealthChangedProc;
            public int HealthChangedValue;
            public int DrainAmount;
            public double DrainSeconds;
            public uint PeriodicFx;
            public double PeriodicFxSeconds;
            /// <summary>Buffs of the same non-zero family replace each other.</summary>
            public int Family;
            public Modifier[] Modifiers;
            /// <summary>Own-view MovementScale while the buff is on (0 = leave it alone).</summary>
            public float MovementScale;
            public UInt16 DissemblingType;
            public UInt32 EffectId;
            /// <summary>
            /// Using it again while it is on switches it off.
            /// </summary>
            public bool Toggle;
            /// <summary>Own-view EvadeShieldHealth starts at ff and clears to 00 when Evade Combat ends.</summary>
            public bool EvadeShield;
        }

        internal const ushort SittingEffect = 671;
        internal const ushort HyperSpeed = 12;
        internal const ushort HyperSprint = 184;
        public const int FamilyHyperDefence = 1, FamilyClassBuff = 2;
        internal const ushort ForceMultiplierUpgrade = 1091;
        internal const ushort UpgradeArtistUpgrade = 1095;
        internal const ushort DoctorUpgrade = 1097;

        internal static bool HasClassSweep(ushort id) => id == ForceMultiplierUpgrade || id == UpgradeArtistUpgrade;
        private const int ClassUpgradeStackingType = 37;
        internal static bool IsStartingClassUpgrade(ushort id)
        {
            var entry = AbilityCatalog.Find(id);
            return entry?.Number("Ability/StackingType") == ClassUpgradeStackingType &&
                entry.Number("Ability/IsAToggleAbility") == 1;
        }
        private const ushort MovementAccelerator = 62;

        private static Modifier Stat(UInt16 stat, Int32 value) => new Modifier(KindStat, stat, value);

        public static readonly Dictionary<UInt16, Buff> Scripted = new Dictionary<UInt16, Buff>
        {
            // tempmods.py EnergizedAttacksInit / PerciseBlowInit set TT_ENERGIZED / TT_PRECISE.
            // These have no numeric modifier; the exclusive switch and combat consumer carry the toggle.
            { 9, new Buff { Exclusive = true, Toggle = true, Family = 20000, Modifiers = Array.Empty<Modifier>() } },
            { 15, new Buff { Exclusive = true, Toggle = true, Family = 20000, Modifiers = Array.Empty<Modifier>() } },
            // GOB 671 and ProxyM2_m2_packets6 lines 69380-69384, add/remove on the sitting stance.
            { SittingEffect, new Buff { Modifiers = new[] {
                Stat(1009, -200), Stat(114, 200), Stat(1010, -200), Stat(112, 300), Stat(1011, -200), Stat(183, -200)
            } } },
            // Stored Gaussian Blur numeric effect + ImpartBlurInit/Term's kBlur (client62521e2f).
            { 49, new Buff { Seconds = 600, Family = 1013, DissemblingType = 0x40, Modifiers = new[] { Stat(520, 25) } } },
        };

        /// <summary>Ability id to the buff it gives; records, values and times from the first retail use found.</summary>
        public static readonly Dictionary<UInt16, Buff> Retail = new Dictionary<UInt16, Buff>
        {
            // Consistent Technique, entilsar lastday2 21:58:20, off 21 s later
            { 6, new Buff { Seconds = 20, Modifiers = new[] { Stat(180, 235) } } },
            // Hyper-Dodge, RajkoPacketsNearTheEnd ChillinginMara
            { 10, new Buff { Exclusive = true, Family = FamilyHyperDefence, Modifiers = new[] { Stat(1010, 51) } } },
            // Hyper-Speed, entilsar lastday2 (off 600 s later with MovementScale 0); own view sent by SendHyperSpeed
            { 12, new Buff { Seconds = 600, MovementScale = 0.45f, DissemblingType = 0x1000, EffectId = 0x28000656,
                Modifiers = new[] { Stat(877, 12), Stat(878, 12), new Modifier(KindLinked, 115, 45) } } },
            // Healer, crashover2k proxy_024: four -25/-18 records, 10 s
            { 55, new Buff { Seconds = 10, Modifiers = new[] { Stat(522, -25), Stat(524, -18), Stat(1032, -18), Stat(342, -25) } } },
            // Resist Combat, crashover2k proxy_024: 600 s countdown
            { 72, new Buff { Seconds = 600, Modifiers = new[] { Stat(520, 20) } } },
            // Hyper-Sprint, luizoe old sniffer Unknown: MovementScale 0.15, EffectID 0x12463119, DissemblingType 04 00
            { HyperSprint, new Buff { Exclusive = true, Toggle = true, DrainAmount = 25, DrainSeconds = 5,
                MovementScale = 0.15f, DissemblingType = 0x0400, EffectId = 0x12463119,
                Modifiers = new[] { new Modifier(KindLinked, 0, 25), Stat(877, 15), Stat(878, 15), new Modifier(KindLinked, 115, 15) } } },
            // Evade Combat, evadecombat.txt: 80 bc 19 .. d9 00 00 00 70 02 .. and 80 bc 1d .. 70 02 00 00 00 00 ..
            // Evade Combat, evadecombat.txt; switched off by using it again (entilsar lastday2 21:57:08-09: the
            // client's second 80 b9 d9 00, then the records off, 80 bd 01 d9 00 04 .. f0 41 01 (30 s) and 80 bd 05 off)
            { 217, new Buff { Exclusive = true, Toggle = true, EvadeShield = true,
                Modifiers = new[] { new Modifier(KindState, 624, 0), new Modifier(KindLinked, 0, 0, 624) } } },
            // Iron Body, medanon niobe3dualduality 79170: ranged and melee damage resistance, 20 s
            { 271, new Buff { Seconds = 20, Modifiers = new[] { Stat(181, 48), Stat(180, 44) } } },
            // Range Booster, old_world Vector 8567: DebuffAreaRadius +21, 30 s
            { 345, new Buff { Seconds = 30, Modifiers = new[] { Stat(177, 21) } } },
            // Force Enhancer, crashover2k proxy_024 13371: 30 s
            { 383, new Buff { Seconds = 30, Modifiers = new[] { Stat(171, 6), Stat(109, -317), Stat(1010, 21), Stat(524, 46) } } },
            // Corrupt Code 2, old_world Vector 46153: DamageResistance +10, CombatTactics +9 for 120 s ("boosts your toughness")
            { 992, new Buff { Seconds = 120, Modifiers = new[] { Stat(144, 10), Stat(163, 9) } } },
            // Serene Calm, medanon niobe3dualduality: 40 s
            { 272, new Buff { Seconds = 40, Modifiers = new[] { Stat(1009, 57), Stat(1011, 49), Stat(183, 54), Stat(1010, 51) } } },
            // Overclock (Avoidance), abilities_useoverclocked
            { 327, new Buff { Seconds = 20, Modifiers = new[] { Stat(172, 43) } } },
            // Combat Hacking, abilities_usecombathacking: 32 s
            { 344, new Buff { Seconds = 32, Modifiers = new[] { Stat(1010, 22), Stat(117, 24), Stat(1009, 22) } } },
            // Kung Fu Grand Master, RajkoPacketsNearTheEnd
            { 1064, new Buff { Exclusive = true, Toggle = true, Family = FamilyClassBuff, Modifiers = new[] { Stat(1009, 8), Stat(106, 137), Stat(522, 13), Stat(159, 6) } } },
            // Aikido Master, medanon niobe3dualduality
            { 1067, new Buff { Exclusive = true, Toggle = true, Family = FamilyClassBuff, Modifiers = new[] { Stat(1009, 7), Stat(106, 148), Stat(522, 4), Stat(159, 2) } } },
            // Howitzer
            { 1087, new Buff { Exclusive = true, Toggle = true, Family = FamilyClassBuff, Modifiers = new[] { Stat(342, 13), Stat(172, 8), Stat(564, 7), Stat(177, 17) } } },
            // Team Healer, crashover2k proxy_024 (replaced Corpsman in the same packet)
            { 1089, new Buff { Exclusive = true, Toggle = true, Family = FamilyClassBuff, Modifiers = new[] { Stat(174, 3), Stat(127, 7), Stat(564, 7), Stat(520, 10) } } },
            // Corpsman, crashover2k proxy_024
            { 1092, new Buff { Exclusive = true, Toggle = true, Family = FamilyClassBuff, Modifiers = new[] { Stat(564, 7), Stat(127, 10), Stat(174, 6) } } },
            // Hyper-Block
            { 1099, new Buff { Exclusive = true, Family = FamilyHyperDefence, Modifiers = new[] { Stat(1009, 52) } } },
            // Hyper-Deflect, entilsar lastday2 (replaced by Hyper-Block)
            { 1100, new Buff { Exclusive = true, Family = FamilyHyperDefence, Modifiers = new[] { Stat(183, 77) } } },
        };

        /// <summary>A buff the player has on: the record ids sent for it, and a token for its expiry.</summary>
        public class Active
        {
            public UInt16 AbilityId;
            public UInt16[] RecordIds;
            public int Generation;
            public double Started;
            public Buff Definition;
            internal DrainLifetime Drain;
        }

        internal sealed class DrainLifetime
        {
            internal readonly WorldClient Client;
            internal readonly ClientData Data;
            internal readonly PlayerCharacter Player;
            internal readonly uint Character;
            private readonly uint district;
            internal readonly int World,Life;
            internal readonly long Transport;
            internal volatile bool Cancelled;
            internal DrainLifetime(WorldClient client)
            {
                Client=client;Data=client.playerData;Player=client.playerInstance;
                Character=Data.getCharID();district=Data.getDistrictId();World=Data.worldGeneration;Life=Data.lifeGeneration;
                Transport=client.TransportFence?.Capture() ?? -1;
            }
            internal bool ScopeCurrent => ReferenceEquals(Client.playerData,Data) && ReferenceEquals(Client.playerInstance,Player) &&
                Data.getCharID()==Character && Data.getDistrictId()==district && Data.worldGeneration==World && Data.lifeGeneration==Life &&
                Client.TransportFence!=null && Client.TransportFence.IsCurrent(Transport) && Client.Alive && Data.getOnWorld() &&
                !Data.worldChangePending && !Data.waitForRPCShutDown;
            internal bool Current => !Cancelled && ScopeCurrent;
        }

        public static byte[] BuildModifier(UInt16 kind, UInt16 recordId, UInt32 ability, UInt16 stat, Int32 value,
            bool off, float seconds)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80bc");
            pak.AddUint16(kind, 1);
            pak.AddUint16(recordId, 1);
            pak.AddByte(0x00);
            pak.AddUint32(ability, 1);
            pak.AddUint16(stat, 1);
            pak.AddUint32(unchecked((UInt32)value), 1);
            pak.AddByte(off ? (byte)0x01 : (byte)0x00);
            pak.AddFloat(seconds, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary><c>80 bd 05 &lt;ability u16&gt; 00 00 00 00 00 &lt;01 on / 00 off&gt;</c> (AbilityCooldowns.BuildTimer's layout, type 0, 0 s)</summary>
        public static byte[] BuildExclusive(UInt16 abilityId, bool on)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80bd05");
            pak.AddUint16(abilityId, 1);
            pak.AddHexBytes("0000000000");
            pak.AddByte(on ? (byte)0x01 : (byte)0x00);
            return pak.ReturnFinalPacket();
        }

        /// <summary>The records for a buff, on or off, with the given record ids.</summary>
        public static List<byte[]> BuildRecords(UInt16 abilityId, Buff buff, UInt16[] recordIds, bool off)
        {
            List<byte[]> records = new List<byte[]>();
            for (int i = 0; i < buff.Modifiers.Length; i++)
            {
                Modifier m = buff.Modifiers[i];
                records.Add(BuildModifier(m.Kind, recordIds[i], m.Source != 0 ? m.Source : abilityId, m.Stat, m.Value,
                    off, buff.Seconds));
            }
            return records;
        }

        /// <summary>
        /// Own-view MovementScale and DissemblingType (attributes 10 and 36), as retail sent when Hyper-Speed
        /// ended (entilsar lastday2, 21:56:07): <c>02 00 02 80 88 00 00 00 00 80 80 80 02 00 00</c>.
        /// </summary>
        public static byte[] BuildMovementUpdate(float movementScale, UInt16 dissemblingType)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByte(0x02);
            pak.AddByte(0x80);              // group 0
            pak.AddByte(0x88);              // group 1: MovementScale (index 10)
            pak.AddFloat(movementScale, 1);
            pak.AddHexBytes("808080");      // groups 2-4
            pak.AddByte(0x02);              // group 5: DissemblingType (index 36)
            pak.AddUint16(dissemblingType, 1);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Own view for Hyper-Sprint and other movement buffs: MovementScale, Inner Strength, EffectID and
        /// counter, DissemblingType (retail Hyper-Sprint: 02 00 02 80 88 9a 99 19 3e 80 90 &lt;IS&gt; b0 19 31 46 12
        /// &lt;ctr&gt; 02 00 04).
        /// </summary>
        public static byte[] BuildMovementBuffUpdate(Buff buff, UInt16 innerStrength, byte counter, ushort? dissemblingType = null,
            float? movementScale = null)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByte(0x02);
            pak.AddByte(0x80);
            pak.AddByte(0x88);
            pak.AddFloat(movementScale ?? buff.MovementScale, 1);
            pak.AddByte(0x80);
            pak.AddByte(0x90);
            pak.AddUint16(innerStrength, 1);
            pak.AddByte(0xb0);
            pak.AddUint32(buff.EffectId, 1);
            pak.AddByte(counter);
            pak.AddByte(0x02);
            pak.AddUint16(dissemblingType ?? buff.DissemblingType, 1);
            return pak.ReturnFinalPacket();
        }

        public static bool IsBuff(UInt16 abilityId)
        {
            return TryGet(abilityId, out _);
        }

        private static readonly Lazy<Dictionary<UInt16, Buff>> catalogBuffs = new Lazy<Dictionary<UInt16, Buff>>(() =>
        {
            var buffs = new Dictionary<UInt16, Buff>();
            foreach (var entry in AbilityCatalog.All.Values)
            {
                if (entry.Number("Ability/CastDirectObjects", -1) != 1 && !entry.FriendlyTarget || entry.Healing) continue;
                var mods = entry.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2 &&
                    !(HasClassSweep(entry.Id) && m.Text("StatModUpdateFunc") == "NegativeConditionSweepPulse")).ToList();
                if (mods.Count == 0 || mods.Any(m => m.Target == 0 || m.Target == 108 || m.State != 0 ||
                    !m.Has("MinValue") || m.Number("MinValue") != m.Number("MaxValue") || m.Has("ValueScale") ||
                    m.Number("ValueFunction") != 0 || !string.IsNullOrEmpty(m.Text("StatModInitFunc")) ||
                    !string.IsNullOrEmpty(m.Text("StatModUpdateFunc")) || !string.IsNullOrEmpty(m.Text("StatModTermFunc")) ||
                    !string.IsNullOrEmpty(m.Text("StatModHealthChangedFunc")) ||
                    m.Number("DurationType", -1) != 0 && m.Number("DurationType", -1) != 2)) continue;
                var durations = mods.Select(m => m.Number("DurationType") == 2 ? 0 : m.Number("Duration")).Distinct().ToList();
                if (durations.Count != 1) continue;
                int stacking = (int)entry.Number("Ability/StackingType");
                buffs[entry.Id] = new Buff { Seconds = (float)durations[0], Exclusive = durations[0] == 0,
                    Toggle = entry.Number("Ability/IsAToggleAbility") == 1 && (stacking != ClassUpgradeStackingType || IsStartingClassUpgrade(entry.Id)),
                    Family = stacking == ClassUpgradeStackingType ? FamilyClassBuff : stacking == 0 ? 0 : 1000 + stacking,
                    Modifiers = mods.Select(m => Stat(m.Target, (int)m.Number("MinValue"))).ToArray(),
                    MovementScale = (float)(mods.FirstOrDefault(m => m.Target == 115)?.Number("MinValue") ?? 0) / 100 };
            }
            return buffs;
        });

        /// <summary>Captured values take precedence over unscaled stored constants; native scaling remains a parity gap.</summary>
        public static bool TryGet(UInt16 id, out Buff buff)
        {
            id = AbilityCatalog.Canonical(id);
            return PrisonDevices.TryGet(id,out buff) || DistractingBlow.TryGetEffectBuff(id, out buff) || AbilityDesperation.TryGet(id,1,1,0,out buff) || AbilityHealthCallbacks.TryGet(id,50,0,out buff) || AbilityScaledBuffs.TryScriptedTimed(id,50,out buff) || AbilityScaledBuffs.TryToggle(id, 50, out buff) || Retail.TryGetValue(id, out buff) || Scripted.TryGetValue(id, out buff) || catalogBuffs.Value.TryGetValue(id, out buff) || AbilityScaledBuffs.TryGet(id, 50, out buff) || CombatSpecials.TryCasterBuff(id, out buff);
        }

        public static ushort DissemblingBits(WorldClient client)
        {
            ushort bits = 0;
            lock (client.playerData.activeBuffs)
                foreach (Active active in client.playerData.activeBuffs.Values)
                    if (ActiveDefinition(active, out Buff buff)) bits |= buff.DissemblingType;
            return bits;
        }

        private static void PresentDissembling(WorldClient client)
        {
            ushort bits = (ushort)(DissemblingBits(client) | Stealth.BitsOf(client));
            client.playerInstance.DissemblingType.setValue(bits);
            client.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((36, PlayerViewAttributes.U16(bits))), false);
            PvpFlag.ToWatchers(client, PlayerViewAttributes.Build((27, PlayerViewAttributes.U16(bits))));
        }

        private static void PresentMovementScale(WorldClient client, float scale)
        {
            client.playerInstance.MovementScale.setValue(scale);
            PvpFlag.ToWatchers(client, PlayerViewAttributes.Build((PlayerViewAttributes.OtherMovementScale, PlayerViewAttributes.F32(scale))));
        }

        private static byte[] ProjectSprintMovement(PlayerCharacter player, float scale, ushort bits)
        {
            player.MovementScale.setValue(scale);
            player.DissemblingType.setValue(bits);
            return PlayerViewAttributes.Build((PlayerViewAttributes.OtherMovementScale, PlayerViewAttributes.F32(scale)),
                (27, PlayerViewAttributes.U16(bits)));
        }

        private static bool IsCatalogSpeedBuff(ushort id) => id == MovementAccelerator || id == 291 || id == 323 || id == 423;

        internal static float MovementScaleFor(WorldClient client, Buff buff)
        {
            var stealth = Stealth.Current(client);
            float stanceScale = stealth?.Def.Ability == Stealth.EscapeAndEvasionAbility
                ? stealth.Mods.Where(modifier => modifier.Stat == SneakMovementStat).Sum(modifier => modifier.Value) / 100f
                : 0f;
            if (buff == null) return stanceScale;
            ushort stat = MovementStatFor(stealth);
            if (stat == RunMovementStat) return buff.MovementScale;
            Modifier modifier = Array.Find(buff.Modifiers, entry => entry.Stat == stat);
            return stanceScale + (modifier.Stat == stat ? modifier.Value / 100f : buff.MovementScale);
        }

        private static ushort MovementStatFor(Stealth.State stealth) => stealth?.Def.Kind switch
        {
            Stealth.Kind.Sneak => SneakMovementStat,
            Stealth.Kind.Invisibility => InvisibleMovementStat,
            Stealth.Kind.Imparted => InvisibleMovementStat,
            _ => RunMovementStat
        };

        private static float RemainingMovementScale(WorldClient client)
        {
            Buff buff;
            lock (client.playerData.activeBuffs)
                buff = client.playerData.activeBuffs.Values.Where(active => active.Definition?.MovementScale > 0)
                    .OrderByDescending(active => active.Generation).FirstOrDefault()?.Definition;
            return MovementScaleFor(client, buff);
        }

        private const ushort CrashAbility = 396, BottleneckField = 402;
        private const ushort RunMovementStat = 115, SneakMovementStat = 877, InvisibleMovementStat = 878;

        private static bool UsesStanceMovementChannel(ushort source) => source == CrashAbility || source == BottleneckField;

        public static bool ProjectsTimedMovementDebuff(ushort source) => source == 86 || source == 130 || UsesStanceMovementChannel(source);

        private static bool ProjectsSignedMovementDebuff(TimedEffects.Active modifier) =>
            ProjectsTimedMovementDebuff(modifier.Parent) &&
            (modifier.Target == RunMovementStat || UsesStanceMovementChannel(modifier.Parent) &&
                (modifier.Target == SneakMovementStat || modifier.Target == InvisibleMovementStat)) &&
            modifier.Proc == TempMods.Proc.Stat && modifier.Value < 0;

        private static bool OverridesMovementScale(TimedEffects.Active modifier) =>
            modifier.Proc == TempMods.Proc.State && modifier.Target == TempMods.RootedAbility &&
            (modifier.Parent == CombatSpecials.SuppressionFire ||
             (modifier.Parent == 176 || modifier.Parent == 254) && modifier.Break == TempMods.BreakRule.Chance);

        internal static float WithTimedMovementDebuffs(WorldClient client, float movementScale)
        {
            var active = TimedEffects.ForPlayer(client).Effects.Snapshot();
            // Vector26 shows the root override and restore for Immobilizing Shot.
            // Warboy ProxyM2_m2_packets_5 28162 and 30146 show it before and during a Suppression Fire renewal.
            if (active.Any(OverridesMovementScale)) return -1f;
            ushort movementStat = MovementStatFor(Stealth.Current(client));
            // Crash and Bottleneck each specify three movement channels. Apply only the current one.
            int points = active.Where(modifier => ProjectsSignedMovementDebuff(modifier) &&
                (!UsesStanceMovementChannel(modifier.Parent) || modifier.Target == movementStat)).Sum(modifier => modifier.Value);
            // Retail Crippling Shot 450 combines signed stat 115 points per hundred with the existing scale.
            return points == 0 ? movementScale : Math.Max(-1f, (float)((decimal)movementScale + points / 100m));
        }

        internal static void PresentTimedMovementChange(WorldClient client, IEnumerable<TimedEffects.Active> changed)
        {
            if (!changed.Any(modifier => ProjectsSignedMovementDebuff(modifier) || OverridesMovementScale(modifier))) return;
            PresentCurrentMovementScale(client);
        }

        internal static void PresentCurrentMovementScale(WorldClient client)
        {
            float scale = WithTimedMovementDebuffs(client, RemainingMovementScale(client));
            float current = client.playerInstance.MovementScale.isActive() ?
                BitConverter.ToSingle(client.playerInstance.MovementScale.getValue(), 0) : 0f;
            if (current == scale) return;
            client.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfMovementScale, PlayerViewAttributes.F32(scale))), false);
            PresentMovementScale(client, scale);
        }

        private static byte[] QueueSprintReset(WorldClient client, ClientData data, PlayerCharacter player)
        {
            float scale = WithTimedMovementDebuffs(client, RemainingMovementScale(client));
            ushort bits = (ushort)(DissemblingBits(client) | Stealth.BitsOf(client));
            client.messageQueue.addObjectMessage(BuildMovementUpdate(scale, bits), false);
            return ProjectSprintMovement(player, scale, bits);
        }

        public static int Total(WorldClient client, UInt16 stat)
        {
            int total = 0;
            lock (client.playerData.activeBuffs)
                foreach (Active active in client.playerData.activeBuffs.Values)
                    if (ActiveDefinition(active, out Buff buff))
                        foreach (Modifier m in buff.Modifiers)
                            if (m.Kind == KindStat && m.Stat == stat) total += m.Value;
            return total + TimedEffects.ForPlayer(client).Effects.Total(stat);
        }

        private static bool ActiveDefinition(Active active, out Buff buff)
        {
            buff = active.Definition;
            return buff != null || TryGet(active.AbilityId, out buff);
        }

        private static int VitalValue(Buff buff, UInt16 stat) => buff.Modifiers.Where(m => m.Kind == KindStat && m.Stat == stat).Sum(m => m.Value);

        public static int VitalTotal(WorldClient client, UInt16 stat)
        {
            int timed = TimedEffects.ForPlayer(client).Effects.Total(stat); // hostile max-vital tempmods (Destroy Health / Resolve)
            lock (client.playerData.activeBuffs)
                return timed + client.playerData.activeBuffs.Values.Sum(active => ActiveDefinition(active, out Buff buff) ? VitalValue(buff, stat) : 0);
        }

        public static UInt16 UnbuffedMaximum(WorldClient client, UInt16 stat)
        {
            lock (client.playerData.activeBuffs)
            {
                int value = NumericalUtils.ByteArrayToUint16((stat == 106 ? client.playerInstance.MaxHealth : client.playerInstance.InnerStrengthMax).getValue(), 1);
                return (UInt16)Math.Clamp(value - (stat == 106 ? client.playerData.buffHealthMaximumDelta : client.playerData.buffISMaximumDelta), 1, UInt16.MaxValue);
            }
        }

        public static void RefreshVitals(WorldClient client, bool present = true)
        {
            var changed = new List<Attribute>();
            UInt16 previousHealth = NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1);
            lock (client.playerData.activeBuffs)
            foreach (var (stat, maximum, current) in new[]
            {
                ((UInt16)106, client.playerInstance.MaxHealth, client.playerInstance.Health),
                ((UInt16)109, client.playerInstance.InnerStrengthMax, client.playerInstance.InnerStrengthAvailable)
            })
            {
                int baseline = UnbuffedMaximum(client, stat);
                ushort bonus = stat == 106 ? (ushort)1057 : (ushort)1056;
                UInt16 max = (UInt16)Math.Clamp(baseline + AbilityStats.PercentDelta(baseline, AbilityStats.Player(client, bonus)) +
                    AbilityStats.Player(client, stat), 1, UInt16.MaxValue);
                if (stat == 106) client.playerData.buffHealthMaximumDelta = max - baseline;
                else client.playerData.buffISMaximumDelta = max - baseline;
                UInt16 previousMax = NumericalUtils.ByteArrayToUint16(maximum.getValue(), 1);
                if (previousMax != max)
                {
                    maximum.setValue(max);
                    changed.Add(maximum);
                    if (present)
                        Output.WriteDebugLog($"[VITALS] char={client.playerData.getCharID()} {(stat == 106 ? "maxHP" : "maxIS")} {previousMax} -> {max}");
                }
                if (NumericalUtils.ByteArrayToUint16(current.getValue(), 1) > max) { current.setValue(max); changed.Add(current); }
            }
            if (changed.Count > 0)
            {
                UInt16 health = NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1);
                if (health != previousHealth) TimedEffects.HealthChanged(TimedEffects.ForPlayer(client), previousHealth - health,
                    health < NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1));
                if (present)
                {
                    client.messageQueue.addObjectMessage(SocialPackets.SelfViewUpdate(client.playerInstance.GetSelfUpdateAttributes(changed, false)), false);
                    byte[] other = client.playerInstance.GetUpdateAttributes(changed);
                    if (other[0] > 0) PvpFlag.ToWatchers(client, SocialPackets.OtherViewUpdate(other));
                }
            }
        }

        public static bool IsActive(WorldClient client, UInt16 abilityId)
        {
            lock (client.playerData.activeBuffs)
            {
                return client.playerData.activeBuffs.ContainsKey(abilityId);
            }
        }

        /// <summary>A toggle buff that is on: using it again switches it off.</summary>
        public static bool IsToggledOn(WorldClient client, UInt16 abilityId)
        {
            return TryGet(abilityId, out Buff buff) && buff.Toggle && IsActive(client, abilityId);
        }

        /// <summary>Whether the ability's cooldown starts when it is used (false for toggles: when they end).</summary>
        public static bool CooldownStartsOnUse(UInt16 abilityId)
        {
            // Stock Deflect Virus378 starts its own10s timer with the on record (proxy_024).
            if (abilityId == Virii.DeflectVirus1Ability || IsStartingClassUpgrade(abilityId)) return true;
            if (AbilityCatalog.Canonical(abilityId) == Stealth.MasterShadowAbility) return false;
            return !AbilityAuras.Friendly(abilityId) && !(TryGet(abilityId, out Buff buff) && buff.Toggle);
        }

        /// <summary>Own-view EvadeShieldHealth (attribute 30): <c>02 00 02 80 80 80 80 04 &lt;value&gt;</c> (retail ff on activation, 00 off).</summary>
        public static byte[] BuildEvadeShieldUpdate(byte health)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddHexBytes("02808080800" + "4");
            pak.AddByte(health);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Puts the buff on (replacing the same buff, and buffs of its family) and queues its records; a timed
        /// buff comes off by itself when its time is up. Returns false for abilities that are not buffs.
        /// The caller flushes the queue.
        /// </summary>
        public static bool TacticalConflict(WorldClient client, ushort ability) =>
            ability == 9 && IsActive(client, 15) || ability == 15 && IsActive(client, 9);

        public static bool Apply(WorldClient client, UInt16 abilityId, WorldClient caster = null)
        {
            // Capture before ANY activation/upgrade lock: reset owns the transport gate first.
            var drain=new DrainLifetime(client);
            var transport = client.TransportFence;
            if (abilityId != Virii.DeflectVirus1Child || transport == null) return ApplyCaptured();

            // The child publishes a world-bound reuse record. Take transport admission before the upgrade lock.
            bool applied = false;
            return transport.TryRun(drain.Transport, () => applied = ApplyCaptured()) && applied;

            bool ApplyCaptured()
            {
                lock (drain.Data.upgradeSync)
                {
                    if (!ReferenceEquals(client.playerData, drain.Data)) return false;
                    return ApplyCore(client, abilityId, caster, drain);
                }
            }
        }

        private static bool ApplyCore(WorldClient client, UInt16 abilityId, WorldClient caster,DrainLifetime drain)
        {
            if (TacticalConflict(client, abilityId)) return false;
            if (!AbilityScaledBuffs.Resolve(caster ?? client, abilityId, out Buff buff))
            {
                return false;
            }

            // The retained self-pulse shows an exclusive child record, separate from its five-second stats.
            bool ownDeflectChild = abilityId == Virii.DeflectVirus1Child && ReferenceEquals(caster, client) &&
                AbilityAuras.IsOn(client, Virii.DeflectVirus1Ability);
            if (ownDeflectChild) buff.Exclusive = true;

            if (!AbilityBuffBuffer.CanApply(client, abilityId, buff))
            {
                Output.WriteDebugLog("[BUFF] " + abilityId + " refused: upgrade buffer full");
                return false;
            }
            ClientData data = client.playerData;
            List<UInt16> replaced = new List<UInt16>();
            lock (data.activeBuffs)
            {
                foreach (UInt16 other in data.activeBuffs.Keys)
                {
                    if (other == abilityId ||
                        (buff.Family != 0 && TryGet(other, out Buff o) && o.Family == buff.Family) ||
                        (IsCatalogSpeedBuff(abilityId) || IsCatalogSpeedBuff(other)) &&
                        AbilityCatalog.Find(other)?.Number("Ability/StackingType") == AbilityCatalog.Find(abilityId)?.Number("Ability/StackingType"))
                    {
                        replaced.Add(other);
                    }
                }
            }
            foreach (UInt16 other in replaced)
            {
                // A movement buff put straight back on keeps its speed: don't send the speed reset in between.
                if (Remove(client, other, false, other == abilityId || buff.MovementScale > 0, keepVitals: true) && other != abilityId)
                    Output.WriteDebugLog($"[BUFF] {other} off (replaced by {abilityId})");
            }

            RemoveTimedFamilyConflicts(client,abilityId);
            Active active;
            lock (data.activeBuffs)
            {
                UInt16[] ids = new UInt16[buff.Modifiers.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = NextRecordId(data);
                }
                active = new Active { AbilityId = abilityId, RecordIds = ids, Generation = ++data.buffGeneration, Started=TimeUtils.GetSimTimeSeconds(), Definition = buff };
                if(buff.DrainAmount>0 && buff.DrainSeconds>0)active.Drain=drain;
                data.activeBuffs[abilityId] = active;
            }

            if (buff.Exclusive)
            {
                client.messageQueue.addRpcMessage(BuildExclusive(abilityId, true));
            }
            if (ownDeflectChild && drain.Current)
            {
                double now = TimeUtils.GetSimTimeSeconds();
                if (!AbilityCooldowns.IsTimerRunning(drain.Data, abilityId, now))
                    AbilityCooldowns.StartCaptured(client, abilityId, now, drain.Data, drain.Player,
                        drain.World, drain.Life, drain.Transport);
            }
            if (buff.EvadeShield)
            {
                EvasionShield.Applied(client,active.Generation);
            }
            foreach (byte[] record in buff.EvadeShield && !EvasionShield.HasState(client) ? new List<byte[]>() : BuildRecords(abilityId, buff, active.RecordIds, false))
            {
                client.messageQueue.addRpcMessage(record);
            }
            RefreshVitals(client);
            float movementScale = buff.MovementScale > 0 ? WithTimedMovementDebuffs(client, MovementScaleFor(client, buff)) : 0f;
            if (abilityId == HyperSprint)
            {
                ushort bits = (ushort)(DissemblingBits(client) | Stealth.BitsOf(client));
                byte counter = (byte)data.assignSpawnIdCounter();
                client.messageQueue.addObjectMessage(BuildMovementBuffUpdate(buff,
                    AbilityHandler.CurrentInnerStrength(client), counter, bits, movementScale), false);
                PvpFlag.ToWatchers(client, ProjectSprintMovement(client.playerInstance, movementScale, bits));
            }
            else if (buff.DissemblingType != 0) PresentDissembling(client);
            if (abilityId == HyperSpeed || IsCatalogSpeedBuff(abilityId))
                PresentMovementScale(client, movementScale);
            if (IsCatalogSpeedBuff(abilityId))
                client.messageQueue.addObjectMessage(BuildMovementUpdate(movementScale,
                    (ushort)(DissemblingBits(client) | Stealth.BitsOf(client))), false);
            Output.WriteDebugLog("[BUFF] " + abilityId + " on" + (buff.Seconds > 0 ? " for " + buff.Seconds + " s" : ""));

            if (HasClassSweep(abilityId))
            {
                AbilityHostile.ApplySweep(caster ?? client, client, abilityId);
                if (!IsActive(client, abilityId))
                    TimedEffects.RemoveParent(TimedEffects.ForPlayer(client), abilityId);
            }
            if (buff.DrainAmount > 0 && buff.DrainSeconds > 0) ScheduleDrain(client, active);
            if (buff.PeriodicFx != 0 && buff.PeriodicFxSeconds > 0) SchedulePeriodicFx(client, active);
            AwakenedTacticalToggles.Schedule(active, drain);
            if (buff.Seconds > 0)
            {
                // Comes off on the timed-effects server tick (TimedEffects.RunDue)
                TimedEffects.ForPlayer(client).TrackBuff(abilityId, active.Generation,
                    TimeUtils.GetSimTimeSeconds() + buff.Seconds);
            }
            return true;
        }

        internal static void RemoveTimedFamilyConflicts(WorldClient client,ushort id)
        {
            double stacking=AbilityCatalog.Find(id)?.Number("Ability/StackingType")??0;
            if(stacking<=0) return;
            var subject=TimedEffects.ForPlayer(client);
            foreach(var other in subject.Effects.Snapshot().Select(a=>a.Parent).Distinct().Where(other=>other!=id &&
                AbilityCatalog.Find(other)?.Number("Ability/StackingType")==stacking).ToList()) TimedEffects.RemoveParent(subject,other);
        }

        // Confirmed quit ends existing drain capabilities even while the UDP endpoint remains reserved.
        internal static void CancelDrains(WorldClient client)
        {
            var data=client?.playerData;if(data==null)return;
            lock(data.activeBuffs)foreach(var active in data.activeBuffs.Values)
                if(active.Drain!=null)active.Drain.Cancelled=true;
        }

        private static void ScheduleDrain(WorldClient client, Active active)
        {
            if(active.Drain==null || !active.Drain.Current)return;
            DelayedActions.AfterFor(client,active.Definition.DrainSeconds,()=>TickActiveDrain(client,active));
        }

        private static void SchedulePeriodicFx(WorldClient client, Active active)
        {
            if (active.Drain?.Current != true) return;
            DelayedActions.AfterFor(client, active.Definition.PeriodicFxSeconds, () =>
            {
                if (TickPeriodicFx(client, active)) SchedulePeriodicFx(client, active);
            });
        }

        internal static bool TickPeriodicFx(WorldClient client, Active active)
        {
            var lease = active.Drain;
            uint effect = active.Definition.PeriodicFx;
            if (effect == 0 || lease == null || !ReferenceEquals(lease.Client, client) ||
                !client.IsCurrentEndpointOwner || !lease.Current) return false;

            bool published = false;
            byte counter = 0;
            client.TransportFence.TryRun(lease.Transport, () =>
            {
                lock (lease.Data.activeBuffs)
                {
                    if (!lease.Current || lease.Data.isDead || lease.Data.duelDeath ||
                        !lease.Data.activeBuffs.TryGetValue(active.AbilityId, out var current) ||
                        !ReferenceEquals(current, active)) return;
                    counter = ServerPackets.QueuePlayerAbilityEffect(client, effect);
                    published = true;
                }
            });
            if (!published) return false;
            client.FlushQueue();
            if (lease.Current)
                PvpFlag.ToWatchers(client, ServerPackets.BuildPlayerAbilityEffect(effect, counter, false));
            return true;
        }

        public static void TickDrain(WorldClient client, ushort id, int generation)
        {
            var data=client.playerData;Active active;
            lock(data.activeBuffs)
                if(!data.activeBuffs.TryGetValue(id,out active) || active.Generation!=generation)return;
            TickActiveDrain(client,active);
        }

        // The ordinary path only removes records. Sprint also has an explicit owner and observer reset below.
        // Health-change listeners end by exact membership removal without invoking the listener.
        internal static bool SupportsDrainTermination(ushort abilityId, Buff buff) => buff!=null && buff.DrainAmount>0 && buff.DrainSeconds>0 &&
            buff.Seconds==0 && buff.Exclusive && buff.Toggle && buff.RefundIS==0 && buff.TermHeal==0 &&
            buff.TermSweepChance==0 && !buff.EvadeShield && buff.Modifiers!=null &&
            (abilityId == HyperSprint
                ? ReferenceEquals(buff, Retail[HyperSprint])
                : buff.MovementScale==0 && buff.DissemblingType==0 && buff.EffectId==0 &&
                    buff.Modifiers.All(m=>(m.Kind==KindStat || m.Kind==TempMods.KindFx) &&
                        (m.Kind!=KindStat || m.Stat!=106 && m.Stat!=109 && m.Stat!=1056 && m.Stat!=1057)));

        internal static void TickActiveDrain(WorldClient client,Active active,Action beforeClaim=null,Action beforePublication=null)
        {
            var lease=active?.Drain;
            if(lease==null || !ReferenceEquals(lease.Client,client))return;
            if(!client.IsCurrentEndpointOwner || !lease.Current){lease.Cancelled=true;return;}
            var data=lease.Data;bool claimed=false,shortfall=false;int missing=0;ushort remaining=0;
            beforeClaim?.Invoke(); // Invocation-local scheduler test seam, outside every lock.
            if(!client.TransportFence.TryRun(lease.Transport,()=>
            {
                lock(data.activeBuffs)
                {
                    if(!lease.Current || !data.activeBuffs.TryGetValue(active.AbilityId,out var current) || !ReferenceEquals(current,active))return;
                    var buff=active.Definition;if(!SupportsDrainTermination(active.AbilityId,buff))return;
                    ushort available=NumericalUtils.ByteArrayToUint16(lease.Player.InnerStrengthAvailable.getValue(),1);
                    shortfall=data.isDead || data.duelDeath || available<buff.DrainAmount;
                    if(shortfall)
                    {
                        missing=Math.Max(0,buff.DrainAmount-available);
                        data.activeBuffs.Remove(active.AbilityId);lease.Cancelled=true;
                    }
                    else
                    {
                        remaining=(ushort)Math.Max(0,available-buff.DrainAmount);
                        lease.Player.InnerStrengthAvailable.setValue(remaining);
                    }
                    claimed=true;
                }
            }) || !claimed){lease.Cancelled=true;return;}
            beforePublication?.Invoke();
            bool published=false;
            byte[] movementReset = null;
            if(!client.TransportFence.TryRun(lease.Transport,()=>
            {
                if(!lease.ScopeCurrent)return;
                if(shortfall)
                {
                    lock (data.activeBuffs)
                    {
                        if (data.activeBuffs.ContainsKey(active.AbilityId)) return; // Never terminate a later activation.
                        if (active.AbilityId == HyperSprint)
                            movementReset = QueueSprintReset(client, data, lease.Player);
                    }
                    if(missing>0)
                    {
                        string name=StringUtils.charBytesToString_NZ(lease.Player.CharacterName.getValue());
                        client.messageQueue.AddWorldBoundRpc(AbilityMessages.AboutAbility(AbilityAuras.DrainDeactivated,true,active.AbilityId,name,name,missing),lease.World,lease.Transport);
                    }
                    foreach(var record in BuildRecords(active.AbilityId,active.Definition,active.RecordIds,true))
                        client.messageQueue.AddWorldBoundRpc(record,lease.World,lease.Transport);
                    client.messageQueue.AddWorldBoundRpc(BuildExclusive(active.AbilityId,false),lease.World,lease.Transport);
                    AbilityCooldowns.StartCaptured(client,active.AbilityId,TimeUtils.GetSimTimeSeconds(),data,lease.Player,lease.World,lease.Life,lease.Transport);
                    published=true;return;
                }
                if(!lease.Current)return;
                client.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfInnerStrength,PlayerViewAttributes.U16(remaining))),false);
                published=true;
            }))lease.Cancelled=true;
            if(!published){lease.Cancelled=true;return;}
            client.FlushQueue(); // Never flush/broadcast/term callbacks inside the transport or activation gates.
            if (movementReset != null && lease.ScopeCurrent && !IsActive(client, HyperSprint))
                PvpFlag.ToWatchers(client, movementReset);
            if(shortfall)Output.WriteDebugLog("[BUFF] "+active.AbilityId+" off");
            else
            {
                Output.WriteDebugLog($"[BUFF-DRAIN] char{data.getCharID()} ability{active.AbilityId} generation{active.Generation} amount{active.Definition.DrainAmount} remaining{remaining}");
                if(lease.Current)ScheduleDrain(client,active);
            }
        }

        /// <summary>
        /// Takes the buff off: queues its records with the off byte, the exclusive switch-off, and for movement
        /// buffs the speed reset. With a generation, only that application of the buff is removed (an expiry
        /// that a later use replaced does nothing). Returns whether the buff was on.
        /// </summary>
        public static bool Remove(WorldClient client, UInt16 abilityId, bool log = true, bool keepMovement = false,
            int generation = -1, bool keepVitals = false)
        {
            if (!TryGet(abilityId, out Buff buff))
            {
                return false;
            }

            ClientData data = client.playerData;
            Active active;
            bool publishUnderLock = abilityId == Virii.DeflectVirus1Child;
            lock (data.activeBuffs)
            {
                if (!data.activeBuffs.TryGetValue(abilityId, out active) ||
                    (generation >= 0 && active.Generation != generation))
                {
                    return false;
                }
                buff = active.Definition ?? buff;
                if(active.Drain!=null)active.Drain.Cancelled=true;
                data.activeBuffs.Remove(abilityId);
                // A five-second expiry and aura refresh can run on different scheduler threads.
                // Queue the old child's removal before a replacement can claim the now-empty slot.
                if (publishUnderLock) QueueRemovalRecords(client, active, buff);
            }
            if (HasClassSweep(abilityId))
                TimedEffects.RemoveParent(TimedEffects.ForPlayer(client), abilityId);
            if (abilityId == 9 || abilityId == 15) client.playerData.combatTacticType = CombatTacticType.Normal;
            TimedEffects.ForPlayer(client).UntrackBuff(abilityId, active.Generation);
            PrisonDevices.Terminated(client, active);
            if (buff.DissemblingType != 0 && !(abilityId == HyperSprint && !keepMovement)) PresentDissembling(client);
            if (!keepVitals) RefreshVitals(client);
            if (buff.RefundIS > 0)
            {
                ushort before = AbilityHandler.CurrentInnerStrength(client);
                ushort inner = (ushort)Math.Min(AbilityHandler.MaxInnerStrength(client), before + buff.RefundIS);
                client.playerInstance.InnerStrengthAvailable.setValue(inner);
                new ServerPackets().sendISCurrent(client,inner);
                Output.WriteDebugLog($"[VITALS] char={data.getCharID()} RefundIS source={abilityId} IS={before}->{inner}");
            }
            // Do not allow a term heal during death cleanup to leave positive HP on a dead character.
            if(buff.TermHeal>0 && !data.isDead && !data.duelDeath) AbilityHandler.HealSelf(client,(ushort)buff.TermHeal);
            if(buff.TermSweepChance>0 && !data.isDead && !data.duelDeath)
                AbilityHostile.Pulse(TimedEffects.ForPlayer(client),new TimedEffects.Active {Parent=abilityId,Value=buff.TermSweepChance},AbilityHealthCallbacks.TermRandom());

            if (!publishUnderLock) QueueRemovalRecords(client, active, buff);
            if (buff.MovementScale > 0 && !keepMovement)
            {
                if (abilityId == HyperSprint)
                    PvpFlag.ToWatchers(client, QueueSprintReset(client, data, client.playerInstance));
                else
                {
                    float scale = WithTimedMovementDebuffs(client, RemainingMovementScale(client));
                    if (abilityId == HyperSpeed || IsCatalogSpeedBuff(abilityId))
                        PresentMovementScale(client, scale);
                    client.messageQueue.addObjectMessage(BuildMovementUpdate(scale, (ushort)(DissemblingBits(client) | Stealth.BitsOf(client))), false);
                }
            }
            if (buff.EvadeShield)
            {
                EvasionShield.Removed(client);
            }
            // Class upgrade toggles already began their own reuse on activation.
            if (buff.Toggle && !IsStartingClassUpgrade(abilityId))
            {
                AbilityCooldowns.Start(client, abilityId, TimeUtils.GetSimTimeSeconds());
            }
            if (log)
            {
                Output.WriteDebugLog("[BUFF] " + abilityId + " off");
            }
            return true;
        }

        private static void QueueRemovalRecords(WorldClient client, Active active, Buff buff)
        {
            foreach (byte[] record in BuildRecords(active.AbilityId, buff, active.RecordIds, true))
                client.messageQueue.addRpcMessage(record);
            if (buff.Exclusive)
                client.messageQueue.addRpcMessage(BuildExclusive(active.AbilityId, false));
        }

        /// <summary>
        /// Takes every buff off (death), and every tempmod others put on the player (retail took them all off at death:
        /// morpheuzzz_001 4276, the four viruses' records off when Morpheuzzz died). The caller flushes the queue.
        /// </summary>
        public static void RemoveAll(WorldClient client)
        {
            EmoteHandler.ClearSitting(client);
            List<UInt16> ids;
            lock (client.playerData.activeBuffs)
            {
                ids = new List<UInt16>(client.playerData.activeBuffs.Keys);
            }
            foreach (UInt16 id in ids)
            {
                Remove(client, id);
            }
            TimedEffects.Clear(TimedEffects.ForPlayer(client));
        }

        /// <summary>The next record id for the player (buffs, tactics and tempmods share them).</summary>
        public static UInt16 NextRecordId(ClientData data)
        {
            lock (data.activeBuffs)
            {
                UInt16 id = data.nextBuffRecordId++;
                if (data.nextBuffRecordId == 0)
                {
                    data.nextBuffRecordId = FirstRecordId;
                }
                return id;
            }
        }
    }
}
