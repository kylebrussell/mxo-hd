using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace hds
{
    /// <summary>
    /// Combat-style special attacks whose behaviour the generic executors did not cover. Everything comes from the stored
    /// GOB fields (data/ability_catalog.json) and the client's own scripts:
    /// <list type="bullet">
    /// <item>Poison Knife 146, Wounding Throw 281, Sever Artery 341: their ThrownDOT (tempmods.py 935: the shared
    /// <see cref="TempMods.DotTick"/> formula) and Sever Artery's fixed 30 s debuffs, as timed effects on the target.</item>
    /// <item>Berserker Attack 535 and Side Roll Escape 121: WhenEnabled 1 modifiers, put on the caster when the move lands.
    /// Side Roll Escape (TestFunction 6, the ILDB's "Withdraw Sideroll" moves) then leaves the fight.</item>
    /// <item>Taunt 647: TauntAggro (tempmods.py 1281) on the NPCs within RadiusOfEffect: AI.setMostHated(caster).</item>
    /// <item>Wooden Stake / Silver Bullet / Holy Water Attack (207, 208, 547) and their Enhanced versions (1022, 1024,
    /// 1023): ability/general.py's DirectObject functions, 10 damage, x10 (x75 Enhanced) against an NPC with the matching
    /// NPCKilledOnlyBy* ability.</item>
    /// </list>
    /// </summary>
    public static class CombatSpecials
    {
        public const ushort PoisonKnife = 146, WoundingThrow = 281, SeverArtery = 341, BerserkerAttack = 535, Taunt = 647,
            SideRollEscape = 121, WoodenStake = 207, SilverBullet = 208, HolyWater = 547, WoodenStakePlus = 1022,
            HolyWaterPlus = 1023, SilverBulletPlus = 1024;
        public const ushort KilledOnlyByHolyWater = 432, KilledOnlyBySilver = 463, KilledOnlyByWood = 464;

        // ability/general.py 9-17
        public const int WoodenStakeDamage = 10, SilverBulletDamage = 10, HolyWaterDamage = 10;
        public const int ExileDamageMultiplier = 10, EnhancedExileDamageMultiplier = 75;

        private static readonly HashSet<ushort> thrownDots = new HashSet<ushort> { PoisonKnife, WoundingThrow, SeverArtery };
        private static readonly HashSet<ushort> casterBuffs = new HashSet<ushort> { BerserkerAttack, SideRollEscape };
        private static readonly Dictionary<ushort, ushort> exileWeakness = new Dictionary<ushort, ushort>
        {
            { WoodenStake, KilledOnlyByWood }, { WoodenStakePlus, KilledOnlyByWood },
            { SilverBullet, KilledOnlyBySilver }, { SilverBulletPlus, KilledOnlyBySilver },
            { HolyWater, KilledOnlyByHolyWater }, { HolyWaterPlus, KilledOnlyByHolyWater },
        };

        public static bool Handles(ushort id)
        {
            id = AbilityCatalog.Canonical(id);
            return thrownDots.Contains(id) || casterBuffs.Contains(id) || exileWeakness.ContainsKey(id) || id == Taunt;
        }

        /// <summary>What the server does for the special (the ability audit's text).</summary>
        public static string Describe(ushort id)
        {
            id = AbilityCatalog.Canonical(id);
            if (thrownDots.Contains(id)) return id == SeverArtery
                ? "stored ThrownDOT + 30 s slow/ForceCombat debuffs on the target; RandomCombatStateChance missing"
                : "stored ThrownDOT on the target (script tick)";
            if (id == BerserkerAttack) return "on landing: caster buff from stored WhenEnabled 1 modifiers";
            if (id == SideRollEscape) return "on landing: caster Combat Evasion buff, then leaves the fight (TestFunction 6 inferred)";
            if (id == Taunt) return "TauntAggro: NPCs in radius hate caster most and turn on them; humans unaffected";
            if (exileWeakness.ContainsKey(id)) return "general.py damage 10, x" +
                (id >= WoodenStakePlus ? EnhancedExileDamageMultiplier : ExileDamageMultiplier) + " vs NPCKilledOnlyBy" +
                (exileWeakness[id] == KilledOnlyByWood ? "Wood" : exileWeakness[id] == KilledOnlyBySilver ? "Silver" : "HolyWater") +
                " (NPC weakness by name: server policy)";
            return null;
        }

        /// <summary>Moves used on an enemy target (they go through the round queue or land on the target when cast).</summary>
        public static bool TargetsEnemy(ushort id) => Handles(id) && AbilityCatalog.Canonical(id) != Taunt;

        /// <summary>Abilities that act around the caster when the cast ends (no target needed).</summary>
        public static bool ActsOnCast(ushort id) => AbilityCatalog.Canonical(id) == Taunt;

        // ---- timed effects on the target ----

        /// <summary>The target's tempmods: the ThrownDOT and the fixed timed stat debuffs (RandomCombatStateChance's
        /// periodic random state is not modelled).</summary>
        public static bool TryTimed(ushort id, out TempMods.Effect effect)
        {
            effect = null;
            id = AbilityCatalog.Canonical(id);
            if (!thrownDots.Contains(id)) return false;
            var entry = AbilityCatalog.Find(id);
            if (entry == null) return false;
            var mods = new List<TempMods.Mod>();
            foreach (var m in entry.Modifiers)
            {
                if (m.Number("WhenEnabled", -1) != 2 || m.Number("DurationType", -1) != 0) continue;
                float seconds = (float)m.Number("Duration");
                int min = (int)m.Number("MinValue"), max = (int)m.Number("MaxValue", min);
                if (m.Text("StatModUpdateFunc").EndsWith("DOT", StringComparison.Ordinal))
                    mods.Add(new TempMods.Mod { Kind = TempMods.KindLinked, Target = TempMods.DOTPlaceHolderAbility, MinValue = min,
                        MaxValue = max, Seconds = seconds, Proc = TempMods.Proc.Dot,
                        Interval = m.Number("UpdateFuncInterval", TempMods.DotIntervalSeconds) });
                else if (m.Target != 0 && m.State == 0 && min == max && string.IsNullOrEmpty(m.Text("StatModInitFunc")))
                    mods.Add(new TempMods.Mod { Kind = TempMods.KindStat, Target = m.Target, MinValue = min, MaxValue = max,
                        Seconds = seconds, Proc = TempMods.Proc.Stat });
            }
            if (mods.Count == 0) return false;
            effect = new TempMods.Effect { Ability = id, Mods = mods.ToArray() };
            return true;
        }

        // ---- buffs on the caster ----

        public static bool TryCasterBuff(ushort id, out AbilityBuffs.Buff buff)
        {
            buff = null;
            id = AbilityCatalog.Canonical(id);
            if (!casterBuffs.Contains(id)) return false;
            var mods = AbilityCatalog.Find(id)?.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 1 && m.Target != 0 &&
                m.Number("DurationType", -1) == 0 && m.Number("MinValue") == m.Number("MaxValue")).ToList();
            if (mods == null || mods.Count == 0) return false;
            buff = new AbilityBuffs.Buff
            {
                Seconds = (float)mods.Max(m => m.Number("Duration")),
                Modifiers = mods.Select(m => new AbilityBuffs.Modifier(AbilityBuffs.KindStat, m.Target, (int)m.Number("MinValue"))).ToArray()
            };
            return true;
        }

        // ---- Exile weapons ----

        /// <summary>The NPCKilledOnlyBy* ability an NPC has. The NPC templates that grant them are not shipped; this server
        /// gives them by name: vampires wood, werewolves silver, zombies holy water (zombie-virus characters receive
        /// NPCKilledOnlyByHolyWater, tempmods.py 2912/2940). Server policy, not retail data.</summary>
        public static ushort Weakness(string npcName)
        {
            string name = npcName ?? "";
            if (name.IndexOf("Vampire", StringComparison.OrdinalIgnoreCase) >= 0) return KilledOnlyByWood;
            if (name.IndexOf("Werewolf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Lycan", StringComparison.OrdinalIgnoreCase) >= 0) return KilledOnlyBySilver;
            if (name.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0) return KilledOnlyByHolyWater;
            return 0;
        }

        private sealed class WeaknessOverride { public ushort Ability; }
        private static readonly ConditionalWeakTable<Mob, WeaknessOverride> weaknessOverrides = new ConditionalWeakTable<Mob, WeaknessOverride>();

        /// <summary>The NPC's weakness: a dev override (?exileweakness, MXO_DEV_COMMANDS=1) or the name policy.</summary>
        public static ushort Weakness(Mob mob)
        {
            if (mob == null) return 0;
            return weaknessOverrides.TryGetValue(mob, out WeaknessOverride o) ? o.Ability : Weakness(mob.getName());
        }

        public static void SetWeakness(Mob mob, ushort ability) => weaknessOverrides.AddOrUpdate(mob, new WeaknessOverride { Ability = ability });

        /// <summary>?exileweakness wood|silver|holywater|none: the NPCKilledOnlyBy* id, or null for an unknown word.</summary>
        public static ushort? WeaknessNamed(string word) => (word ?? "").ToLowerInvariant() switch
        {
            "wood" => KilledOnlyByWood, "silver" => KilledOnlyBySilver, "holywater" => KilledOnlyByHolyWater, "none" => 0,
            _ => (ushort?)null
        };

        public static bool IsExileWeapon(ushort id) => exileWeakness.ContainsKey(AbilityCatalog.Canonical(id));

        /// <summary>general.py's damage: 10, times 10 (75 for the Enhanced versions) against the matching weakness.</summary>
        public static ushort ExileDamage(ushort id, ushort targetWeakness)
        {
            id = AbilityCatalog.Canonical(id);
            if (!exileWeakness.TryGetValue(id, out ushort weakness)) return 0;
            int damage = id == SilverBullet || id == SilverBulletPlus ? SilverBulletDamage :
                id == HolyWater || id == HolyWaterPlus ? HolyWaterDamage : WoodenStakeDamage;
            bool enhanced = id == WoodenStakePlus || id == SilverBulletPlus || id == HolyWaterPlus;
            if (targetWeakness == weakness) damage *= enhanced ? EnhancedExileDamageMultiplier : ExileDamageMultiplier;
            return (ushort)damage;
        }

        /// <summary>The damage a landed special does instead of the generic attack-ability fallback, or null.</summary>
        public static ushort? Damage(ushort id, Mob mob)
        {
            if (IsExileWeapon(id)) return ExileDamage(id, Weakness(mob));
            return Handles(id) ? (ushort)0 : (ushort?)null;
        }

        /// <summary>After a move lands: the caster's buff, and Side Roll Escape's withdrawal.</summary>
        public static void Landed(WorldClient caster, ushort id)
        {
            id = AbilityCatalog.Canonical(id);
            if (casterBuffs.Contains(id) && AbilityBuffs.Apply(caster, id)) caster.FlushQueue();
            if (id == SideRollEscape)
            {
                byte[] leave = null;
                DelayedActions.AfterFor(caster, 0, () => CombatManager.GetHandler(caster).ProcessLeaveCloseCombat(ref leave));
                Output.WriteDebugLog("[SPECIAL] Side Roll Escape: " + caster.playerData.getCharID() + " rolls out of the fight");
            }
        }

        // ---- Taunt ----

        private sealed class TauntState { public WorldClient Taunter; }
        private static readonly ConditionalWeakTable<Mob, TauntState> taunts = new ConditionalWeakTable<Mob, TauntState>();

        public static WorldClient MostHated(Mob mob)
        {
            if (mob == null || !taunts.TryGetValue(mob, out TauntState state)) return null;
            lock (state) return state.Taunter;
        }

        /// <summary>TauntAggro: every hostile NPC around the caster now hates the caster most; one that is not already
        /// fighting the caster turns on them. Returns the NPCs taunted.</summary>
        public static List<Mob> CastTaunt(WorldClient caster)
        {
            var taunted = new List<Mob>();
            var entry = AbilityCatalog.Find(Taunt);
            double radius = entry?.Number("Ability/RadiusOfEffect") ?? 0;
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(caster.playerInstance.Position.getValue(), ref x, ref y, ref z);
            ushort district = (ushort)caster.playerData.getDistrictId();
            lock (WorldServer.mobs.SyncRoot)
                foreach (Mob mob in WorldServer.mobs)
                {
                    if (mob.isFriendly || mob.getIsDead() || mob.getDistrict() != district) continue;
                    if (!AbilityTargets.WithinArea(x, y, z, mob.getXPos(), mob.getYPos(), mob.getZPos(), radius)) continue;
                    if (caster.viewMan.FindViewForEntity(mob.getEntityId()) == null) continue;
                    taunted.Add(mob);
                }
            foreach (Mob mob in taunted)
            {
                TauntState state = taunts.GetValue(mob, _ => new TauntState());
                lock (state) state.Taunter = caster;
                Output.WriteDebugLog("[SPECIAL] Taunt: " + mob.getName() + " now hates " + caster.playerData.getCharID() + " most");
                if (!CombatManager.GetHandler(caster).IsFighting(mob)) MobAggro.Retaliate(caster, mob);
            }
            return taunted;
        }

        /// <summary>A taunted NPC directs its attacks at its taunter while the taunter is fighting it: another player's
        /// fight with it gets no NPC hit. The taunt lapses when the taunter is no longer fighting that NPC.</summary>
        public static bool TurnedAway(Mob mob, WorldClient player)
        {
            WorldClient taunter = MostHated(mob);
            if (taunter == null || taunter == player) return false;
            if (taunter.playerData.isDead || !CombatManager.GetHandler(taunter).IsFighting(mob))
            {
                if (taunts.TryGetValue(mob, out TauntState state)) lock (state) if (state.Taunter == taunter) state.Taunter = null;
                return false;
            }
            return true;
        }
    }
}
