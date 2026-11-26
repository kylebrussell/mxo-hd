using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Coder simulacra (pets): Remote Proxy, Logic Daemon and Patch Daemon summons, the abilities that act on the caster's
    /// own simulacrum (Repair/Fix, Fortify, Power Boost, Charge, Detonate Proxy, Drain Code), Daemon Boost, and the client's orders
    /// (<c>81 7a</c>/<c>81 7b</c>/<c>81 7c</c>). See docs/CODER-PETS.md for the evidence behind each number.
    /// </summary>
    public static class CoderPets
    {
        public enum Kind { Proxy, Nuker, Healer }

        /// <summary>Orders, as client.dll's Simulacra window sends them (viewsimulacra.cpp; FourCC button messages).</summary>
        public enum Command : byte { Stay = 0, Deactivate = 1, Attack = 2, Follow = 3, Mode = 5 }

        /// <summary>81 7c command 5 values: 'SDef' 0, 'SAgr' 1, 'SPas' 2.</summary>
        public enum Stance : byte { Defensive = 0, Aggressive = 1, Passive = 2 }

        /// <summary>The window addresses "the current simulacrum" with ID 0xfe.</summary>
        public const byte CurrentSimulacrum = 0xfe;

        public class Summon
        {
            public UInt16 Ability;
            public Kind Kind;
            public int MinLevel, MaxLevel;
        }

        /// <summary>simulacra.py *_Subject: sheet and sim_clamp(min, max, subject.Level).</summary>
        public static readonly Dictionary<UInt16, Summon> Summons = new Dictionary<UInt16, Summon>
        {
            { 27, new Summon { Ability = 27, Kind = Kind.Proxy, MinLevel = 1, MaxLevel = 4 } },
            { 28, new Summon { Ability = 28, Kind = Kind.Proxy, MinLevel = 4, MaxLevel = 7 } },
            { 29, new Summon { Ability = 29, Kind = Kind.Proxy, MinLevel = 8, MaxLevel = 17 } },
            { 586, new Summon { Ability = 586, Kind = Kind.Proxy, MinLevel = 18, MaxLevel = 34 } },
            { 594, new Summon { Ability = 594, Kind = Kind.Proxy, MinLevel = 35, MaxLevel = 50 } },
            { 22, new Summon { Ability = 22, Kind = Kind.Nuker, MinLevel = 4, MaxLevel = 7 } },
            { 601, new Summon { Ability = 601, Kind = Kind.Nuker, MinLevel = 8, MaxLevel = 17 } },
            { 611, new Summon { Ability = 611, Kind = Kind.Nuker, MinLevel = 18, MaxLevel = 34 } },
            { 616, new Summon { Ability = 616, Kind = Kind.Nuker, MinLevel = 35, MaxLevel = 50 } },
            { 602, new Summon { Ability = 602, Kind = Kind.Healer, MinLevel = 12, MaxLevel = 22 } },
            { 612, new Summon { Ability = 612, Kind = Kind.Healer, MinLevel = 23, MaxLevel = 43 } },
            { 617, new Summon { Ability = 617, Kind = Kind.Healer, MinLevel = 44, MaxLevel = 50 } },
        };

        /// <summary>simulacra.simulacra_choices: Remote Proxy picks one at random.</summary>
        public static readonly string[] ProxySheets = { "Aikido", "Karate", "KungFu", "Pistol", "Rifle", "SMG" };

        internal static string NormalizeProxySheetOverride(string configured)
        {
            const string prefix = "Simulacra.";
            configured = configured?.Trim();
            return ProxySheets.FirstOrDefault(sheet => string.Equals(configured, prefix + sheet, StringComparison.OrdinalIgnoreCase));
        }

        public const UInt16 PowerBoost = 25;
        public const UInt16 AbsorbSimulacra = 607;
        internal const UInt16 ProxyMasterDiscipline = 599, ProxyMasterGrant = 1107;
        private static readonly Lazy<(int CostReduction, int RegenBonus, int HealthRegenBonus, double Duration)> proxyMasterResources =
            new Lazy<(int CostReduction, int RegenBonus, int HealthRegenBonus, double Duration)>(() =>
        {
            var modifiers = AbilityCatalog.Find(ProxyMasterGrant).Modifiers.ToArray();
            var cost = modifiers.Single(mod => mod.Target == 117);
            var regen = modifiers.Single(mod => mod.Target == 114);
            var health = modifiers.Single(mod => mod.Target == 112);
            return ((int)cost.Number("MinValue"), (int)regen.Number("MinValue"), (int)health.Number("MinValue"), cost.Number("Duration"));
        });
        public const UInt16 DrainCode = 19, FortifySimulacra = 20, DetonateProxy = 34, StopProxyEffect = 1109, ChargeSimulacra = 603;
        public static readonly UInt16[] Repairs = { 30, 31, 587, 596, 615 };
        internal static readonly UInt16[] ContinuousLoadAbilities = { FortifySimulacra, 30, ChargeSimulacra, AbsorbSimulacra, ProxyMasterGrant };

        /// <summary>Abilities whose target must be the caster's own simulacrum (CastValidTarget 11 and shipped owner tests).</summary>
        public static bool TargetsOwnPet(UInt16 id) => id == DrainCode || id == FortifySimulacra || id == DetonateProxy ||
            id == ChargeSimulacra || id == PowerBoost || id == AbsorbSimulacra || id == ProxyMasterGrant || Repairs.Contains(id);

        internal static string DisplayName(UInt16 id) => id switch
        {
            ChargeSimulacra => "Charge Simulacra",
            DetonateProxy => "Detonate Proxy",
            AbsorbSimulacra => "Absorb Simulacra",
            ProxyMasterGrant => "Proxy Master",
            _ => null
        };

        public static bool Handles(UInt16 id) => Summons.ContainsKey(id) || TargetsOwnPet(id) || DaemonBoost.Handles(id);

        /// <summary>NPC_BASE_SIMULACRA (gameobjects.csv 40376 = 0x9db8).</summary>
        public const UInt16 SimulacraGoId = 40376;

        /// <summary>Retail EffectID on a fresh summon: bytes 0c 06 00 28 (FX_HACKER_SIMULACRA_SUMMON_BASE).</summary>
        public const UInt32 SummonFx = 0x2800060c, DeathFx = 0x2800060d, DismissFx = 0x2800060e;

        public class Pet
        {
            public WorldClient Owner;
            public Mob Mob;
            public UInt16 Ability;
            public Kind Kind;
            public string Sheet;
            public byte SimulacraId;
            public Command Order = Command.Follow;
            public Stance Stance = Stance.Defensive;
            public Mob Target;
            internal int TargetDeathGeneration;
            public double NextAttack;
            public UInt16 BaseMaxHealth;
            public double FortifyUntil;
            public UInt16 FortifyHealth;
            public double ChargeUntil;
            internal double ProxyMasterUntil;
            // OURS: retail pet sheet IS/cost/regen values are not established. A real bounded pool makes 603 meaningful.
            public double InnerStrength = 100, MaxInnerStrength = 100, LastResourceTick;
            internal double ProxyMasterHealingFraction;
            public int FortifyResistance;
            public double PowerBoostUntil;
            internal int PowerForceCombat;
            public int PowerMeleeDamage, PowerBallisticDamage;
            internal DaemonBoost.PulseEffect DaemonBoostEffect, DaemonBoostAccuracyEffect;
            public bool Gone, WindowRemoved;
            public byte Counter;
            internal bool Walking;
            internal NavPoint[] Route;
            internal (double X, double Y, double Z) RouteTarget;
            internal double RetryRouteAt;
            internal int RouteIndex;
        }

        private static readonly Dictionary<UInt32, Pet> pets = new Dictionary<UInt32, Pet>();
        private static readonly Dictionary<UInt32, byte> simulacraCounters = new Dictionary<UInt32, byte>();
        private static readonly Random random = new Random();
        private static Timer timer;

        public const double TickSeconds = 1.0;
        /// <summary>Seconds between a simulacrum's attacks. Client var Simulacra_AttackActionPeriod exists; its value is unknown.</summary>
        public const double AttackPeriodSeconds = 3.0;
        public const string FortifyExpiryQa = "MXO_PET_FORTIFY_EXPIRY_QA";
        public const string ChargeRangeQa = "MXO_PET_CHARGE_RANGE_QA";
        public const string ProxyMasterSweepQa = "MXO_PROXY_MASTER_SWEEP_QA";
        public const string ProxyMasterMeterQa = "MXO_PROXY_MASTER_METER_QA";
        public const string DrainDamageQa = "MXO_SIMULACRUM_QA_DRAIN_DAMAGE";
        public const string RifleProxyQa = "MXO_SIMULACRUM_QA_RIFLE";
        public const string KarateProxyQa = "MXO_SIMULACRUM_QA_KARATE";
        public const double FollowDistanceMetres = 2.5, MeleeRangeMetres = 2.5, RangedRangeMetres = 15.0, LeashMetres = 60.0;
        public const double StepMetres = 5.0;
        private const double RouteReplanDistance = 100, RouteRetrySeconds = 3;

        public static Pet Of(WorldClient owner)
        {
            if (owner == null) return null;
            lock (pets) return pets.TryGetValue(owner.playerData.getCharID(), out Pet pet) && !pet.Gone ? pet : null;
        }

        public static Pet OfMob(Mob mob)
        {
            lock (pets) return pets.Values.FirstOrDefault(p => p.Mob == mob && !p.Gone);
        }

        public static int Count
        {
            get { lock (pets) return pets.Values.Count(p => !p.Gone); }
        }

        // ---- Retail numbers --------------------------------------------------------------------------------------

        public static int SimLevel(Summon summon, int casterLevel) => Math.Clamp(casterLevel, summon.MinLevel, summon.MaxLevel);

        /// <summary>
        /// Retail simulacra spawns showed MaxHealth 337 at level 7 and 1950 at level 50 (Nuker, Aikido; an SMG proxy had 2744,
        /// probably Fortified). Levels between are a straight line through those two points: our choice.
        /// </summary>
        public static UInt16 MaxHealthAt(int level)
        {
            double hp = 337 + (level - 7) * (1950.0 - 337.0) / 43.0;
            return (UInt16)Math.Max(50, Math.Round(hp));
        }

        public static string NameOf(Kind kind, string sheet) => kind == Kind.Nuker ? "Nuker Simulacrum" :
            kind == Kind.Healer ? "Healer Simulacrum" : sheet + " Simulacrum";

        /// <summary>Retail Description ids: Nuker L7 0x580004d2/0x580004d4, Nuker L50 0x58000513, Aikido and SMG L50 0xb40001b9.</summary>
        public static UInt32 DescriptionOf(Kind kind, int level, bool female)
        {
            if (kind == Kind.Proxy) return 0xb40001b9;
            if (level <= 17) return female ? 0x580004d4u : 0x580004d2u;
            return 0x58000513;
        }

        /// <summary>
        /// The id as the hex of its wire bytes (little-endian), the form Mob's rsi hex and Description take: 0x58000513 is
        /// "13050058". Writing the id's own digits instead sent 58 00 05 13, a template the client doesn't have, and the
        /// simulacrum spawned invisible (native QA of PR 107).
        /// </summary>
        public static string WireHex(UInt32 id) => StringUtils.bytesToString_NS(NumericalUtils.uint32ToByteArray(id, 1)).ToLowerInvariant();

        /// <summary>Retail EquippedItemID: Nuker 0x0a06, Aikido 0xb917, SMG 0xba5b. Other proxies borrow the closest one.</summary>
        public static UInt32 WeaponOf(Kind kind, string sheet)
        {
            if (kind != Kind.Proxy) return 0x0a06;
            switch (sheet)
            {
                case "SMG": case "Pistol": case "Rifle": return 0xba5b;
                default: return 0xb917;
            }
        }

        public static bool Ranged(Pet pet) => pet.Kind != Kind.Proxy || pet.Sheet == "SMG" || pet.Sheet == "Pistol" || pet.Sheet == "Rifle";

        /// <summary>
        /// tempmods.DetonateProxyProc: the pet's health capped at a quarter of its max, at least a tenth of it. The
        /// proc buffs the pet's Viral Damage by value - (buff + 1) and StopProxy1Effect (1109) deals its stored 1, so the
        /// blast is the computed value.
        /// </summary>
        public static int DetonateValue(int health, int maxHealth)
        {
            int val = Math.Min(health, maxHealth / 4);
            return Math.Max(val, maxHealth / 10);
        }

        // Native 62144ed0/62125940, consumer 625de800: reserved byte, ID, name[32], status[10].
        public static byte[] BuildWindow(Pet pet, bool register)
        {
            var bytes = new byte[register ? 46 : 13];
            bytes[0] = 0x81; bytes[1] = register ? (byte)0x76 : (byte)0x78;
            int status = register ? 36 : 3;
            if (register)
            {
                bytes[3] = pet.SimulacraId;
                var name = System.Text.Encoding.ASCII.GetBytes(pet.Mob.getName());
                Array.Copy(name, 0, bytes, 4, Math.Min(30, name.Length));
            }
            else bytes[2] = pet.SimulacraId;
            BitConverter.GetBytes(pet.Mob.getHealthC()).CopyTo(bytes, status + 1);
            BitConverter.GetBytes(pet.Mob.getHealthM()).CopyTo(bytes, status + 3);
            bytes[status + 5] = (byte)pet.Order;
            bytes[status + 6] = (byte)pet.Stance;
            bytes[status + 7] = (byte)pet.Mob.getLevel();
            return bytes;
        }

        private static void SendWindow(Pet pet, bool register)
        {
            if (pet.WindowRemoved) return;
            pet.Owner.messageQueue?.addRpcMessage(BuildWindow(pet, register));
            pet.Owner.FlushQueue();
        }

        private static void RemoveWindow(Pet pet)
        {
            if (pet.WindowRemoved) return;
            pet.WindowRemoved = true;
            pet.Owner.messageQueue?.addRpcMessage(new byte[] { 0x81, 0x77, pet.SimulacraId });
            pet.Owner.FlushQueue();
        }

        // OURS baseline: 10 IS per action, 2 IS/s, 100 maximum. Catalog 603's percentages are sourced.
        public static void Regenerate(Pet pet, double now)
        {
            double elapsed = Math.Max(0, now - pet.LastResourceTick);
            double charged = Math.Max(0, Math.Min(now, pet.ChargeUntil) - pet.LastResourceTick);
            double mastered = Math.Max(0, Math.Min(now, pet.ProxyMasterUntil) - pet.LastResourceTick);
            double gained = elapsed * 2 + charged;
            if (mastered > 0)
            {
                gained += mastered * 2 * proxyMasterResources.Value.RegenBonus / 100;
                RegenerateProxyMasterHealth(pet, mastered);
            }
            pet.InnerStrength = Math.Min(pet.MaxInnerStrength, pet.InnerStrength + gained);
            pet.LastResourceTick = Math.Max(now, pet.LastResourceTick);
        }

        private static void RegenerateProxyMasterHealth(Pet pet, double seconds)
        {
            var mob = pet.Mob;
            if (pet.Gone || mob == null || mob.getIsDead() || mob.getHealthC() == 0 || mob.getHealthC() >= mob.getHealthM())
            {
                pet.ProxyMasterHealingFraction = 0;
                return;
            }

            // Apply only the authored bonus. Reuse the common maximum-health rate without giving unbuffed pets
            // a new passive heal. No separate baseline for a retail simulacrum has been recovered.
            double combatRate = mob.CombatantCount > 0 || pet.Target != null ? CombatCalculator.FIGHT_REGEN_FACTOR : 1;
            pet.ProxyMasterHealingFraction += seconds * mob.getHealthM() * HealthRegen.FractionPerSecond *
                combatRate * proxyMasterResources.Value.HealthRegenBonus / 100;
            int whole = (int)pet.ProxyMasterHealingFraction;
            if (whole == 0) return;
            pet.ProxyMasterHealingFraction -= whole;
            HealPetLocked(pet, whole, 0);
            if (mob.getHealthC() >= mob.getHealthM()) pet.ProxyMasterHealingFraction = 0;
        }

        public static bool SpendAction(Pet pet, double now)
        {
            Regenerate(pet, now);
            double cost = ActionCost(pet, now);
            if (pet.InnerStrength < cost) return false;
            pet.InnerStrength -= cost;
            return true;
        }

        private static double ActionCost(Pet pet, double now)
        {
            int reduction = (now < pet.ChargeUntil ? 20 : 0) +
                (now < pet.ProxyMasterUntil ? proxyMasterResources.Value.CostReduction : 0);
            return 10 * (100 - reduction) / 100.0;
        }

        internal static void ApplyProxyMasterResources(Pet pet, double now)
        {
            // Settle all old intervals before starting or refreshing this pet's generation.
            Regenerate(pet, now);
            if (now >= pet.ProxyMasterUntil) pet.ProxyMasterHealingFraction = 0;
            pet.ProxyMasterUntil = now + proxyMasterResources.Value.Duration;
        }

        // NPC retaliation already includes the level ratio. Normalize the shipped armor curve
        // against zero toughness so Fortify adds only the change from its resistance rating.
        internal static int MitigateRetaliation(Pet pet, int attackerLevel, int damage, double now)
        {
            if (damage <= 0 || now >= pet.FortifyUntil) return damage;
            return Math.Max(1, (int)TypedAbilityResistance.Amount(damage, attackerLevel, pet.Mob.getLevel(), pet.FortifyResistance));
        }

        // OURS base roll/level curve; sourced 700 accuracy (level*1.3) now changes the actual attack roll.
        public static double HitChance(Pet pet, int targetLevel, double now)
        {
            bool ranged = Ranged(pet);
            int accuracy = DaemonBoost.Accuracy(pet, now, ranged) + ProxyTechnician.Accuracy(pet, now, ranged);
            return CalculateHitChance(pet, targetLevel, accuracy);
        }

        private static double CalculateHitChance(Pet pet, int targetLevel, int accuracy) =>
            Math.Clamp(75 + (pet.Mob.getLevel() - targetLevel) * 2 + accuracy, 5, 95);

        public sealed class OwnPetCast
        {
            public readonly Pet Target;
            public readonly int CasterLevel, CasterLife, CasterWorld;
            internal readonly int? LoadedSince;
            public OwnPetCast(WorldClient caster, Pet target, int? loadedSince = null)
            {
                Target = target; CasterLevel = caster.playerInstance.Level.getValue()[0];
                CasterLife = caster.playerData.lifeGeneration; CasterWorld = caster.playerData.worldGeneration;
                LoadedSince = loadedSince;
            }
            public bool Matches(WorldClient caster, Pet current) => Target != null && Target == current &&
                Target.Owner == caster && !Target.Gone && !Target.Mob.getIsDead() &&
                caster.playerData.lifeGeneration == CasterLife && caster.playerData.worldGeneration == CasterWorld;
        }

        public static void ApplyPowerBoost(Pet pet, int casterLevel, double now)
        {
            var entry = AbilityCatalog.Find(PowerBoost);
            pet.PowerMeleeDamage = pet.PowerBallisticDamage = pet.PowerForceCombat = 0;
            foreach (var modifier in entry.Modifiers)
            {
                if (!AbilityValues.TryValue(modifier, casterLevel, casterLevel, out int value)) continue;
                if (modifier.Target == 159) pet.PowerMeleeDamage = value;
                if (modifier.Target == 171) pet.PowerBallisticDamage = value;
                if (modifier.Target == CombatTactics.StatForceCombat) pet.PowerForceCombat = value;
            }
            pet.PowerBoostUntil = now + entry.Modifiers.First(m => m.Target == 159).Number("Duration");
        }

        internal static int ActivePowerForce(Pet pet, double now) => now < pet.PowerBoostUntil ? pet.PowerForceCombat : 0;

        /// <summary>159/171 damage influence uses /100, as the existing combat damage pipeline does.
        /// Only proxy melee/firearms qualify: Nuker viral attacks and healer healing receive neither stat.</summary>
        public static UInt16 PowerBoostDamage(Pet pet, int baseDamage, double now)
        {
            int influence = pet.Kind != Kind.Proxy || now >= pet.PowerBoostUntil ? 0 :
                Ranged(pet) ? pet.PowerBallisticDamage : pet.PowerMeleeDamage;
            return (UInt16)Math.Clamp((int)(baseDamage * (1 + influence / 100.0)), 0, UInt16.MaxValue);
        }

        public static UInt16 DaemonBoostDamage(Pet pet, int damage, double now) =>
            (UInt16)Math.Clamp((int)(damage * (1 + (pet.Kind == Kind.Nuker ? DaemonBoost.Values(pet, now).Viral : 0) / 100.0)), 0, UInt16.MaxValue);

        public static UInt16 DaemonBoostHealing(Pet pet, int healing, double now) =>
            (UInt16)Math.Clamp((int)(healing * (1 + (pet.Kind == Kind.Healer ? DaemonBoost.Values(pet, now).Healing : 0) / 100.0)), 0, UInt16.MaxValue);

        // ---- Spawning --------------------------------------------------------------------------------------------

        private static readonly double[] SummonStandDistances = { 150, 100, 75 };
        private static readonly int[] SummonStandTurns = { 0, 1, -1, 2, -2, 3, -3, 4 };

        internal static (double X, double Y, double Z) SummonStand(ushort district, double x, double y, double z)
        {
            // Without a mapped origin, preserve the established offset and height.
            if (!NavMesh.Locate(district, x, y, z, out NavLocation origin)) return (x + 150, y, z);
            foreach (double radius in SummonStandDistances)
            {
                foreach (int turn in SummonStandTurns)
                {
                    double angle = turn * Math.PI / 4;
                    double tx = x + radius * Math.Cos(angle), tz = z + radius * Math.Sin(angle);
                    if (NavMesh.WalkableLine(district, origin, x, z, tx, tz, out NavLocation end) &&
                        Math.Abs(end.floorY - origin.floorY) <= 40)
                    {
                        return (tx, end.floorY + 95, tz);
                    }
                }
            }
            // The owner is standing on the only verified floor left.
            return (x, origin.floorY + 95, z);
        }

        public static Pet SpawnFor(WorldClient owner, UInt16 ability)
        {
            lock (owner.playerData.craftingSync) return SpawnForLocked(owner, ability);
        }

        private static Pet SpawnForLocked(WorldClient owner, UInt16 ability)
        {
            if (!Summons.TryGetValue(ability, out Summon summon)) return null;
            Pet previous = Of(owner);
            bool replaceAfterPreparation = ability == 28;
            if (!replaceAfterPreparation && previous != null) Dismiss(owner, false, previous);
            int casterLevel = owner.playerInstance.Level.getValue()[0];
            int level = SimLevel(summon, casterLevel);
            UInt32 charId = owner.playerData.getCharID();
            bool fixtureOwner = charId == 110 && QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"), Store.config?.dbParams?.DatabaseName,
                    charId, owner.playerData.getOnWorld() && !owner.playerData.worldChangePending,
                    owner.playerData.isDead || owner.playerData.duelDeath);
            bool fixtureRifle = fixtureOwner && ability == 28 && Environment.GetEnvironmentVariable(RifleProxyQa) == "1";
            bool fixtureDrain = fixtureOwner && ability == 27 && Environment.GetEnvironmentVariable(DrainDamageQa) == "1";
            string sheet;
            bool female;
            lock (random)
            {
                sheet = summon.Kind == Kind.Proxy
                    ? Store.worldConfig?.RemoteProxySheet ?? ProxySheets[random.Next(ProxySheets.Length)]
                    : summon.Kind.ToString();
                female = random.Next(2) == 0;
            }

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(owner.playerInstance.Position.getValue(), ref x, ref y, ref z);
            ushort district = (ushort)owner.playerData.getDistrictId();
            (x, y, z) = SummonStand(district, x, y, z);

            if (fixtureRifle) sheet = "Rifle";
            if (fixtureOwner && ability == 28 && Environment.GetEnvironmentVariable(KarateProxyQa) == "1") sheet = "Karate";
            byte simId;
            lock (simulacraCounters)
            {
                simulacraCounters.TryGetValue(charId, out simId);
                simId = (byte)(simId % 0xfd + 1);
                simulacraCounters[charId] = simId;
            }

            var mob = new Mob();
            mob.setEntityId(WorldServer.entityIdCounter++);
            mob.setGoId(new byte[] { (byte)(SimulacraGoId & 0xff), (byte)(SimulacraGoId >> 8) });
            mob.setDistrict(district);
            mob.setDistrictName(owner.playerData.getDistrict());
            mob.setName(NameOf(summon.Kind, sheet));
            mob.setLevel((UInt16)level);
            UInt16 max = fixtureDrain ? (UInt16)9000 : MaxHealthAt(level);
            mob.setHealthM(max);
            mob.setHealthC(max);
            mob.setRsiHex(WireHex(DescriptionOf(summon.Kind, level, female)));
            mob.setWeapon(WeaponOf(summon.Kind, sheet));
            mob.setXPos(x); mob.setYPos(y); mob.setZPos(z);
            mob.xBase = x; mob.yBase = y; mob.zBase = z;
            mob.setRotation(owner.playerInstance.YawInterval.getValue()[0]);
            mob.setIsDead(false);
            mob.setIsLootable(false);
            mob.isFriendly = true; // never an aggro, ability or close-combat target for the server's own NPC logic
            mob.aggroKind = MobAggroKind.Passive;

            var pet = new Pet
            {
                Owner = owner, Mob = mob, Ability = ability, Kind = summon.Kind, Sheet = sheet, SimulacraId = simId,
                BaseMaxHealth = max, LastResourceTick = TimeUtils.GetSimTimeSeconds()
            };
            mob.pet = pet;
            if (replaceAfterPreparation)
            {
                // RemoteProxy2_Subject creates, reports the creation, then destroys the current summon.
                // Resolve that sequence as preparation followed by retirement of the previous pet.
                Output.WriteDebugLog($"[PET] Remote Proxy 2.0 prepared simulacrum {simId}; previous {previous?.SimulacraId ?? 0}");
                if (previous != null) Dismiss(owner, false, previous);
            }
            lock (pet)
            {
                lock (pets)
                {
                    if (!OwnerPresent(pet) || pets.ContainsKey(charId)) return null;
                    pets.Add(charId, pet);
                }
                lock (WorldServer.mobs.SyncRoot) WorldServer.mobs.Add(mob);
                lock (WorldServer.gameServerEntities.SyncRoot) WorldServer.gameServerEntities.Add(mob);
                SendWindow(pet, true);
            }
            EnsureTimer();
            Output.WriteDebugLog("[PET] " + TimedEffects.PlayerName(owner) + " summoned " + mob.getName() + " L" + level +
                                 " (ability " + ability + ", simulacrum " + simId + ")" +
                                 FormattableString.Invariant($" at ({x:0.##},{y:0.##},{z:0.##})"));
            return pet;
        }

        /// <summary>
        /// NPC_BASE_SIMULACRA creation, as retail sent a fresh summon to its owner (MaraC capture line 78483; Syntax capture line
        /// 4213): name, TitleAbility 00 10 00 00, CombatantMode 0x22, EffectCounter 1, MasterCharacterID and OwnerCharacterID =
        /// owner, Description, EffectID 0x2800060c, SimulacraID, Position, owner OrganizationID, NPCRank 2, Level, Health,
        /// MaxHealth, CurrentState 0x080d, EquippedItemID and StealthAwareness. Flagged views can carry ConditionStateFlags.
        /// No IsEnemy/IsFriendly/RSIDescription.
        /// </summary>
        public static GameObject BuildCreation(Mob mob) => BuildCreation(mob, Presentation(mob.pet, mob.pet.Owner));

        internal static (byte Organization, uint Flags) Presentation(Pet pet, WorldClient recipient)
        {
            byte[] ownerOrganization = pet.Owner.playerInstance.OrganizationID.getValue();
            byte organization = ownerOrganization?.Length > 0 ? ownerOrganization[0] : (byte)0;
            uint flags = pet.Mob.ConditionFlags;
            int mode = PvpFlag.ModeFor(recipient);
            // The stock client compares the pet's own org and flag in flag-based PvP modes.
            if ((mode == 2 || mode == 4 || mode == 6) && pet.Owner.playerData.pvpFlag &&
                (recipient == pet.Owner || PvpCombat.Check(recipient, pet.Owner) == PvpRules.Verdict.Allowed))
                flags |= PvpFlag.PvpBit;
            return (organization, flags);
        }

        internal static void RefreshPresentation(Pet pet, WorldClient recipient, ClientView view)
        {
            if (view == null) return;
            lock (view)
            {
                if (!view.viewCreated || view.viewNeedsToBeDeleted || pet.Gone || pet.Mob.getIsDead()) return;
                var presentation = Presentation(pet, recipient);
                if (view.SimulacrumPresentation == presentation) return;
                view.SimulacrumPresentation = presentation;
                new ServerPackets().SendNpcUpdateData(view.ViewID, recipient,
                    BuildPresentationUpdate(presentation.Organization, presentation.Flags));
            }
        }

        internal static void RefreshPresentationForWatchers(Pet pet)
        {
            foreach (var recipient in SocialHandler.OnlineWhere(client => client.playerData.getDistrictId() == pet.Mob.getDistrict()))
                RefreshPresentation(pet, recipient, recipient.viewMan.FindViewForEntity(pet.Mob.getEntityId()));
        }

        internal static byte[] BuildPresentationUpdate(byte organization, uint flags)
        {
            // GO40376 update attributes 12 (OrganizationID) and 43 (ConditionStateFlags).
            byte[] update = PlayerViewAttributes.Build((12, new[] { organization }), (43, PlayerViewAttributes.U32(flags)));
            update[0] = 0x04;
            return update;
        }

        internal static GameObject BuildCreation(Mob mob, (byte Organization, uint Flags) presentation)
        {
            Pet pet = mob.pet;
            var go = new AttributeClass3828("NPC_BASE_SIMULACRA", 40376); // one object, not all ~40k definitions
            go.DisableAllAttributes();
            go.CharacterName.enable(); go.CharacterName.setValue(mob.getName());
            go.TitleAbility.enable(); go.TitleAbility.setValue(new byte[] { 0x00, 0x10, 0x00, 0x00 });
            go.CombatantMode.enable(); go.CombatantMode.setValue((byte)0x22);
            go.EffectCounter.enable(); go.EffectCounter.setValue((byte)1);
            go.MasterCharacterID.enable(); go.MasterCharacterID.setValue(pet.Owner.playerData.getCharID());
            go.Description.enable(); go.Description.setValue(StringUtils.hexStringToBytes(mob.getRsiHex()));
            go.EffectID.enable(); go.EffectID.setValue(SummonFx);
            go.SimulacraID.enable(); go.SimulacraID.setValue(pet.SimulacraId);
            go.Position.enable(); go.Position.setValue(NumericalUtils.doublesToLtVector3d(mob.getXPos(), mob.getYPos(), mob.getZPos()));
            go.OrganizationID.enable(); go.OrganizationID.setValue(presentation.Organization);
            if (presentation.Flags != 0)
            {
                go.ConditionStateFlags.enable(); go.ConditionStateFlags.setValue(presentation.Flags);
            }
            go.NPCRank.enable(); go.NPCRank.setValue((byte)2);
            go.Level.enable(); go.Level.setValue((byte)mob.getLevel());
            go.Health.enable(); go.Health.setValue(mob.getHealthC());
            go.MaxHealth.enable(); go.MaxHealth.setValue(mob.getHealthM());
            go.CurrentState.enable(); go.CurrentState.setValue(new byte[] { 0x0d, 0x08, 0x00, 0x00 });
            go.EquippedItemID.enable(); go.EquippedItemID.setValue(mob.getWeapon());
            go.OwnerCharacterID.enable(); go.OwnerCharacterID.setValue(pet.Owner.playerData.getCharID());
            go.StealthAwareness.enable(); go.StealthAwareness.setValue((byte)(mob.getLevel() >= 50 ? 0x0c : 0x01));
            return go;
        }

        // ---- Lifetime --------------------------------------------------------------------------------------------

        /// <summary>The simulacrum leaves (Deactivate order, a new summon, logout, zoning). Returns false without one.</summary>
        public static bool Dismiss(WorldClient owner, bool withEffect = true)
            => Dismiss(owner, withEffect, null);

        private static bool Dismiss(WorldClient owner, bool withEffect, Pet expected)
        {
            Pet pet = expected ?? Of(owner);
            if (pet == null || !ReferenceEquals(owner, pet.Owner)) return false;
            UInt32 charId = owner.playerData.getCharID();
            lock (pet)
            {
                lock (pets)
                {
                    if (!pets.TryGetValue(charId, out Pet current) || current != pet) return false;
                    pets.Remove(charId);
                }
                RemoveLocked(pet, withEffect ? DismissFx : 0);
            }
            return true;
        }

        internal static void DeactivateUnloadedSummon(WorldClient owner, Pet pet)
        {
            if (Dismiss(owner, true, pet))
                Output.WriteDebugLog($"[PET] Summon {pet.Ability} unloaded: dismissed simulacrum {pet.SimulacraId}");
        }

        private static void Remove(Pet pet, UInt32 fx)
        {
            lock (pet) RemoveLocked(pet, fx);
        }

        private static void RemoveLocked(Pet pet, UInt32 fx)
        {
            if (pet.Gone) return;
            ReleaseClosePair(pet);
            pet.Gone = true;
            TimedEffects.RemoveParent(TimedEffects.ForMob(pet.Mob), ProxyMasterGrant);
            RemoveWindow(pet);
            if (fx != 0 && !pet.Mob.getIsDead()) SendHit(pet, fx);
            lock (WorldServer.mobs.SyncRoot) WorldServer.mobs.Remove(pet.Mob);
            lock (WorldServer.gameServerEntities.SyncRoot) WorldServer.gameServerEntities.Remove(pet.Mob);
            Store.world?.DeleteMobViews(pet.Mob);
            Output.WriteDebugLog("[PET] " + pet.Mob.getName() + " of " + TimedEffects.PlayerName(pet.Owner) + " removed");
        }

        /// <summary>The simulacrum dies: retail's death update and FX_HACKER_SIMULACRA_DEATH_BASE, then its view goes.</summary>
        public static void Kill(Pet pet)
        {
            if (pet == null) return;
            lock (pet) KillLocked(pet);
        }

        private static void KillLocked(Pet pet, uint impactFx = 0)
        {
            if (pet.Gone || !pet.Mob.TryClaimDeath()) return;
            ReleaseClosePair(pet);
            pet.Mob.setHealthC(0);
            TimedEffects.RemoveParent(TimedEffects.ForMob(pet.Mob), ProxyMasterGrant);
            RemoveWindow(pet);
            // Keep lethal impact and default death FX in one ordered view list.
            if (impactFx != 0 && impactFx != DeathFx)
            {
                var impact = new PacketContent();
                impact.AddByteArray(Mob.BuildHitUpdate(0, impactFx, ++pet.Counter));
                var deathFx = Mob.BuildHitUpdate(0, DeathFx, ++pet.Counter);
                Store.world?.SendViewUpdateToClientsWhoHasMobSpawned(impact, pet.Mob, deathFx);
            }
            else SendHit(pet, DeathFx);
            var death = new PacketContent();
            death.AddByteArray(ServerPackets.BuildNpcDeathUpdate(pet.Mob.NextAnimationSeq(), (byte)pet.Mob.getRotation(),
                (float)pet.Mob.getXPos(), (float)pet.Mob.getYPos(), (float)pet.Mob.getZPos()));
            Store.world?.SendViewUpdateToClientsWhoHasMobSpawned(death, pet.Mob);
            lock (pets)
            {
                UInt32 id = pet.Owner.playerData.getCharID();
                if (pets.TryGetValue(id, out Pet current) && current == pet) pets.Remove(id);
            }
            new ServerPackets().sendSystemChatMessage(pet.Owner, "Your simulacrum has been destroyed.", "BROADCAST");
            pet.Owner.FlushQueue();
            DelayedActions.After(5, () => Remove(pet, 0));
        }

        /// <summary>Logout or zoning: no simulacrum survives its owner leaving the district.</summary>
        public static void OwnerLeft(WorldClient owner)
        {
            Dismiss(owner, false);
        }

        // ---- Orders ----------------------------------------------------------------------------------------------

        /// <summary>81 7a ID Command / 81 7b ID Command TargetObject(view u16, spawn u16) / 81 7c ID Command Value.</summary>
        public static bool TryParseOrder(int header, byte[] data, out byte id, out Command command, out UInt32 target, out byte value)
        {
            id = 0; command = 0; target = 0; value = 0;
            if (data == null || data.Length < 2) return false;
            id = data[0];
            command = (Command)data[1];
            if (header == 0x17b)
            {
                if (data.Length < 6) return false;
                target = BitConverter.ToUInt32(data, 2);
            }
            else if (header == 0x17c)
            {
                if (data.Length < 3) return false;
                value = data[2];
            }
            return true;
        }

        public static void HandleOrderRpc(WorldClient client, int header, byte[] data)
        {
            if (!TryParseOrder(header, data, out byte id, out Command command, out UInt32 target, out byte value))
            {
                Output.WriteDebugLog("[PET] Malformed order " + header.ToString("x") + " " + StringUtils.bytesToString_NS(data ?? new byte[0]));
                return;
            }
            Output.WriteDebugLog("[PET] Order " + header.ToString("x") + " id " + id + " command " + command + " target " + target.ToString("x8") + " value " + value);
            // The stock Attack button resolves a selected object before sending 81 7b (client.dll 620efaf0).
            // A missing or unsupported selection must never retarget to a nearby NPC.
            string refusal = Order(client, id, command, target, value, fromClient: true);
            if (refusal != null)
                Output.WriteDebugLog("[PET] Order refused: " + refusal);
        }

        /// <summary>Carries out an order. Returns a line for the dev command, null when it was done.</summary>
        public static string Order(WorldClient client, byte id, Command command, UInt32 targetObject, byte value, bool fromClient = false)
        {
            Pet pet = Of(client);
            if (pet == null) return "You have no simulacrum.";
            if (id != CurrentSimulacrum && id != pet.SimulacraId && id != 0) return "No simulacrum with that ID.";
            if (command == Command.Deactivate)
                return Dismiss(client, true, pet) ? null : "You have no active simulacrum.";
            lock (pet)
            {
                if (pet.Gone || pet.Mob.getIsDead()) return "You have no active simulacrum.";
                return ApplyOrder(pet, client, command, targetObject, value, fromClient);
            }
        }

        private static string ApplyOrder(Pet pet, WorldClient client, Command command, UInt32 targetObject, byte value, bool fromClient)
        {
            switch (command)
            {
                case Command.Stay:
                    ReleaseClosePair(pet);
                    pet.Order = Command.Stay; pet.Target = null; pet.Route = null; SetStanding(pet); SendWindow(pet, false); return null;
                case Command.Follow:
                    ReleaseClosePair(pet);
                    pet.Order = Command.Follow; pet.Target = null; pet.Route = null; SendWindow(pet, false); return null;
                case Command.Attack:
                    Mob target = ResolveAttackTarget(pet, client, targetObject, fromClient);
                    if (target == null) return "Choose a hostile NPC within your simulacrum's leash.";
                    if (pet.Target != target)
                    {
                        ReleaseClosePair(pet);
                        pet.Route = null;
                    }
                    pet.TargetDeathGeneration = target.DeathGeneration;
                    pet.Order = Command.Attack; pet.Target = target; SendWindow(pet, false); return null;
                case Command.Mode:
                    if (value > 2) return "Unknown mode.";
                    SetStance(pet, (Stance)value);
                    SendWindow(pet, false); return null;
                default:
                    return "Unknown order.";
            }
        }

        internal static Mob ResolveAttackTarget(Pet pet, WorldClient client, UInt32 targetObject, bool fromClient)
        {
            // PR772 stock Attack repeats the selected view and spawn from 81 51.
            // The zero-extended view remains accepted for older headless callers.
            UInt16 targetView = (UInt16)(targetObject & 0xffff);
            UInt16 targetSpawn = (UInt16)(targetObject >> 16);
            if (targetObject == 0 && !fromClient) return DefaultTarget(client, pet);
            if (targetView <= 2) return null;
            Mob target = AbilityTargets.FindMob(client, targetView, out ClientView view);
            return view?.viewCreated == true && !view.viewNeedsToBeDeleted &&
                (targetSpawn == 0 || view.spawnId == targetSpawn) && CanPursue(pet, target) ? target : null;
        }

        internal static void SetStance(Pet pet, Stance stance)
        {
            pet.Stance = stance;
            if (stance == Stance.Passive && pet.Order != Command.Attack)
            {
                ReleaseClosePair(pet);
                pet.Target = null;
            }
        }

        private static bool CanPursue(Pet pet, Mob target) =>
            target != null && !target.isFriendly && target.pet == null && !target.getIsDead() && target.getIsSpawned() &&
            target.getDistrict() == pet.Mob.getDistrict() && OwnerDistanceTo(pet, target) <= LeashMetres;

        private static NpcEvasion CloseEntryShield(Pet pet) => Ranged(pet) ? null : pet.Target?.CapturedEvasion;

        private static void ReleaseClosePair(Pet pet) => pet.Target?.CapturedEvasion?.ReleasePet(pet);

        /// <summary>The owner's close-combat target, or the nearest hostile within 20 m.</summary>
        public static Mob DefaultTarget(WorldClient owner, Pet pet)
        {
            Mob fighting = CombatManager.GetHandler(owner).CurrentNpcTarget;
            if (CanPursue(pet, fighting)) return fighting;
            return NearestHostile(pet, 20);
        }

        private static Mob NearestHostile(Pet pet, double metres)
        {
            Mob best = null;
            double bestD = metres * 100;
            lock (WorldServer.mobs.SyncRoot)
                foreach (Mob mob in WorldServer.mobs)
                {
                    if (!CanPursue(pet, mob)) continue;
                    double d = Distance(pet.Mob, mob.getXPos(), mob.getYPos(), mob.getZPos());
                    if (d < bestD) { bestD = d; best = mob; }
                }
            return best;
        }

        // ---- Abilities -------------------------------------------------------------------------------------------

        internal const ushort DerezAbility = 26;

        internal static uint PrepareDerez(WorldClient caster, ushort targetView, ushort? spawn, out Pet target)
        {
            target = null;
            if (targetView <= 2 || !spawn.HasValue) return AbilityRefusals.InvalidTarget;
            if (!AbilityPassives.IsLoaded(caster, DerezAbility)) return AbilityRefusals.NotLoaded;
            if (CombatManager.GetHandler(caster).isCombatRunning) return AbilityRefusals.MustBeOutsideCombat;
            var mob = AbilityTargets.FindMob(caster, targetView, out ClientView view);
            if (view?.viewCreated != true || view.viewNeedsToBeDeleted || view.spawnId != spawn.Value)
                return AbilityRefusals.InvalidTarget;
            Pet pet = mob?.pet;
            if (pet == null) return AbilityRefusals.NotEnemySimulacrum;
            if (pet.Gone || mob.getIsDead() || !mob.getIsSpawned() || !OwnerPresent(pet) || Of(pet.Owner) != pet)
                return AbilityRefusals.InvalidTarget;
            var permission = PvpCombat.Check(caster, pet.Owner);
            if (permission == PvpRules.Verdict.NotOpponent) return AbilityRefusals.NotEnemySimulacrum;
            if (permission != PvpRules.Verdict.Allowed) return AbilityRefusals.InvalidTarget;

            var definition = AbilityCatalog.Find(DerezAbility);
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(caster.playerInstance.Position.getValue(), ref x, ref y, ref z);
            if (!(Distance(mob, x, y, z) <= definition.Number("Ability/SelectionRange") * CombatCalculator.WorldUnitsPerMetre))
                return AbilityRefusals.TargetOutOfRange;
            if (definition.Number("Ability/DoesNOTRequireLOS") == 0 && !CombatSight.Between(caster, mob))
                return AbilityRefusals.TargetNotInSight;
            target = pet;
            return 0;
        }

        internal static void Derez(WorldClient caster, Pet target, ushort targetView, ushort? spawn)
        {
            lock (target)
            {
                if (PrepareDerez(caster, targetView, spawn, out Pet selected) != 0 || selected != target) return;
                var hit = AbilityCatalog.Find(DerezAbility).Modifiers.Single(m => m.Target == 108 &&
                    m.Number("WhenEnabled", -1) == 2 && m.Number("DurationType", -1) == 1 && m.Number("DamageType", -1) == 5);
                if (!AbilityValues.TryValue(hit, 0, 0, out int damage) || damage <= 0) return;
                DamagePetLocked(target, damage, (uint)hit.Number("ActivationFX"));
                caster.messageQueue.addRpcMessage(AbilityMessages.AboutAbility((uint)hit.Number("ActivationStringSubject"), true, DerezAbility,
                    TimedEffects.PlayerName(caster), target.Mob.getName()));
                caster.FlushQueue();
                Output.WriteDebugLog("[PET] Proxy Coder derezzed " + target.Mob.getName() + " of " +
                    TimedEffects.PlayerName(target.Owner) + " for " + damage);
            }
        }

        internal static bool StarterSummonRequiresLoaded(ushort id) => id == 22 || id == 27 || id == 28 || id == 29 || id == 602;

        internal static bool IsCastLoaded(WorldClient caster, ushort id) =>
            (id != DrainCode && id != PowerBoost && !ContinuousLoadAbilities.Contains(id)) || AbilityPassives.IsLoaded(caster, id);

        private static bool RequiresOutsideCombat(ushort id) =>
            id == PowerBoost || id == DrainCode || id == AbsorbSimulacra || id == ProxyMasterGrant;

        private static bool HasVisibilityRequirement(ushort id) => id == AbsorbSimulacra || id == ProxyMasterGrant;

        private static bool ViolatesOutsideCombat(WorldClient caster, ushort id)
        {
            if (!RequiresOutsideCombat(id)) return false;
            var fight = CombatManager.GetHandler(caster);
            return fight.isCombatRunning || id == ProxyMasterGrant && fight.HasActiveInterlock;
        }

        /// <summary>The cast's refusal, or null when it may go ahead (checked before cost and cooldown).</summary>
        internal static string Prepare(WorldClient caster, UInt16 id, UInt16 targetView, UInt16? targetSpawn,
            out OwnPetCast cast, out uint refusal)
        {
            cast = null;
            refusal = AbilityRefusals.LegacyAmbiguous;
            if (Summons.ContainsKey(id))
            {
                return CombatManager.GetHandler(caster).isCombatRunning ? "You cannot summon a simulacrum in combat." : null;
            }
            if (TargetsOwnPet(id))
            {
                int? membership = AbilityPassives.PetCastMembership(caster, id);
                if (ContinuousLoadAbilities.Contains(id) && !membership.HasValue)
                {
                    refusal = AbilityRefusals.NotLoaded;
                    return "Load this ability before using it.";
                }
                if (ViolatesOutsideCombat(caster, id))
                {
                    refusal = AbilityRefusals.MustBeOutsideCombat;
                    return "You may only use this ability outside combat.";
                }
                Pet pet = Of(caster);
                if (HasVisibilityRequirement(id) && (targetView <= 2 || pet == null || pet.Mob.getIsDead()))
                {
                    refusal = AbilityRefusals.InvalidTarget;
                    return "Select your living simulacrum first.";
                }
                if (pet == null || pet.Mob.getIsDead()) return "You need an active simulacrum.";
                // Decoy1_Test, StopProxy1_Test and AcousticDecoy2_Test check direct-object ownership.
                if (!MatchesSelection(caster, pet, targetView, targetSpawn))
                {
                    refusal = AbilityRefusals.InvalidTarget;
                    return "That is not your simulacrum.";
                }
                if (!InAbilityRange(pet, id))
                {
                    refusal = AbilityRefusals.TargetOutOfRange;
                    return "Your simulacrum is out of range.";
                }
                if (HasVisibilityRequirement(id) && !CombatSight.Between(caster, pet.Mob))
                {
                    refusal = AbilityRefusals.TargetNotInSight;
                    return "Your simulacrum is out of sight.";
                }
                cast = new OwnPetCast(caster, pet, membership);
            }
            return null;
        }

        internal static bool MatchesSelection(WorldClient caster, Pet pet, UInt16 viewId, UInt16? spawn)
        {
            if (viewId <= 2) return true;
            var view = caster.viewMan.getViewById(viewId);
            return view?.viewCreated == true && !view.viewNeedsToBeDeleted && view.entityId == pet.Mob.getEntityId() &&
                (!spawn.HasValue || spawn.Value == view.spawnId);
        }

        internal static bool InAbilityRange(Pet pet, UInt16 id)
        {
            // AcousticDecoy1_GetRange in ability/hacker/simulacra.py supplies a range in centimetres.
            // MessageCloud_DirectObject sets SelectionRangeAbility to the percentage decrease.
            double range = id == ChargeSimulacra
                ? 50.0 * (100 - Math.Clamp(AbilityStats.Player(pet.Owner, 214), 0, 100)) / 100
                : AbilityCatalog.Find(id)?.Number("Ability/SelectionRange", 16) ?? 16;
            return OwnerDistanceMetres(pet) <= range;
        }

        /// <summary>The cast finished.</summary>
        internal static void Land(WorldClient caster, UInt16 id, OwnPetCast cast, bool summonUnloaded = false)
        {
            if (Summons.ContainsKey(id))
            {
                lock (caster.playerData.craftingSync)
                {
                    if (!summonUnloaded && AbilityPassives.IsLoaded(caster, id))
                    {
                        Pet summoned = SpawnForLocked(caster, id);
                        if (summoned != null && (id == 594 || id == 616 || id == 617))
                        {
                            // These shipped callbacks notify their owner. The localized string ID
                            // is unavailable, so use the stock-visible private system channel.
                            new ServerPackets().sendSystemChatMessage(caster,
                                $"You summoned a level {summoned.Mob.getLevel()} {summoned.Mob.getName()}.", "BROADCAST");
                        }
                    }
                    else if (StarterSummonRequiresLoaded(id))
                        Output.WriteDebugLog($"[PET] {id} fizzles: the summon was unloaded during the cast");
                }
                return;
            }
            Pet pet = cast?.Target;
            if (pet == null) return;
            lock (caster.playerData.craftingSync)
            lock (pet)
            {
                if (!cast.Matches(caster, Of(caster)) || !OwnerPresent(pet) || !InAbilityRange(pet, id) ||
                    (HasVisibilityRequirement(id) && !CombatSight.Between(caster, pet.Mob)) ||
                    (ViolatesOutsideCombat(caster, id)))
                {
                    Output.WriteDebugLog("[PET] " + id + " fizzles: original pet, owner, range or combat requirement changed");
                    return;
                }
                if (!IsCastLoaded(caster, id) || ContinuousLoadAbilities.Contains(id) &&
                    (!cast.LoadedSince.HasValue || cast.LoadedSince != AbilityPassives.PetCastMembership(caster, id)))
                {
                    Output.WriteDebugLog($"[PET] {id} fizzles: the ability was unloaded during the cast");
                    return;
                }
                LandOnPet(caster, pet, id, cast.CasterLevel);
            }
        }

        private static void LandOnPet(WorldClient caster, Pet pet, UInt16 id, int casterLevel)
        {
            if (pet.Gone || pet.Mob.getIsDead()) return;
            double now = TimeUtils.GetSimTimeSeconds();
            var entry = AbilityCatalog.Find(id);
            if (Repairs.Contains(id))
            {
                int amount = FixedHeal(caster, entry);
                HealPet(pet, amount, FirstFx(entry));
                Output.WriteDebugLog("[PET] Repair " + id + " heals " + pet.Mob.getName() + " for " + amount);
            }
            else if (id == FortifySimulacra)
            {
                // Native function 3: Max Health level*15 and four resistance scores level*2 for600s.
                pet.FortifyHealth = (UInt16)(casterLevel * 15);
                pet.FortifyResistance = casterLevel * 2;
                pet.FortifyUntil = now + 600;
                ApplyMaxHealth(pet);
                SendHit(pet, FirstFx(entry));
                Output.WriteDebugLog("[PET] Fortify Simulacra (20) pet=" + pet.SimulacraId + " maxHP=" + pet.Mob.getHealthM() +
                    " resistance=" + pet.FortifyResistance + " duration=600");
            }
            else if (id == PowerBoost)
            {
                ApplyPowerBoost(pet, casterLevel, now);
                SendHit(pet, FirstFx(entry));
                Output.WriteDebugLog("[PET] Power Boost (25) pet=" + pet.SimulacraId + " melee=" + pet.PowerMeleeDamage +
                    " ballistic=" + pet.PowerBallisticDamage + " duration=300");
            }
            else if (id == ChargeSimulacra)
            {
                // Sourced 20% cost reduction / 50% regen bonus for 600 s; see explicit OURS baseline below.
                Regenerate(pet, now);
                pet.ChargeUntil = now + 600;
                Output.WriteDebugLog("[PET] Charge Simulacra (603) pet=" + pet.SimulacraId + " costReduction=20% regenIncrease=50% duration=600");
            }
            else if (id == ProxyMasterGrant)
            {
                ApplyProxyMasterResources(pet, now);
                SendHit(pet, FirstFx(entry));
                AbilityHostile.ApplySweepToSubject(caster, TimedEffects.ForMob(pet.Mob), id);
                var (reduction, regen, healthRegen, duration) = proxyMasterResources.Value;
                Output.WriteDebugLog($"[PET] Proxy Master (1107) owner={caster.playerData.getCharID()} pet={pet.SimulacraId} " +
                    $"costReduction={reduction}% regenIncrease={regen}% duration={duration} healthRegenIncrease={healthRegen}%");
            }
            else if (id == DetonateProxy)
            {
                Detonate(pet);
            }
            else if (id == AbsorbSimulacra)
            {
                var (damage, healing) = AbsorbValues(casterLevel);
                if (damage <= 0 || healing == 0) return;
                UInt16 petHealth = pet.Mob.getHealthC();
                UInt16 ownerHealth = PvpCombat.HealthOf(caster);
                DamagePetLocked(pet, damage, 0);
                UInt16 healed = AbilityHandler.HealSelf(caster, healing);
                Output.WriteDebugLog($"[PET] Absorb Simulacra (607) owner={caster.playerData.getCharID()} pet={pet.SimulacraId} type=5 raw={damage} HP {petHealth}->{pet.Mob.getHealthC()}");
                Output.WriteDebugLog($"[VITALS] char={caster.playerData.getCharID()} ability=607 type=6 raw={healing} HP {ownerHealth} -> {healed}");
            }
            else if (id == DrainCode)
            {
                // Drain Code's target health effect and caster IS effect are separate instant GOB records.
                var hit = entry.Modifiers.Single(mod => mod.Target == 108 && mod.Number("WhenEnabled") == 2);
                var restore = entry.Modifiers.Single(mod => mod.Target == 111 && mod.Number("WhenEnabled") == 1);
                int damage = (int)hit.Number("MinValue");
                int gain = (int)restore.Number("MinValue");
                ushort health = pet.Mob.getHealthC();
                DamagePetLocked(pet, damage, 0);
                UInt16 max = AbilityHandler.MaxInnerStrength(caster);
                UInt16 current = AbilityHandler.CurrentInnerStrength(caster);
                UInt16 next = (UInt16)Math.Min(max, current + gain);
                caster.playerInstance.InnerStrengthAvailable.setValue(next);
                Output.WriteDebugLog($"[PET] Drain Code (19) pet={pet.SimulacraId} type={hit.Number("DamageType")} " +
                    $"damage={damage} HP {health}->{pet.Mob.getHealthC()} IS {current}->{next}");
                new ServerPackets().sendISCurrent(caster, next);
                caster.FlushQueue();
            }
        }

        private static UInt32 FirstFx(AbilityCatalog.Entry entry) =>
            (UInt32)(entry?.Modifiers.Select(m => m.Number("ActivationFX")).FirstOrDefault(n => n > 0) ?? 0);

        internal static (int Damage, ushort Healing) AbsorbValues(int casterLevel)
        {
            // 607's direct object receives neutral damage 5. The caster independently receives NeutralHeal 6.
            var modifiers = AbilityCatalog.Find(AbsorbSimulacra).Modifiers;
            var damage = modifiers.Single(mod => mod.Target == 108 && mod.Number("WhenEnabled") == 2 &&
                mod.Number("DamageType") == 5 && mod.Number("DurationType") == 1);
            var healing = modifiers.Single(mod => mod.Target == 108 && mod.Number("WhenEnabled") == 1 &&
                mod.Number("DamageType") == 6 && mod.Number("DurationType") == 1);
            AbilityValues.TryValue(damage, 0, casterLevel, out int hit);
            AbilityValues.TryValue(healing, 0, casterLevel, out int heal);
            return (Math.Max(0, hit), (ushort)Math.Clamp(heal, 0, ushort.MaxValue));
        }

        /// <summary>Repair/Fix Simulacra's stored fixed heal, with the caster's healing influence (as AbilityHealing does for players).</summary>
        public static int FixedHeal(WorldClient caster, AbilityCatalog.Entry entry)
        {
            if (entry == null) return 0;
            int amount = 0;
            foreach (var mod in entry.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2 && m.Target == 108 && m.Number("DamageType", -1) == 3))
                if (AbilityValues.TryValue(mod, 50, caster.playerInstance.Level.getValue()[0], out int value)) amount += value;
            float influence = (float)Math.Max(0, 1 + AbilityStats.Influence(caster, 174) / 100.0);
            return (int)(amount * (double)influence);
        }

        private static void Detonate(Pet pet)
        {
            int blast = DetonateValue(pet.Mob.getHealthC(), pet.Mob.getHealthM());
            var entry = AbilityCatalog.Find(StopProxyEffect);
            var parent = AbilityCatalog.Find(DetonateProxy).Modifiers.Single(modifier =>
                modifier.Text("StatModTermFunc") == "DetonateProxyEnd");
            double radius = entry?.Number("Ability/RadiusOfEffect", 8) ?? 8;
            UInt32 fx = (UInt32)(entry?.Number("Ability/ExecutionFX", 0) ?? 0);
            var hit = new List<Mob>();
            lock (WorldServer.mobs.SyncRoot)
                foreach (Mob mob in WorldServer.mobs)
                {
                    if (mob == pet.Mob || mob.isFriendly || mob.pet != null || mob.getIsDead() || mob.getDistrict() != pet.Mob.getDistrict()) continue;
                    if (AbilityTargets.WithinArea(pet.Mob.getXPos(), pet.Mob.getYPos(), pet.Mob.getZPos(), mob.getXPos(), mob.getYPos(), mob.getZPos(), radius))
                        hit.Add(mob);
                }
            int landed = 0;
            foreach (Mob mob in hit)
            {
                if (mob.missionInstance != 0 && MissionRuntime.FindMissionMob(pet.Owner.playerData, mob.getEntityId()) != mob)
                    continue;
                ushort damage = DetonateDamage(pet, mob, blast);
                // A pet-centred area can reach an NPC outside the owner's current view list.
                // Credit the pet owner through the ordinary damage/death path either way.
                var view = pet.Owner.viewMan.FindViewForEntity(mob.getEntityId());
                ushort viewId = view?.viewCreated == true && !view.viewNeedsToBeDeleted ? view.ViewID : (ushort)0;
                if (CombatManager.GetHandler(pet.Owner).ApplyAbilityToNpc(mob, viewId, damage, fx))
                    landed++;
            }
            var owner = pet.Owner;
            string source = TimedEffects.PlayerName(owner);
            foreach (var victim in AbilityHostile.PlayersInArea(owner, pet.Mob.getXPos(), pet.Mob.getYPos(), pet.Mob.getZPos(), radius))
            {
                if (PvpCombat.Check(owner, victim) != PvpRules.Verdict.Allowed || PvpCombat.HealthOf(victim) == 0) continue;
                ushort damage = TypedAbilityResistance.Player(pet.Mob.getLevel(), victim, StopProxyEffect, blast);
                string target = TimedEffects.PlayerName(victim);
                PvpFlag.OnFightStarts(owner, true);
                victim.messageQueue.addRpcMessage(AbilityMessages.Damage(false, DetonateProxy, source, target, damage, blast - damage));
                owner.messageQueue.addRpcMessage(AbilityMessages.Damage(true, DetonateProxy, source, target, damage, blast - damage));
                if (PvpCombat.Damage(victim, damage, fx)) PvpCombat.Defeated(victim, owner);
                victim.FlushQueue();
                owner.FlushQueue();
                Output.WriteDebugLog($"[PET] Detonate Proxy player source={source} target={target} raw={blast} damage={damage}");
            }
            Output.WriteDebugLog(FormattableString.Invariant($"[PET] Detonate Proxy: {blast} to {landed} NPC(s) within {radius} m, pet {pet.SimulacraId}, start {TimeUtils.GetSimTimeSeconds():F3}"));
            // DetonateProxyEnd, when the 0.5 s tempmod ends: 13000 viral to the simulacrum
            DelayedActions.After(parent.Number("Duration"), () =>
            {
                lock (pet)
                {
                    if (pet.Gone || pet.Mob.getIsDead()) return;
                    Output.WriteDebugLog(FormattableString.Invariant($"[PET] Detonate Proxy term: pet {pet.SimulacraId}, at {TimeUtils.GetSimTimeSeconds():F3}"));
                    DamagePetLocked(pet, 13000, 0);
                }
            });
        }

        internal static ushort DetonateDamage(Pet pet, Mob victim, int blast) =>
            TypedAbilityResistance.Npc(pet.Mob.getLevel(), victim, StopProxyEffect, blast);

        // ---- Health ----------------------------------------------------------------------------------------------

        internal static void ShowAuraEffect(Pet pet, UInt32 fx) => SendHit(pet,fx);

        private static void SendHit(Pet pet, UInt32 fx)
        {
            var pak = new PacketContent();
            pak.AddByteArray(Mob.BuildHitUpdate(pet.Mob.getHealthC(), fx, ++pet.Counter));
            Store.world?.SendViewUpdateToClientsWhoHasMobSpawned(pak, pet.Mob);
            if (pet.Owner != null && !pet.Mob.getIsDead()) SendWindow(pet, false);
        }

        /// <summary>NPC view MaxHealth (update attribute 16) and Health (27): 04 80 80 84 &lt;max u16&gt; 40 &lt;health u16&gt;.</summary>
        public static byte[] BuildHealthUpdate(UInt16 health, UInt16 max)
        {
            var pak = new PacketContent();
            pak.AddHexBytes("04808084");
            pak.AddUint16(max, 1);
            pak.AddHexBytes("40");
            pak.AddUint16(health, 1);
            return pak.ReturnFinalPacket();
        }

        private static void ApplyMaxHealth(Pet pet)
        {
            double now = TimeUtils.GetSimTimeSeconds();
            UInt16 max = (UInt16)Math.Min(UInt16.MaxValue, pet.BaseMaxHealth + (now < pet.FortifyUntil ? pet.FortifyHealth : 0));
            ushort previousMax = pet.Mob.getHealthM();
            if (max == previousMax) return;
            pet.Mob.setHealthM(max);
            Output.WriteDebugLog($"[VITALS] owner={pet.Owner.playerData.getCharID()} pet={pet.SimulacraId} maxHP {previousMax} -> {max}");
            if (pet.Mob.getHealthC() > max) pet.Mob.setHealthC(max);
            var pak = new PacketContent();
            pak.AddByteArray(BuildHealthUpdate(pet.Mob.getHealthC(), max));
            Store.world?.SendViewUpdateToClientsWhoHasMobSpawned(pak, pet.Mob);
            SendWindow(pet, false);
        }

        public static void HealPet(Pet pet, int amount, UInt32 fx)
        {
            if (pet == null) return;
            lock (pet) HealPetLocked(pet, amount, fx);
        }

        private static void HealPetLocked(Pet pet, int amount, UInt32 fx)
        {
            if (pet.Gone || pet.Mob.getIsDead() || amount <= 0) return;
            pet.Mob.setHealthC((UInt16)Math.Min(pet.Mob.getHealthM(), pet.Mob.getHealthC() + amount));
            SendHit(pet, fx);
        }

        public static void DamagePet(Pet pet, int amount, UInt32 fx)
        {
            if (pet == null) return;
            lock (pet) DamagePetLocked(pet, amount, fx);
        }

        private static void DamagePetLocked(Pet pet, int amount, UInt32 fx)
        {
            if (pet.Gone || pet.Mob.getIsDead()) return;
            int left = Math.Max(0, pet.Mob.getHealthC() - amount);
            pet.Mob.setHealthC((UInt16)left);
            if (left == 0)
            {
                KillLocked(pet, fx);
                return;
            }
            SendHit(pet, fx);
        }

        /// <summary>Damage from the simulacrum to an NPC, landed through its owner's fight so death, experience and loot go the usual way.</summary>
        private static void DamageForOwner(Pet pet, Mob target, UInt16 damage, UInt32 fx)
        {
            ClientView view = pet.Owner.viewMan.FindViewForEntity(target.getEntityId());
            if (view == null || !view.viewCreated)
            {
                target.HitEnemyWithDamage(damage, fx);
                return;
            }
            CombatManager.GetHandler(pet.Owner).ApplyAbilityToNpc(target, view.ViewID, damage, fx);
        }

        // ---- AI --------------------------------------------------------------------------------------------------

        private static void EnsureTimer()
        {
            lock (pets)
            {
                if (timer == null) timer = new Timer(_ => TickAll(), null, (int)(TickSeconds * 1000), (int)(TickSeconds * 1000));
            }
        }

        private static void TickAll()
        {
            try
            {
                double now = TimeUtils.GetSimTimeSeconds();
                List<Pet> all;
                lock (pets) all = pets.Values.ToList();
                foreach (Pet pet in all) Tick(pet, now);
            }
            catch (Exception e)
            {
                Output.WriteDebugLog("[PET] Tick failed: " + e);
            }
        }

        public static bool OwnerPresent(Pet pet) =>
            pet.Owner != null && pet.Owner.Alive && pet.Owner.playerData.getOnWorld() && !pet.Owner.playerData.worldChangePending &&
            pet.Owner.playerData.getDistrictId() == pet.Mob.getDistrict();

        internal static bool AuraOwnerReady(WorldClient owner) => owner != null && owner.Alive && owner.playerData.getOnWorld() &&
            !owner.playerData.worldChangePending && !owner.playerData.DistrictIsLA() && !owner.playerData.isDead && !owner.playerData.duelDeath;

        internal static bool FriendlyAuraRecipient(WorldClient source, Pet pet, double radius) =>
            AuraOwnerReady(source) && pet != null && AuraOwnerReady(pet.Owner) && ReferenceEquals(Of(pet.Owner), pet) &&
            !pet.Gone && !pet.Mob.getIsDead() && pet.Mob.getDistrict() == source.playerData.getDistrictId() &&
            pet.Owner.playerData.getDistrictId() == source.playerData.getDistrictId() && AbilitySupport.FriendlyRecipient(source, pet.Owner) &&
            CombatCalculator.CalculateDistanceMetres(source.playerInstance.Position.getValue(), pet.Mob.getXPos(), pet.Mob.getYPos(), pet.Mob.getZPos()) <= radius;

        public static void Tick(Pet pet, double now)
        {
            if (pet == null) return;
            lock (pet) TickLocked(pet, now);
        }

        private static void TickLocked(Pet pet, double now)
        {
            ProxyTechnician.Prune(pet,now);
            if (pet.Gone) return;
            if (!OwnerPresent(pet) || OwnerDistanceMetres(pet) > LeashMetres * 4)
            {
                OwnerLeft(pet.Owner);
                return;
            }
            if (pet.Mob.getIsDead()) return;
            Regenerate(pet, now);
            if (pet.FortifyUntil > 0 && now >= pet.FortifyUntil) { pet.FortifyUntil = 0; ApplyMaxHealth(pet); }

            if (pet.Target != null && (!CanPursue(pet, pet.Target) ||
                CloseEntryShield(pet) != null && pet.Target.DeathGeneration != pet.TargetDeathGeneration))
            {
                ReleaseClosePair(pet);
                pet.Target = null;
                if (pet.Order == Command.Attack)
                {
                    pet.Order = Command.Follow;
                    SendWindow(pet, false);
                }
            }
            // Defensive and Aggressive simulacra help with the owner's fight; Aggressive ones also take on nearby hostiles.
            if (pet.Target == null && pet.Order != Command.Stay && pet.Stance != Stance.Passive && pet.Kind != Kind.Healer)
            {
                Mob assist = CombatManager.GetHandler(pet.Owner).CurrentNpcTarget;
                if (!CanPursue(pet, assist))
                    assist = pet.Stance == Stance.Aggressive ? NearestHostile(pet, 10) : null;
                if (assist != null)
                {
                    pet.Target = assist;
                    pet.TargetDeathGeneration = assist.DeathGeneration;
                    Output.WriteDebugLog("[PET] " + pet.Mob.getName() + " assists against " + assist.getName());
                }
            }

            if (pet.Kind == Kind.Healer)
            {
                HealerTick(pet, now);
                if (pet.Order != Command.Stay) MoveToward(pet, OwnerPosition(pet), FollowDistanceMetres, now);
                return;
            }

            if (pet.Target != null)
            {
                double reach = Ranged(pet) ? RangedRangeMetres : MeleeRangeMetres;
                bool inReach = MoveToward(pet, (pet.Target.getXPos(), pet.Target.getYPos(), pet.Target.getZPos()), reach, now,
                    pet.Order == Command.Stay);
                NpcEvasion shield = CloseEntryShield(pet);
                if (!inReach && shield != null)
                {
                    shield.ReleasePet(pet);
                    return;
                }

                if (inReach && now >= pet.NextAttack)
                {
                    pet.NextAttack = now + AttackPeriodSeconds;
                    if (shield != null && !shield.TryOpenPet(pet, pet.Target, now)) return;
                    Attack(pet, pet.Target, now);
                }
                if (shield == null || shield.HasPet(pet)) Retaliate(pet, pet.Target, now);
                return;
            }
            if (pet.Order == Command.Follow) MoveToward(pet, OwnerPosition(pet), FollowDistanceMetres, now);
        }

        /// <summary>
        /// A simulacrum's hit: the NPC round damage model at its level against the target's (our choice; no retail capture shows
        /// a simulacrum's damage), with typed Power Boost/Daemon Boost influence while each lasts.
        /// </summary>
        private static void Attack(Pet pet, Mob target, double now)
        {
            if (!SpendAction(pet, now)) return;
            bool ranged = Ranged(pet);
            int daemonAccuracy = DaemonBoost.Accuracy(pet, now, ranged);
            int technician = ProxyTechnician.Accuracy(pet, now, ranged);
            double roll=ProxyTechnician.AttackRoll(pet,out bool forced);
            double chance = CalculateHitChance(pet, target.getLevel(), technician + daemonAccuracy);
            if(forced || technician!=0) Output.WriteDebugLog("[PROXYTECH] attack owner="+TimedEffects.PlayerName(pet.Owner)+" bonus="+technician+" roll="+roll+" chance="+chance+" hit="+(roll<chance));
            if (daemonAccuracy != 0)
            {
                string channel = pet.Kind == Kind.Nuker ? "viral" : ranged ? "ballistic" : "melee";
                Output.WriteDebugLog($"[DAEMON BOOST] 700 attack owner={TimedEffects.PlayerName(pet.Owner)} channel={channel} accuracy={daemonAccuracy}");
            }
            if (roll >= chance) return;
            UInt16 rawDamage = CombatCalculator.CalculateMobDamageWithTactics(pet.Mob.getLevel(), target.getLevel());
            UInt16 damage = DaemonBoostDamage(pet, PowerBoostDamage(pet, rawDamage, now), now);
            if (damage != rawDamage) Output.WriteDebugLog("[PET] typed influence " + pet.Kind + " base " + rawDamage + " final " + damage);
            UInt32 fx = ranged ? CombatCalculator.GetRandomRangedHitFx() : CombatCalculator.GetRandomMeleeHitFx();
            FaceTowards(pet, target.getXPos(), target.getZPos());
            DamageForOwner(pet, target, damage, fx);
            Output.WriteDebugLog("[PET] " + pet.Mob.getName() + " hits " + target.getName() + " for " + damage);
        }

        /// <summary>An NPC the simulacrum fights, and that isn't fighting a player, hits the simulacrum back each round.</summary>
        private static void Retaliate(Pet pet, Mob target, double now)
        {
            if (target.PassiveTarget || target.getIsDead() || target.IsStunned(now)) return;
            if (OwnerDistanceTo(pet, target) > LeashMetres || Distance(pet.Mob, target.getXPos(), target.getYPos(), target.getZPos()) > RangedRangeMetres * 100) return;
            var mob = pet.Mob;
            ushort district = mob.getDistrict();
            if (NavMesh.Locate(district, mob.getXPos(), mob.getYPos(), mob.getZPos(), out var from) &&
                !GroundLine(district, from, mob.getXPos(), mob.getZPos(), (target.getXPos(), target.getYPos(), target.getZPos())))
                return;
            if (!target.TryRetaliate(now, AttackPeriodSeconds, pet)) return;
            ushort raw = CombatCalculator.CalculateMobDamageWithTactics(target.getLevel(), mob.getLevel());
            int damage = MitigateRetaliation(pet, target.getLevel(), raw, now);
            ushort before = mob.getHealthC();
            DamagePet(pet, damage, CombatCalculator.GetRandomMeleeHitFx());
            int rating = now < pet.FortifyUntil ? pet.FortifyResistance : 0;
            Output.WriteDebugLog(FormattableString.Invariant(
                $"[PET RETALIATION] attacker={target.getName()} attackerLevel={target.getLevel()} pet={pet.SimulacraId} petLevel={mob.getLevel()} meleeRating={rating} raw={raw} final={damage} HP={before}->{mob.getHealthC()} owner={TimedEffects.PlayerName(pet.Owner)} at={now:F3}"));
        }

        /// <summary>Patch Daemon heals its owner when hurt. No retail value is known: it heals the NPC round amount at its level (our choice).</summary>
        private static void HealerTick(Pet pet, double now)
        {
            if (now < pet.NextAttack) return;
            WorldClient owner = pet.Owner;
            if (owner.playerData.isDead || owner.playerData.duelDeath) return;
            UInt16 max = NumericalUtils.ByteArrayToUint16(owner.playerInstance.MaxHealth.getValue(), 1);
            UInt16 current = NumericalUtils.ByteArrayToUint16(owner.playerInstance.Health.getValue(), 1);
            if (current >= max || OwnerDistanceMetres(pet) > 20) return;
            pet.NextAttack = now + AttackPeriodSeconds;
            if (!SpendAction(pet, now)) return;
            int amount = CombatCalculator.CalculateMobDamageWithTactics(pet.Mob.getLevel(), owner.playerInstance.Level.getValue()[0]);
            int rawHealing = amount;
            amount = DaemonBoostHealing(pet, amount, now);
            if (amount != rawHealing) Output.WriteDebugLog("[PET] typed healing base " + rawHealing + " final " + amount);
            UInt16 healed = AbilityHandler.HealSelf(owner, (UInt16)amount);
            Output.WriteDebugLog($"[VITALS] char={owner.playerData.getCharID()} pet={pet.SimulacraId} ability={pet.Ability} HP {current} -> {healed}");
        }

        internal static Pet[] SnapshotPets() { lock(pets) return pets.Values.ToArray(); }

        // ---- Dev command (MXO_DEV_COMMANDS=1) ------------------------------------------------------------------

        public static readonly UInt16[] DevAbilities = { 27, 28, 29, 586, 594, 22, 601, 611, 616, 602, 612, 617, 30, 31, 587, 596, 615,
            20, 25, 603, 34, 19, AbsorbSimulacra, 609, 619, ProxyMasterGrant };

        /// <summary>?pet: casts through the real ability path, summons directly, orders, and a hostile dummy to fight.</summary>
        public static string DevCommand(WorldClient client, string[] args)
        {
            string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "";
            UInt16 ability = 0;
            if (args.Length > 2) UInt16.TryParse(args[2], out ability);
            switch (sub)
            {
                case "cast":
                    if (!Handles(ability)) return "Not a simulacrum ability. ?pet abilities lists them.";
                    // As if the client sent 80 b9 <ability> <view>: cast bar, cost, cooldown, cast time and the landing
                    UInt16 view = args.Length > 3 && UInt16.TryParse(args[3], out UInt16 v) ? v : (UInt16)0;
                    byte[] packet = { (byte)ability, (byte)(ability >> 8), (byte)view, (byte)(view >> 8) };
                    client.playerData.lastAbilityId = 0;
                    new AbilityHandler().ProcessAbility(ref packet);
                    return "Casting " + ability + ".";
                case "summon":
                    if (!Summons.ContainsKey(ability)) return "?pet summon <27|28|29|586|594|22|601|611|616|602|612|617>";
                    Pet pet = SpawnFor(client, ability);
                    if (pet == null) return "The simulacrum was not summoned.";
                    return pet.Mob.getName() + " (level " + pet.Mob.getLevel() + ", " + pet.Mob.getHealthM() + " health) is next to you.";
                case "dummy":
                    Mob dummy = SpawnDummy(client, args.Length > 2 && int.TryParse(args[2], out int lvl) ? lvl : 0);
                    return dummy.getName() + " (level " + dummy.getLevel() + ") stands 8 m away.";
                case "follow": return Order(client, CurrentSimulacrum, Command.Follow, 0, 0) ?? "Following.";
                case "stay": return Order(client, CurrentSimulacrum, Command.Stay, 0, 0) ?? "Staying.";
                case "dismiss": return Order(client, CurrentSimulacrum, Command.Deactivate, 0, 0) ?? "Dismissed.";
                case "attack": return Order(client, CurrentSimulacrum, Command.Attack, 0, 0) ?? "Attacking.";
                case "defensive": return Order(client, CurrentSimulacrum, Command.Mode, 0, (byte)Stance.Defensive) ?? "Defensive.";
                case "aggressive": return Order(client, CurrentSimulacrum, Command.Mode, 0, (byte)Stance.Aggressive) ?? "Aggressive.";
                case "passive": return Order(client, CurrentSimulacrum, Command.Mode, 0, (byte)Stance.Passive) ?? "Passive.";
                case "exhaust":
                    Pet exhausted = Of(client);
                    if (exhausted == null) return "No simulacrum.";
                    lock (exhausted) { exhausted.InnerStrength = 0; exhausted.LastResourceTick = TimeUtils.GetSimTimeSeconds(); }
                    return "Simulacrum IS exhausted (dev fixture).";
                case "master-wound":
                    if (Environment.GetEnvironmentVariable(ProxyMasterSweepQa) != "1" ||
                        (client.playerData.getCharID() != 110 && client.playerData.getCharID() != 6111) ||
                        !QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                            Store.config?.dbParams?.DatabaseName, client.playerData.getCharID(),
                            client.playerData.getOnWorld() && !client.playerData.worldChangePending,
                            client.playerData.isDead || client.playerData.duelDeath))
                        return "Proxy Master wound preparation is available only in its disposable fixture.";
                    Pet wounded = Of(client);
                    if (wounded == null) return "No simulacrum.";
                    lock (wounded)
                    {
                        if (wounded.Gone || wounded.Mob.getIsDead()) return "No simulacrum.";
                        wounded.Mob.setHealthC((ushort)Math.Max(1, wounded.Mob.getHealthM() / 2));
                        SendHit(wounded, 0);
                        // Use the actual hostile Slow record, with no caster/aggro side effects from setup.
                        if (!TempMods.TryGet(86, out var slow)) return "Fixture Slow definition is missing.";
                        TimedEffects.Apply(TimedEffects.ForMob(wounded.Mob), slow, TimeUtils.GetSimTimeSeconds());
                        return $"Proxy Master wound ready: owner={client.playerData.getCharID()} pet={wounded.SimulacraId} HP={wounded.Mob.getHealthC()}/{wounded.Mob.getHealthM()}, Slow 86.";
                    }
                case "meter":
                    if (Environment.GetEnvironmentVariable(ProxyMasterMeterQa) != "1" ||
                        !QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                            Store.config?.dbParams?.DatabaseName, client.playerData.getCharID(),
                            client.playerData.getOnWorld() && !client.playerData.worldChangePending,
                            client.playerData.isDead || client.playerData.duelDeath))
                        return "The pet resource meter is available only in its disposable fixture.";
                    Pet measured = Of(client);
                    if (measured == null) return "No simulacrum.";
                    lock (measured)
                    {
                        if (measured.Gone || measured.Mob.getIsDead()) return "No simulacrum.";
                        string sample = FormattableString.Invariant($"[PET METER] owner={client.playerData.getCharID()} pet={measured.SimulacraId} tick={measured.LastResourceTick:F3} IS={measured.InnerStrength:F3} cost={ActionCost(measured, measured.LastResourceTick):F2} HP={measured.Mob.getHealthC()}/{measured.Mob.getHealthM()}");
                        Output.WriteDebugLog(sample);
                        return sample;
                    }
                case "fixture-range":
                    if (Environment.GetEnvironmentVariable(ChargeRangeQa) != "1" ||
                        client.playerData.getCharID() != 110 ||
                        !QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                            Store.config?.dbParams?.DatabaseName, client.playerData.getCharID(),
                            client.playerData.getOnWorld() && !client.playerData.worldChangePending,
                            client.playerData.isDead || client.playerData.duelDeath))
                        return "Charge range preparation is available only in its disposable fixture.";
                    // Holds the value supplied by MessageCloud_DirectObject long enough for the stock range comparison.
                    TimedEffects.Apply(TimedEffects.ForPlayer(client), new TempMods.Effect { Ability = 214, Mods = new[]
                    {
                        new TempMods.Mod { Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = 214,
                            MinValue = 80, MaxValue = 80, Seconds = 120 }
                    } }, TimeUtils.GetSimTimeSeconds());
                    return "Selection range reduced by 80 percent for 120 seconds.";
                case "fixture-expire-fortify":
                    if (Environment.GetEnvironmentVariable(FortifyExpiryQa) != "1" ||
                        !QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                            Store.config?.dbParams?.DatabaseName, client.playerData.getCharID(),
                            client.playerData.getOnWorld(), client.playerData.isDead))
                        return "Fortify expiry is available only in its disposable fixture.";
                    var fortified = Of(client);
                    if (fortified == null) return "No simulacrum.";
                    lock (fortified)
                    {
                        double now = TimeUtils.GetSimTimeSeconds();
                        if (fortified.Gone || fortified.Mob.getIsDead() || fortified.FortifyUntil <= now)
                            return "The simulacrum has no active Fortify.";
                        fortified.FortifyUntil = now;
                    }
                    return "Fortify expiry armed for the next pet tick.";
                case "abilities":
                    return "Simulacrum abilities: " + string.Join(", ", DevAbilities.Select(a => a + " " + (DataLoader.getInstance().getAbilityByID(a)?.getAbilityName() ?? "?")));
                default:
                {
                    Pet current = Of(client);
                    if (current == null)
                        return "No simulacrum. ?pet cast|summon <ability>, ?pet dummy [level], ?pet follow|stay|attack|dismiss|defensive|aggressive|passive, ?pet abilities";
                    double now = TimeUtils.GetSimTimeSeconds();
                    var daemonBoost = DaemonBoost.Values(current, now);
                    return current.Mob.getName() + " L" + current.Mob.getLevel() + " " + current.Mob.getHealthC() + "/" + current.Mob.getHealthM() +
                        " HP, " + Math.Round(current.InnerStrength, 1) + "/" + current.MaxInnerStrength + " IS, " + current.Order +
                        ", " + current.Stance + ", cost " + ActionCost(current, now) +
                        ", resistance " + (now < current.FortifyUntil ? current.FortifyResistance : 0) +
                        ", aura viral/heal " + daemonBoost.Viral + "/" + daemonBoost.Healing +
                        ", power melee/ballistic " + (now < current.PowerBoostUntil ? current.PowerMeleeDamage : 0) + "/" +
                        (now < current.PowerBoostUntil ? current.PowerBallisticDamage : 0) +
                        ", accuracy " + DaemonBoost.Accuracy(current, now, Ranged(current)) +
                        (current.Target != null ? ", fighting " + current.Target.getName() : "") +
                        ", technician melee/ballistic " + ProxyTechnician.Accuracy(current, now, false) + "/" + ProxyTechnician.Accuracy(current, now, true);
                }
            }
        }

        /// <summary>A hostile NPC that doesn't start fights, 8 m in front of the player, for a simulacrum to fight.</summary>
        public static Mob SpawnDummy(WorldClient client, int level)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            int lvl = level > 0 ? Math.Min(level, 99) : client.playerInstance.Level.getValue()[0];
            var mob = new Mob();
            mob.setEntityId(WorldServer.entityIdCounter++);
            mob.setDistrict((UInt16)client.playerData.getDistrictId());
            mob.setDistrictName(client.playerData.getDistrict());
            mob.setName("Simulacra Dummy");
            mob.setLevel((UInt16)lvl);
            UInt16 hp = (UInt16)(150 + 40 * lvl);
            mob.setHealthM(hp);
            mob.setHealthC(hp);
            mob.setMobId(0x0556);
            mob.setRsiHex(WireHex(0x56050004)); // the "HD Protector" model ?spawnmob uses
            z += 800;
            mob.setXPos(x); mob.setYPos(y); mob.setZPos(z);
            mob.xBase = x; mob.yBase = y; mob.zBase = z;
            mob.setRotation(0);
            mob.setIsDead(false);
            mob.setIsLootable(false);
            mob.aggroKind = MobAggroKind.Passive;
            lock (WorldServer.mobs.SyncRoot) WorldServer.mobs.Add(mob);
            lock (WorldServer.gameServerEntities.SyncRoot) WorldServer.gameServerEntities.Add(mob);
            return mob;
        }

        // ---- Movement --------------------------------------------------------------------------------------------

        private static (double x, double y, double z) OwnerPosition(Pet pet)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(pet.Owner.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return (x, y, z);
        }

        private static double Distance(Mob mob, double x, double y, double z)
        {
            double dx = mob.getXPos() - x, dy = mob.getYPos() - y, dz = mob.getZPos() - z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static double OwnerDistanceMetres(Pet pet)
        {
            var (x, y, z) = OwnerPosition(pet);
            return Distance(pet.Mob, x, y, z) / 100.0;
        }

        private static double OwnerDistanceTo(Pet pet, Mob mob)
        {
            var (x, y, z) = OwnerPosition(pet);
            return Distance(mob, x, y, z) / 100.0;
        }

        private static void FaceTowards(Pet pet, double x, double z)
        {
            double angle = Math.Atan2(x - pet.Mob.getXPos(), z - pet.Mob.getZPos());
            pet.Mob.setRotation((uint)((int)Math.Round(angle * 128 / Math.PI) & 0xff));
        }

        private static void SetStanding(Pet pet)
        {
            pet.Walking = false;
            pet.Mob.updateAnimation((byte)Mob.Animations.STAND);
        }

        /// <summary>Follow a walkable path without treating a nearby point through a wall as reached.</summary>
        private static bool MoveToward(Pet pet, (double x, double y, double z) goal, double stopMetres, double now, bool stay = false)
        {
            Mob mob = pet.Mob;
            double x = mob.getXPos(), y = mob.getYPos(), z = mob.getZPos();
            ushort district = mob.getDistrict();
            if (!NavMesh.Locate(district, x, y, z, out NavLocation origin))
                return MoveWithoutNav(pet, goal, stopMetres, stay);

            double distance = Distance(mob, goal.x, goal.y, goal.z), stop = stopMetres * 100;
            if (!NavMesh.Locate(district, goal.x, goal.y, goal.z, out NavLocation destination) ||
                Math.Abs(origin.floorY - destination.floorY) > 150)
            {
                pet.Route = null;
                if (pet.Walking) SetStanding(pet);
                return false;
            }

            bool direct = distance <= stop
                ? GroundLine(district, origin, x, z, goal)
                : NavMesh.HasSightPath(district, x, y, z, goal.x, goal.y, goal.z);
            if (direct && distance <= stop)
            {
                pet.Route = null;
                if (pet.Walking) SetStanding(pet);
                return true;
            }
            if (stay) return false;

            double advance = Math.Min(StepMetres * 100, distance - stop * 0.8);
            NavLocation reached;
            if (direct && GroundStep(pet, origin, x, z, goal.x, goal.z, advance, out reached))
                pet.Route = null;
            else if (!PetWaypoint(pet, (x, y, z), goal, now, out NavPoint waypoint) ||
                !GroundStep(pet, origin, x, z, waypoint.x, waypoint.z, StepMetres * 100, out reached))
            {
                if (pet.Route?.Length > 0)
                {
                    pet.Route = Array.Empty<NavPoint>();
                    pet.RetryRouteAt = now + RouteRetrySeconds;
                }
                if (pet.Walking) SetStanding(pet);
                return false;
            }

            bool inReach = Distance(mob, goal.x, goal.y, goal.z) <= stop &&
                GroundLine(district, reached, mob.getXPos(), mob.getZPos(), goal);
            pet.Walking = !inReach;
            pet.Mob.updateAnimation((byte)(inReach ? Mob.Animations.STAND : Mob.Animations.WALK));
            return inReach;
        }

        internal static bool PetWaypoint(Pet pet, (double x, double y, double z) from, (double x, double y, double z) goal,
            double now, out NavPoint point)
        {
            var old = pet.RouteTarget;
            bool goalMoved = Math.Pow(old.X - goal.x, 2) + Math.Pow(old.Y - goal.y, 2) + Math.Pow(old.Z - goal.z, 2) >=
                RouteReplanDistance * RouteReplanDistance;
            if (pet.Route == null || goalMoved || (pet.RouteIndex >= pet.Route.Length && now >= pet.RetryRouteAt))
            {
                pet.Route = NavMesh.WalkingPath(pet.Mob.getDistrict(), new[] { from.x, from.y, from.z }, new[] { goal.x, goal.y, goal.z });
                pet.RouteTarget = goal;
                pet.RouteIndex = 0;
                pet.RetryRouteAt = now + RouteRetrySeconds;
            }
            while (pet.RouteIndex < pet.Route.Length)
            {
                point = pet.Route[pet.RouteIndex];
                if (Math.Pow(point.x - from.x, 2) + Math.Pow(point.z - from.z, 2) >= 0.01) return true;
                pet.RouteIndex++;
            }
            point = default;
            return false;
        }

        private static bool GroundLine(ushort district, NavLocation from, double x, double z, (double x, double y, double z) goal) =>
            NavMesh.Locate(district, goal.x, goal.y, goal.z, out NavLocation target) &&
            Math.Abs(target.floorY - from.floorY) <= 150 &&
            NavMesh.WalkableLine(district, from, x, z, goal.x, goal.z, out NavLocation end) &&
            (end.sector, end.region, end.indoors) == (target.sector, target.region, target.indoors);

        private static bool GroundStep(Pet pet, NavLocation origin, double x, double z, double tx, double tz, double advance,
            out NavLocation reached)
        {
            reached = origin;
            double dx = tx - x, dz = tz - z, length = Math.Sqrt(dx * dx + dz * dz);
            if (length < 0.01) return false;
            double fraction = Math.Min(1, advance / length);
            double nx = x + dx * fraction, nz = z + dz * fraction;
            if (!NavMesh.WalkableLine(pet.Mob.getDistrict(), origin, x, z, nx, nz, out reached)) return false;
            FaceTowards(pet, nx, nz);
            pet.Mob.setXPos(nx);
            pet.Mob.setYPos(reached.floorY + 95);
            pet.Mob.setZPos(nz);
            return true;
        }

        private static bool MoveWithoutNav(Pet pet, (double x, double y, double z) goal, double stopMetres, bool stay)
        {
            pet.Route = null;
            double d = Distance(pet.Mob, goal.x, goal.y, goal.z) / 100.0;
            if (d <= stopMetres) return true;
            if (stay) return false;
            double step = Math.Min(StepMetres, d - stopMetres * 0.8);
            double f = step / d;
            FaceTowards(pet, goal.x, goal.z);
            pet.Mob.setXPos(pet.Mob.getXPos() + (goal.x - pet.Mob.getXPos()) * f);
            pet.Mob.setYPos(pet.Mob.getYPos() + (goal.y - pet.Mob.getYPos()) * f);
            pet.Mob.setZPos(pet.Mob.getZPos() + (goal.z - pet.Mob.getZPos()) * f);
            pet.Walking = d - step > stopMetres;
            pet.Mob.updateAnimation((byte)(pet.Walking ? Mob.Animations.WALK : Mob.Animations.STAND));
            return !pet.Walking;
        }
    }
}
