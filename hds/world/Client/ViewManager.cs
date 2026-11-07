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
