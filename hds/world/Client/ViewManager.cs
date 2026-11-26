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

    }
    /// <summary>
    /// The view list with entity and view-id indexes kept in step with every add and remove, so the first view
    /// for an entity or id is found without scanning. Order and first-match semantics are the list's own.
    /// </summary>
    public sealed class ViewList : System.Collections.ObjectModel.Collection<ClientView>
    {
        private readonly Dictionary<UInt64, List<ClientView>> byEntity = new Dictionary<UInt64, List<ClientView>>();
        private readonly Dictionary<UInt16, List<ClientView>> byViewId = new Dictionary<UInt16, List<ClientView>>();

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
