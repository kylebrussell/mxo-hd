using System;
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
        UInt16 currentViewID = 3; // We start from 3 to increment


        public ViewManager(){
        }

        public ClientView addView(UInt64 entityId, UInt32 goId)
        {
            lock (views)
            {
                // Add the Object to the client View List
                currentViewID++;
                ClientView newView = new ClientView(goId, currentViewID, entityId);
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
            lock (views)
            {
                return views.FirstForEntity(entityId);
            }
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
        /// appends stale created player views not in <paramref name="nearbySet"/> to <paramref name="toDelete"/>,
        /// and returns the current view-list version without allocating in the steady state.
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
            toSpawn.Clear();
            toDelete.Clear();
            lock (views)
            {
                int nearbyCount = nearbyClients.Count;
                for (int i = 0; i < nearbyCount; i++)
                {
                    ulong entityId = nearbyEntities[i];
                    ClientView view = views.FirstForEntity(entityId);
                    if (view == null || view.ViewID == 0)
                    {
                        view = addView(entityId, nearbyGoids[i]);
                    }
                    if (!view.viewCreated)
                    {
                        toSpawn.Add((nearbyClients[i], view));
                    }
                }

                List<ClientView> backing = views.BackingList;
                for (int i = 0; i < backing.Count; i++)
                {
                    ClientView view = backing[i];
                    if (view != null && view.viewCreated && view.GoID == playerGoId && !nearbySet.Contains(view.entityId))
                    {
                        toDelete.Add(view);
                    }
                }

                return views.Version;
            }
        }

        /// <summary>
        /// Collects created views matching <paramref name="goId"/> whose entity is not in <paramref name="nearbySet"/>
        /// under a single lock without copying the entire view list.
        /// </summary>
        internal void CollectStaleViewsByGoId(UInt32 goId, HashSet<ulong> nearbySet, List<ClientView> toDelete)
        {
            toDelete.Clear();
            lock (views)
            {
                List<ClientView> backing = views.BackingList;
                for (int i = 0; i < backing.Count; i++)
                {
                    ClientView view = backing[i];
                    if (view != null && view.viewCreated && view.GoID == goId && !nearbySet.Contains(view.entityId))
                    {
                        toDelete.Add(view);
                    }
                }
            }
        }

        /// <summary>
        /// Collects created mob views whose entity is in <paramref name="mobsByEntity"/> and either not in
        /// <paramref name="nearbySet"/> or in another district, without copying the entire view list.
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
                List<ClientView> backing = views.BackingList;
                for (int i = 0; i < backing.Count; i++)
                {
                    ClientView view = backing[i];
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
        /// stale mob views without copying the view list.
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

                List<ClientView> backing = views.BackingList;
                for (int i = 0; i < backing.Count; i++)
                {
                    ClientView view = backing[i];
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
        /// Collects visible ordinary mobs (and any currently selected mob) directly from the view list
        /// without allocating a Snapshot() copy.
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
                List<ClientView> backing = views.BackingList;
                for (int i = 0; i < backing.Count; i++)
                {
                    ClientView view = backing[i];
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

    }
    /// <summary>
    /// The view list with entity and view-id indexes kept in step with every add and remove, so the first view
    /// for an entity or id is found without scanning. Order and first-match semantics are the list's own.
    /// </summary>
    public sealed class ViewList : System.Collections.ObjectModel.Collection<ClientView>
    {
        private readonly Dictionary<UInt64, List<ClientView>> byEntity = new Dictionary<UInt64, List<ClientView>>();
        private readonly Dictionary<UInt16, List<ClientView>> byViewId = new Dictionary<UInt16, List<ClientView>>();

        internal List<ClientView> BackingList => (List<ClientView>)Items;

        /// <summary>Bumped by every add, remove, replace and clear: the visibility pass skips a player whose views and surroundings are unchanged.</summary>
        public long Version { get; private set; }

        public ClientView FirstForEntity(UInt64 entityId) =>
            byEntity.TryGetValue(entityId, out var list) && list.Count > 0 ? list[0] : null;

        public ClientView FirstWithViewId(UInt16 viewId) =>
            byViewId.TryGetValue(viewId, out var list) && list.Count > 0 ? list[0] : null;

        protected override void InsertItem(int index, ClientView item)
        {
            base.InsertItem(index, item);
            Version++;
            if (item == null) return;
            if (index == Count - 1)
            {
                Append(byEntity, item.entityId, item);
                Append(byViewId, item.ViewID, item);
            }
            else Rebuild();
        }

        protected override void RemoveItem(int index)
        {
            ClientView item = this[index];
            base.RemoveItem(index);
            Version++;
            if (item == null) return;
            Unindex(byEntity, item.entityId, item);
            Unindex(byViewId, item.ViewID, item);
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
            byEntity.Clear();
            byViewId.Clear();
        }

        private void Rebuild()
        {
            byEntity.Clear();
            byViewId.Clear();
            foreach (ClientView view in this)
            {
                if (view == null) continue;
                Append(byEntity, view.entityId, view);
                Append(byViewId, view.ViewID, view);
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
