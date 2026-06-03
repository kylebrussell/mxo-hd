using hds;

namespace hds.Tests;

public class ViewManagerTests
{
    [Fact]
    public void GetViewForEntityAndGoCreatesViewOncePerEntity()
    {
        ViewManager manager = new ViewManager();

        ClientView first = manager.GetViewForEntityAndGo(42, 100);
        ClientView second = manager.GetViewForEntityAndGo(42, 100);

        Assert.Same(first, second);
        Assert.Equal(4, first.ViewID);
        Assert.Single(manager.views);
    }

    [Fact]
    public void RemoveViewByViewIdIgnoresMissingView()
    {
        ViewManager manager = new ViewManager();
        manager.GetViewForEntityAndGo(42, 100);

        manager.removeViewByViewId(999);

        Assert.Single(manager.views);
    }
}
