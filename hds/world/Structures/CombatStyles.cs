using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>
    /// Combat styles: the IsCombatExclusive toggles (Self Defense 17, Aikido 101, Karate 132, Kung-Fu 133, Hacker Style
    /// 1006, Coder Style 1008, Spy Style 1112 ...). The client picks one with <c>3e</c> (<see cref="CombatSelection"/>);
    /// tempmods.UpdateCombatStyle only calls the native AbilityInv.updateCombatStyle, so the rules below are the
    /// stored GOB fields:
    /// <list type="bullet">
    /// <item>Only the active style's WhenEnabled 2 / DurationType 2 modifiers count (MeleeAcc, MeleeDam, ... at
    /// ValueFunction 2 of the style's level). An inactive loaded style contributes nothing.</item>
    /// <item>A special move's (<c>Ability/CombatSpecialMove</c>) <c>Ability/CastRequiredCombatStance</c> names the style
    /// it needs: Berserker Attack 535 and Sky High Sidekick 267 need Karate 132, Wounding Throw 281 Spy Style 1112. The
    /// client's special-move picker sends 3e to switch to that style itself (client.dll 6210677b); the server does the
    /// same for a loaded style and refuses the move when the style isn't loaded. Other abilities with a stance
    /// (viruses need Hacker Style, Poison Knife Spy Style) are not gated until the native client's auto-switch for them
    /// is checked.</item>
    /// <item>The interlock shows the style to everyone in the fight: the ILDB's exchange groups are lettered by style,
    /// and its own transition groups give the letters ("Stand Transitions:To KungFuAbility" holds Trans Stand-WS..WB).</item>
    /// </list>
    /// </summary>
    public static class CombatStyles
    {
        public const ushort SelfDefense = 17, Aikido = 101, Karate = 132, KungFu = 133, HackerStyle = 1006,
            CoderStyle = 1008, SpyStyle = 1112;

        /// <summary>Handguns, Rifles and Submachine Guns are combat-exclusive too, but their numeric modifiers belong to the
        /// weapon-proficiency work (not this module): they can be the active style without contributing here.</summary>
        public static readonly HashSet<ushort> WeaponStyles = new HashSet<ushort> { 129, 147, 158 };

        public static bool IsStyle(ushort ability) => AbilityCatalog.Find(ability)?.Number("Ability/IsCombatExclusive") == 1;

        /// <summary>The modifiers that are on while the style is the active one.</summary>
        public static IEnumerable<AbilityCatalog.Modifier> Modifiers(ushort style)
        {
            var entry = AbilityCatalog.Find(style);
            if (entry == null || entry.Number("Ability/IsCombatExclusive") != 1 || WeaponStyles.Contains(AbilityCatalog.Canonical(style)))
                return Enumerable.Empty<AbilityCatalog.Modifier>();
            return entry.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2 && m.Number("DurationType", -1) == 2 &&
                m.Target != 0 && m.State == 0 && m.Number("TargetAbilityGrant") == 0);
        }

        /// <summary>The style a special move needs (CastRequiredCombatStance), or 0 when it needs none.</summary>
        public static ushort RequiredStyle(ushort ability)
        {
            var entry = AbilityCatalog.Find(ability);
            ushort stance = AbilityCatalog.IdOf((uint)(entry?.Number("Ability/CastRequiredCombatStance") ?? 0));
            return stance != 0 && IsStyle(stance) ? stance : (ushort)0;
        }

        /// <summary>The style in effect: the selected one while it is loaded (directly or as a grant), else Self Defense.</summary>
        public static ushort Active(WorldClient client)
        {
            ushort style = client.playerData.combatStyle;
            return style != 0 && AbilityPassives.Level(client, style) > 0 || style == SelfDefense ? style : SelfDefense;
        }

        public static bool IsSpecialMove(ushort ability) => AbilityCatalog.Find(ability)?.Number("Ability/CombatSpecialMove") == 1;

        /// <summary>Whether the special move can be used in the current style (abilities that aren't special moves always can).</summary>
        public static bool CanUse(WorldClient client, ushort ability)
        {
            ushort required = IsSpecialMove(ability) ? RequiredStyle(ability) : (ushort)0;
            return required == 0 || Active(client) == required;
        }

        /// <summary>Before a special move: switches to its style when that style is loaded, as the client's picker does.
        /// False when the move needs a style the player doesn't have loaded.</summary>
        public static bool EnsureStyleFor(WorldClient client, ushort ability)
        {
            if (CanUse(client, ability)) return true;
            ushort required = RequiredStyle(ability);
            if (AbilityPassives.Level(client, required) <= 0) return false;
            Select(client, required);
            client.playerData.combatSelection = required;
            client.playerInstance.CurCombatExclusiveAbility.setValue((uint)required);
            client.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((39, PlayerViewAttributes.U32(required))), false);
            return true;
        }

        public static string Name(ushort style) => style switch
        {
            SelfDefense => "Self Defense", Aikido => "Aikido", Karate => "Karate", KungFu => "Kung-Fu",
            HackerStyle => "Hacker Style", CoderStyle => "Coder Style", SpyStyle => "Spy Style",
            _ => AbilityCatalog.Find(style)?.Name ?? style.ToString()
        };

        /// <summary>The style's letter in the ILDB's exchange groups. Hacker, Coder and Spy Style have no exchange set of
        /// their own in the ILDB (it has A D G K P R S W pairs; M and T have transitions only); they use Self-Defense's.</summary>
        public static char Letter(ushort style) => style switch
        {
            Aikido => 'A', Karate => 'K', KungFu => 'W', _ => 'S'
        };

        public static char Letter(WorldClient client) => client == null ? 'S' : Letter(Active(client));

        /// <summary>Makes the selected style the active one: its stat records replace the previous style's.</summary>
        public static void Select(WorldClient client, ushort style)
        {
            if (client.playerData.combatStyle == style) return;
            client.playerData.combatStyle = style;
            AbilityPassives.Refresh(client);
            Output.WriteDebugLog("[STYLE] " + client.playerData.getCharID() + " now fights with " + Name(Active(client)));
        }
    }
}
