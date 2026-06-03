using hds;

namespace hds.Tests;

public class SpatialGridTests
{
    [Fact]
    public void GetNeighborCellsReturnsItemsInNearbyCells()
    {
        SpatialGrid<string> grid = new SpatialGrid<string>(10);
        grid.Add(0, 0, "origin");
        grid.Add(9, 9, "same-cell");
        grid.Add(30, 30, "far");

        List<string> nearby = grid.GetNeighborCells(0, 0, 10)
            .SelectMany(cell => cell)
            .ToList();

        Assert.Contains("origin", nearby);
        Assert.Contains("same-cell", nearby);
        Assert.DoesNotContain("far", nearby);
    }

    [Fact]
    public void GetNeighborCellsHandlesNegativeCoordinates()
    {
        SpatialGrid<string> grid = new SpatialGrid<string>(10);
        grid.Add(-11, -11, "negative");

        List<string> nearby = grid.GetNeighborCells(-10, -10, 5)
            .SelectMany(cell => cell)
            .ToList();

        Assert.Single(nearby);
        Assert.Equal("negative", nearby[0]);
    }
}
