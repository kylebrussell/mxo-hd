using System.Globalization;
using hds;

namespace HeadlessClient;

public static class MissionCatalogEntrances
{
    static string Point(IEnumerable<double> xyz) => string.Join(",", xyz.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));

    public static string[] Describe(MissionProgress.SavedState state)
    {
        if (state.SiteDoors.Length == 0) return Array.Empty<string>();
        var street = MervFollowupStreetEntry.ForSite(state);
        string prefix = $"Mission {state.Mission} phase {state.Phase} (area {state.Phase + 1}); world units";
        if (street == null) return new[] { prefix + ": no measured public entrance saved; stop before using raw CSV coordinates." };
        var door = street.Raw.GetProperty("Context").GetProperty("Door");
        return new[] {
            $"{prefix}: street Door {door.GetProperty("Key").GetUInt32()} at {Point(door.GetProperty("Position").EnumerateArray().Select(v => v.GetDouble()))}.",
            $"Lobby car {street.Lobby.Key} at {Point(street.Lobby.Position)}; stand {Point(street.Lobby.Standing!)}. Floor car {street.Floor.Key} at {Point(street.Floor.Position)}; arrive {Point(street.Floor.Standing!)}."
        };
    }
}
