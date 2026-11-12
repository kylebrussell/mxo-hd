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

        public const UInt16 PowerBoost = 25;
        public const UInt16 DrainCode = 19, FortifySimulacra = 20, DetonateProxy = 34, StopProxyEffect = 1109, ChargeSimulacra = 603,
            DaemonBoost1 = 609, DaemonBoost2 = 619, DaemonBoostEffect1 = 699, DaemonBoostEffect2 = 700;
        public static readonly UInt16[] Repairs = { 30, 31, 587, 596, 615 };

        /// <summary>Abilities whose target must be the caster's own simulacrum (CastValidTarget 11; Decoy1_Test/StopProxy1_Test).</summary>
        public static bool TargetsOwnPet(UInt16 id) => id == DrainCode || id == FortifySimulacra || id == DetonateProxy ||
            id == ChargeSimulacra || id == PowerBoost || Repairs.Contains(id);

        public static bool IsDaemonBoost(UInt16 id) => id == DaemonBoost1 || id == DaemonBoost2;

        public static bool Handles(UInt16 id) => Summons.ContainsKey(id) || TargetsOwnPet(id) || IsDaemonBoost(id);

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
            public double NextAttack;
            public double NextRetaliation;
            public UInt16 BaseMaxHealth;
            public double FortifyUntil;
            public UInt16 FortifyHealth;
            public double ChargeUntil;
            // OURS: retail pet sheet IS/cost/regen values are not established. A real bounded pool makes 603 meaningful.
            public double InnerStrength = 100, MaxInnerStrength = 100, LastResourceTick;
            public int FortifyResistance, BoostAccuracy;
            public double PowerBoostUntil;
            public int PowerMeleeDamage, PowerBallisticDamage;
            public double BoostUntil;
            public int BoostViralDamage, BoostHealing;
            public bool Gone, WindowRemoved;
            public byte Counter;
        }

        private static readonly Dictionary<UInt32, Pet> pets = new Dictionary<UInt32, Pet>();
        private static readonly Dictionary<UInt32, byte> simulacraCounters = new Dictionary<UInt32, byte>();
        private static readonly Dictionary<UInt32, double> nextBoostPulse = new Dictionary<UInt32, double>();
        private static readonly Dictionary<UInt32, UInt16> boosts = new Dictionary<UInt32, UInt16>();
        private static readonly Random random = new Random();
        private static Timer timer;

        public const double TickSeconds = 1.0;
        /// <summary>Seconds between a simulacrum's attacks. Client var Simulacra_AttackActionPeriod exists; its value is unknown.</summary>
        public const double AttackPeriodSeconds = 3.0;
        public const double FollowDistanceMetres = 2.5, MeleeRangeMetres = 2.5, RangedRangeMetres = 15.0, LeashMetres = 60.0;
        public const double StepMetres = 5.0;

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
            pet.InnerStrength = Math.Min(pet.MaxInnerStrength, pet.InnerStrength + elapsed * 2 + charged);
            pet.LastResourceTick = Math.Max(now, pet.LastResourceTick);
        }

        public static bool SpendAction(Pet pet, double now)
        {
            Regenerate(pet, now);
            double cost = now < pet.ChargeUntil ? 8 : 10;
            if (pet.InnerStrength < cost) return false;
            pet.InnerStrength -= cost;
            return true;
        }

        // OURS combat conversion; all four sourced Fortify resistance scores use the same rating curve.
        public static int Mitigate(Pet pet, int damage, double now) => now < pet.FortifyUntil
            ? (int)Math.Ceiling(damage * 100.0 / (100 + pet.FortifyResistance)) : damage;

        // OURS base roll/level curve; sourced 700 accuracy (level*1.3) now changes the actual attack roll.
        public static double HitChance(Pet pet, int targetLevel, double now) => Math.Clamp(
            75 + (pet.Mob.getLevel() - targetLevel) * 2 + (now < pet.BoostUntil ? pet.BoostAccuracy : 0), 5, 95);

        public sealed class PowerBoostCast
        {
            public readonly Pet Target;
            public readonly int CasterLevel, CasterLife, CasterWorld;
            public PowerBoostCast(WorldClient caster, Pet target)
            {
                Target = target; CasterLevel = caster.playerInstance.Level.getValue()[0];
                CasterLife = caster.playerData.lifeGeneration; CasterWorld = caster.playerData.worldGeneration;
            }
            public bool Matches(WorldClient caster, Pet current) => Target != null && Target == current &&
                Target.Owner == caster && !Target.Gone && !Target.Mob.getIsDead() &&
                caster.playerData.lifeGeneration == CasterLife && caster.playerData.worldGeneration == CasterWorld;
        }

        // A 4s cast remains bound to the original pet and caster lifecycle, never a replacement pet.
        public static PowerBoostCast CapturePowerBoost(WorldClient caster) => new PowerBoostCast(caster, Of(caster));

        public static void LandPowerBoost(WorldClient caster, PowerBoostCast cast)
        {
            Pet pet = cast?.Target;
            if (pet == null) return;
            lock (pet)
            {
                if (!cast.Matches(caster, Of(caster)) || !OwnerPresent(pet) ||
                    Refusal(caster, PowerBoost, 0) != null) return;
                ApplyPowerBoost(pet, cast.CasterLevel, TimeUtils.GetSimTimeSeconds());
                SendHit(pet, FirstFx(AbilityCatalog.Find(PowerBoost)));
            }
        }

        public static void ApplyPowerBoost(Pet pet, int casterLevel, double now)
        {
            var entry = AbilityCatalog.Find(PowerBoost);
            pet.PowerMeleeDamage = pet.PowerBallisticDamage = 0;
            foreach (var modifier in entry.Modifiers)
            {
                if (!AbilityValues.TryValue(modifier, casterLevel, casterLevel, out int value)) continue;
                if (modifier.Target == 159) pet.PowerMeleeDamage = value;
                if (modifier.Target == 171) pet.PowerBallisticDamage = value;
            }
            pet.PowerBoostUntil = now + entry.Modifiers.First(m => m.Target == 159).Number("Duration");
            // ForceCombat40 has no pet forced-interlock/shield consumer; intentionally remains a documented gap.
        }

        /// <summary>159/171 damage influence uses /100, as the existing combat damage pipeline does.
        /// Only proxy melee/firearms qualify: Nuker viral attacks and healer healing receive neither stat.</summary>
        public static UInt16 PowerBoostDamage(Pet pet, int baseDamage, double now)
        {
            int influence = pet.Kind != Kind.Proxy || now >= pet.PowerBoostUntil ? 0 :
                Ranged(pet) ? pet.PowerBallisticDamage : pet.PowerMeleeDamage;
            return (UInt16)Math.Clamp((int)(baseDamage * (1 + influence / 100.0)), 0, UInt16.MaxValue);
        }

        /// <summary>Sourced 699 stats172/174: viral damage/healing influence, not flat generic damage.</summary>
        public static void ApplyDaemonBoost(Pet pet, UInt16 effect, int casterLevel, double now)
        {
            pet.BoostViralDamage = pet.BoostHealing = pet.BoostAccuracy = 0;
            var entry = AbilityCatalog.Find(effect);
            foreach (var modifier in entry.Modifiers)
            {
                if (!AbilityValues.TryValue(modifier, casterLevel, casterLevel, out int value)) continue;
                if (modifier.Target == 172) pet.BoostViralDamage = value;
                if (modifier.Target == 174) pet.BoostHealing = value;
                if (effect == DaemonBoostEffect2) pet.BoostAccuracy = value; // all3 accuracy modifiers share level*1.3
            }
            pet.BoostUntil = now + entry.Modifiers.First().Number("Duration");
        }

        public static UInt16 DaemonBoostDamage(Pet pet, int damage, double now) =>
            (UInt16)Math.Clamp((int)(damage * (1 + (pet.Kind == Kind.Nuker && now < pet.BoostUntil ? pet.BoostViralDamage : 0) / 100.0)), 0, UInt16.MaxValue);

        public static UInt16 DaemonBoostHealing(Pet pet, int healing, double now) =>
            (UInt16)Math.Clamp((int)(healing * (1 + (pet.Kind == Kind.Healer && now < pet.BoostUntil ? pet.BoostHealing : 0) / 100.0)), 0, UInt16.MaxValue);

        // ---- Spawning --------------------------------------------------------------------------------------------

        public static Pet SpawnFor(WorldClient owner, UInt16 ability)
        {
            if (!Summons.TryGetValue(ability, out Summon summon)) return null;
            Dismiss(owner, false);
            int casterLevel = owner.playerInstance.Level.getValue()[0];
            int level = SimLevel(summon, casterLevel);
            string sheet;
            bool female;
            lock (random)
            {
                sheet = summon.Kind == Kind.Proxy ? ProxySheets[random.Next(ProxySheets.Length)] : summon.Kind.ToString();
                female = random.Next(2) == 0;
            }

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(owner.playerInstance.Position.getValue(), ref x, ref y, ref z);
            x += 150;

            UInt32 charId = owner.playerData.getCharID();
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
            mob.setDistrict((UInt16)owner.playerData.getDistrictId());
            mob.setDistrictName(owner.playerData.getDistrict());
            mob.setName(NameOf(summon.Kind, sheet));
            mob.setLevel((UInt16)level);
            UInt16 max = MaxHealthAt(level);
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
            lock (pets) pets[charId] = pet;
            lock (WorldServer.mobs.SyncRoot) WorldServer.mobs.Add(mob);
            lock (WorldServer.gameServerEntities.SyncRoot) WorldServer.gameServerEntities.Add(mob);
            SendWindow(pet, true);
            EnsureTimer();
            Output.WriteDebugLog("[PET] " + TimedEffects.PlayerName(owner) + " summoned " + mob.getName() + " L" + level +
                                 " (ability " + ability + ", simulacrum " + simId + ")");
            return pet;
        }

        /// <summary>
        /// NPC_BASE_SIMULACRA creation, as retail sent a fresh summon to its owner (MaraC capture line 78483; Syntax capture line
        /// 4213): name, TitleAbility 00 10 00 00, CombatantMode 0x22, EffectCounter 1, MasterCharacterID and OwnerCharacterID =
        /// owner, Description, EffectID 0x2800060c, SimulacraID, Position, OrganizationID 3, NPCRank 2, Level, Health,
        /// MaxHealth, CurrentState 0x080d, EquippedItemID and StealthAwareness. No IsEnemy/IsFriendly/RSIDescription.
        /// </summary>
        public static GameObject BuildCreation(Mob mob)
        {
            Pet pet = mob.pet;
            var go = new GameObjectDefinitions().Object40376;
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
            go.OrganizationID.enable(); go.OrganizationID.setValue((byte)3);
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
        {
            Pet pet;
            lock (pets)
            {
                if (owner == null || !pets.TryGetValue(owner.playerData.getCharID(), out pet)) return false;
                pets.Remove(owner.playerData.getCharID());
            }
            Remove(pet, withEffect ? DismissFx : 0);
            return true;
        }

        private static void Remove(Pet pet, UInt32 fx)
        {
            lock (pet) RemoveLocked(pet, fx);
        }

        private static void RemoveLocked(Pet pet, UInt32 fx)
        {
            if (pet.Gone) return;
            pet.Gone = true;
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

        private static void KillLocked(Pet pet)
        {
            if (pet.Gone || !pet.Mob.TryClaimDeath()) return;
            pet.Mob.setHealthC(0);
            RemoveWindow(pet);
            SendHit(pet, DeathFx);
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
            lock (boosts) { boosts.Remove(owner.playerData.getCharID()); nextBoostPulse.Remove(owner.playerData.getCharID()); }
        }

        // ---- Orders ----------------------------------------------------------------------------------------------

        /// <summary>81 7a ID Command / 81 7b ID Command TargetObject(u32: view u16, spawn u16) / 81 7c ID Command Value.</summary>
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
            Order(client, id, command, (UInt16)(target & 0xffff), value);
        }

        /// <summary>Carries out an order. Returns a line for the dev command, null when it was done.</summary>
        public static string Order(WorldClient client, byte id, Command command, UInt16 targetView, byte value)
        {
            Pet pet = Of(client);
            if (pet == null) return "You have no simulacrum.";
            if (id != CurrentSimulacrum && id != pet.SimulacraId && id != 0) return "No simulacrum with that ID.";
            switch (command)
            {
                case Command.Stay:
                    pet.Order = Command.Stay; pet.Target = null; SetStanding(pet); SendWindow(pet, false); return null;
                case Command.Follow:
                    pet.Order = Command.Follow; pet.Target = null; SendWindow(pet, false); return null;
                case Command.Deactivate:
                    Dismiss(client); return null;
                case Command.Attack:
                    Mob target = targetView > 2 ? AbilityTargets.FindMob(client, targetView, out _) : null;
                    if (target == null) target = DefaultTarget(client, pet);
                    if (target == null || target.getIsDead() || target.isFriendly || target.pet != null) return "Choose a hostile target.";
                    pet.Order = Command.Attack; pet.Target = target; SendWindow(pet, false); return null;
                case Command.Mode:
                    if (value > 2) return "Unknown mode.";
                    pet.Stance = (Stance)value; SendWindow(pet, false); return null;
                default:
                    return "Unknown order.";
            }
        }

        /// <summary>The owner's close-combat target, or the nearest hostile within 20 m.</summary>
        public static Mob DefaultTarget(WorldClient owner, Pet pet)
        {
            Mob fighting = CombatManager.GetHandler(owner).CurrentNpcTarget;
            if (fighting != null && !fighting.getIsDead()) return fighting;
            return NearestHostile(pet.Mob, 20);
        }

        private static Mob NearestHostile(Mob from, double metres)
        {
            Mob best = null;
            double bestD = metres * 100;
            lock (WorldServer.mobs.SyncRoot)
                foreach (Mob mob in WorldServer.mobs)
                {
                    if (mob == from || mob.isFriendly || mob.pet != null || mob.getIsDead() || !mob.getIsSpawned() || mob.getDistrict() != from.getDistrict()) continue;
                    double d = Distance(from, mob.getXPos(), mob.getYPos(), mob.getZPos());
                    if (d < bestD) { bestD = d; best = mob; }
                }
            return best;
        }

        // ---- Abilities -------------------------------------------------------------------------------------------

        /// <summary>The cast's refusal, or null when it may go ahead (checked before cost and cooldown).</summary>
        public static string Refusal(WorldClient caster, UInt16 id, UInt16 targetView)
        {
            if (Summons.ContainsKey(id))
            {
                return CombatManager.GetHandler(caster).isCombatRunning ? "You cannot summon a simulacrum in combat." : null;
            }
            if (TargetsOwnPet(id))
            {
                if (id == PowerBoost && CombatManager.GetHandler(caster).isCombatRunning)
                    return "You cannot use Power Boost in combat.";
                Pet pet = Of(caster);
                if (pet == null || pet.Mob.getIsDead()) return "You need an active simulacrum.";
                // Decoy1_Test / StopProxy1_Test / AcousticDecoy2_Test: the direct object must be owned by the subject. Without a
                // selection (or the caster selected) the caster's own simulacrum is meant.
                if (targetView > 2)
                {
                    Mob selected = AbilityTargets.FindMob(caster, targetView, out _);
                    if ((selected != null && selected != pet.Mob) || (id == PowerBoost && selected == null)) return "That is not your simulacrum.";
                }
                double range = AbilityCatalog.Find(id)?.Number("Ability/SelectionRange", 16) ?? 16;
                if (OwnerDistanceMetres(pet) > range) return "Your simulacrum is out of range.";
            }
            return null;
        }

        public static bool IsToggledOn(WorldClient caster, UInt16 id)
        {
            lock (boosts) return boosts.TryGetValue(caster.playerData.getCharID(), out UInt16 on) && on == id;
        }

        /// <summary>Daemon Boost used while on switches it off.</summary>
        public static void ToggleOff(WorldClient caster)
        {
            lock (boosts) { boosts.Remove(caster.playerData.getCharID()); nextBoostPulse.Remove(caster.playerData.getCharID()); }
            Pet pet = Of(caster);
            if (pet != null) pet.BoostUntil = 0;
        }

        /// <summary>The cast finished.</summary>
        public static void Land(WorldClient caster, UInt16 id, UInt16 targetView)
        {
            if (Summons.ContainsKey(id))
            {
                SpawnFor(caster, id);
                return;
            }
            if (IsDaemonBoost(id))
            {
                lock (boosts) boosts[caster.playerData.getCharID()] = id;
                EnsureTimer();
                BoostTick(caster, TimeUtils.GetSimTimeSeconds());
                return;
            }
            Pet pet = Of(caster);
            if (pet == null || pet.Mob.getIsDead()) return;
            lock (pet) LandOnPet(caster, pet, id);
        }

        private static void LandOnPet(WorldClient caster, Pet pet, UInt16 id)
        {
            if (pet.Gone || pet.Mob.getIsDead()) return;
            double now = TimeUtils.GetSimTimeSeconds();
            int casterLevel = caster.playerInstance.Level.getValue()[0];
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
            }
            else if (id == ChargeSimulacra)
            {
                // Sourced 20% cost reduction / 50% regen bonus for 600 s; see explicit OURS baseline below.
                Regenerate(pet, now);
                pet.ChargeUntil = now + 600;
            }
            else if (id == DetonateProxy)
            {
                Detonate(caster, pet);
            }
            else if (id == DrainCode)
            {
                // Drain Code (Decoy1Ability): 8000 (damage type 5) to the simulacrum, +50 Current Inner Strength to the caster
                DamagePet(pet, 8000, 0);
                UInt16 max = AbilityHandler.MaxInnerStrength(caster);
                UInt16 next = (UInt16)Math.Min(max, AbilityHandler.CurrentInnerStrength(caster) + 50);
                caster.playerInstance.InnerStrengthAvailable.setValue(next);
                new ServerPackets().sendISCurrent(caster, next);
                caster.FlushQueue();
            }
        }

        private static UInt32 FirstFx(AbilityCatalog.Entry entry) =>
            (UInt32)(entry?.Modifiers.Select(m => m.Number("ActivationFX")).FirstOrDefault(n => n > 0) ?? 0);

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

        public static void Detonate(WorldClient caster, Pet pet)
        {
            int blast = DetonateValue(pet.Mob.getHealthC(), pet.Mob.getHealthM());
            var entry = AbilityCatalog.Find(StopProxyEffect);
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
            foreach (Mob mob in hit) DamageForOwner(pet, mob, (UInt16)Math.Min(blast, UInt16.MaxValue), fx);
            Output.WriteDebugLog("[PET] Detonate Proxy: " + blast + " to " + hit.Count + " NPC(s) within " + radius + " m");
            // DetonateProxyEnd, when the 0.5 s tempmod ends: 13000 viral to the simulacrum
            DelayedActions.After(0.5, () => DamagePet(pet, 13000, 0));
        }

        // ---- Health ----------------------------------------------------------------------------------------------

        private static void SendHit(Pet pet, UInt32 fx)
        {
            var pak = new PacketContent();
            pak.AddByteArray(Mob.BuildHitUpdate(pet.Mob.getHealthC(), fx, ++pet.Counter));
            Store.world?.SendViewUpdateToClientsWhoHasMobSpawned(pak, pet.Mob);
            if (!pet.Mob.getIsDead()) SendWindow(pet, false);
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
            if (max == pet.Mob.getHealthM()) return;
            pet.Mob.setHealthM(max);
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
            if (left == 0) { Kill(pet); return; }
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
                List<(UInt32, UInt16)> on;
                lock (boosts) on = boosts.Select(b => (b.Key, b.Value)).ToList();
                foreach (var (charId, _) in on)
                {
                    WorldClient owner = SocialHandler.OnlineWhere(c => c.playerData.getCharID() == charId).FirstOrDefault();
                    if (owner == null) { lock (boosts) boosts.Remove(charId); continue; }
                    BoostTick(owner, now);
                }
            }
            catch (Exception e)
            {
                Output.WriteDebugLog("[PET] Tick failed: " + e);
            }
        }

        public static bool OwnerPresent(Pet pet) =>
            pet.Owner != null && pet.Owner.Alive && pet.Owner.playerData.getOnWorld() && !pet.Owner.playerData.worldChangePending &&
            pet.Owner.playerData.getDistrictId() == pet.Mob.getDistrict();

        public static void Tick(Pet pet, double now)
        {
            if (pet == null) return;
            lock (pet) TickLocked(pet, now);
        }

        private static void TickLocked(Pet pet, double now)
        {
            if (pet.Gone) return;
            if (!OwnerPresent(pet) || OwnerDistanceMetres(pet) > LeashMetres * 4)
            {
                OwnerLeft(pet.Owner);
                return;
            }
            if (pet.Mob.getIsDead()) return;
            Regenerate(pet, now);
            if (pet.FortifyUntil > 0 && now >= pet.FortifyUntil) { pet.FortifyUntil = 0; ApplyMaxHealth(pet); }

            if (pet.Target != null && (pet.Target.getIsDead() || !pet.Target.getIsSpawned() || pet.Target.getDistrict() != pet.Mob.getDistrict() ||
                OwnerDistanceTo(pet, pet.Target) > LeashMetres))
            {
                pet.Target = null;
                if (pet.Order == Command.Attack) pet.Order = Command.Follow;
            }
            // Defensive and Aggressive simulacra help with the owner's fight; Aggressive ones also take on nearby hostiles.
            if (pet.Target == null && pet.Order != Command.Stay && pet.Stance != Stance.Passive && pet.Kind != Kind.Healer)
            {
                Mob assist = CombatManager.GetHandler(pet.Owner).CurrentNpcTarget;
                if (assist == null && pet.Stance == Stance.Aggressive) assist = NearestHostile(pet.Mob, 10);
                if (assist != null && !assist.getIsDead()) pet.Target = assist;
            }

            if (pet.Kind == Kind.Healer)
            {
                HealerTick(pet, now);
                if (pet.Order != Command.Stay) MoveToward(pet, OwnerPosition(pet), FollowDistanceMetres);
                return;
            }

            if (pet.Target != null)
            {
                double reach = Ranged(pet) ? RangedRangeMetres : MeleeRangeMetres;
                bool inReach = MoveToward(pet, (pet.Target.getXPos(), pet.Target.getYPos(), pet.Target.getZPos()), reach,
                    pet.Order == Command.Stay);
                if (inReach && now >= pet.NextAttack)
                {
                    pet.NextAttack = now + AttackPeriodSeconds;
                    Attack(pet, pet.Target, now);
                }
                if (now >= pet.NextRetaliation) Retaliate(pet, pet.Target, now);
                return;
            }
            if (pet.Order == Command.Follow) MoveToward(pet, OwnerPosition(pet), FollowDistanceMetres);
        }

        /// <summary>
        /// A simulacrum's hit: the NPC round damage model at its level against the target's (our choice; no retail capture shows
        /// a simulacrum's damage), with typed Power Boost/Daemon Boost influence while each lasts.
        /// </summary>
        private static void Attack(Pet pet, Mob target, double now)
        {
            if (!SpendAction(pet, now)) return;
            if (Random.Shared.NextDouble() * 100 >= HitChance(pet, target.getLevel(), now)) return;
            UInt16 rawDamage = CombatCalculator.CalculateMobDamageWithTactics(pet.Mob.getLevel(), target.getLevel());
            UInt16 damage = DaemonBoostDamage(pet, PowerBoostDamage(pet, rawDamage, now), now);
            if (damage != rawDamage) Output.WriteDebugLog("[PET] typed influence " + pet.Kind + " base " + rawDamage + " final " + damage);
            UInt32 fx = Ranged(pet) ? CombatCalculator.GetRandomRangedHitFx() : CombatCalculator.GetRandomMeleeHitFx();
            FaceTowards(pet, target.getXPos(), target.getZPos());
            DamageForOwner(pet, target, damage, fx);
            Output.WriteDebugLog("[PET] " + pet.Mob.getName() + " hits " + target.getName() + " for " + damage);
        }

        /// <summary>An NPC the simulacrum fights, and that isn't fighting a player, hits the simulacrum back each round.</summary>
        private static void Retaliate(Pet pet, Mob target, double now)
        {
            if (target.getIsDead() || target.getCombatTarget() != null || target.IsStunned(now)) return;
            if (OwnerDistanceTo(pet, target) > LeashMetres || Distance(pet.Mob, target.getXPos(), target.getYPos(), target.getZPos()) > RangedRangeMetres * 100) return;
            pet.NextRetaliation = now + AttackPeriodSeconds;
            UInt16 damage = CombatCalculator.CalculateMobDamageWithTactics(target.getLevel(), pet.Mob.getLevel());
            DamagePet(pet, Mitigate(pet, damage, now), CombatCalculator.GetRandomMeleeHitFx());
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
            AbilityHandler.HealSelf(owner, (UInt16)amount);
        }

        /// <summary>
        /// Daemon Boost: ISDrainWithAbilityDeactivation takes 5 Inner Strength every 5 s (switching off when there isn't enough),
        /// and ApplyAuraEffect reapplies 699/700 every 5 s: 5 s of Viral Damage/Healing (699, character level x 0.5) or accuracy
        /// (700) within its radius (20/10 m). 700 modifies the actual attack roll.
        /// </summary>
        private static void BoostTick(WorldClient owner, double now)
        {
            UInt16 on;
            lock (boosts) if (!boosts.TryGetValue(owner.playerData.getCharID(), out on)) return;
            Pet pet = Of(owner);
            lock (boosts)
            {
                if (nextBoostPulse.TryGetValue(owner.playerData.getCharID(), out double next) && now < next) return;
                nextBoostPulse[owner.playerData.getCharID()] = now + 5;
            }
            UInt16 current = AbilityHandler.CurrentInnerStrength(owner);
            if (current < 5)
            {
                ToggleOff(owner);
                new ServerPackets().sendSystemChatMessage(owner, "Daemon Boost ends: not enough Inner Strength.", "BROADCAST");
                owner.FlushQueue();
                return;
            }
            UInt16 left = AbilityHandler.SpendInnerStrength(owner, 5);
            new ServerPackets().sendISCurrent(owner, left);
            owner.FlushQueue();
            if (pet == null || pet.Mob.getIsDead()) return;
            UInt16 effect = on == DaemonBoost1 ? DaemonBoostEffect1 : DaemonBoostEffect2;
            var entry = AbilityCatalog.Find(effect);
            double radius = entry?.Number("Ability/RadiusOfEffect", 20) ?? 20;
            if (OwnerDistanceMetres(pet) > radius) { pet.BoostUntil = now + 5; pet.BoostViralDamage = pet.BoostHealing = pet.BoostAccuracy = 0; return; }
            lock (pet) ApplyDaemonBoost(pet, effect, owner.playerInstance.Level.getValue()[0], now);
            SendHit(pet, FirstFx(entry));
        }

        // ---- Dev command (MXO_DEV_COMMANDS=1) ------------------------------------------------------------------

        public static readonly UInt16[] DevAbilities = { 27, 28, 29, 586, 594, 22, 601, 611, 616, 602, 612, 617, 30, 31, 587, 596, 615,
            20, 25, 603, 34, 19, 609, 619 };

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
                case "abilities":
                    return "Simulacrum abilities: " + string.Join(", ", DevAbilities.Select(a => a + " " + (DataLoader.getInstance().getAbilityByID(a)?.getAbilityName() ?? "?")));
                default:
                    Pet current = Of(client);
                    return current == null
                        ? "No simulacrum. ?pet cast|summon <ability>, ?pet dummy [level], ?pet follow|stay|attack|dismiss|defensive|aggressive|passive, ?pet abilities"
                        : current.Mob.getName() + " L" + current.Mob.getLevel() + " " + current.Mob.getHealthC() + "/" + current.Mob.getHealthM() + " HP, " + Math.Round(current.InnerStrength, 1) + "/" + current.MaxInnerStrength + " IS" +
                          ", " + current.Order + ", " + current.Stance + ", cost " + (TimeUtils.GetSimTimeSeconds() < current.ChargeUntil ? 8 : 10) + ", resistance " + (TimeUtils.GetSimTimeSeconds() < current.FortifyUntil ? current.FortifyResistance : 0) + ", aura viral/heal " + (TimeUtils.GetSimTimeSeconds() < current.BoostUntil ? current.BoostViralDamage : 0) + "/" + (TimeUtils.GetSimTimeSeconds() < current.BoostUntil ? current.BoostHealing : 0) + ", power melee/ballistic " + (TimeUtils.GetSimTimeSeconds() < current.PowerBoostUntil ? current.PowerMeleeDamage : 0) + "/" + (TimeUtils.GetSimTimeSeconds() < current.PowerBoostUntil ? current.PowerBallisticDamage : 0) + ", accuracy " + (TimeUtils.GetSimTimeSeconds() < current.BoostUntil ? current.BoostAccuracy : 0) + (current.Target != null ? ", fighting " + current.Target.getName() : "");
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
            pet.Mob.updateAnimation((byte)Mob.Animations.STAND);
        }

        /// <summary>
        /// Steps the simulacrum towards a point until it is within <paramref name="stopMetres"/>, at most 5 m a tick, with the walk
        /// animation the wandering NPCs use (02 0e). Returns true once within reach.
        /// </summary>
        private static bool MoveToward(Pet pet, (double x, double y, double z) goal, double stopMetres, bool stay = false)
        {
            double d = Distance(pet.Mob, goal.x, goal.y, goal.z) / 100.0;
            if (d <= stopMetres) return true;
            if (stay) return false;
            double step = Math.Min(StepMetres, d - stopMetres * 0.8);
            double f = step / d;
            FaceTowards(pet, goal.x, goal.z);
            pet.Mob.setXPos(pet.Mob.getXPos() + (goal.x - pet.Mob.getXPos()) * f);
            pet.Mob.setYPos(pet.Mob.getYPos() + (goal.y - pet.Mob.getYPos()) * f);
            pet.Mob.setZPos(pet.Mob.getZPos() + (goal.z - pet.Mob.getZPos()) * f);
            pet.Mob.updateAnimation((byte)(d - step <= stopMetres ? Mob.Animations.STAND : Mob.Animations.WALK));
            return d - step <= stopMetres;
        }
    }
}
