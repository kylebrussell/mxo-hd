using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace hds
{
    /// <summary>
    /// A client's views. The client's own packet thread, the visibility thread, combat timers and the door timer
    /// all add, remove and read views, so every access locks the list; readers that iterate take a Snapshot().
    /// (Copying the list while another thread added to it threw and took the whole server down.)
    /// </summary>
    public class ViewManager
    {
        /// <summary>The views in creation order, indexed by entity and view id: the visibility pass looks up a view for
        /// every pair of nearby players each pass, and a linear Find made that quadratic in the view count.</summary>
        public readonly ViewList views = new ViewList();
        internal const UInt16 FirstDynamicViewId = 4; // 0 = Protocol-03 end sentinel, 1..3 = system/self views
        UInt16 currentViewID = 3; // We start from 3 to increment (first allocated is 4)
        private long reconcileStamp;


        public ViewManager(){
        }

        internal void SetCurrentViewIdForTesting(UInt16 value)
        {
            lock (views)
            {
                currentViewID = value;
            }
        }

        private UInt16 AllocateViewIdLocked()
        {
            const int maxDynamicIds = UInt16.MaxValue - FirstDynamicViewId + 1;
            UInt16 candidate = currentViewID;
            for (int attempts = 0; attempts < maxDynamicIds; attempts++)
            {
                candidate = candidate == UInt16.MaxValue || candidate < 3
                    ? FirstDynamicViewId
                    : (UInt16)(candidate + 1);
                if (views.FirstWithViewId(candidate) == null)
                {
                    currentViewID = candidate;
                    return candidate;
                }
            }

            // All 65,532 dynamic IDs are live; still never hand out reserved 0..3.
            currentViewID = currentViewID == UInt16.MaxValue || currentViewID < 3
                ? FirstDynamicViewId
                : (UInt16)(currentViewID + 1);
            return currentViewID;
        }

        public ClientView addView(UInt64 entityId, UInt32 goId)
        {
            lock (views)
            {
                // Add the Object to the client View List
                UInt16 nextId = AllocateViewIdLocked();
                ClientView newView = new ClientView(goId, nextId, entityId);
                views.Add(newView);
                return newView;
            }
        }

        public ClientView getViewById(UInt16 viewId)
        {
            lock (views)
            {
                return views.FirstWithViewId(viewId);
            }
        }

        public ClientView FindViewForEntity(UInt64 entityId)
        {
            return views.FirstForEntityLockFree(entityId);
        }

        public void removeViewByViewId(UInt16 viewId)
        {
            lock (views)
            {
                ClientView view = views.FirstWithViewId(viewId);
                if (view != null)
                {
                    views.Remove(view);
                }
            }
        }

        /// <summary>
        /// Forgets every view. Call when the client reloads its world (81 07 → reset): it drops all views then,
        /// and views still marked created here kept NPCs, players and objects from being spawned again after a
        /// hardline teleport within the same district. View ids keep counting up, so a late packet for an old
        /// view can't land on a new one.
        /// </summary>
        public void Clear()
        {
            lock (views)
            {
                views.Clear();
            }
        }

        /// <summary>See ViewList.Version.</summary>
        public long Version { get { lock (views) return views.Version; } }

        public int Count
        {
            get
            {
                lock (views)
                {
                    return views.Count;
                }
            }
        }

        public List<ClientView> Snapshot()
        {
            lock (views)
            {
                return new List<ClientView>(views);
            }
        }

        // This method should check if client has a view for a GoID
        // If not it adds it to his List of Views and response the ClientView Object
        public ClientView GetViewForEntityAndGo(UInt64 entityId, UInt32 goID)
        {
            lock (views)
            {
                ClientView view = views.FirstForEntity(entityId);
                if (view == null || view.ViewID == 0)
                {
                    view = addView(entityId, goID);
                }
                return view;
            }
        }

        /// <summary>
        /// Reconciles nearby player entities against this client's views under a single lock acquisition.
        /// Creates missing ClientView slots, appends uncreated nearby views to <paramref name="toSpawn"/>,
        /// appends stale created player views to <paramref name="toDelete"/>, and returns the current
        /// view-list version without allocating in the steady state.
        /// </summary>
        internal long ReconcilePlayerViews(
            List<WorldClient> nearbyClients,
            List<ulong> nearbyEntities,
            List<UInt16> nearbyGoids,
            HashSet<ulong> nearbySet,
            UInt16 playerGoId,
            List<(WorldClient Other, ClientView View)> toSpawn,
            List<ClientView> toDelete)
        {
            return ReconcilePlayerViews(nearbyClients, nearbyEntities, nearbyGoids, playerGoId, toSpawn, toDelete);
        }

        internal long ReconcilePlayerViews(
            List<WorldClient> nearbyClients,
            List<ulong> nearbyEntities,
            List<UInt16> nearbyGoids,
            UInt16 playerGoId,
            List<(WorldClient Other, ClientView View)> toSpawn,
            List<ClientView> toDelete)
        {
            toSpawn.Clear();
            toDelete.Clear();
            lock (views)
            {
                long stamp = ++reconcileStamp;
                int nearbyCount = nearbyClients.Count;
                for (int i = 0; i < nearbyCount; i++)
                {
                    ulong entityId = nearbyEntities[i];
                    ClientView view = views.FirstForEntity(entityId);
                    if (view == null || view.ViewID == 0)
                    {
                        view = addView(entityId, nearbyGoids[i]);
                        view.ReconcileStamp = stamp;
                    }
                    else
                    {
                        view.ReconcileStamp = stamp;
                        if (views.HasMultiEntity)
                        {
                            views.StampExtraEntityViews(entityId, stamp);
                        }
                    }

                    if (!view.viewCreated)
                    {
                        toSpawn.Add((nearbyClients[i], view));
                    }
                }

                List<ClientView> playerViews = views.ViewsWithGoId(playerGoId);
                if (playerViews != null)
                {
                    for (int i = 0; i < playerViews.Count; i++)
                    {
                        ClientView view = playerViews[i];
                        if (view != null && view.viewCreated && view.ReconcileStamp != stamp)
                        {
                            toDelete.Add(view);
                        }
                    }
                }

                return views.Version;
            }
        }

        internal long ReconcilePlayerSamples(
            List<WorldThreads.PlayerSample> nearbySamples,
            UInt16 playerGoId,
            List<(WorldClient Other, ClientView View)> toSpawn,
            List<ClientView> toDelete)
        {
            toSpawn.Clear();
            toDelete.Clear();
            lock (views)
            {
                long stamp = ++reconcileStamp;
                bool hasMulti = views.HasMultiEntity;
                int nearbyCount = nearbySamples.Count;
                for (int i = 0; i < nearbyCount; i++)
                {
                    WorldThreads.PlayerSample sample = nearbySamples[i];
                    ulong entityId = sample.Entity;
                    ClientView view = views.FirstForEntity(entityId);
                    if (view == null || view.ViewID == 0)
                    {
                        view = addView(entityId, sample.Goid);
                        view.ReconcileStamp = stamp;
                    }
                    else
                    {
                        view.ReconcileStamp = stamp;
                        if (hasMulti)
                        {
                            views.StampExtraEntityViews(entityId, stamp);
                        }
                    }

                    if (!view.viewCreated)
                    {
                        toSpawn.Add((sample.Client, view));
                    }
                }

                List<ClientView> playerViews = views.ViewsWithGoId(playerGoId);
                if (playerViews != null)
                {
                    for (int i = 0; i < playerViews.Count; i++)
                    {
                        ClientView view = playerViews[i];
                        if (view != null && view.viewCreated && view.ReconcileStamp != stamp)
                        {
                            toDelete.Add(view);
                        }
                    }
                }

                return views.Version;
            }
        }

        /// <summary>
        /// Collects created views matching <paramref name="goId"/> whose entity is not in <paramref name="nearbySet"/>
        /// under a single lock without scanning unrelated views or copying the view list.
        /// </summary>
        internal void CollectStaleViewsByGoId(UInt32 goId, HashSet<ulong> nearbySet, List<ClientView> toDelete)
        {
            toDelete.Clear();
            lock (views)
            {
                List<ClientView> matching = views.ViewsWithGoId(goId);
                if (matching == null) return;
                for (int i = 0; i < matching.Count; i++)
                {
                    ClientView view = matching[i];
                    if (view != null && view.viewCreated && !nearbySet.Contains(view.entityId))
                    {
                        toDelete.Add(view);
                    }
                }
            }
        }

        /// <summary>
        /// Collects created mob views whose entity is in <paramref name="mobsByEntity"/> and either not in
        /// <paramref name="nearbySet"/> or in another district, scanning only non-player views.
        /// </summary>
        internal void CollectStaleMobViews(
            ushort districtId,
            HashSet<ulong> nearbySet,
            Dictionary<ulong, Mob> mobsByEntity,
            List<(ClientView View, Mob Mob)> toDelete)
        {
            toDelete.Clear();
            lock (views)
            {
                List<ClientView> nonPlayers = views.NonPlayerViews;
                for (int i = 0; i < nonPlayers.Count; i++)
                {
                    ClientView view = nonPlayers[i];
                    if (view == null || !view.viewCreated) continue;
                    if (!mobsByEntity.TryGetValue(view.entityId, out Mob mob)) continue;
                    if (!nearbySet.Contains(view.entityId) || mob.getDistrict() != districtId)
                    {
                        toDelete.Add((view, mob));
                    }
                }
            }
        }

        /// <summary>
        /// Reconciles nearby introducible mobs against this client's views under a single lock acquisition:
        /// creates missing ClientView slots, collects uncreated spawns and active pet refreshes, and collects
        /// stale mob views by scanning only non-player views.
        /// </summary>
        internal void ReconcileMobViews(
            ushort districtId,
            List<Mob> introducibleMobs,
            HashSet<ulong> nearbyMobEntities,
            Dictionary<ulong, Mob> mobsByEntity,
            List<(Mob Mob, ClientView View)> toSpawn,
            List<(Mob Mob, ClientView View)> petsToRefresh,
            List<(ClientView View, Mob Mob)> toDelete)
        {
            toSpawn.Clear();
            petsToRefresh.Clear();
            toDelete.Clear();
            lock (views)
            {
                int count = introducibleMobs.Count;
                for (int i = 0; i < count; i++)
                {
                    Mob mob = introducibleMobs[i];
                    ClientView view = views.FirstForEntity(mob.getEntityId());
                    if (view == null || view.ViewID == 0)
                    {
                        view = addView(mob.getEntityId(), NumericalUtils.ByteArrayToUint16(mob.getGoId(), 1));
                    }
                    if (!view.viewCreated)
                    {
                        toSpawn.Add((mob, view));
                    }
                    else if (!view.viewNeedsToBeDeleted && mob.pet != null)
                    {
                        petsToRefresh.Add((mob, view));
                    }
                }

                List<ClientView> nonPlayers = views.NonPlayerViews;
                for (int i = 0; i < nonPlayers.Count; i++)
                {
                    ClientView view = nonPlayers[i];
                    if (view == null || !view.viewCreated) continue;
                    if (!mobsByEntity.TryGetValue(view.entityId, out Mob mob)) continue;
                    if (!nearbyMobEntities.Contains(view.entityId) || mob.getDistrict() != districtId)
                    {
                        toDelete.Add((view, mob));
                    }
                }
            }
        }

        /// <summary>
        /// Stamp-based mob view reconciliation (zero HashSet): stamps non-introducible nearby corpses so they
        /// stay alive in range, reconciles introducible mobs in a single lookup, and scans only non-player views.
        /// </summary>
        internal void ReconcileMobViews(
            ushort districtId,
            List<Mob> nonIntroducibleNearby,
            List<Mob> introducibleMobs,
            Dictionary<ulong, Mob> mobsByEntity,
            List<(Mob Mob, ClientView View)> toSpawn,
            List<(Mob Mob, ClientView View)> petsToRefresh,
            List<(ClientView View, Mob Mob)> toDelete)
        {
            toSpawn.Clear();
            petsToRefresh.Clear();
            toDelete.Clear();
            lock (views)
            {
                long stamp = ++reconcileStamp;
                bool hasMulti = views.HasMultiEntity;
                for (int i = 0; i < nonIntroducibleNearby.Count; i++)
                {
                    ulong entityId = nonIntroducibleNearby[i].getEntityId();
                    ClientView existing = views.FirstForEntity(entityId);
                    if (existing != null)
                    {
                        existing.ReconcileStamp = stamp;
                        if (hasMulti) views.StampExtraEntityViews(entityId, stamp);
                    }
                }

                int count = introducibleMobs.Count;
                for (int i = 0; i < count; i++)
                {
                    Mob mob = introducibleMobs[i];
                    ulong entityId = mob.getEntityId();
                    ClientView view = views.FirstForEntity(entityId);
                    if (view == null || view.ViewID == 0)
                    {
                        view = addView(entityId, NumericalUtils.ByteArrayToUint16(mob.getGoId(), 1));
                    }
                    else if (hasMulti)
                    {
                        views.StampExtraEntityViews(entityId, stamp);
                    }
                    view.ReconcileStamp = stamp;
                    if (!view.viewCreated)
                    {
                        toSpawn.Add((mob, view));
                    }
                    else if (!view.viewNeedsToBeDeleted && mob.pet != null)
                    {
                        petsToRefresh.Add((mob, view));
                    }
                }

                List<ClientView> nonPlayers = views.NonPlayerViews;
                for (int i = 0; i < nonPlayers.Count; i++)
                {
                    ClientView view = nonPlayers[i];
                    if (view == null || !view.viewCreated) continue;
                    if (!mobsByEntity.TryGetValue(view.entityId, out Mob mob)) continue;
                    if (view.ReconcileStamp != stamp || mob.getDistrict() != districtId)
                    {
                        toDelete.Add((view, mob));
                    }
                }
            }
        }

        /// <summary>
        /// Collects visible ordinary mobs (and any currently selected mob) directly from non-player views
        /// without allocating a Snapshot() copy or probing player views against mobsByEntity.
        /// </summary>
        internal void CollectVisibleMobs(
            Dictionary<ulong, Mob> mobs,
            uint districtId,
            bool isDead,
            ushort targetView,
            ushort targetSpawn,
            HashSet<Mob> visible,
            HashSet<Mob> selected)
        {
            lock (views)
            {
                List<ClientView> nonPlayers = views.NonPlayerViews;
                for (int i = 0; i < nonPlayers.Count; i++)
                {
                    ClientView view = nonPlayers[i];
                    if (view == null || !view.viewCreated || view.viewNeedsToBeDeleted) continue;
                    if (!mobs.TryGetValue(view.entityId, out Mob mob)) continue;
                    if (!MobMotion.Ordinary(mob) || mob.getDistrict() != districtId) continue;
                    visible.Add(mob);
                    if (!isDead && view.ViewID == targetView && view.spawnId == targetSpawn)
                    {
                        selected.Add(mob);
                    }
                }
            }
        }

        /// <summary>
        /// Collects views whose entityId falls in [minEntityInclusive, maxEntityExclusive) by scanning only
        /// non-player views under a single lock, avoiding Snapshot() copies in DataMining.Check.
        /// </summary>
        internal void CollectNonPlayerViewsInEntityRange(ulong minEntityInclusive, ulong maxEntityExclusive, List<ClientView> destination)
        {
            destination.Clear();
            lock (views)
            {
                List<ClientView> nonPlayers = views.NonPlayerViews;
                for (int i = 0; i < nonPlayers.Count; i++)
                {
                    ClientView view = nonPlayers[i];
                    if (view != null && view.entityId >= minEntityInclusive && view.entityId < maxEntityExclusive)
                    {
                        destination.Add(view);
                    }
                }
            }
        }

    }
    /// <summary>
    /// The view list with entity and view-id indexes kept in step with every add and remove, so the first view
    /// for an entity or id is found without scanning. Order and first-match semantics are the list's own.
    /// </summary>
    public sealed class ViewList : System.Collections.ObjectModel.Collection<ClientView>
    {
        internal const UInt32 PlayerGoId = 12;
        // Single-hop direct index for the 99.99% case of one view per entity; multiByEntity is allocated
        // lazily only if a duplicate entityId is ever inserted, preserving exact first-match list order.
        private readonly Dictionary<UInt64, ClientView> directByEntity = new Dictionary<UInt64, ClientView>();
        private Dictionary<UInt64, List<ClientView>> multiByEntity;
        private readonly ConcurrentDictionary<UInt64, ClientView> firstByEntity = new ConcurrentDictionary<UInt64, ClientView>();
        private readonly Dictionary<UInt16, ClientView> directByViewId = new Dictionary<UInt16, ClientView>();
        private Dictionary<UInt16, List<ClientView>> multiByViewId;
        private readonly Dictionary<UInt32, List<ClientView>> byGoId = new Dictionary<UInt32, List<ClientView>>();
        private readonly List<ClientView> nonPlayerViews = new List<ClientView>();

        internal List<ClientView> BackingList => (List<ClientView>)Items;
        internal List<ClientView> NonPlayerViews => nonPlayerViews;
        internal bool HasMultiEntity => multiByEntity != null && multiByEntity.Count > 0;

        /// <summary>Bumped by every add, remove, replace and clear: the visibility pass skips a player whose views and surroundings are unchanged.</summary>
        public long Version { get; private set; }

        public ClientView FirstForEntity(UInt64 entityId) =>
            directByEntity.TryGetValue(entityId, out ClientView view) ? view : null;

        internal ClientView FirstForEntityLockFree(UInt64 entityId) =>
            firstByEntity.TryGetValue(entityId, out ClientView view) ? view : null;

        internal void StampExtraEntityViews(UInt64 entityId, long stamp)
        {
            if (multiByEntity != null && multiByEntity.TryGetValue(entityId, out List<ClientView> list))
            {
                for (int i = 1; i < list.Count; i++)
                {
                    list[i].ReconcileStamp = stamp;
                }
            }
        }

        internal List<ClientView> ViewsWithGoId(UInt32 goId) =>
            byGoId.TryGetValue(goId, out var list) ? list : null;

        public ClientView FirstWithViewId(UInt16 viewId) =>
            directByViewId.TryGetValue(viewId, out ClientView view) ? view : null;

        protected override void InsertItem(int index, ClientView item)
        {
            base.InsertItem(index, item);
            Version++;
            if (item == null) return;
            if (index == Count - 1)
            {
                AppendEntity(item.entityId, item);
                AppendViewId(item.ViewID, item);
                Append(byGoId, item.GoID, item);
                if (item.GoID != PlayerGoId) nonPlayerViews.Add(item);
            }
            else Rebuild();
        }

        protected override void RemoveItem(int index)
        {
            ClientView item = this[index];
            base.RemoveItem(index);
            Version++;
            if (item == null) return;
            UnindexEntity(item.entityId, item);
            UnindexViewId(item.ViewID, item);
            Unindex(byGoId, item.GoID, item);
            if (item.GoID != PlayerGoId) nonPlayerViews.Remove(item);
        }

        protected override void SetItem(int index, ClientView item)
        {
            base.SetItem(index, item);
            Version++;
            Rebuild();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            Version++;
            directByEntity.Clear();
            multiByEntity?.Clear();
            firstByEntity.Clear();
            directByViewId.Clear();
            multiByViewId?.Clear();
            byGoId.Clear();
            nonPlayerViews.Clear();
        }

        private void Rebuild()
        {
            directByEntity.Clear();
            multiByEntity?.Clear();
            directByViewId.Clear();
            multiByViewId?.Clear();
            byGoId.Clear();
            nonPlayerViews.Clear();
            foreach (ClientView view in this)
            {
                if (view == null) continue;
                AppendEntityDirectOnly(view.entityId, view);
                AppendViewId(view.ViewID, view);
                Append(byGoId, view.GoID, view);
                if (view.GoID != PlayerGoId) nonPlayerViews.Add(view);
            }

            foreach (KeyValuePair<UInt64, ClientView> kv in directByEntity)
            {
                firstByEntity[kv.Key] = kv.Value;
            }
            foreach (UInt64 existing in firstByEntity.Keys)
            {
                if (!directByEntity.ContainsKey(existing))
                {
                    firstByEntity.TryRemove(existing, out _);
                }
            }
        }

        private void AppendEntityDirectOnly(UInt64 entityId, ClientView item)
        {
            if (!directByEntity.TryGetValue(entityId, out ClientView first))
            {
                directByEntity[entityId] = item;
                return;
            }

            if (multiByEntity == null) multiByEntity = new Dictionary<UInt64, List<ClientView>>();
            if (!multiByEntity.TryGetValue(entityId, out List<ClientView> list))
            {
                list = new List<ClientView>(2) { first };
                multiByEntity[entityId] = list;
            }
            list.Add(item);
        }

        private void AppendEntity(UInt64 entityId, ClientView item)
        {
            if (!directByEntity.TryGetValue(entityId, out ClientView first))
            {
                directByEntity[entityId] = item;
                firstByEntity[entityId] = item;
                return;
            }

            if (multiByEntity == null) multiByEntity = new Dictionary<UInt64, List<ClientView>>();
            if (!multiByEntity.TryGetValue(entityId, out List<ClientView> list))
            {
                list = new List<ClientView>(2) { first };
                multiByEntity[entityId] = list;
            }
            list.Add(item);
        }

        private void UnindexEntity(UInt64 entityId, ClientView item)
        {
            if (multiByEntity != null && multiByEntity.TryGetValue(entityId, out List<ClientView> list))
            {
                list.Remove(item);
                if (list.Count == 0)
                {
                    multiByEntity.Remove(entityId);
                    directByEntity.Remove(entityId);
                    firstByEntity.TryRemove(entityId, out _);
                }
                else
                {
                    ClientView nextFirst = list[0];
                    directByEntity[entityId] = nextFirst;
                    firstByEntity[entityId] = nextFirst;
                    if (list.Count == 1)
                    {
                        multiByEntity.Remove(entityId);
                    }
                }
                return;
            }

            if (directByEntity.TryGetValue(entityId, out ClientView current) && ReferenceEquals(current, item))
            {
                directByEntity.Remove(entityId);
                firstByEntity.TryRemove(entityId, out _);
            }
        }

        private void AppendViewId(UInt16 viewId, ClientView item)
        {
            if (!directByViewId.TryGetValue(viewId, out ClientView first))
            {
                directByViewId[viewId] = item;
                return;
            }

            if (multiByViewId == null) multiByViewId = new Dictionary<UInt16, List<ClientView>>();
            if (!multiByViewId.TryGetValue(viewId, out List<ClientView> list))
            {
                list = new List<ClientView>(2) { first };
                multiByViewId[viewId] = list;
            }
            list.Add(item);
        }

        private void UnindexViewId(UInt16 viewId, ClientView item)
        {
            if (multiByViewId != null && multiByViewId.TryGetValue(viewId, out List<ClientView> list))
            {
                list.Remove(item);
                if (list.Count == 0)
                {
                    multiByViewId.Remove(viewId);
                    directByViewId.Remove(viewId);
                }
                else
                {
                    directByViewId[viewId] = list[0];
                    if (list.Count == 1)
                    {
                        multiByViewId.Remove(viewId);
                    }
                }
                return;
            }

            if (directByViewId.TryGetValue(viewId, out ClientView current) && ReferenceEquals(current, item))
            {
                directByViewId.Remove(viewId);
            }
        }

        private static void Append<TKey>(Dictionary<TKey, List<ClientView>> index, TKey key, ClientView item)
        {
            if (!index.TryGetValue(key, out var list)) index[key] = list = new List<ClientView>(1);
            list.Add(item);
        }

        private static void Unindex<TKey>(Dictionary<TKey, List<ClientView>> index, TKey key, ClientView item)
        {
            if (!index.TryGetValue(key, out var list)) return;
            list.Remove(item);
            if (list.Count == 0) index.Remove(key);
        }
    }
}
