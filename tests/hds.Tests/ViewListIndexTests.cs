using hds;

namespace hds.Tests;

public class ViewListIndexTests
{
    [Fact]
    public void IndexedLookupsKeepTheListsFirstMatchAcrossAddsRemovesAndClear()
    {
        var views = new ViewManager();
        var a = views.addView(100, 12);
        var b = views.addView(200, 599);
        views.views.Add(new ClientView(12, 40, 300) { viewCreated = true }); // tests add directly
        var dupe = new ClientView(599, 41, 200);
        views.views.Add(dupe);
        Assert.Same(a, views.FindViewForEntity(100));
        Assert.Same(b, views.FindViewForEntity(200)); // first of two for the entity, as List.Find returned
        Assert.Same(b, views.getViewById(b.ViewID));
        Assert.Equal(40, views.FindViewForEntity(300)!.ViewID);
        Assert.Null(views.FindViewForEntity(999));
        Assert.Same(b, views.GetViewForEntityAndGo(200, 599));

        views.removeViewByViewId(b.ViewID);
        Assert.Same(dupe, views.FindViewForEntity(200));
        Assert.Null(views.getViewById(b.ViewID));
        Assert.Equal(new[] { a.ViewID, (ushort)40, (ushort)41 }, views.Snapshot().Select(v => v.ViewID));

        var made = views.GetViewForEntityAndGo(500, 12);
        Assert.Same(made, views.FindViewForEntity(500));
        Assert.Same(made, views.getViewById(made.ViewID));
        Assert.True(made.ViewID > b.ViewID);

        long version = views.Version;
        views.Clear();
        Assert.True(views.Version > version); // the visibility pass re-evaluates a player whose views changed
        Assert.Equal(0, views.Count);
        Assert.Null(views.FindViewForEntity(100));
        Assert.Null(views.getViewById(a.ViewID));
    }

    [Fact]
    public void LookupsStayFlatWithManyViews()
    {
        var views = new ViewManager();
        for (ulong e = 1; e <= 5000; e++) views.addView(e, 12);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 200000; i++) Assert.NotNull(views.FindViewForEntity((ulong)(i % 5000) + 1));
        Assert.InRange(clock.Elapsed.TotalSeconds, 0, 2);
    }
}
