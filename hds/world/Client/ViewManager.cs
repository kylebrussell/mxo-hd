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
        public List<ClientView> views = new List<ClientView>();
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
                return views.Find(delegate(ClientView cv) { return cv.ViewID == viewId; });
            }
        }

        public ClientView FindViewForEntity(UInt64 entityId)
        {
            lock (views)
            {
                return views.Find(delegate(ClientView cv) { return cv.entityId == entityId; });
            }
        }

        public void removeViewByViewId(UInt16 viewId)
        {
            lock (views)
            {
                ClientView view = views.Find(delegate(ClientView cv) { return cv.ViewID == viewId; });
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
                ClientView view = views.Find(delegate(ClientView cv) { return cv.entityId == entityId; });
                if (view == null || view.ViewID == 0)
                {
                    view = addView(entityId, goID);
                }
                return view;
            }
        }

    }
}
