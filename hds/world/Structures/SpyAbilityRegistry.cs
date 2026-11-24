using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Per-ability hook contract for reviewed Spy-family abilities so each ability lives in its own file
    /// without touching shared dispatch switches in AbilityHandler / AbilityTargets / PvpCombat.
    /// </summary>
    public interface ISpyAbilityHandler
    {
        ushort[] AbilityIds { get; }

        bool RetainsStealthDuringCast(ushort abilityId) => false;

        bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            refusalReason = 0;
            return false;
        }

        bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage)
        {
            damage = 0;
            return false;
        }

        void OnLanded(WorldClient caster, ushort abilityId, ulong targetEntity) { }
    }

    /// <summary>
    /// Auto-discovered registry of <see cref="ISpyAbilityHandler"/> implementations in this assembly.
    /// </summary>
    public static class SpyAbilityRegistry
    {
        private static readonly Lazy<IReadOnlyDictionary<ushort, ISpyAbilityHandler>> handlers =
            new Lazy<IReadOnlyDictionary<ushort, ISpyAbilityHandler>>(
                () => BuildMap(typeof(SpyAbilityRegistry).Assembly.GetTypes()));

        internal static IReadOnlyDictionary<ushort, ISpyAbilityHandler> BuildMap(IEnumerable<Type> types)
        {
            var map = new Dictionary<ushort, ISpyAbilityHandler>();
            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface || !typeof(ISpyAbilityHandler).IsAssignableFrom(type))
                    continue;
                var handler = Activator.CreateInstance(type) as ISpyAbilityHandler;
                if (handler == null)
                    continue;
                foreach (ushort id in handler.AbilityIds)
                {
                    ushort canonical = AbilityCatalog.Canonical(id);
                    if (map.ContainsKey(canonical))
                        throw new InvalidOperationException(
                            $"Duplicate ISpyAbilityHandler registered for ability {canonical}: {type.Name}");
                    map[canonical] = handler;
                }
            }
            return map;
        }

        public static bool TryGet(ushort abilityId, out ISpyAbilityHandler handler) =>
            handlers.Value.TryGetValue(AbilityCatalog.Canonical(abilityId), out handler);

        public static bool RetainsStealthDuringCast(ushort abilityId) =>
            TryGet(abilityId, out var handler) && handler.RetainsStealthDuringCast(AbilityCatalog.Canonical(abilityId));

        public static bool TryRefuse(WorldClient client, ushort abilityId, ushort targetViewId, ushort? targetSpawnId, out uint refusalReason)
        {
            ushort canonical = AbilityCatalog.Canonical(abilityId);
            if (TryGet(canonical, out var handler))
                return handler.TryRefuse(client, canonical, targetViewId, targetSpawnId, out refusalReason);
            refusalReason = 0;
            return false;
        }

        public static bool TryResolveDamage(AbilityCatalog.Entry source, int characterLevel, Random random, out ushort damage)
        {
            if (source != null && TryGet(source.Id, out var handler))
                return handler.TryResolveDamage(source, characterLevel, random, out damage);
            damage = 0;
            return false;
        }

        public static void Landed(WorldClient caster, ushort abilityId, ulong targetEntity = 0)
        {
            ushort canonical = AbilityCatalog.Canonical(abilityId);
            if (!TryGet(canonical, out var handler)) return;
            if (handler.RetainsStealthDuringCast(canonical))
                Stealth.BreakForAction(caster, "landed ability " + canonical);
            handler.OnLanded(caster, canonical, targetEntity);
        }
    }
}
