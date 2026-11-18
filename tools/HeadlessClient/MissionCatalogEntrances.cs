using System.Globalization;
using hds;

namespace HeadlessClient;

public static class MissionCatalogEntrances
{
    static string Point(IEnumerable<double> xyz) => string.Join(",", xyz.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));

    public static string[] Describe(MissionProgress.SavedState state, string? root = null)
    {
        string prefix = $"Mission {state.Mission} phase {state.Phase} (area {state.Phase + 1}); world units";
        if (state.Waypoints.Length == 0 && state.Exits.Length == 0)
            return new[] { prefix + ": outdoor phase; no current street door or elevator." };
        if (state.SiteDoors.Length == 0) return Array.Empty<string>();
        var street = MervFollowupStreetEntry.ForSite(state);
        if (street == null)
        {
            string missing = prefix + ": no measured public entrance saved.";
            if (root == null || state.SiteDoors.Last().metrId != 1)
                return new[] { missing + " Stop before using raw CSV coordinates." };
            try
            {
                var rows = new[] { "staticObjects_slums_1.csv", "staticObjects_slums_2.csv" }
                    .SelectMany(file => File.ReadLines(Path.Combine(root, "data", file)));
                var ride = MervFollowupElevatorFixture.Select(state, true, rows, state.Objects.Select(obj => obj.Id).ToArray());
                string position = string.Join(" ", ride.Approach.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));
                return new[] { missing, $"Assisted lobby approach: ?tp {position} 0 0 0, then use lobby car {ride.Key}. This skips public entry." };
            }
            catch (InvalidDataException)
            {
                return new[] { missing + " No resolved lobby car. Stop on this entrance." };
            }
        }
        var door = street.Raw.GetProperty("Context").GetProperty("Door");
        return new[] {
            $"{prefix}: street Door {door.GetProperty("Key").GetUInt32()} at {Point(door.GetProperty("Position").EnumerateArray().Select(v => v.GetDouble()))}.",
            $"Lobby car {street.Lobby.Key} at {Point(street.Lobby.Position)}; stand {Point(street.Lobby.Standing!)}. Floor car {street.Floor.Key} at {Point(street.Floor.Position)}; arrive {Point(street.Floor.Standing!)}."
        };
    }
}
