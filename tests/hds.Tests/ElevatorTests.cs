using hds;

namespace hds.Tests;

/// <summary>Elevator panels, from the client's own floor data (data/elevator_panels.csv) checked against retail's answers.</summary>
public class ElevatorTests
{
    private static void Load()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "elevator_panels.csv")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        Elevators.Load(Path.Combine(dir, "data", "elevator_panels.csv"));
    }

    private static StaticWorldObject Panel(ushort metr, ushort sector, string hex)
    {
        return new StaticWorldObject
        {
            metrId = metr, sectorID = sector, mxoStaticId = BitConverter.ToUInt32(Convert.FromHexString(hex), 0),
            type = BitConverter.GetBytes(Elevators.PanelGoId)
        };
    }

    [Fact]
    public void APanelSendsRetailsRegion()
    {
        Load();

        // luizoe Recursion: 81 3f 95 01 1f 00 for the bottom panel of sector 405's shaft at 60300, 33390
        Elevators.PanelFloor bottom = Elevators.Find(Panel(1, 405, "b8035019"))!;
        Assert.Equal(0x1f, bottom.region);
        Assert.Equal(0, bottom.floorFromBottom);

        // ... and 0x60 for the bottom panel of sector 1334's at 46700, 22010
        Assert.Equal(0x60, Elevators.Find(Panel(1, 1334, "670f6053"))!.region);
    }

    [Fact]
    public void AButtonIsAFloorOfTheSameShaft()
    {
        Load();
        Elevators.PanelFloor bottom = Elevators.Find(Panel(1, 405, "b8035019"))!;

        // Retail's button 7 there went to the floor whose region is 0x19, six floors up
        Elevators.PanelFloor seventh = Elevators.FloorOf(bottom, 7)!;
        Assert.Equal(0x19, seventh.region);
        Assert.Equal(bottom.y + 2400, seventh.y);
        Assert.Null(Elevators.FloorOf(bottom, (ushort)(bottom.floors + 1)));
    }

    [Fact]
    public void ThePanelRecordIsRetails()
    {
        Assert.Equal(new byte[] { 0x81, 0x3f, 0x95, 0x01, 0x1f, 0x00 }, Elevators.BuildPanel(405, 0x1f).Skip(Elevators.BuildPanel(405, 0x1f).Length - 6).ToArray());
    }
}
