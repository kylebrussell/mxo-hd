using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Ability cooldowns. Retail started one with <c>80 bd 01 &lt;ability u16&gt; &lt;type&gt; &lt;seconds f32&gt; 01</c>
    /// and ended it with the same record and 00 (Repair RSI 1.0 on self: <c>80 bd 01 4d 00 04 00 00 80 40 01</c>).
    /// Type 04 is the ability's own timer. Type 00 is a shared group timer (aaMeleeMove1, aaVirusDamage1, ...),
    /// sent in the same packet, which greys out every ability of the group. The durations below are the most
    /// common ones in the retail captures under research/packet-dumps.
    /// </summary>
    public static class AbilityCooldowns
    {
        public const byte OwnTimer = 0x04;
        public const byte GroupTimer = 0x00;

        public struct Cooldown
        {
            public float Seconds;
            public UInt16 Group;
            public float GroupSeconds;

            public Cooldown(float seconds, UInt16 group = 0, float groupSeconds = 0)
            {
                Seconds = seconds;
                Group = group;
                GroupSeconds = groupSeconds;
            }
        }

        // Shared groups seen in retail
        public const UInt16 VirusDamage1 = 682, VirusEnhancedDamage = 683, LogicCannon = 684;
        public const UInt16 Heal1 = 685, Heal2 = 686, MeleeMove1 = 687, MeleeMove2 = 688;
        /// <summary>aaHealPill: 10 s (abilities_mobiuscode; Mobius Code starts it too).</summary>
        public const UInt16 HealPill = 711;
        /// <summary>aaAntibioticPill: 8 s, started with the item's own timer when Antibiotic 5.0 was used.</summary>
        public const UInt16 AntibioticPill = 740;

        /// <summary>Ability id (abilityIDs.csv) to its retail cooldown.</summary>
        public static readonly Dictionary<UInt16, Cooldown> Retail = new Dictionary<UInt16, Cooldown>
        {
            { 6, new Cooldown(60) },            // Consistent Technique
            { 55, new Cooldown(43) },           // Patcher
            // Lower Logic Blasts / Logic Bomb 1 were not captured; the client tooltip gives Logic Blast 1.0
            // "Re-use Timer: 4secs", so they follow Logic Blast 3.0 / Logic Bomb 2 below.
            { 57, new Cooldown(4, VirusDamage1, 4) },  // Logic Blast 1.0
            { 58, new Cooldown(4, VirusDamage1, 4) },  // Logic Blast 2.0
            { 59, new Cooldown(4, VirusDamage1, 4) },  // Logic Blast 3 (old id)
            { 60, new Cooldown(2, VirusDamage1, 2) },  // Logic Bomb 1
            { 72, new Cooldown(13) },           // Resist Combat
            { 77, new Cooldown(4, Heal2, 4) },  // Repair RSI 1.0
            { 97, new Cooldown(58) },           // UI Lag 1.0
            { 135, new Cooldown(90, MeleeMove1, 5) }, // Martial Arts
            { 184, new Cooldown(5) },           // Hyper-Sprint
            { 197, new Cooldown(30) },          // Head Butt
            { 198, new Cooldown(20) },          // Cheap Shot
            { 217, new Cooldown(30) },          // Evade Combat
            { 260, new Cooldown(10, MeleeMove2, 5) }, // Misdirect Punch
            { 261, new Cooldown(10, MeleeMove1, 5) }, // Machinegun Fist Combo
            { 262, new Cooldown(10, MeleeMove1, 5) }, // Piston Kicks
            { 267, new Cooldown(10, MeleeMove1, 5) }, // Sky High Sidekick
            { 271, new Cooldown(28) },          // Iron Body
            { 272, new Cooldown(180) },         // Serene Calm
            { 314, new Cooldown(90) },          // Upgrade Attacks
            { 327, new Cooldown(90) },          // Overclock
            { 329, new Cooldown(4, VirusDamage1, 4) },        // Logic Blast 3.0
            { 330, new Cooldown(4, VirusEnhancedDamage, 4) }, // Logic Barrage 4.0
            { 331, new Cooldown(15, LogicCannon, 15) },       // Logic Cannon
            { 333, new Cooldown(30) },          // Stun 2.0
            { 336, new Cooldown(2, VirusDamage1, 2) },        // Logic Bomb 2
            { 337, new Cooldown(2, VirusEnhancedDamage, 2) }, // Logic Bomb 3
            { 344, new Cooldown(298) },         // Combat Hacking
            { 345, new Cooldown(180) },         // Range Booster
            { 357, new Cooldown(2, VirusDamage1, 2) },        // Logic Bomb 4
            { 359, new Cooldown(4, VirusEnhancedDamage, 4) }, // Code Nuke
            { 360, new Cooldown(58) },          // Insidious Code
            { 371, new Cooldown(4, Heal1, 4) }, // Group Repairs 3
            { 372, new Cooldown(4, Heal2, 4) }, // Group Repairs 4
            { 378, new Cooldown(10) },          // Deflect Virus 1
            { 383, new Cooldown(300) },         // Force Enhancer
            { 396, new Cooldown(15) },          // Crash
            { 404, new Cooldown(30) },          // Despoiler
            { 428, new Cooldown(10, MeleeMove2, 5) }, // Suicidal Butterfly
            { 429, new Cooldown(10, MeleeMove2, 5) }, // Extreme Falling Kick
            { 430, new Cooldown(10, MeleeMove1, 5) }, // Wooden Dummy Drill
            { 466, new Cooldown(12) },          // Plague Zone 2
            { 485, new Cooldown(15) },          // Combat Aura 2
            { 861, new Cooldown(60) },          // Balance
            { 868, new Cooldown(15) },          // Purity
            { 870, new Cooldown(10, MeleeMove2, 5) }, // Spinning Flower
            { 967, new Cooldown(600, HealPill, 10) }, // Mobius Code: 80 bd 01 c7 03 04 .. 600 s + 80 bd 01 c7 02 00 .. 10 s
            { 992, new Cooldown(600) },         // Corrupt Code 2
            { 1063, new Cooldown(30) },         // Kung Fu Master buff
            { 1064, new Cooldown(30) },         // Kung Fu Grand Master buff
            { 1067, new Cooldown(30) },         // Aikido Master buff
            { 1084, new Cooldown(30) },         // Ballista buff
            { 1087, new Cooldown(30) },         // Howitzer buff
            { 1088, new Cooldown(90) },         // Destroyer debuff
            { 1089, new Cooldown(30) },         // Team Healer buff
            { 1092, new Cooldown(30) },         // Corpsman buff
            { 1102, new Cooldown(30) },         // Determinate
            { 1237, new Cooldown(90) },         // Zombie Virus 3 Bite
        };

        /// <summary>80 ba code retail answered a use with while the ability's timer ran (a 80 bd lock).</summary>
        public const byte DeniedTimerRunning = 0x1f;

        /// <summary>
        /// 80 ba code for a use while another cast runs. A guess at the meaning: retail answered quick repeats of an
        /// ability with it (06 for ability 0x1ae in the Rajko "Zombies" log, 0x366 in Warboy's m2 log), and it is the commonest code.
        /// </summary>
        public const byte DeniedBusy = 0x06;

        /// <summary>True while a cast started with <see cref="StartCast"/> runs: the cooldown only starts when it ends.</summary>
        public static bool IsCasting(ClientData data, double now)
        {
            return now < data.castEndsAt;
        }

        public static void StartCast(ClientData data, double now, float castSeconds)
        {
            data.castEndsAt = now + castSeconds;
        }

        /// <summary>
        /// <c>80 ba &lt;ability u16&gt; 00 00 6b &lt;code&gt;</c> and eleven zero bytes: retail's answer to a use it refused,
        /// instead of the 80 ac cast echo (e.g. <c>80 ba 49 01 00 00 6b 1f 00 ...</c> while 80 bd held ability 0x149).
        /// </summary>
        public static byte[] BuildDenied(UInt16 abilityId, byte code)
        {
            byte[] record = new byte[19];
            record[0] = 0x80;
            record[1] = 0xba;
            record[2] = (byte)abilityId;
            record[3] = (byte)(abilityId >> 8);
            record[6] = 0x6b;
            record[7] = code;
            return record;
        }

        /// <summary><c>80 bd 01 &lt;id u16&gt; &lt;type&gt; &lt;seconds f32&gt; &lt;01 start / 00 end&gt;</c></summary>
        public static byte[] BuildTimer(UInt16 id, byte type, float seconds, bool start)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("80bd01");
            pak.AddUint16(id, 1);
            pak.AddByte(type);
            pak.AddFloat(seconds, 1);
            pak.AddByte(start ? (byte)0x01 : (byte)0x00);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Starts the timers of a used item: one keyed by the item's own goid and one for the item group, both
        /// type 00 and the same length, as retail did when Antibiotic 5.0 (goid 39410) was used
        /// (PacketsZombiesPortingEtc line 77221): <c>80 bd 01 f2 99 00 00 00 00 41 01</c> and
        /// <c>80 bd 01 e4 02 00 00 00 00 41 01</c>. The end records follow when the time is up.
        /// </summary>
        public static void StartItem(WorldClient client, UInt16 itemGoid, UInt16 group, float seconds, double now)
        {
            ClientData data = client.playerData;
            UInt16[] ids = group != 0 ? new[] { itemGoid, group } : new[] { itemGoid };
            lock (data.cooldownEndsAt)
            {
                foreach (UInt16 id in ids)
                {
                    data.cooldownEndsAt[id] = now + seconds;
                }
            }

            foreach (UInt16 id in ids)
            {
                client.messageQueue.addRpcMessage(BuildTimer(id, GroupTimer, seconds, true));
            }
            client.FlushQueue();

            DelayedActions.After(seconds, () =>
            {
                foreach (UInt16 id in ids)
                {
                    client.messageQueue.addRpcMessage(BuildTimer(id, GroupTimer, seconds, false));
                }
                client.FlushQueue();
            });
        }

        /// <summary>True while a timer started with <see cref="StartItem"/> (item or group) is running.</summary>
        public static bool IsTimerRunning(ClientData data, UInt16 id, double now)
        {
            lock (data.cooldownEndsAt)
            {
                return data.cooldownEndsAt.TryGetValue(id, out double end) && now < end;
            }
        }

        /// <summary>True while the ability, or the group it shares a timer with, is cooling down.</summary>
        public static bool IsCoolingDown(ClientData data, UInt16 abilityId, double now)
        {
            abilityId = AbilityCatalog.Canonical(abilityId);
            lock (data.cooldownEndsAt)
            {
                if (data.cooldownEndsAt.TryGetValue(abilityId, out double end) && now < end)
                {
                    return true;
                }

                return TryGet(abilityId, out Cooldown cd) && cd.Group != 0 &&
                       data.cooldownEndsAt.TryGetValue(cd.Group, out double groupEnd) && now < groupEnd;
            }
        }

        /// <summary>
        /// Starts the ability's retail cooldown (and its group's): records when it ends, sends the start records,
        /// and sends the end records when the time is up. Abilities without a known cooldown have none.
        /// </summary>
        public static void Start(WorldClient client, UInt16 abilityId, double now)
        {
            abilityId = AbilityCatalog.Canonical(abilityId);
            if (!TryGet(abilityId, out Cooldown cd))
            {
                return;
            }

            ClientData data = client.playerData;
            lock (data.cooldownEndsAt)
            {
                data.cooldownEndsAt[abilityId] = now + cd.Seconds;
                if (cd.Group != 0)
                {
                    data.cooldownEndsAt[cd.Group] = now + cd.GroupSeconds;
                }
            }

            client.messageQueue.addRpcMessage(BuildTimer(abilityId, OwnTimer, cd.Seconds, true));
            if (cd.Group != 0)
            {
                client.messageQueue.addRpcMessage(BuildTimer(cd.Group, GroupTimer, cd.GroupSeconds, true));
                DelayedActions.After(cd.GroupSeconds, () =>
                {
                    client.messageQueue.addRpcMessage(BuildTimer(cd.Group, GroupTimer, cd.GroupSeconds, false));
                    client.FlushQueue();
                });
            }
            client.FlushQueue();

            DelayedActions.After(cd.Seconds, () =>
            {
                client.messageQueue.addRpcMessage(BuildTimer(abilityId, OwnTimer, cd.Seconds, false));
                client.FlushQueue();
            });
        }

        public static bool TryGet(UInt16 id, out Cooldown cooldown)
        {
            id = AbilityCatalog.Canonical(id);
            // Supported toggle term callbacks AddRecastTimer use their stored Value15, not an absent RecastTime.
            if (id == 42 || id == 442) { cooldown = new Cooldown(15); return true; }
            if (Retail.TryGetValue(id, out cooldown)) return true;
            var entry = AbilityCatalog.Find(id);
            if (entry == null || entry.Number("Ability/RecastTime") <= 0) return false;
            UInt16 family = AbilityCatalog.IdOf((UInt32)entry.Number("Ability/FamilyRecastTimer"));
            float seconds = (float)entry.Number("Ability/RecastTime");
            float shared = (float)(AbilityCatalog.Find(family)?.Number("Ability/RecastTime", seconds) ?? seconds);
            cooldown = new Cooldown(seconds, family, shared);
            return true;
        }
    }
}
