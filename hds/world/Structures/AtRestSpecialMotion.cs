using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// The targeted free-attack special motion for selected specials that land at rest. The stock client's built-in
    /// action list (ClientDB.AnimationList) names a FreeAttackWeaponSM* action for each weapon special; the knife throw
    /// (0x40) is the stock-verified SneakFree throw motion (StealthShieldAbilities.PlayThrow). Thrown specials with no
    /// action of their own use the knife throw. Interlocked casts keep their round presentation.
    /// </summary>
    public static class AtRestSpecialMotion
    {
        public const byte KnifeThrow = 0x40, PistolHinderingShot = 0x5d, RifleDeadlyShot = 0x60;

        public static readonly IReadOnlyDictionary<ushort, byte> Actions = new Dictionary<ushort, byte>
        {
            { 279, KnifeThrow },          // Deadly Throw
            { 280, KnifeThrow },          // Crippling Throw
            { 130, PistolHinderingShot }, // Hindering Shot: FreeAttackWeaponSMPistolHinderingShot
            { 454, RifleDeadlyShot },     // Deadly Shot: FreeAttackWeaponSMRifleDeadlyShot
        };

        internal static bool Applies(WorldClient caster, ushort ability, out byte action)
        {
            action = 0;
            var handler = CombatManager.GetHandler(caster);
            return Actions.TryGetValue(AbilityCatalog.Canonical(ability), out action) &&
                !handler.isCombatRunning && !handler.HasActiveInterlock && handler.pvpFight == null;
        }

        internal static void Landed(WorldClient caster, ushort ability, ulong targetEntity)
        {
            if (!Applies(caster, ability, out byte action)) return;
            StealthShieldAbilities.PlayThrow(caster, targetEntity, AbilityCatalog.Canonical(ability), action);
            Output.WriteDebugLog($"[AT-REST-MOTION] {AbilityCatalog.Canonical(ability)} action 0x{action:x2}");
        }
    }
}
